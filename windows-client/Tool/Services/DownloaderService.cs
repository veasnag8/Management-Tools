using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tool.Models;

namespace Tool.Services
{
    public interface IDownloaderService
    {
        Task<bool> DownloadEpisodeAsync(
            EpisodeModel episode,
            string dramaTitle,
            string outputDirectory,
            IProgress<(double Progress, string Speed, string Eta)>? progress = null,
            CancellationToken ct = default);
    }

    public class DownloaderService : IDownloaderService
    {
        private readonly string _ytdlpPath;
        private readonly string _ffmpegPath;
        private readonly string _toolsDir;

        private static readonly Regex PercentageRegex = new(@"(?:(\d+(?:\.\d+)?)%|progress:\s*(\d+(?:\.\d+)?))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SpeedRegex = new(@"(\d+(?:\.\d+)?\s*(?:MiB|KiB|GiB|MB|KB|GB|B)\/s)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EtaRegex = new(@"(?:ETA\s*|time=)(\d{2}:\d{2}:\d{2}|\d{2}:\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SizeRegex = new(@"(?:of\s+~?\s*|Lsize=\s*)(\d+(?:\.\d+)?\s*(?:MiB|KiB|GiB|MB|KB|GB|B))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public DownloaderService(string? cliToolPath = null)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var baseDir = AppContext.BaseDirectory;

            if (!string.IsNullOrEmpty(cliToolPath) && File.Exists(cliToolPath))
            {
                if (cliToolPath.Contains("yt-dlp", StringComparison.OrdinalIgnoreCase))
                {
                    _ytdlpPath = cliToolPath;
                    _ffmpegPath = FindTool("ffmpeg.exe", Path.GetDirectoryName(cliToolPath) ?? "", appDir, baseDir);
                }
                else
                {
                    _ffmpegPath = cliToolPath;
                    _ytdlpPath = FindTool("yt-dlp.exe", Path.GetDirectoryName(cliToolPath) ?? "", appDir, baseDir);
                }
            }
            else
            {
                _ytdlpPath = FindTool("yt-dlp.exe", appDir, baseDir);
                _ffmpegPath = FindTool("ffmpeg.exe", appDir, baseDir);
            }

            _toolsDir = !string.IsNullOrEmpty(_ytdlpPath)
                ? Path.GetDirectoryName(_ytdlpPath)!
                : (!string.IsNullOrEmpty(_ffmpegPath) ? Path.GetDirectoryName(_ffmpegPath)! : Path.Combine(appDir, "Tools"));
        }

        private static string FindTool(string toolName, params string[] searchDirs)
        {
            foreach (var dir in searchDirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;

                var inTools = Path.Combine(dir, "Tools", toolName);
                if (File.Exists(inTools)) return inTools;

                var direct = Path.Combine(dir, toolName);
                if (File.Exists(direct)) return direct;

                try
                {
                    var devDir = Path.Combine(dir, "..", "..", "..", "Tools", toolName);
                    if (File.Exists(devDir)) return Path.GetFullPath(devDir);
                }
                catch { }
            }

            // Check system PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var candidate = Path.Combine(segment.Trim(), toolName);
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }

            return string.Empty;
        }

        public async Task<bool> DownloadEpisodeAsync(
            EpisodeModel episode,
            string dramaTitle,
            string outputDirectory,
            IProgress<(double Progress, string Speed, string Eta)>? progress = null,
            CancellationToken ct = default)
        {
            episode.Status = EpisodeDownloadStatus.Downloading;
            episode.Progress = 0;
            episode.Speed = "Connecting...";
            episode.Eta = "--:--";
            episode.ErrorMessage = null;

            Directory.CreateDirectory(outputDirectory);
            var sanitizedDramaTitle = SanitizeFileName(dramaTitle);
            var sanitizedEpTitle = SanitizeFileName(episode.Title);
            var finalFileName = $"{sanitizedDramaTitle} - {sanitizedEpTitle}.mp4";
            var finalFilePath = Path.Combine(outputDirectory, finalFileName);
            episode.OutputFilePath = finalFilePath;

            if (string.IsNullOrWhiteSpace(episode.StreamUrl))
            {
                episode.Status = EpisodeDownloadStatus.Failed;
                episode.ErrorMessage = "No stream URL found for this episode.";
                return false;
            }

            // 1. Primary Engine: yt-dlp with ffmpeg merger (Production Grade)
            if (!string.IsNullOrEmpty(_ytdlpPath) && File.Exists(_ytdlpPath))
            {
                return await RunYtDlpDownloadAsync(episode, finalFilePath, outputDirectory, progress, ct);
            }

            // 2. Secondary Engine: direct ffmpeg stream copy
            if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
            {
                return await RunFfmpegDownloadAsync(episode, finalFilePath, outputDirectory, progress, ct);
            }

            episode.Status = EpisodeDownloadStatus.Failed;
            episode.ErrorMessage = "Download engine missing: yt-dlp.exe and ffmpeg.exe were not found.";
            return false;
        }

