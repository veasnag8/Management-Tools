using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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
            await Task.Delay(400, ct);

            var random = new Random();
            var detectedPlatform = DetectPlatformFromUrl(urlOrAlbumId, platform);
            var title = ExtractTitleFromInput(urlOrAlbumId, detectedPlatform);
            var albumId = string.IsNullOrWhiteSpace(urlOrAlbumId) ? $"ALB-{random.Next(100000, 999999)}" : urlOrAlbumId;

            // HongGuo micro-dramas usually have 60-100 short episodes
            int episodeCount = detectedPlatform == PlatformType.HongGuo ? random.Next(60, 100) : random.Next(24, 45);

            var drama = new DramaModel
            {
                Id = albumId,
                Title = title,
                Platform = detectedPlatform,
                Resolution = detectedPlatform == PlatformType.HongGuo ? "1080p FHD (Vertical Short Drama)" : "4K Ultra HD / 1080p",
                TotalEpisodes = episodeCount,
                CoverUrl = GetCoverForPlatform(detectedPlatform, 0),
                Summary = detectedPlatform switch
                {
                    PlatformType.HongGuo => "HongGuo (红果短剧) Trending Short Drama Series. High-bitrate 1080p vertical video.",
                    PlatformType.WeTV => "WeTV (腾讯视频) Official High-Definition Streaming Series with Dolby Surround Audio.",
                    _ => "iQiyi (爱奇艺) 4K Ultra HD Official Streaming Drama Series with Multi-Language Subtitles."
                }
            };

            for (int i = 1; i <= episodeCount; i++)
            {
                var durationStr = detectedPlatform == PlatformType.HongGuo
                    ? $"01m {random.Next(20, 58):D2}s"
                    : $"{random.Next(38, 52)}m {random.Next(10, 59):D2}s";

                drama.Episodes.Add(new EpisodeModel
                {
                    EpisodeNumber = i,
                    Title = $"EP{i:D2}",
                    Duration = durationStr,
                    ThumbnailUrl = GetEpisodeThumbnail(i),
                    StreamUrl = $"https://stream.{detectedPlatform.ToString().ToLower()}.com/video/{albumId}/ep_{i}.m3u8",
                    IsSelected = true
                });
            }

            return drama;
        }

        public async Task<List<DramaModel>> GetFeaturedLibraryAsync(PlatformType platform, CancellationToken ct = default)
        {
            await Task.Delay(200, ct);
            var list = new List<DramaModel>();

            var dramaData = platform switch
            {
                PlatformType.HongGuo => new[]
                {
                    ("The Double: Reborn Empress 墨雨云间之凤还巢", 80, "1080p Vertical FHD", "Top trending revenge & rebirth short drama on HongGuo."),
                    ("Billionaire in Disguise 隐形首富之龙王归来", 100, "1080p Vertical FHD", "Urban billionaire romance and action series."),
                    ("The Hidden Tycoon's Vengeance 绝世龙帅", 90, "1080p Vertical FHD", "Master martial artist returns to protect his family."),
                    ("Secret Heiress's Counterattack 假千金逆袭归来", 85, "1080p Vertical FHD", "High society drama and romantic counterattack."),
                    ("President's Contract Bride 霸道总裁的小娇妻", 95, "1080p Vertical FHD", "Sweet modern romance between CEO and contract bride."),
                    ("Reborn as Martial God 重生之狂神天下", 80, "1080p Vertical FHD", "Fantasy reincarnation and martial cultivation hit."),
                    ("Supreme Immortal King 绝品仙尊在都市", 100, "1080p Vertical FHD", "Urban fantasy cultivator standing atop the modern world."),
                    ("Love Beyond Time and Destiny 穿越时空之恋", 88, "1080p Vertical FHD", "Time travel historical comedy and romance.")
                },
                PlatformType.WeTV => new[]
                {
                    ("Joy of Life Season 2 庆余年2", 36, "4K Ultra HD", "Fan Xian faces dangerous court intrigues in this epic sequel."),
                    ("Hidden Love 偷偷藏不住", 25, "4K Ultra HD", "Sweet youth campus romance starring Zhao Lusi and Chen Zheyuan."),
                    ("The Untamed 陈情令", 50, "1080p FHD", "Masterpiece cultivation series with Wei Wuxian and Lan Wangji."),
                    ("Lost You Forever 长相思", 39, "4K Ultra HD", "Mythological fantasy romance in ancient Dahuang."),
                    ("You Are My Glory 你是我的荣耀", 32, "4K Ultra HD", "Aerospace engineer and top actress gaming love story."),
                    ("Love Like the Galaxy 星汉灿烂", 56, "4K Ultra HD", "General Ling Buyi and rebellious Cheng Shaoshang."),
                    ("The Longest Promise 玉骨遥", 43, "4K Ultra HD", "Prince of Kongsang and passionate princess of the Chi Clan.")
                },
                _ => new[]
                {
                    ("Story of Kunning Palace 宁安如梦", 38, "4K Ultra HD", "Jiang Xuening is reborn and strives to change her tragic fate."),
                    ("Love Between Fairy and Devil 苍兰诀", 36, "4K Ultra HD", "Dongfang Qingcang and little fairy Xiao Lanhua romance."),
                    ("Till the End of the Moon 长月烬明", 40, "4K Ultra HD", "Epic xianxia romance between Devil God and goddess Li Susu."),
                    ("Destined 长风渡", 40, "4K Ultra HD", "Playboy Gu Jiusi and clever merchant Liu Yuru journey."),
                    ("Mysterious Lotus Casebook 莲花楼", 40, "4K Ultra HD", "Legendary swordsman Li Xiangyi travels as a wandering doctor."),
                    ("My Journey to You 云之羽", 24, "4K Ultra HD", "Assassin Wufeng spy enters the mysterious Gong Residence."),
                    ("New Life Begins 卿卿日常", 40, "4K Ultra HD", "Warm and humorous daily palace life comedy.")
                }
            };

            for (int i = 0; i < dramaData.Length; i++)
            {
                var (title, epCount, resolution, summary) = dramaData[i];
                var drama = new DramaModel
                {
                    Id = $"{platform.ToString().ToUpper()}-2026-{i + 101:D3}",
                    Title = title,
                    Platform = platform,
                    Resolution = resolution,
                    TotalEpisodes = epCount,
                    CoverUrl = GetCoverForPlatform(platform, i),
                    Summary = summary
                };

                for (int ep = 1; ep <= epCount; ep++)
                {
                    var durationStr = platform == PlatformType.HongGuo
                        ? $"01m {20 + (ep * 7) % 38:D2}s"
                        : $"{40 + (ep % 8)}m {15 + (ep * 13) % 44:D2}s";

                    drama.Episodes.Add(new EpisodeModel
                    {
                        EpisodeNumber = ep,
                        Title = $"EP{ep:D2}",
                        Duration = durationStr,
                        ThumbnailUrl = GetEpisodeThumbnail(ep),
                        StreamUrl = $"https://stream.{platform.ToString().ToLower()}.com/video/{drama.Id}/ep_{ep}.m3u8",
                        IsSelected = true
                    });
                }

                list.Add(drama);
            }

            return list;
        }

        private static PlatformType DetectPlatformFromUrl(string input, PlatformType defaultPlatform)
        {
            if (string.IsNullOrWhiteSpace(input)) return defaultPlatform;
            var lower = input.ToLowerInvariant();
            if (lower.Contains("hongguo") || lower.Contains("novel.snssdk") || lower.Contains("fanqie") || lower.Contains("pipix") || lower.Contains("douyin"))
                return PlatformType.HongGuo;
            if (lower.Contains("wetv") || lower.Contains("v.qq.com") || lower.Contains("tencent"))
                return PlatformType.WeTV;
            if (lower.Contains("iqiyi") || lower.Contains("iq.com"))
                return PlatformType.iQIYI;

            return defaultPlatform;
        }

        private static string ExtractTitleFromInput(string input, PlatformType platform)
        {
            if (string.IsNullOrWhiteSpace(input)) return $"{platform} Trending Series 2026";
            if (input.Contains("/"))
            {
                var segs = input.TrimEnd('/').Split('/');
                var lastSeg = segs[^1].Replace("-", " ").Replace("_", " ");
                if (lastSeg.Length > 2) return lastSeg;
            }
            return $"{platform} Drama #{input}";
        }

        private static string GetCoverForPlatform(PlatformType platform, int index)
        {
            var covers = new[]
            {
                "https://images.unsplash.com/photo-1578022761797-b8636ac1773c?w=400&q=80",
                "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=400&q=80",
                "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=400&q=80",
                "https://images.unsplash.com/photo-1518676590629-3dcbd9c5a5c9?w=400&q=80",
                "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=400&q=80",
                "https://images.unsplash.com/photo-1524712245354-2c4e5e7121c0?w=400&q=80",
                "https://images.unsplash.com/photo-1514306191717-452ec28c7814?w=400&q=80",
                "https://images.unsplash.com/photo-1509281373149-e957c6296406?w=400&q=80"
            };
            return covers[index % covers.Length];
        }

        private static string GetEpisodeThumbnail(int episodeNum)
        {
            return "https://images.unsplash.com/photo-1485846234645-a62644f84728?w=320&q=80";
        }
    }
}
