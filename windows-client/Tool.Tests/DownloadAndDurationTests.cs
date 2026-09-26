using System;
using System.IO;
using System.Threading.Tasks;
using Tool.Models;
using Tool.Services;
using Xunit;

namespace Tool.Tests
{
    public class DownloadAndDurationTests
    {
        [Theory]
        [InlineData(2765, "46m 05s")]
        [InlineData(2537, "42m 17s")]
        [InlineData(1286, "21m 26s")]
        [InlineData(95, "1m 35s")]
        [InlineData(3665, "1h 01m 05s")]
        [InlineData(0, "--")]
        [InlineData(-10, "--")]
        public void FormatDuration_ReturnsAccurateRealTimeDuration(int seconds, string expected)
        {
            var result = DramaCrawlerService.FormatDuration(seconds);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatFileSize_WithExactBytes_ReturnsAccurateFormattedSize()
        {
            long bytes500Mb = 500L * 1024 * 1024;
            var result500 = DramaCrawlerService.FormatFileSize(bytes500Mb);
            Assert.Equal("500 MB", result500);

            long bytes1Gb = (long)(1.5 * 1024 * 1024 * 1024);
            var result1Gb = DramaCrawlerService.FormatFileSize(bytes1Gb);
            Assert.Equal("1.50 GB", result1Gb);
        }

        [Fact]
        public void FormatFileSize_WithDuration_ReturnsEstimated1080pSize()
        {
            // 45 minutes = 2700 seconds
            var result = DramaCrawlerService.FormatFileSize(0, 2700);
            Assert.StartsWith("~", result);
            Assert.EndsWith("MB", result);
        }

        [Fact]
        public void EpisodeModel_Properties_CanStoreRealMetadata()
        {
            var episode = new EpisodeModel
            {
                EpisodeNumber = 1,
                Title = "EP01",
                Duration = "46m 05s",
                FileSize = "448 MB",
                ThumbnailUrl = "https://i.ytimg.com/vi/0ADl2hgSgfs/hq720.jpg",
                StreamUrl = "https://www.youtube.com/watch?v=0ADl2hgSgfs"
            };

            Assert.Equal(1, episode.EpisodeNumber);
            Assert.Equal("EP01", episode.Title);
            Assert.Equal("46m 05s", episode.Duration);
            Assert.Equal("448 MB", episode.FileSize);
            Assert.Equal("https://www.youtube.com/watch?v=0ADl2hgSgfs", episode.StreamUrl);
        }

        [Fact]
        public async Task GetFeaturedLibrary_InitializesDramasWithCleanPendingStatus()
        {
            var crawler = new DramaCrawlerService();
            var wetvList = await crawler.GetFeaturedLibraryAsync(PlatformType.WeTV);

            Assert.NotEmpty(wetvList);
            var firstDrama = wetvList[0];
            Assert.Contains("Against The Current", firstDrama.Title);
            Assert.NotEmpty(firstDrama.Episodes);

            // Placeholder duration should be -- before live sync
            Assert.Equal("--", firstDrama.Episodes[0].Duration);
        }

        [Fact]
        public async Task DownloaderService_CanDownloadRealClip_AndPopulatesRealFileSize()
        {
            var downloader = new DownloaderService();
            var tempDir = Path.Combine(Path.GetTempPath(), "SnaPro_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var episode = new EpisodeModel
                {
                    EpisodeNumber = 1,
                    Title = "TestClip",
                    Duration = "32s",
                    StreamUrl = "https://www.youtube.com/watch?v=IbCpqYhoTIk"
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var success = await downloader.DownloadEpisodeAsync(episode, "TestDrama", tempDir, ct: cts.Token);

                Assert.True(success);
                Assert.Equal(EpisodeDownloadStatus.Completed, episode.Status);
                Assert.NotEqual("--", episode.FileSize);
                Assert.True(File.Exists(episode.OutputFilePath));
                Assert.True(new FileInfo(episode.OutputFilePath).Length > 0);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
