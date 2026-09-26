using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
        Task SyncRealDramaEpisodesAsync(DramaModel drama, CancellationToken ct = default);
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

            var ytdlpTarget = urlOrAlbumId.Trim();
            if (!ytdlpTarget.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                ytdlpTarget = $"ytsearch30:{ytdlpTarget} full episode";
            }

            var json = await RunYtDlpDumpAsync(ytdlpTarget, ct);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("title", out var tProp) && !string.IsNullOrEmpty(tProp.GetString()))
                    {
                        title = tProp.GetString()!;
                        title = Regex.Replace(title, @"\s*[-–|]\s*(WeTV|iQIYI|Tencent Video|HongGuo|爱奇艺|腾讯视频).*$", "", RegexOptions.IgnoreCase).Trim();
                    }

                    if (root.TryGetProperty("thumbnail", out var thProp) && !string.IsNullOrEmpty(thProp.GetString()))
                    {
                        coverUrl = thProp.GetString()!;
                    }

                    if (root.TryGetProperty("description", out var descProp) && !string.IsNullOrEmpty(descProp.GetString()))
                    {
                        summary = descProp.GetString()!;
                    }

                    var drama = new DramaModel
                    {
                        Id = urlOrAlbumId,
                        Title = string.IsNullOrWhiteSpace(title) ? CleanUrlToTitle(urlOrAlbumId, detectedPlatform) : title,
                        ChineseTitle = title,
                        Platform = detectedPlatform,
                        Resolution = detectedPlatform == PlatformType.HongGuo ? "1080p Vertical FHD" : "4K Ultra HD",
                        CoverUrl = string.IsNullOrWhiteSpace(coverUrl) ? "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350" : coverUrl,
                        Summary = string.IsNullOrWhiteSpace(summary) ? "Official High-Definition Streaming Drama Series with Multi-Language Subtitles." : summary
                    };

                    drama.Tags.Add(detectedPlatform == PlatformType.HongGuo ? "短剧" : "电视剧");
                    drama.Tags.Add("热播");
                    drama.Tags.Add("高清");

                    // Check for playlist entries
                    if (root.TryGetProperty("entries", out var entriesProp) && entriesProp.ValueKind == JsonValueKind.Array)
                    {
                        int epIndex = 1;
                        foreach (var entry in entriesProp.EnumerateArray())
                        {
                            var epTitle = entry.TryGetProperty("title", out var etProp) ? etProp.GetString() ?? $"EP{epIndex:D2}" : $"EP{epIndex:D2}";
                            var sec = entry.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out var dSec) ? (int)dSec : 0;
                            var epUrl = entry.TryGetProperty("url", out var uProp) ? uProp.GetString() ?? "" : "";
                            if (string.IsNullOrEmpty(epUrl) && entry.TryGetProperty("webpage_url", out var wuProp))
                            {
                                epUrl = wuProp.GetString() ?? "";
                            }

                            string epThumb = drama.CoverUrl;
                            if (entry.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
                            {
                                var lastThumb = thumbs.EnumerateArray().LastOrDefault();
                                if (lastThumb.ValueKind == JsonValueKind.Object && lastThumb.TryGetProperty("url", out var tuProp))
                                {
                                    epThumb = tuProp.GetString() ?? drama.CoverUrl;
                                }
                            }
                            else if (entry.TryGetProperty("thumbnail", out var stProp))
                            {
                                epThumb = stProp.GetString() ?? drama.CoverUrl;
                            }

                            long bytes = 0;
                            if (entry.TryGetProperty("filesize_approx", out var fapp) && fapp.TryGetInt64(out var faVal))
                                bytes = faVal;
                            else if (entry.TryGetProperty("filesize", out var fs) && fs.TryGetInt64(out var fsVal))
                                bytes = fsVal;

                            drama.Episodes.Add(new EpisodeModel
                            {
                                EpisodeNumber = epIndex,
                                Title = CleanEpisodeTitle(epTitle, epIndex),
                                Duration = FormatDuration(sec),
                                FileSize = FormatFileSize(bytes, sec),
                                ThumbnailUrl = epThumb,
                                StreamUrl = epUrl,
                                IsSelected = true
                            });

                            epIndex++;
                        }
                    }
                    else
                    {
                        // Single video URL
                        var sec = root.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out var dSec) ? (int)dSec : 0;
                        long bytes = 0;
                        if (root.TryGetProperty("filesize_approx", out var fapp) && fapp.TryGetInt64(out var faVal))
                            bytes = faVal;
                        else if (root.TryGetProperty("filesize", out var fs) && fs.TryGetInt64(out var fsVal))
                            bytes = fsVal;

                        drama.Episodes.Add(new EpisodeModel
                        {
                            EpisodeNumber = 1,
                            Title = CleanEpisodeTitle(title, 1),
                            Duration = FormatDuration(sec),
                            FileSize = FormatFileSize(bytes, sec),
                            ThumbnailUrl = drama.CoverUrl,
                            StreamUrl = urlOrAlbumId,
                            IsSelected = true
                        });
                    }

                    drama.TotalEpisodes = drama.Episodes.Count;
                    drama.EpisodeBadge = $"全{drama.Episodes.Count}集";
                    return drama;
                }
                catch { }
            }

            // Fallback for HTML page metadata
            if (Uri.TryCreate(urlOrAlbumId, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                try
                {
                    var response = await _httpClient.GetAsync(urlOrAlbumId, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var html = await response.Content.ReadAsStringAsync(ct);

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

                        var imageMatch = Regex.Match(html, @"<meta\s+property=""og:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (!imageMatch.Success)
                            imageMatch = Regex.Match(html, @"<meta\s+name=""twitter:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);

                        if (imageMatch.Success)
                        {
                            coverUrl = imageMatch.Groups[1].Value.Trim();
                            if (coverUrl.StartsWith("//")) coverUrl = "https:" + coverUrl;
                        }

                        var descMatch = Regex.Match(html, @"<meta\s+property=""og:description""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (descMatch.Success)
                        {
                            summary = HttpUtility.HtmlDecode(descMatch.Groups[1].Value).Trim();
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = CleanUrlToTitle(urlOrAlbumId, detectedPlatform);
            }

            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                coverUrl = "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350";
            }

            var fallbackDrama = new DramaModel
            {
                Id = urlOrAlbumId,
                Title = title,
                ChineseTitle = title,
                Platform = detectedPlatform,
                Resolution = "4K Ultra HD",
                TotalEpisodes = 1,
                EpisodeBadge = "全1集",
                CoverUrl = coverUrl,
                Summary = string.IsNullOrWhiteSpace(summary) ? "Official High-Definition Streaming Drama Series with Multi-Language Subtitles." : summary
            };

            fallbackDrama.Tags.Add("热播");
            fallbackDrama.Tags.Add("高清");

            fallbackDrama.Episodes.Add(new EpisodeModel
            {
                EpisodeNumber = 1,
                Title = "EP01",
                Duration = "--",
                FileSize = "--",
                ThumbnailUrl = coverUrl,
                StreamUrl = urlOrAlbumId,
                IsSelected = true
            });

            return fallbackDrama;
        }

        public async Task SyncRealDramaEpisodesAsync(DramaModel drama, CancellationToken ct = default)
        {
            if (drama == null) return;

            // If already resolved with real stream URLs and real durations, avoid duplicate network calls
            if (drama.Episodes.Count > 0 &&
                drama.Episodes.Any(e => !string.IsNullOrEmpty(e.StreamUrl) && e.StreamUrl.StartsWith("http") && e.Duration != "--"))
            {
                return;
            }

            // Extract clean title for query
            var cleanTitle = Regex.Replace(drama.Title, @"\([^)]*\)", "").Trim();
            if (string.IsNullOrWhiteSpace(cleanTitle))
            {
                cleanTitle = drama.Title;
            }

            var platformKey = drama.Platform switch
            {
                PlatformType.WeTV => "WeTV",
                PlatformType.iQIYI => "iQIYI",
                PlatformType.HongGuo => "短剧",
                _ => ""
            };

            var query = $"ytsearch{Math.Clamp(drama.TotalEpisodes, 10, 40)}:{cleanTitle} {platformKey} EP";
            var json = await RunYtDlpDumpAsync(query, ct);
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("entries", out var entriesProp) && entriesProp.ValueKind == JsonValueKind.Array)
                {
                    var realEpisodes = new List<EpisodeModel>();
                    int epIndex = 1;

                    foreach (var entry in entriesProp.EnumerateArray())
                    {
                        var epTitle = entry.TryGetProperty("title", out var etProp) ? etProp.GetString() ?? $"EP{epIndex:D2}" : $"EP{epIndex:D2}";
                        var sec = entry.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out var dSec) ? (int)dSec : 0;
                        var epUrl = entry.TryGetProperty("url", out var uProp) ? uProp.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(epUrl) && entry.TryGetProperty("webpage_url", out var wuProp))
                        {
                            epUrl = wuProp.GetString() ?? "";
                        }

                        string epThumb = drama.CoverUrl;
                        if (entry.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
                        {
                            var lastThumb = thumbs.EnumerateArray().LastOrDefault();
                            if (lastThumb.ValueKind == JsonValueKind.Object && lastThumb.TryGetProperty("url", out var tuProp))
                            {
                                epThumb = tuProp.GetString() ?? drama.CoverUrl;
                            }
                        }
                        else if (entry.TryGetProperty("thumbnail", out var stProp))
                        {
                            epThumb = stProp.GetString() ?? drama.CoverUrl;
                        }

                        long bytes = 0;
                        if (entry.TryGetProperty("filesize_approx", out var fapp) && fapp.TryGetInt64(out var faVal))
                            bytes = faVal;
                        else if (entry.TryGetProperty("filesize", out var fs) && fs.TryGetInt64(out var fsVal))
                            bytes = fsVal;

                        realEpisodes.Add(new EpisodeModel
                        {
                            EpisodeNumber = epIndex,
                            Title = CleanEpisodeTitle(epTitle, epIndex),
                            Duration = FormatDuration(sec),
                            FileSize = FormatFileSize(bytes, sec),
                            ThumbnailUrl = epThumb,
                            StreamUrl = epUrl,
                            IsSelected = true
                        });

                        epIndex++;
                    }

                    if (realEpisodes.Count > 0)
                    {
                        drama.Episodes.Clear();
                        foreach (var ep in realEpisodes)
                        {
                            drama.Episodes.Add(ep);
                        }
                        drama.TotalEpisodes = drama.Episodes.Count;
                        drama.EpisodeBadge = $"全{drama.Episodes.Count}集";
                    }
                }
            }
            catch { }
        }

        public async Task<List<DramaModel>> GetFeaturedLibraryAsync(PlatformType platform, CancellationToken ct = default)
        {
            await Task.Delay(20, ct);
            var list = new List<DramaModel>();

            if (platform == PlatformType.WeTV)
            {
                var wetvShows = new (string Title, int Eps, string[] Tags, string Cover, string Summary)[]
                {
                    ("Against The Current (逆流而上)", 40, new[] { "热播", "都市", "独播" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350", "Tan Songyun and Liu Xueyi in an Intense Push and Pull romance drama."),
                    ("Renegade Immortal (仙逆)", 52, new[] { "仙侠", "热血", "玄幻" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/19q8yj9d3bzqfqk1768287025002/350", "王林逆天修仙，踏破三界寻道，战天斗地的史诗传奇。"),
                    ("Soul Land 2: The Peerless Tang Clan (斗罗大陆2)", 52, new[] { "动漫", "玄幻", "热血" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wu7vz4vgfi8ugan1768575666842/350", "霍雨浩携手唐门重现昔日辉煌，史莱克七怪再战星罗大陆。"),
                    ("Madame Sonya (索尼亚夫人)", 36, new[] { "都市", "爱情", "剧情" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/kh22ysbh4ut91ch1786784512775/350", "Would you risk love despite the age gap? A modern emotional masterpiece."),
                    ("The Road to Splendor (Thai Ver.)", 38, new[] { "古装", "甜宠", "海外" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/0w5mbk4kmcjaq2x1787546330162/350", "Ding Yuxi and Deng Enxi in a Romance Destined by Fate."),
                    ("Supreme God Emperor (无上神帝)", 60, new[] { "玄幻", "修仙", "逆袭" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/mun1x5gdwe30r9q1762486532294/350", "一代仙王重生微末，横扫诸天万界，再登至尊神位。"),
                    ("Perfect World (完美世界)", 60, new[] { "玄幻", "神话", "热血" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/oz5ppkfjx9niv571730718320442_vgv0tm2m/350", "一粒尘可填海，一根草斩尽日月星辰！荒天帝石昊登峰造极。"),
                    ("Bound to My Missing Wife (锁爱三生)", 30, new[] { "民国", "虐恋", "豪门" }, "https://vcover-vt-pic.puui.qpic.cn/vcover_vt_pic/0/121773914004579/350", "冷酷军阀与失忆千金在乱世恩怨纠葛中再续前缘。"),
                    ("Khom Khlang (Uncut Ver.)", 24, new[] { "动作", "悬疑", "热播" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/l8u24vqut3sseql1787815415019/350", "WeTV Exclusive Action Thriller with High-Octane Martial Arts."),
                    ("The Road to Splendor (长乐曲)", 40, new[] { "古装", "悬疑", "探案" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/lyq6l6wc4nncrky1785812716372/350", "内刑司总领沈渡与刑部小吏颜幸先婚后爱，联手勘破大案。"),
                    ("Love's Ambition (许我耀眼)", 36, new[] { "都市", "商战", "爱情" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/hiw723xwp01jtp01758854547398/350", "赵露思与陈伟霆上演势均力敌的成人爱情商战博弈。"),
                    ("Obsessed (执念)", 28, new[] { "悬疑", "爱情", "都市" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/vo9o0yzkxzrn0sh1738817562858/350", "Intense romantic drama full of unexpected plot twists and emotional depth."),
                    ("Blossoms of Power (长风渡)", 40, new[] { "古装", "经商", "传奇" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/le1lbx64do19qal1783060775293/350", "纨绔子弟顾九思与布商之女柳玉茹相濡以沫成就一代商界传奇。"),
                    ("The Qinling Bronze Occult Chronicles (秦岭神树)", 36, new[] { "探险", "盗墓", "悬疑" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/27ell6rtltizhdg1776826659762/350", "吴邪深入秦岭腹地探寻千年青铜神树的终极秘辛。"),
                    ("Pursuit of Jade (追玉)", 32, new[] { "古装", "言情", "传奇" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/3p20haaqwgcp5zb1772589211786/350", "A gripping historical romance filled with thrilling mystery and passion."),
                    ("The First Jasmine (第一茉莉)", 25, new[] { "青春", "甜宠", "治愈" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wpl6nyn3iifw70h1780649837178/350", "Sweet romantic journey about youthful dreams, courage, and love."),
                    ("The Glory Fades (风华凋零)", 35, new[] { "年代", "传奇", "恩怨" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/stki3y2360atn3m1769968286667/350", "Dramatic family saga spanning decades of generational conflicts."),
                    ("Shine on Me (向光而行)", 30, new[] { "励志", "职场", "成长" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wovs9yfw0u87ukq1767076299386/350", "Inspiring workplace story about perseverance, ambition, and true romance."),
                    ("A Prophet (预言家)", 36, new[] { "科幻", "悬疑", "犯罪" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/za1c6jcuc05w85n1789963482123/350", "High concept sci-fi investigative mystery uncovering the secrets of tomorrow."),
                    ("Khom Khlang (คมขลัง)", 24, new[] { "动作", "剧情", "独播" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/2uxsii1m3u1uh2w1787814937272/350", "WeTV Original International Crime Action Hit Series."),
                    ("庆余年 第二季 (Joy of Life 2)", 36, new[] { "古装", "权谋", "玄幻" }, "https://puui.wetvinfo.com/wetv/cms/6182_1790220831_19201080.jpeg", "范闲重返京都，以智谋与天下第一棋手庆帝展开殊死博弈。"),
                    ("偷偷藏不住 (Hidden Love)", 25, new[] { "青春", "甜宠", "校园" }, "https://puui.wetvinfo.com/wetv/cms/5352_1789108391_19201080.png", "桑稚与段嘉许跨越时光的青涩与高甜守护爱恋。"),
                    ("你是我的荣耀 (You Are My Glory)", 32, new[] { "都市", "航天", "电竞" }, "https://puui.wetvinfo.com/wetv/cms/895_1790243473_19201080.jpeg", "顶流女星与航天工程师王者峡谷重逢，奔赴星辰大海。"),
                    ("陈情令 (The Untamed)", 50, new[] { "仙侠", "热血", "古装" }, "https://puui.wetvinfo.com/wetv/cms/8896_1790220120_19201080.png", "魏无羡与蓝忘机锄奸扶弱匡扶天下苍生。"),
                    ("长相思 (Lost You Forever)", 39, new[] { "神话", "言情", "虐恋" }, "https://puui.wetvinfo.com/wetv/cms/8867_1789962859_19201080.png", "小夭与玱玹、涂山璟、相柳之间的宿命纠葛与家国大爱。"),
                    ("星汉灿烂 (Love Like the Galaxy)", 56, new[] { "古装", "宅斗", "爱情" }, "https://puui.wetvinfo.com/wetv/cms/4817_1790160676_19201080.png", "程少商与少年将军凌不疑在波谲云诡的朝堂中相守相知。")
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
                            Duration = "--",
                            FileSize = "--",
                            ThumbnailUrl = cover,
                            StreamUrl = "",
                            IsSelected = true
                        });
                    }

                    list.Add(drama);
                }
            }
            else if (platform == PlatformType.HongGuo)
            {
                var hongguoShows = new (string Title, int Eps, string[] Tags, string Cover, string Summary)[]
                {
                    ("夫人虐翻全场 (Revenge of Madam)", 80, new[] { "都市", "逆袭", "热播" }, "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=500", "千金归来，马甲全开虐爆渣男反派！"),
                    ("绝世龙帅 (Dragon Commander)", 75, new[] { "战神", "逆袭", "爽剧" }, "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=500", "三年蛰伏，镇国龙帅一声令下十万将士归位！"),
                    ("首富千金的千层套路 (Billionaire Heiress)", 68, new[] { "恋爱", "甜宠", "豪门" }, "https://images.unsplash.com/photo-1517841905240-472988babdf9?w=500", "假千金真首富，玩转商海甜宠不断。"),
                    ("重返1990当首富 (Back to 1990)", 90, new[] { "重生", "商战", "逆袭" }, "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=500", "重回黄金九十年代，逆转人生踏上世界巅峰。"),
                    ("太后今天逼宫了吗 (The Empress Regnant)", 85, new[] { "古装", "穿越", "权谋" }, "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=500", "现代特工穿越成冷宫太后，反手执掌天下大权。"),
                    ("无敌神医在都市 (Urban Miracle Doctor)", 72, new[] { "神医", "打脸", "都市" }, "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=500", "太乙神针妙手回春，都市纵横无敌手。"),
                    ("天降萌宝总裁爹地请接招", 80, new[] { "萌宝", "甜宠", "豪门" }, "https://images.unsplash.com/photo-1539571696357-5a69c17a67c6?w=500", "天才萌宝携妈咪霸气回归，财阀爹地追妻火葬场。"),
                    ("开局九个未婚妻", 78, new[] { "爽剧", "修罗场", "搞笑" }, "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=500", "奉师命下山退婚，却被绝美总裁们排队倒追。")
                };

                foreach (var (title, eps, tags, cover, desc) in hongguoShows)
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
                            Duration = "--",
                            FileSize = "--",
                            ThumbnailUrl = cover,
                            StreamUrl = "",
                            IsSelected = true
                        });
                    }

                    list.Add(drama);
                }
            }
            else // iQIYI
            {
                var iqiyiShows = new (string Title, int Eps, string[] Tags, string Cover, string Summary)[]
                {
                    ("狂飙 (The Knockout)", 39, new[] { "警匪", "悬疑", "正邪博弈" }, "https://pic7.iqiyipic.com/image/20230114/8f/ba/v_172909405_m_601_m1_260_360.jpg", "扫黑除恶二十载，安欣与高启强的宿命对决。"),
                    ("苍兰诀 (Love Between Fairy and Devil)", 36, new[] { "仙侠", "甜虐", "奇幻" }, "https://pic4.iqiyipic.com/image/20220807/0c/3d/v_167824101_m_601_m1_260_360.jpg", "息山神女与月尊东方青苍旷世爱恋。"),
                    ("卿卿日常 (New Life Begins)", 40, new[] { "古装", "轻喜", "甜宠" }, "https://pic6.iqiyipic.com/image/20221110/36/44/v_170327318_m_601_m1_260_360.jpg", "新川九川擢选，展开一段啼笑皆非的温暖日常。"),
                    ("莲花楼 (Mysterious Lotus Casebook)", 40, new[] { "武侠", "悬疑", "江湖" }, "https://pic0.iqiyipic.com/image/20230723/79/fc/v_175369651_m_601_m1_260_360.jpg", "李相夷化身游医李莲花重现江湖破奇案。"),
                    ("宁安如梦 (Story of Kunning Palace)", 38, new[] { "重生", "权谋", "爱恨" }, "https://pic0.iqiyipic.com/image/20231107/b4/0f/v_176883236_m_601_m1_260_360.jpg", "姜雪宁重获新生，逆天改命救赎命运。"),
                    ("风吹半夏 (Wild Bloom)", 36, new[] { "时代", "商战", "女性励志" }, "https://pic1.iqiyipic.com/image/20221127/3f/82/v_170724622_m_601_m1_260_360.jpg", "许半夏带领兄弟在改革开放大潮中搏击钢铁商海。")
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
                            Duration = "--",
                            FileSize = "--",
                            ThumbnailUrl = cover,
                            StreamUrl = "",
                            IsSelected = true
                        });
                    }

                    list.Add(drama);
                }
            }

            return list;
        }

        private async Task<string?> RunYtDlpDumpAsync(string target, CancellationToken ct)
        {
            var ytdlpPath = ResolveYtDlpPath();
            if (!File.Exists(ytdlpPath)) return null;

            var startInfo = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $"--dump-single-json --flat-playlist --no-warnings \"{target}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);
                if (process.ExitCode == 0)
                {
                    return await outputTask;
                }
            }
            catch { }
            return null;
        }

        private static string ResolveYtDlpPath()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(appDir, "Tools", "yt-dlp.exe");
            if (File.Exists(path)) return path;

            var baseDir = AppContext.BaseDirectory;
            var path2 = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
            if (File.Exists(path2)) return path2;

            try
            {
                var devDir = Path.Combine(appDir, "..", "..", "..", "Tools", "yt-dlp.exe");
                if (File.Exists(devDir)) return Path.GetFullPath(devDir);
            }
            catch { }

            return "yt-dlp.exe";
        }

        public static string FormatDuration(int seconds)
        {
            if (seconds <= 0) return "--";
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            int remainingSeconds = seconds % 60;
            if (hours > 0)
            {
                return $"{hours}h {minutes:D2}m {remainingSeconds:D2}s";
            }
            return $"{minutes}m {remainingSeconds:D2}s";
        }

        public static string FormatFileSize(long bytes, int durationSeconds = 0)
        {
            if (bytes > 0)
            {
                double mb = bytes / (1024.0 * 1024.0);
                if (mb >= 1024)
                {
                    return $"{mb / 1024.0:F2} GB";
                }
                return $"{mb:F0} MB";
            }
            if (durationSeconds > 0)
            {
                // Realistic 1080p stream estimate: ~2.5 Mbps = ~18.75 MB/min
                long estimatedBytes = (long)(durationSeconds * (2.5 * 1000000.0 / 8.0));
                double mb = estimatedBytes / (1024.0 * 1024.0);
                if (mb >= 1024)
                {
                    return $"~{mb / 1024.0:F1} GB";
                }
                return $"~{mb:F0} MB";
            }
            return "--";
        }

        private static string CleanEpisodeTitle(string rawTitle, int index)
        {
            if (string.IsNullOrWhiteSpace(rawTitle)) return $"EP{index:D2}";
            var match = Regex.Match(rawTitle, @"\b(?:EP|Episode|E)[\s._-]*(\d+)\b", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var epNum))
            {
                return $"EP{epNum:D2}";
            }
            return $"EP{index:D2}";
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

            return $"{platform} Drama Series";
        }
    }
}