        private async Task<bool> RunYtDlpDownloadAsync(
            EpisodeModel episode,
            string finalFilePath,
            string outputDirectory,
            IProgress<(double Progress, string Speed, string Eta)>? progress,
            CancellationToken ct)
        {
            var ffmpegArg = !string.IsNullOrEmpty(_toolsDir) && Directory.Exists(_toolsDir)
                ? $"--ffmpeg-location \"{_toolsDir}\" "
                : "";

            var startInfo = new ProcessStartInfo
            {
                FileName = _ytdlpPath,
                Arguments = $"{ffmpegArg}--merge-output-format mp4 --newline --no-part --force-overwrites -o \"{finalFilePath}\" \"{episode.StreamUrl}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = outputDirectory
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ParseLineTelemetry(e.Data, episode, progress);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ParseLineTelemetry(e.Data, episode, progress);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (ct.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    }
                    catch { }
                }))
                {
                    await process.WaitForExitAsync(ct);
                }

                if (process.ExitCode == 0)
                {
                    // Detect created file in case yt-dlp altered extension
                    var resolvedPath = ResolveActualOutputFile(finalFilePath, outputDirectory);
                    episode.OutputFilePath = resolvedPath;

                    if (File.Exists(resolvedPath))
                    {
                        var fileInfo = new FileInfo(resolvedPath);
                        episode.FileSize = FormatBytes(fileInfo.Length);
                    }

                    episode.Status = EpisodeDownloadStatus.Completed;
                    episode.Progress = 100;
                    episode.Speed = "Done";
                    episode.Eta = "00:00";
                    progress?.Report((100, "Done", "00:00"));
                    return true;
                }
                else
                {
                    episode.Status = ct.IsCancellationRequested ? EpisodeDownloadStatus.Cancelled : EpisodeDownloadStatus.Failed;
                    episode.ErrorMessage = $"Exit Code: {process.ExitCode}";
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                episode.Status = EpisodeDownloadStatus.Cancelled;
                episode.ErrorMessage = "Download cancelled by user.";
                return false;
            }
            catch (Exception ex)
            {
                episode.Status = EpisodeDownloadStatus.Failed;
                episode.ErrorMessage = ex.Message;
                return false;
            }
        }

        private async Task<bool> RunFfmpegDownloadAsync(
            EpisodeModel episode,
            string finalFilePath,
            string outputDirectory,
            IProgress<(double Progress, string Speed, string Eta)>? progress,
            CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -i \"{episode.StreamUrl}\" -c copy -bsf:a aac_adtstoasc \"{finalFilePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = outputDirectory
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ParseLineTelemetry(e.Data, episode, progress);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ParseLineTelemetry(e.Data, episode, progress);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (ct.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    }
                    catch { }
                }))
                {
                    await process.WaitForExitAsync(ct);
                }

                if (process.ExitCode == 0 && File.Exists(finalFilePath))
                {
                    var fileInfo = new FileInfo(finalFilePath);
                    episode.FileSize = FormatBytes(fileInfo.Length);
                    episode.Status = EpisodeDownloadStatus.Completed;
                    episode.Progress = 100;
                    episode.Speed = "Done";
                    episode.Eta = "00:00";
                    progress?.Report((100, "Done", "00:00"));
                    return true;
                }
                else
                {
                    episode.Status = ct.IsCancellationRequested ? EpisodeDownloadStatus.Cancelled : EpisodeDownloadStatus.Failed;
                    episode.ErrorMessage = $"FFmpeg Exit Code: {process.ExitCode}";
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                episode.Status = EpisodeDownloadStatus.Cancelled;
                episode.ErrorMessage = "Download cancelled by user.";
                return false;
            }
            catch (Exception ex)
            {
                episode.Status = EpisodeDownloadStatus.Failed;
                episode.ErrorMessage = ex.Message;
                return false;
            }
        }

        private void ParseLineTelemetry(
            string line,
            EpisodeModel episode,
            IProgress<(double Progress, string Speed, string Eta)>? progress)
        {
            // 1. Percentage
            var pctMatch = PercentageRegex.Match(line);
            if (pctMatch.Success)
            {
                var valStr = pctMatch.Groups[1].Success ? pctMatch.Groups[1].Value : pctMatch.Groups[2].Value;
                if (double.TryParse(valStr, out var pct))
                {
                    episode.Progress = Math.Min(100.0, Math.Max(0.0, pct));
                }
            }

            // 2. Speed
            var speedMatch = SpeedRegex.Match(line);
            if (speedMatch.Success)
            {
                episode.Speed = speedMatch.Groups[1].Value;
            }

            // 3. ETA
            var etaMatch = EtaRegex.Match(line);
            if (etaMatch.Success)
            {
                episode.Eta = etaMatch.Groups[1].Value;
            }

            // 4. File Size
            var sizeMatch = SizeRegex.Match(line);
            if (sizeMatch.Success)
            {
                episode.FileSize = sizeMatch.Groups[1].Value.Trim();
            }

            progress?.Report((episode.Progress, episode.Speed, episode.Eta));
        }

        private static string ResolveActualOutputFile(string finalFilePath, string outputDirectory)
        {
            if (File.Exists(finalFilePath)) return finalFilePath;

            var baseName = Path.GetFileNameWithoutExtension(finalFilePath);
            var candidates = Directory.GetFiles(outputDirectory, $"{baseName}.*");
            if (candidates.Length > 0)
            {
                return candidates[0];
            }
            return finalFilePath;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "--";
            double mb = bytes / (1024.0 * 1024.0);
            if (mb >= 1024)
            {
                return $"{mb / 1024.0:F2} GB";
            }
            return $"{mb:F1} MB";
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
