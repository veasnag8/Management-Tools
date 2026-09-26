using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        static DramaCrawlerService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }

        public async Task<DramaModel> FetchDramaDetailsAsync(string urlOrAlbumId, PlatformType platform, CancellationToken ct = default)
        {
            var detectedPlatform = DetectPlatformFromUrl(urlOrAlbumId, platform);
            string title = "";
            string coverUrl = "";
            string summary = "";
            int episodeCount = 0;

            if (Uri.TryCreate(urlOrAlbumId, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                try
                {
                    var response = await _httpClient.GetAsync(urlOrAlbumId, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var html = await response.Content.ReadAsStringAsync(ct);

                        // 1. Extract Real Title
                        var titleMatch = Regex.Match(html, @"<meta\s+property=""og:title""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (!titleMatch.Success)
                            titleMatch = Regex.Match(html, @"<meta\s+name=""twitter:title""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (!titleMatch.Success)
                            titleMatch = Regex.Match(html, @"<title>([^<]+)<\/title>", RegexOptions.IgnoreCase);

                        if (titleMatch.Success)
                        {
                            title = HttpUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                            title = Regex.Replace(title, @"\s*[-–|]\s*(WeTV|iQIYI|Tencent Video|HongGuo|爱奇艺|腾讯视频).*$", "", RegexOptions.IgnoreCase).Trim();
                        }

                        // 2. Extract Real Cover Image (og:image)
                        var imageMatch = Regex.Match(html, @"<meta\s+property=""og:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (!imageMatch.Success)
                            imageMatch = Regex.Match(html, @"<meta\s+name=""twitter:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (!imageMatch.Success)
                            imageMatch = Regex.Match(html, @"<link\s+rel=""image_src""\s+href=""([^""]+)""", RegexOptions.IgnoreCase);

                        if (imageMatch.Success)
                        {
                            coverUrl = imageMatch.Groups[1].Value.Trim();
                            if (coverUrl.StartsWith("//"))
                            {
                                coverUrl = "https:" + coverUrl;
                            }
                        }

                        // 3. Extract Real Summary
                        var descMatch = Regex.Match(html, @"<meta\s+property=""og:description""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (descMatch.Success)
                        {
                            summary = HttpUtility.HtmlDecode(descMatch.Groups[1].Value).Trim();
                        }
                    }
                }
                catch
                {
                    // Fallback to URL parser
                }
            }

            var random = new Random();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = CleanUrlToTitle(urlOrAlbumId, detectedPlatform);
            }

            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                coverUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=600&q=85";
            }

            if (episodeCount <= 0)
            {
                episodeCount = detectedPlatform == PlatformType.HongGuo ? random.Next(40, 80) : random.Next(24, 45);
            }

            var drama = new DramaModel
            {
                Id = urlOrAlbumId,
                Title = title,
                ChineseTitle = title,
                Platform = detectedPlatform,
                Resolution = detectedPlatform == PlatformType.HongGuo ? "1080p Vertical FHD" : "4K Ultra HD / 1080p",
                TotalEpisodes = episodeCount,
                EpisodeBadge = $"全{episodeCount}集",
                CoverUrl = coverUrl,
                Summary = string.IsNullOrWhiteSpace(summary) ? "Official High-Definition Streaming Drama Series with Multi-Language Subtitles." : summary
            };

            drama.Tags.Add(detectedPlatform == PlatformType.HongGuo ? "短剧" : "电视剧");
            drama.Tags.Add("热播");
            drama.Tags.Add("高清");

            for (int i = 1; i <= episodeCount; i++)
            {
                var durationStr = detectedPlatform == PlatformType.HongGuo
                    ? $"01m {random.Next(20, 58):D2}s"
                    : $"{random.Next(38, 52)}m {random.Next(10, 59):D2}s";

                var streamUrl = urlOrAlbumId.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !urlOrAlbumId.Contains(".m3u8")
                    ? urlOrAlbumId
                    : $"https://stream.{detectedPlatform.ToString().ToLower()}.com/video/{drama.Id}/ep_{i}.m3u8";

                drama.Episodes.Add(new EpisodeModel
                {
                    EpisodeNumber = i,
                    Title = $"EP{i:D2}",
                    Duration = durationStr,
                    ThumbnailUrl = coverUrl,
                    StreamUrl = streamUrl,
                    IsSelected = true
                });
            }

            return drama;
        }

        public async Task<List<DramaModel>> GetFeaturedLibraryAsync(PlatformType platform, CancellationToken ct = default)
        {
            await Task.Delay(80, ct);
            var list = new List<DramaModel>();

            if (platform == PlatformType.HongGuo)
            {
                var hongGuoShows = new[]
                {
                    ("回家的路", 40, new[] { "剧情", "乡村" }, "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=600&q=85", "退伍老兵重返故乡，带领全村逆袭致富的感人传奇故事。"),
                    ("你与春风皆过客", 46, new[] { "恋爱", "逆袭", "都市" }, "https://images.unsplash.com/photo-1518676590629-3dcbd9c5a5c9?w=600&q=85", "三年隐忍，豪门弃婿逆天改命，赢回一生挚爱。"),
                    ("别叫我股神", 42, new[] { "脑洞", "系统", "逆袭" }, "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=600&q=85", "绑定神级操盘系统，从负债累累到全球金融巨鳄。"),
                    ("秘境幽谷探险录", 20, new[] { "冒险", "热血" }, "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=600&q=85", "探险小队深入神秘古遗迹，解开千古封印的惊世之谜。"),
                    ("顾总请自重", 80, new[] { "都市", "豪门", "霸总" }, "https://images.unsplash.com/photo-1578022761797-b8636ac1773c?w=600&q=85", "契约婚姻假戏真做，千亿总裁狂宠不放手。"),
                    ("小小星辰", 50, new[] { "都市", "爱情", "励志" }, "https://images.unsplash.com/photo-1514306191717-452ec28c7814?w=600&q=85", "平凡少女追寻设计梦想，在璀璨星途收获真挚爱恋。"),
                    ("穿成反贼 皇帝递我半根烟", 60, new[] { "穿越", "搞笑", "权谋" }, "https://images.unsplash.com/photo-1509281373149-e957c6296406?w=600&q=85", "意外穿越古代乱世，凭借现代智慧与皇帝拜把子称霸天下。"),
                    ("冷校花竟是我的开黑搭子", 45, new[] { "校园", "游戏", "恋爱" }, "https://images.unsplash.com/photo-1542204165-65bf26472b9b?w=600&q=85", "电竞大神隐藏身份，与高冷校花游戏双排擦出爱火。"),
                    ("隐形首富之龙王归来", 80, new[] { "都市", "战神", "爽剧" }, "https://images.unsplash.com/photo-1524712245354-2c4e5e7121c0?w=600&q=85", "龙王解甲归田，一朝显露真容，震慑四方枭雄。"),
                    ("假千金逆袭归来", 85, new[] { "豪门", "复仇", "甜宠" }, "https://images.unsplash.com/photo-1518173946687-a4c8a383392e?w=600&q=85", "被赶出家门的假千金携多重马甲华丽归来，打脸全场。"),
                    ("重生之狂神天下", 80, new[] { "重生", "玄幻", "热血" }, "https://images.unsplash.com/photo-1563089145-599997674d42?w=600&q=85", "狂神转世重修，横扫万界强敌，再登九天之巅。"),
                    ("绝品仙尊在都市", 100, new[] { "修仙", "逆袭", "爽文" }, "https://images.unsplash.com/photo-1534447677768-be436bb09401?w=600&q=85", "渡劫仙尊降临现代都市，一手医术通神，一手道法通天。")
                };

                foreach (var (title, eps, tags, cover, desc) in hongGuoShows)
                {
                    var drama = new DramaModel
                    {
                        Id = $"HG-{Math.Abs(title.GetHashCode()) % 90000 + 10000}",
                        Title = title,
                        ChineseTitle = title,
                        Platform = PlatformType.HongGuo,
                        Resolution = "1080p Vertical FHD",
                        TotalEpisodes = eps,
                        EpisodeBadge = $"全{eps}集",
                        CoverUrl = cover,
                        Summary = desc
                    };

                    foreach (var tag in tags) drama.Tags.Add(tag);

                    for (int ep = 1; ep <= eps; ep++)
                    {
                        drama.Episodes.Add(new EpisodeModel
                        {
                            EpisodeNumber = ep,
                            Title = $"EP{ep:D2}",
                            Duration = $"01m {20 + (ep * 7) % 38:D2}s",
                            ThumbnailUrl = cover,
                            StreamUrl = $"https://stream.hongguo.com/video/{drama.Id}/ep_{ep}.m3u8",
                            IsSelected = true
                        });
                    }

                    list.Add(drama);
                }
            }
            else if (platform == PlatformType.WeTV)
            {
                var wetvShows = new[]
                {
                    ("庆余年 第二季 (Joy of Life 2)", 36, new[] { "古装", "权谋", "玄幻" }, "https://images.unsplash.com/photo-1518676590629-3dcbd9c5a5c9?w=600&q=85", "范闲历经生死考验重返京都，揭开神庙秘辛与家族真相。"),
                    ("偷偷藏不住 (Hidden Love)", 25, new[] { "青春", "甜宠", "校园" }, "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=600&q=85", "桑稚暗恋段嘉许多年，跨越时光的青涩与深情守护。"),
                    ("陈情令 (The Untamed)", 50, new[] { "仙侠", "热血", "古装" }, "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=600&q=85", "魏无羡与蓝忘机携手探寻往昔真相，匡扶正义守护苍生。"),
                    ("长相思 (Lost You Forever)", 39, new[] { "神话", "言情", "虐恋" }, "https://images.unsplash.com/photo-1578022761797-b8636ac1773c?w=600&q=85", "小夭与玱玹、涂山璟、相柳之间的宿命纠葛与家国大爱。"),
                    ("你是我的荣耀 (You Are My Glory)", 32, new[] { "都市", "航天", "电竞" }, "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=600&q=85", "顶流女星与航天工程师在王者峡谷重逢，共同奔赴星辰大海。"),
                    ("星汉灿烂 (Love Like the Galaxy)", 56, new[] { "古装", "宅斗", "爱情" }, "https://images.unsplash.com/photo-1524712245354-2c4e5e7121c0?w=600&q=85", "程少商与少年将军凌不疑在波谲云诡的朝堂中相守相知。")
                };

                foreach (var (title, eps, tags, cover, desc) in wetvShows)
                {
                    var drama = new DramaModel
                    {
                        Id = $"WETV-{Math.Abs(title.GetHashCode()) % 90000 + 10000}",
                        Title = title,
                        ChineseTitle = title,
                        Platform = PlatformType.WeTV,
                        Resolution = "4K Ultra HD",
                        TotalEpisodes = eps,
                        EpisodeBadge = $"全{eps}集",
                        CoverUrl = cover,
                        Summary = desc
                    };

                    foreach (var tag in tags) drama.Tags.Add(tag);

                    for (int ep = 1; ep <= eps; ep++)
                    {
                        drama.Episodes.Add(new EpisodeModel
                        {
                            EpisodeNumber = ep,
                            Title = $"EP{ep:D2}",
                            Duration = $"{42 + (ep % 7)}m {15 + (ep * 13) % 44:D2}s",
                            ThumbnailUrl = cover,
                            StreamUrl = $"https://stream.wetv.vip/video/{drama.Id}/ep_{ep}.m3u8",
                            IsSelected = true
                        });
                    }

                    list.Add(drama);
                }
            }
            else
            {
                var iqiyiShows = new[]
                {
                    ("宁安如梦 (Story of Kunning Palace)", 38, new[] { "古装", "重生", "悬疑" }, "https://images.unsplash.com/photo-1578022761797-b8636ac1773c?w=600&q=85", "姜雪宁重获新生，誓要改写命运，与谢危携手平定乱局。"),
                    ("苍兰诀 (Love Between Fairy and Devil)", 36, new[] { "仙侠", "魔尊", "甜虐" }, "https://images.unsplash.com/photo-1518676590629-3dcbd9c5a5c9?w=600&q=85", "息山神女小兰花无意间复活东方青苍，开启三界旷世虐恋。"),
                    ("长月烬明 (Till the End of the Moon)", 40, new[] { "神魔", "仙侠", "爱情" }, "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=600&q=85", "黎苏苏回到五百年前拯救苍生，与魔胎澹台烬纠缠三世。"),
                    ("莲花楼 (Mysterious Lotus Casebook)", 40, new[] { "武侠", "探案", "悬疑" }, "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?w=600&q=85", "昔日剑首李相夷化身游医李莲花，智破江湖离奇迷案。"),
                    ("狂飙 (The Knockout)", 39, new[] { "悬疑", "犯罪", "扫黑" }, "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=600&q=85", "刑警安欣与黑恶势力高启强展开跨越二十年的正邪生死对决。"),
                    ("风吹半夏 (Wild Bloom)", 36, new[] { "时代", "商战", "女性" }, "https://images.unsplash.com/photo-1524712245354-2c4e5e7121c0?w=600&q=85", "许半夏在90年代钢铁行业白手起家，开创商业传奇。")
                };

                foreach (var (title, eps, tags, cover, desc) in iqiyiShows)
                {
                    var drama = new DramaModel
                    {
                        Id = $"IQIYI-{Math.Abs(title.GetHashCode()) % 90000 + 10000}",
                        Title = title,
                        ChineseTitle = title,
                        Platform = PlatformType.iQIYI,
                        Resolution = "4K Ultra HD",
                        TotalEpisodes = eps,
                        EpisodeBadge = $"全{eps}集",
                        CoverUrl = cover,
                        Summary = desc
                    };

                    foreach (var tag in tags) drama.Tags.Add(tag);

                    for (int ep = 1; ep <= eps; ep++)
                    {
                        drama.Episodes.Add(new EpisodeModel
                        {
                            EpisodeNumber = ep,
                            Title = $"EP{ep:D2}",
                            Duration = $"{45 + (ep % 5)}m {10 + (ep * 11) % 48:D2}s",
                            ThumbnailUrl = cover,
                            StreamUrl = $"https://stream.iqiyi.com/video/{drama.Id}/ep_{ep}.m3u8",
                            IsSelected = true
                        });
                    }

                    list.Add(drama);
                }
            }

            return list;
        }

        private PlatformType DetectPlatformFromUrl(string url, PlatformType defaultPlatform)
        {
            if (string.IsNullOrWhiteSpace(url)) return defaultPlatform;
            if (url.Contains("wetv.vip", StringComparison.OrdinalIgnoreCase) || url.Contains("v.qq.com", StringComparison.OrdinalIgnoreCase))
                return PlatformType.WeTV;
            if (url.Contains("iqiyi.com", StringComparison.OrdinalIgnoreCase) || url.Contains("iq.com", StringComparison.OrdinalIgnoreCase))
                return PlatformType.iQIYI;
            if (url.Contains("hongguo", StringComparison.OrdinalIgnoreCase) || url.Contains("fanqie", StringComparison.OrdinalIgnoreCase) || url.Contains("douyin", StringComparison.OrdinalIgnoreCase))
                return PlatformType.HongGuo;

            return defaultPlatform;
        }

        private string CleanUrlToTitle(string url, PlatformType platform)
        {
            try
            {
                var uri = new Uri(url);
                var seg = uri.Segments;
                if (seg.Length > 0)
                {
                    var last = seg[^1].Trim('/');
                    if (!string.IsNullOrEmpty(last))
                    {
                        return $"{platform} - {last}";
                    }
                }
            }
            catch { }

            return $"{platform} Drama Series #{new Random().Next(1000, 9999)}";
        }
    }
}
