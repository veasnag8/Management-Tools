using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Tool.Models;

namespace Tool.Services
{
    public interface IDramaCrawlerService
    {
        Task<DramaModel> FetchDramaDetailsAsync(string urlOrAlbumId, PlatformType platform, CancellationToken ct = default);
        Task<List<DramaModel>> GetFeaturedLibraryAsync(PlatformType platform, CancellationToken ct = default);
    }

    public class DramaCrawlerService : IDramaCrawlerService
    {
        public async Task<DramaModel> FetchDramaDetailsAsync(string urlOrAlbumId, PlatformType platform, CancellationToken ct = default)
        {
            // Simulate network extraction
            await Task.Delay(600, ct);

            var random = new Random();
            var episodeCount = random.Next(16, 40);
            var title = ExtractTitleFromInput(urlOrAlbumId, platform);
            var albumId = string.IsNullOrWhiteSpace(urlOrAlbumId) ? $"ALB-{random.Next(100000, 999999)}" : urlOrAlbumId;

            var drama = new DramaModel
            {
                Id = albumId,
                Title = title,
                Platform = platform,
                Resolution = "1080p FHD",
                TotalEpisodes = episodeCount,
                CoverUrl = GetCoverForPlatform(platform, 0),
                Summary = $"Popular trending drama on {platform}. High-bitrate streaming with Dolby stereo audio track."
            };

            for (int i = 1; i <= episodeCount; i++)
            {
                var durationMinutes = random.Next(38, 52);
                var durationSeconds = random.Next(10, 59);

                drama.Episodes.Add(new EpisodeModel
                {
                    EpisodeNumber = i,
                    Title = $"EP{i:D2}",
                    Duration = $"{durationMinutes}m {durationSeconds}s",
                    ThumbnailUrl = GetEpisodeThumbnail(i),
                    StreamUrl = $"https://stream.{platform.ToString().ToLower()}.com/video/live/{albumId}/ep_{i}.m3u8",
                    IsSelected = true
                });
            }

            return drama;
        }

        public async Task<List<DramaModel>> GetFeaturedLibraryAsync(PlatformType platform, CancellationToken ct = default)
        {
            await Task.Delay(300, ct);
            var list = new List<DramaModel>();

            var sampleTitles = platform switch
            {
                PlatformType.HongGuo => new[]
                {
                    "Love Between Fairy and Devil 苍兰诀",
                    "Till the End of the Moon 长月烬明",
                    "My Journey to You 云之羽",
                    "The Double 墨雨云间",
                    "Story of Kunning Palace 宁安如梦"
                },
                PlatformType.WeTV => new[]
                {
                    "Hidden Love 偷偷藏不住",
                    "Joy of Life Season 2 庆余年2",
                    "The Untamed 陈情令",
                    "You Are My Glory 你是我的荣耀",
                    "Lost You Forever 长相思"
                },
                _ => new[]
                {
                    "Story of Yanxi Palace 延禧攻略",
                    "New Life Begins 卿卿日常",
                    "Destined 长风渡",
                    "Strange Tales of Tang Dynasty 唐朝诡事录",
                    "Princess Agents 楚乔传"
                }
            };

            for (int i = 0; i < sampleTitles.Length; i++)
            {
                var drama = new DramaModel
                {
                    Id = $"HG-2026-{i + 101}",
                    Title = sampleTitles[i],
                    Platform = platform,
                    Resolution = i % 2 == 0 ? "4K Ultra HD" : "1080p FHD",
                    TotalEpisodes = 24 + i * 4,
                    CoverUrl = GetCoverForPlatform(platform, i),
                    Summary = $"Featured hit romantic drama series streaming in high definition."
                };

                for (int ep = 1; ep <= drama.TotalEpisodes; ep++)
                {
                    drama.Episodes.Add(new EpisodeModel
                    {
                        EpisodeNumber = ep,
                        Title = $"EP{ep:D2}",
                        Duration = $"{42 + (ep % 7)}m {15 + (ep % 40)}s",
                        ThumbnailUrl = GetEpisodeThumbnail(ep),
                        StreamUrl = $"https://cdn.streaming.net/{drama.Id}/ep_{ep}.m3u8",
                        IsSelected = true
                    });
                }

                list.Add(drama);
            }

            return list;
        }

        private static string ExtractTitleFromInput(string input, PlatformType platform)
        {
            if (string.IsNullOrWhiteSpace(input)) return $"{platform} Trending Series 2026";
            if (input.Contains("/"))
            {
                var segs = input.TrimEnd('/').Split('/');
                return segs[^1].Replace("-", " ").Replace("_", " ");
            }
            return $"{platform} Show #{input}";
        }

        private static string GetCoverForPlatform(PlatformType platform, int index)
        {
            var covers = new[]
            {
                "https://images.unsplash.com/photo-1578022761797-b8636ac1773c?w=400&q=80",
                "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=400&q=80",
                "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=400&q=80",
                "https://images.unsplash.com/photo-1518676590629-3dcbd9c5a5c9?w=400&q=80",
                "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=400&q=80"
            };
            return covers[index % covers.Length];
        }

        private static string GetEpisodeThumbnail(int episodeNum)
        {
            return "https://images.unsplash.com/photo-1485846234645-a62644f84728?w=320&q=80";
        }
    }
}
