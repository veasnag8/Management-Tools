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

                        var descMatch = Regex.Match(html, @"<meta\s+property=""og:description""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
                        if (descMatch.Success)
                        {
                            summary = HttpUtility.HtmlDecode(descMatch.Groups[1].Value).Trim();
                        }
                    }
                }
                catch { }
            }

            var random = new Random();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = CleanUrlToTitle(urlOrAlbumId, detectedPlatform);
            }

            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                coverUrl = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=600&q=85";
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
                Resolution = detectedPlatform == PlatformType.HongGuo ? "1080p Vertical FHD" : "4K Ultra HD",
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
            await Task.Delay(50, ct);
            var list = new List<DramaModel>();

            if (platform == PlatformType.HongGuo)
            {
                var hongGuoShows = new (string Title, int Eps, string[] Tags, string Cover, string Summary)[]
                {
                    ("回家的路", 40, new[] { "剧情", "乡村" }, "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&q=85", "退伍老兵重返故乡，带领全村逆袭致富的感人传奇故事。"),
                    ("你与春风皆过客", 46, new[] { "恋爱", "逆袭", "都市" }, "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=600&q=85", "三年隐忍，豪门弃婿逆天改命，赢回一生挚爱。"),
                    ("别叫我股神", 42, new[] { "脑洞", "系统", "逆袭" }, "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=600&q=85", "绑定神级操盘系统，从负债累累到全球金融巨鳄。"),
                    ("秘境幽谷探险录", 20, new[] { "冒险", "热血" }, "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=600&q=85", "探险小队深入神秘古遗迹，解开千古封印的惊世之谜。"),
                    ("顾总请自重", 80, new[] { "都市", "豪门", "霸总" }, "https://images.unsplash.com/photo-1539571696357-5a69c17a67c6?w=600&q=85", "契约婚姻假戏真做，千亿总裁狂宠不放手。"),
                    ("小小星辰", 50, new[] { "都市", "爱情", "励志" }, "https://images.unsplash.com/photo-1517841905240-472988babdf9?w=600&q=85", "平凡少女追寻设计梦想，在璀璨星途收获真挚爱恋。"),
                    ("穿成反贼 皇帝递我半根烟", 60, new[] { "穿越", "搞笑", "权谋" }, "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=600&q=85", "意外穿越古代乱世，凭借现代智慧与皇帝拜把子称霸天下。"),
                    ("冷校花竟是我的开黑搭子", 45, new[] { "校园", "游戏", "恋爱" }, "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=600&q=85", "电竞大神隐藏身份，与高冷校花游戏双排擦出爱火。"),
                    ("隐形首富之龙王归来", 80, new[] { "都市", "战神", "爽剧" }, "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=600&q=85", "龙王解甲归田，一朝显露真容，震慑四方枭雄。"),
                    ("假千金逆袭归来", 85, new[] { "豪门", "复仇", "甜宠" }, "https://images.unsplash.com/photo-1529626455594-4ff0802cfb7e?w=600&q=85", "被赶出家门的假千金携多重马甲华丽归来，打脸全场。"),
                    ("重生之狂神天下", 80, new[] { "重生", "玄幻", "热血" }, "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?w=600&q=85", "狂神转世重修，横扫万界强敌，再登九天之巅。"),
                    ("绝品仙尊在都市", 100, new[] { "修仙", "逆袭", "爽文" }, "https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=600&q=85", "渡劫仙尊降临现代都市，一手医术通神，一手道法通天。"),
                    ("绝品神医闯都市", 88, new[] { "神医", "无敌", "都市" }, "https://images.unsplash.com/photo-1508214751196-bcfd4ca60f91?w=600&q=85", "奉师命下山退婚，却意外成为江南第一神医。"),
                    ("离婚后，前妻哭着求复合", 90, new[] { "虐恋", "豪门", "逆袭" }, "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=600&q=85", "签字离婚的那一刻，他千亿财阀继承人的身份曝光。"),
                    ("无双龙帅", 100, new[] { "战神", "热血", "爽文" }, "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&q=85", "统帅百万将士守护国门，凯旋归来扫平一切不公。"),
                    ("真假千金大对决", 75, new[] { "真假千金", "豪门", "打脸" }, "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=600&q=85", "真千金王者归来，智商碾压全场恶毒反派。"),
                    ("开局觉醒至尊神瞳", 85, new[] { "异能", "鉴宝", "爽文" }, "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=600&q=85", "双眼能鉴天下古玩，赌石成神，财富惊天动地。"),
                    ("我真没想当首富啊", 70, new[] { "神豪", "系统", "都市" }, "https://images.unsplash.com/photo-1539571696357-5a69c17a67c6?w=600&q=85", "花钱就能百倍返现，随手投资成就万亿商业帝国。"),
                    ("仙王回归当保安", 95, new[] { "仙尊", "扮猪吃虎" }, "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=600&q=85", "九天仙王隐居都市大厦当保安，悄然守护绝美总裁。"),
                    ("新婚夜，植物人老公站起来了", 80, new[] { "甜宠", "先婚后爱" }, "https://images.unsplash.com/photo-1517841905240-472988babdf9?w=600&q=85", "替嫁给植物人少爷，新婚夜他竟然苏醒并将她宠上天。"),
                    ("退婚后我成了千亿大佬", 82, new[] { "逆袭", "神豪", "打脸" }, "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=600&q=85", "被前女友嫌贫爱富退婚，转身接管全球顶级财团。"),
                    ("第一狂婿", 92, new[] { "赘婿", "无敌", "热血" }, "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=600&q=85", "入赘三年人人嘲笑，一朝龙抬头，万家俯首称臣。"),
                    ("女总裁的贴身兵王", 88, new[] { "兵王", "都市", "保镖" }, "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=600&q=85", "最强兵王回归都市，贴身保护绝美女总裁一路逆袭。"),
                    ("闪婚后千亿总裁马甲掉了", 78, new[] { "甜宠", "豪门", "恋爱" }, "https://images.unsplash.com/photo-1529626455594-4ff0802cfb7e?w=600&q=85", "相亲闪婚的外卖小哥，居然是身价千亿的帝国首富。")
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
                var wetvShows = new (string Title, int Eps, string[] Tags, string Cover, string Summary)[]
                {
                    ("庆余年 第二季 (Joy of Life 2)", 36, new[] { "古装", "权谋", "玄幻" }, "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&q=85", "范闲历经生死考验重返京都，揭开神庙秘辛与家族真相。"),
                    ("偷偷藏不住 (Hidden Love)", 25, new[] { "青春", "甜宠", "校园" }, "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=600&q=85", "桑稚暗恋段嘉许多年，跨越时光的青涩与深情守护。"),
                    ("陈情令 (The Untamed)", 50, new[] { "仙侠", "热血", "古装" }, "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=600&q=85", "魏无羡与蓝忘机携手探寻往昔真相，匡扶正义守护苍生。"),
                    ("长相思 (Lost You Forever)", 39, new[] { "神话", "言情", "虐恋" }, "https://images.unsplash.com/photo-1517841905240-472988babdf9?w=600&q=85", "小夭与玱玹、涂山璟、相柳之间的宿命纠葛与家国大爱。"),
                    ("你是我的荣耀 (You Are My Glory)", 32, new[] { "都市", "航天", "电竞" }, "https://images.unsplash.com/photo-1539571696357-5a69c17a67c6?w=600&q=85", "顶流女星与航天工程师在王者峡谷重逢，共同奔赴星辰大海。"),
                    ("星汉灿烂 (Love Like the Galaxy)", 56, new[] { "古装", "宅斗", "爱情" }, "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=600&q=85", "程少商与少年将军凌不疑在波谲云诡的朝堂中相守相知。"),
                    ("梦华录 (A Dream of Splendor)", 40, new[] { "古装", "女性", "励志" }, "https://images.unsplash.com/photo-1529626455594-4ff0802cfb7e?w=600&q=85", "赵盼儿与姐妹三人东京创业，书写北宋传奇女子风采。"),
                    ("繁花 (Blossoms Shanghai)", 30, new[] { "时代", "商战", "剧情" }, "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=600&q=85", "九十年代上海滩风起云涌，阿宝在时代大潮中搏击商海。"),
                    ("与凤行 (The Legend of ShenLi)", 39, new[] { "仙侠", "神话", "恋爱" }, "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?w=600&q=85", "灵界碧苍王沈璃逃婚坠入凡间，与上古神行止结下不解之缘。"),
                    ("承欢记 (Best Choice Ever)", 37, new[] { "都市", "家庭", "成长" }, "https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=600&q=85", "麦承欢在母女关系与职场风波中破茧成蝶，走出独立人生。"),
                    ("春色寄情人 (Will Love in Spring)", 21, new[] { "治愈", "爱情", "都市" }, "https://images.unsplash.com/photo-1508214751196-bcfd4ca60f91?w=600&q=85", "遗体整容师与残疾医疗销售在故乡小镇相互治愈的纯爱故事。"),
                    ("玫瑰的故事 (The Tale of Rose)", 38, new[] { "都市", "情感", "女性" }, "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=600&q=85", "黄亦玫跨越二十年的四段情感历程与自我觉醒之路。"),
                    ("雪中悍刀行 (Sword Snow Stride)", 38, new[] { "武侠", "江湖", "权谋" }, "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=600&q=85", "北椋世子徐凤年千里历练，终成一代北椋王。"),
                    ("斗罗大陆 (Douluo Continent)", 40, new[] { "玄幻", "热血", "修真" }, "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=600&q=85", "唐三携史莱克七怪勇闯魂师界，创立唐门名震大陆。")
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
                var iqiyiShows = new (string Title, int Eps, string[] Tags, string Cover, string Summary)[]
                {
                    ("宁安如梦 (Story of Kunning Palace)", 38, new[] { "古装", "重生", "悬疑" }, "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=600&q=85", "姜雪宁重获新生，誓要改写命运，与谢危携手平定乱局。"),
                    ("苍兰诀 (Love Between Fairy and Devil)", 36, new[] { "仙侠", "魔尊", "甜虐" }, "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&q=85", "息山神女小兰花无意间复活东方青苍，开启三界旷世虐恋。"),
                    ("长月烬明 (Till the End of the Moon)", 40, new[] { "神魔", "仙侠", "爱情" }, "https://images.unsplash.com/photo-1517841905240-472988babdf9?w=600&q=85", "黎苏苏回到五百年前拯救苍生，与魔胎澹台烬纠缠三世。"),
                    ("莲花楼 (Mysterious Lotus Casebook)", 40, new[] { "武侠", "探案", "悬疑" }, "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=600&q=85", "昔日剑首李相夷化身游医李莲花，智破江湖离奇迷案。"),
                    ("狂飙 (The Knockout)", 39, new[] { "悬疑", "犯罪", "扫黑" }, "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=600&q=85", "刑警安欣与黑恶势力高启强展开跨越二十年的正邪生死对决。"),
                    ("风吹半夏 (Wild Bloom)", 36, new[] { "时代", "商战", "女性" }, "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=600&q=85", "许半夏在90年代钢铁行业白手起家，开创商业传奇。"),
                    ("唐朝诡事录之西行", 40, new[] { "古装", "悬疑", "探案" }, "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=600&q=85", "苏无名与卢凌风西行一路屡破奇案，揭示大唐盛世隐秘。"),
                    ("卿卿日常 (New Life Begins)", 40, new[] { "古装", "轻喜", "甜宠" }, "https://images.unsplash.com/photo-1529626455594-4ff0802cfb7e?w=600&q=85", "李薇与六少主尹峥在九川联姻中相守相伴，烟火日常温馨动人。"),
                    ("云之羽 (My Journey to You)", 24, new[] { "江湖", "谍战", "悬疑" }, "https://images.unsplash.com/photo-1539571696357-5a69c17a67c6?w=600&q=85", "无锋刺客云为衫潜入宫门，与叛逆公子宫子羽展开生死博弈。"),
                    ("一念关山 (A Journey to Love)", 40, new[] { "武侠", "公路", "热血" }, "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=600&q=85", "任如意与宁远舟率领使团跨越千山万水，匡扶江山社稷。"),
                    ("追风者 (War of Faith)", 38, new[] { "民国", "金融", "谍战" }, "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?w=600&q=85", "金融天才魏若来在动荡上海滩探寻救国真理，成长为红色金融家。"),
                    ("我的阿勒泰 (To the Wonder)", 8, new[] { "治愈", "自然", "成长" }, "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=600&q=85", "汉族少女李文秀在新疆阿勒泰辽阔草原上寻觅生命纯真与诗意。")
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
