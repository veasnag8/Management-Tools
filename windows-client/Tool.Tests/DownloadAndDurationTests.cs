using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tool.Models;
using Tool.Services;
using Xunit;

namespace Tool.Tests
{
    public class DownloadAndDurationTests
    {
        [Theory]
        [InlineData(PlatformType.HongGuo)]
        [InlineData(PlatformType.WeTV)]
        [InlineData(PlatformType.iQIYI)]
        public async Task DramaEpisodes_HaveRealisticDurationAndDiverseThumbnails(PlatformType platform)
        {
            var crawler = new DramaCrawlerService();
            var library = await crawler.GetFeaturedLibraryAsync(platform);

            Assert.NotEmpty(library);
            var drama = library.First();
            Assert.NotEmpty(drama.Episodes);

            foreach (var ep in drama.Episodes)
            {
                // Verify episode duration is between 40m and 50m
                Assert.Matches(@"^4[0-9]m \d{2}s$", ep.Duration);
                Assert.DoesNotContain("01m", ep.Duration);
            }

            // Verify episode thumbnails are not all identical
            var uniqueThumbnails = drama.Episodes.Select(e => e.ThumbnailUrl).Distinct().Count();
            Assert.True(uniqueThumbnails > 1, "Episodes should have diverse scene thumbnails, not all identical.");
        }

        [Fact]
        public async Task DownloaderService_ProducesRealMultiMegabyteMp4()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "DramaTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var downloader = new DownloaderService();
                var episode = new EpisodeModel
                {
                    EpisodeNumber = 1,
                    Title = "EP01",
                    Duration = "45m 12s",
                    StreamUrl = "https://raw.githubusercontent.com/bower-media-samples/big-buck-bunny-1080p-30s/master/video.mp4"
                };

                var success = await downloader.DownloadEpisodeAsync(episode, "TestDrama", tempDir);
                Assert.True(success);
                Assert.True(File.Exists(episode.OutputFilePath));

                var fileInfo = new FileInfo(episode.OutputFilePath!);
                // Must be a real video file (at least 1 MB, definitely not 108 bytes!)
                Assert.True(fileInfo.Length > 1_000_000, $"Expected file size > 1MB, but got {fileInfo.Length} bytes.");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
