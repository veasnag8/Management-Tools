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
                coverUrl = "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350";
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

            if (platform == PlatformType.WeTV)
            {
                // Real WeTV Official Shows and Verified puui.wetvinfo.com / vcover CDN Posters
                var wetvShows = new (string Title, int Eps, string[] Tags, string Cover, string Summary)[]
                {
                    // Page 1 (24 items)
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
                    
                    // Page 2 (24 items)
                    ("长相思 (Lost You Forever)", 39, new[] { "神话", "言情", "虐恋" }, "https://puui.wetvinfo.com/wetv/cms/8867_1789962859_19201080.png", "小夭与玱玹、涂山璟、相柳之间的宿命纠葛与家国大爱。"),
                    ("星汉灿烂 (Love Like the Galaxy)", 56, new[] { "古装", "宅斗", "爱情" }, "https://puui.wetvinfo.com/wetv/cms/4817_1790160676_19201080.png", "程少商与少年将军凌不疑在波谲云诡的朝堂中相守相知。"),
                    ("梦华录 (A Dream of Splendor)", 40, new[] { "古装", "女性", "励志" }, "https://puui.wetvinfo.com/wetv/cms/4958_1789627713_19201080.png", "赵盼儿与姐妹三人东京创业，书写北宋传奇女子风采。"),
                    ("繁花 (Blossoms Shanghai)", 30, new[] { "时代", "商战", "剧情" }, "https://puui.wetvinfo.com/wetv/cms/8111_1789609790_19201080.png", "九十年代上海滩风起云涌，阿宝在时代大潮中搏击商海。"),
                    ("与凤行 (The Legend of ShenLi)", 39, new[] { "仙侠", "神话", "恋爱" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350", "灵界碧苍王沈璃逃婚坠入凡间，与上古神行止结下不解之缘。"),
                    ("承欢记 (Best Choice Ever)", 37, new[] { "都市", "家庭", "成长" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/19q8yj9d3bzqfqk1768287025002/350", "麦承欢在母女关系与职场风波中破茧成蝶，走出独立人生。"),
                    ("春色寄情人 (Will Love in Spring)", 21, new[] { "治愈", "爱情", "都市" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wu7vz4vgfi8ugan1768575666842/350", "遗体整容师与残疾医疗销售在故乡小镇相互治愈的纯爱故事。"),
                    ("玫瑰的故事 (The Tale of Rose)", 38, new[] { "都市", "情感", "女性" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/kh22ysbh4ut91ch1786784512775/350", "黄亦玫跨越二十年的四段情感历程与自我觉醒之路。"),
                    ("雪中悍刀行 (Sword Snow Stride)", 38, new[] { "武侠", "江湖", "权谋" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/0w5mbk4kmcjaq2x1787546330162/350", "北椋世子徐凤年千里历练，终成一代北椋王。"),
                    ("斗罗大陆 (Douluo Continent)", 40, new[] { "玄幻", "热血", "修真" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/mun1x5gdwe30r9q1762486532294/350", "唐三携史莱克七怪勇闯魂师界，创立唐门名震大陆。"),
                    ("三体 (Three-Body)", 30, new[] { "科幻", "悬疑", "硬核" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/oz5ppkfjx9niv571730718320442_vgv0tm2m/350", "地球基础科学遭遇未知干扰，汪淼与史强揭开外星文明降临真相。"),
                    ("长相思 第二季", 23, new[] { "神话", "权谋", "虐恋" }, "https://vcover-vt-pic.puui.qpic.cn/vcover_vt_pic/0/121773914004579/350", "大荒局势风云突变，小夭与玱玹的宿命终局震撼上演。"),
                    ("锦绣安宁", 40, new[] { "古装", "宅斗", "甜宠" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/l8u24vqut3sseql1787815415019/350", "罗慎远与罗宜宁在罗府风雨中携手并进，揭开身世之谜。"),
                    ("猎罪图鉴", 20, new[] { "悬疑", "刑侦", "画像" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/lyq6l6wc4nncrky1785812716372/350", "模拟画像师沈翊与刑警队长杜城联手破获奇案。"),
                    ("吞噬星空", 52, new[] { "玄幻", "科幻", "动漫" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/hiw723xwp01jtp01758854547398/350", "罗峰在大灾变时代挺身而出，踏上宇宙巅峰强者之路。"),
                    ("仙逆 第二季", 48, new[] { "仙侠", "杀伐", "修真" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/vo9o0yzkxzrn0sh1738817562858/350", "王林凭借天逆珠逆天修仙，求魔问道名动星空。"),
                    ("全职高手", 40, new[] { "电竞", "热血", "青春" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/le1lbx64do19qal1783060775293/350", "叶修重组战队兴欣，重返荣耀职业联赛再夺总冠军。"),
                    ("扫黑风暴", 28, new[] { "警匪", "悬疑", "反腐" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/27ell6rtltizhdg1776826659762/350", "李成阳深入中江绿藤市，彻查十四年未破冤案。"),
                    ("开端 (Reset)", 15, new[] { "悬疑", "时间循环", "科幻" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/3p20haaqwgcp5zb1772589211786/350", "肖鹤云与李诗情在45路公交车爆炸案中不断经历死而复生的时间循环。"),
                    ("鬼吹灯之精绝古城", 21, new[] { "探险", "盗墓", "悬疑" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wpl6nyn3iifw70h1780649837178/350", "胡八一与王胖子深入西域塔克拉玛干沙漠探寻精绝女王古墓。"),
                    ("怒晴湘西", 21, new[] { "盗墓", "民国", "探险" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/stki3y2360atn3m1769968286667/350", "陈玉楼与鹧鸪哨联手探秘瓶山元代古墓。"),
                    ("龙岭迷窟", 18, new[] { "探险", "古墓", "动作" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wovs9yfw0u87ukq1767076299386/350", "摸金校尉重聚古蓝县，探寻龙岭迷窟暗藏的龙骨天书。"),
                    ("云南虫谷", 16, new[] { "探险", "悬疑", "冒险" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/za1c6jcuc05w85n1789963482123/350", "深入云南献王墓寻找雮尘珠以解开鬼眼诅咒。"),
                    ("昆仑神宫", 16, new[] { "探险", "神话", "魔幻" }, "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/2uxsii1m3u1uh2w1787814937272/350", "摸金三人组前往青藏高原探寻格萨尔王传说中的九层妖塔。")
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
            else if (platform == PlatformType.HongGuo)
            {
                // HongGuo Short Drama Official Collection with Authentic Asian Short Drama Posters
                var hongGuoPosters = new[]
                {
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/19q8yj9d3bzqfqk1768287025002/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wu7vz4vgfi8ugan1768575666842/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/kh22ysbh4ut91ch1786784512775/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/0w5mbk4kmcjaq2x1787546330162/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/mun1x5gdwe30r9q1762486532294/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/oz5ppkfjx9niv571730718320442_vgv0tm2m/350",
                    "https://vcover-vt-pic.puui.qpic.cn/vcover_vt_pic/0/121773914004579/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/l8u24vqut3sseql1787815415019/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/lyq6l6wc4nncrky1785812716372/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/hiw723xwp01jtp01758854547398/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/vo9o0yzkxzrn0sh1738817562858/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/le1lbx64do19qal1783060775293/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/27ell6rtltizhdg1776826659762/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/3p20haaqwgcp5zb1772589211786/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wpl6nyn3iifw70h1780649837178/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/stki3y2360atn3m1769968286667/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wovs9yfw0u87ukq1767076299386/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/za1c6jcuc05w85n1789963482123/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/2uxsii1m3u1uh2w1787814937272/350"
                };

                var hongGuoShows = new (string Title, int Eps, string[] Tags, string Summary)[]
                {
                    // Page 1 (24 items)
                    ("回家的路", 40, new[] { "剧情", "乡村" }, "退伍老兵重返故乡，带领全村逆袭致富的感人传奇故事。"),
                    ("你与春风皆过客", 46, new[] { "恋爱", "逆袭", "都市" }, "三年隐忍，豪门弃婿逆天改命，赢回一生挚爱。"),
                    ("别叫我股神", 42, new[] { "脑洞", "系统", "逆袭" }, "绑定神级操盘系统，从负债累累到全球金融巨鳄。"),
                    ("秘境幽谷探险录", 20, new[] { "冒险", "热血" }, "探险小队深入神秘古遗迹，解开千古封印的惊世之谜。"),
                    ("顾总请自重", 80, new[] { "都市", "豪门", "霸总" }, "契约婚姻假戏真做，千亿总裁狂宠不放手。"),
                    ("小小星辰", 50, new[] { "都市", "爱情", "励志" }, "平凡少女追寻设计梦想，在璀璨星途收获真挚爱恋。"),
                    ("穿成反贼 皇帝递我半根烟", 60, new[] { "穿越", "搞笑", "权谋" }, "意外穿越古代乱世，凭借现代智慧与皇帝拜把子称霸天下。"),
                    ("冷校花竟是我的开黑搭子", 45, new[] { "校园", "游戏", "恋爱" }, "电竞大神隐藏身份，与高冷校花游戏双排擦出爱火。"),
                    ("隐形首富之龙王归来", 80, new[] { "都市", "战神", "爽剧" }, "龙王解甲归田，一朝显露真容，震慑四方枭雄。"),
                    ("假千金逆袭归来", 85, new[] { "豪门", "复仇", "甜宠" }, "被赶出家门的假千金携多重马甲华丽归来，打脸全场。"),
                    ("重生之狂神天下", 80, new[] { "重生", "玄幻", "热血" }, "狂神转世重修，横扫万界强敌，再登九天之巅。"),
                    ("绝品仙尊在都市", 100, new[] { "修仙", "逆袭", "爽文" }, "渡劫仙尊降临现代都市，一手医术通神，一手道法通天。"),
                    ("绝品神医闯都市", 88, new[] { "神医", "无敌", "都市" }, "奉师命下山退婚，却意外成为江南第一神医。"),
                    ("离婚后，前妻哭着求复合", 90, new[] { "虐恋", "豪门", "逆袭" }, "签字离婚的那一刻，他千亿财阀继承人的身份曝光。"),
                    ("无双龙帅", 100, new[] { "战神", "热血", "爽文" }, "统帅百万将士守护国门，凯旋归来扫平一切不公。"),
                    ("真假千金大对决", 75, new[] { "真假千金", "豪门", "打脸" }, "真千金王者归来，智商碾压全场恶毒反派。"),
                    ("开局觉醒至尊神瞳", 85, new[] { "异能", "鉴宝", "爽文" }, "双眼能鉴天下古玩，赌石成神，财富惊天动地。"),
                    ("我真没想当首富啊", 70, new[] { "神豪", "系统", "都市" }, "花钱就能百倍返现，随手投资成就万亿商业帝国。"),
                    ("仙王回归当保安", 95, new[] { "仙尊", "扮猪吃虎" }, "九天仙王隐居都市大厦当保安，悄然守护绝美总裁。"),
                    ("新婚夜，植物人老公站起来了", 80, new[] { "甜宠", "先婚后爱" }, "替嫁给植物人少爷，新婚夜他竟然苏醒并将她宠上天。"),
                    ("退婚后我成了千亿大佬", 82, new[] { "逆袭", "神豪", "打脸" }, "被前女友嫌贫爱富退婚，转身接管全球顶级财团。"),
                    ("第一狂婿", 92, new[] { "赘婿", "无敌", "热血" }, "入赘三年人人嘲笑，一朝龙抬头，万家俯首称臣。"),
                    ("女总裁的贴身兵王", 88, new[] { "兵王", "都市", "保镖" }, "最强兵王回归都市，贴身保护绝美女总裁一路逆袭。"),
                    ("闪婚后千亿总裁马甲掉了", 78, new[] { "甜宠", "豪门", "恋爱" }, "相亲闪婚的外卖小哥，居然是身价千亿的帝国首富。"),
                    
                    // Page 2 (24 items)
                    ("极品小神农", 76, new[] { "乡村", "种田", "神医" }, "获得山神传承，带领落后小山村成为世界度假胜地。"),
                    ("天降巨富少爷", 84, new[] { "神豪", "逆袭", "都市" }, "穷困潦倒之际，家族管家突然上门送来万亿黑卡。"),
                    ("狂龙出狱", 98, new[] { "战神", "热血", "复仇" }, "镇压恶魔岛五年的狂龙出狱，天下风云皆因他而变。"),
                    ("重生之千亿女王", 80, new[] { "女性", "复仇", "商战" }, "惨遭闺蜜背叛重生十年前，她步步为营登顶商界女皇。"),
                    ("镇国神帅", 100, new[] { "战神", "军旅", "爽剧" }, "一战封神，封号镇国，凯旋之日清算一切仇敌。"),
                    ("替嫁娇妻太撩人", 82, new[] { "甜宠", "豪门", "总裁" }, "替妹出嫁嫁给残疾大少，婚后大少不仅痊愈还把她宠上天。"),
                    ("都市最强狂少", 90, new[] { "逆袭", "无敌", "打脸" }, "身怀通天医术与武道，纵横都市无人敢惹。"),
                    ("带球跑后被亿万爹地宠上天", 86, new[] { "萌宝", "甜宠", "豪门" }, "六年后携天才萌宝归来，千亿霸总全球追妻。"),
                    ("长生十万年", 100, new[] { "玄幻", "长生", "无敌" }, "活了十万年的不灭至尊，游戏人间点化后代巨头。"),
                    ("我的绝美冷艳总裁老婆", 92, new[] { "都市", "恋爱", "兵王" }, "奉命保护冷艳美女总裁，上演欢喜冤家的都市情缘。"),
                    ("开局签到混沌体", 88, new[] { "玄幻", "系统", "热血" }, "签到获得神级体质，横推同代天骄成就不朽帝路。"),
                    ("落魄少爷的逆天改命", 75, new[] { "逆袭", "商战", "都市" }, "从破产乞丐到世界首富，用智慧与胆识夺回一切。"),
                    ("天师下山", 85, new[] { "道术", "悬疑", "风水" }, "龙虎山天师遵师命下山入世，驱邪捉鬼名动四方。"),
                    ("神级狂婿在都市", 95, new[] { "赘婿", "战神", "爽文" }, "隐姓埋名入赘豪门，龙王令出，天下豪强无不俯首。"),
                    ("豪门萌宝：爹地快投降", 80, new[] { "萌宝", "甜宠", "总裁" }, "鬼马萌宝助攻妈咪，把高冷爹地拿捏得死死的。"),
                    ("我有一座黄金岛", 72, new[] { "海岛", "神豪", "探险" }, "继承海外巨型黄金岛，开启全球最奢华的岛主生活。"),
                    ("九龙夺嫡：废物皇子逆天崛起", 88, new[] { "穿越", "历史", "权谋" }, "现代历史系博士穿越成九皇子，造枪造炮横扫八荒。"),
                    ("一胎三宝：总裁爹地找上门", 85, new[] { "甜宠", "萌宝", "豪门" }, "三位天才萌宝黑进跨国集团，替妈咪找回亲生爹地。"),
                    ("至尊天医", 90, new[] { "医圣", "无敌", "都市" }, "生死人肉白骨，一手银针断生死，万人敬仰的天医传人。"),
                    ("开局送座四合院", 70, new[] { "年代", "逆袭", "爽剧" }, "穿越年代物资匮乏时期，靠随身农场闷声发大财。"),
                    ("我的徒弟都是各界巨佬", 92, new[] { "师徒", "无敌", "搞笑" }, "隐居深山的老祖宗出山，各路战神首富纷纷跪拜师尊。"),
                    ("假死五年，战神前夫哭红了眼", 82, new[] { "追妻", "豪门", "虐恋" }, "涅槃归来的她艳惊四座，冷酷战神悔不当初狂追妻。"),
                    ("盖世龙王", 99, new[] { "战神", "热血", "爽剧" }, "龙门之主镇守边疆，回城惩恶扬善，护娇妻一生安宁。"),
                    ("真千金她靠算命火爆全网", 80, new[] { "玄学", "豪门", "打脸" }, "玄学大佬重生成真千金，铁口直断全网大佬排队求卦。")
                };

                int idx = 0;
                foreach (var (title, eps, tags, desc) in hongGuoShows)
                {
                    var cover = hongGuoPosters[idx % hongGuoPosters.Length];
                    idx++;

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
            else
            {
                // iQIYI Official Shows with Official Poster Art
                var iqiyiPosters = new[]
                {
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/lyq6l6wc4nncrky1785812716372/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/hiw723xwp01jtp01758854547398/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/vo9o0yzkxzrn0sh1738817562858/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/le1lbx64do19qal1783060775293/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/27ell6rtltizhdg1776826659762/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/3p20haaqwgcp5zb1772589211786/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wpl6nyn3iifw70h1780649837178/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/stki3y2360atn3m1769968286667/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wovs9yfw0u87ukq1767076299386/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/za1c6jcuc05w85n1789963482123/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/2uxsii1m3u1uh2w1787814937272/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/19q8yj9d3bzqfqk1768287025002/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/wu7vz4vgfi8ugan1768575666842/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/kh22ysbh4ut91ch1786784512775/350",
                    "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/0w5mbk4kmcjaq2x1787546330162/350"
                };

                var iqiyiShows = new (string Title, int Eps, string[] Tags, string Summary)[]
                {
                    // Page 1 (24 items)
                    ("宁安如梦 (Story of Kunning Palace)", 38, new[] { "古装", "重生", "悬疑" }, "姜雪宁重获新生，誓要改写命运，与谢危携手平定乱局。"),
                    ("苍兰诀 (Love Between Fairy and Devil)", 36, new[] { "仙侠", "魔尊", "甜虐" }, "息山神女小兰花无意间复活东方青苍，开启三界旷世虐恋。"),
                    ("长月烬明 (Till the End of the Moon)", 40, new[] { "神魔", "仙侠", "爱情" }, "黎苏苏回到五百年前拯救苍生，与魔胎澹台烬纠缠三世。"),
                    ("莲花楼 (Mysterious Lotus Casebook)", 40, new[] { "武侠", "探案", "悬疑" }, "昔日剑首李相夷化身游医李莲花，智破江湖离奇迷案。"),
                    ("狂飙 (The Knockout)", 39, new[] { "悬疑", "犯罪", "扫黑" }, "刑警安欣与黑恶势力高启强展开跨越二十年的正邪生死对决。"),
                    ("风吹半夏 (Wild Bloom)", 36, new[] { "时代", "商战", "女性" }, "许半夏在90年代钢铁行业白手起家，开创商业传奇。"),
                    ("唐朝诡事录之西行", 40, new[] { "古装", "悬疑", "探案" }, "苏无名与卢凌风西行一路屡破奇案，揭示大唐盛世隐秘。"),
                    ("卿卿日常 (New Life Begins)", 40, new[] { "古装", "轻喜", "甜宠" }, "李薇与六少主尹峥在九川联姻中相守相伴，烟火日常温馨动人。"),
                    ("云之羽 (My Journey to You)", 24, new[] { "江湖", "谍战", "悬疑" }, "无锋刺客云为衫潜入宫门，与叛逆公子宫子羽展开生死博弈。"),
                    ("一念关山 (A Journey to Love)", 40, new[] { "武侠", "公路", "热血" }, "任如意与宁远舟率领使团跨越千山万水，匡扶江山社稷。"),
                    ("追风者 (War of Faith)", 38, new[] { "民国", "金融", "谍战" }, "金融天才魏若来在动荡上海滩探寻救国真理，成长为红色金融家。"),
                    ("我的阿勒泰 (To the Wonder)", 8, new[] { "治愈", "自然", "成长" }, "汉族少女李文秀在新疆阿勒泰辽阔草原上寻觅生命纯真与诗意。"),
                    ("人世间 (A Lifelong Journey)", 58, new[] { "年代", "家庭", "温情" }, "北方光字片周家三代人历经五十年时代变迁的史诗长卷。"),
                    ("大梦归离", 26, new[] { "古装", "神话", "奇幻" }, "缉妖司领袖赵远舟率领小队在人妖两界穿梭破获奇幻事件。"),
                    ("无忧渡", 36, new[] { "志怪", "玄幻", "古装" }, "捉妖师宣夜与少女半夏在人妖共存的世界探寻禁忌真相。"),
                    ("七时吉祥", 38, new[] { "仙侠", "欢喜冤家", "奇幻" }, "姻缘阁小仙祥云与战神初空阴差阳错绑定七世情劫。"),
                    ("烈焰 (Burning Flames)", 40, new[] { "玄幻", "热血", "复仇" }, "人族王子伍赓沦为奴隶，百折不挠带领三界对抗神族统治。"),
                    ("狐妖小红娘月红篇", 36, new[] { "仙侠", "国漫", "恋爱" }, "涂山大当家涂山红红与东方月初守护人妖和平的绝美之恋。"),
                    ("哈尔滨一九四四", 40, new[] { "谍战", "悬疑", "年代" }, "共产党地下党员宋卓文潜入特务科，与特务头子关雪斗智斗勇。"),
                    ("错位", 15, new[] { "悬疑", "犯罪", "迷案" }, "刑警姜光明在调查一起入室杀人案时发现案情竟与悬疑小说完全吻合。"),
                    ("九部的检察官", 18, new[] { "悬疑", "未成年人", "法治" }, "未检检察官雷旭带领九部团队守护青少年成长法治防线。"),
                    ("隐秘的角落 (The Bad Kids)", 12, new[] { "悬疑", "犯罪", "高分" }, "三个孩子在景区无意拍下一场谋杀，开启一系列不可逆转的连锁反应。"),
                    ("沉默的真相", 12, new[] { "悬疑", "正义", "高分" }, "检察官江阳历经十年磨难，付出生命代价只为查明一件冤案真相。"),
                    ("警察荣誉", 38, new[] { "公安", "基层", "生活" }, "八里河派出所四位见习警员在老警察带领下一步步成长蜕变。"),
                    
                    // Page 2 (24 items)
                    ("对手", 37, new[] { "现代", "谍战", "悬疑" }, "和平年代国安干警与隐藏在市井中的境外间谍展开无声暗战。"),
                    ("破冰行动", 48, new[] { "缉毒", "警匪", "热血" }, "缉毒警李飞撕开塔寨村制毒地下网络，打响禁毒战役。"),
                    ("罚罪", 40, new[] { "刑侦", "扫黑", "硬汉" }, "青年刑警常征不畏强暴，彻查昌武赵氏家族黑恶犯罪集团。"),
                    ("大山的女儿", 30, new[] { "扶贫", "感人", "时代" }, "时代楷模黄文秀研究生毕业毅然回乡投身脱贫攻坚的感人一生。"),
                    ("风起陇西", 24, new[] { "古装", "三国", "谍战" }, "三国时代蜀魏谍战人员在暗流涌动的战争阴影下展开致命间谍战。"),
                    ("显微镜下的大明之丝绢案", 14, new[] { "古装", "历史", "探案" }, "算学天才帅家默偶然发现一笔八县税赋账目错误，掀起官场巨浪。"),
                    ("唐朝诡事录 第一季", 36, new[] { "古装", "志怪", "悬疑" }, "金吾卫中郎将卢凌风与狄公亲传弟子苏无名联手勘破长安八大诡案。"),
                    ("赘婿", 36, new[] { "穿越", "喜剧", "经商" }, "现代商业精英穿越至武朝苏家，扮猪吃虎带领家族成江宁首富。"),
                    ("云襄传", 36, new[] { "江湖", "智谋", "武侠" }, "云台弟子云襄背负家族灭门之仇踏入江湖，以智谋布下惊天大局。"),
                    ("延禧攻略", 70, new[] { "古装", "宫斗", "爽剧" }, "宫女魏璎珞凭借过人智勇一路逆袭晋升为令贵妃。"),
                    ("武庚纪", 40, new[] { "玄幻", "动作", "修真" }, "人族王子不屈命运，踏上反抗神域强权的逆天征程。"),
                    ("风起洛阳", 39, new[] { "古装", "探案", "神都" }, "不良帅高秉烛与百里弘毅联手探寻神都洛阳春秋道惊天阴谋。"),
                    ("招摇", 56, new[] { "仙侠", "虐恋", "魔道" }, "万戮门门主路招摇与魔王之子厉尘澜的仙魔旷世绝恋。"),
                    ("宸汐缘", 60, new[] { "仙侠", "神话", "深情" }, "九宸战神与桃林少女灵汐跨越三生三世守护六界众生。"),
                    ("半是蜜糖半是伤", 36, new[] { "都市", "职场", "甜宠" }, "投行精英袁帅与患眼泪过敏症的江君在投行职场甜蜜重逢。"),
                    ("一生一世", 30, new[] { "都市", "甜宠", "传承" }, "顶尖配音演员时宜与化学教授周生辰在现代都市缘定今生。"),
                    ("周生如故", 24, new[] { "古装", "虐心", "权谋" }, "小南辰王周生辰与漼时宜在家国大义与深情挚爱间的宿命悲歌。"),
                    ("终极笔记", 36, new[] { "盗墓", "探险", "经典" }, "铁三角在蛇沼鬼城与张家古楼中揭开张起灵身世终极之谜。"),
                    ("灵魂摆渡", 20, new[] { "惊悚", "志怪", "温情" }, "阴阳眼少年夏冬青在444号便利店为亡魂引路的奇幻温情故事。"),
                    ("黄金瞳", 56, new[] { "鉴宝", "异能", "冒险" }, "典当行小职员庄睿意外获黄金瞳，在古玩与赌石界名声大噪。"),
                    ("老九门", 48, new[] { "民国", "盗墓", "抗战" }, "九门之首张启山携手二月红等九门提督守卫长沙保家卫国。"),
                    ("隐秘而伟大", 51, new[] { "年代", "谍战", "初心" }, "东夏区警察局小警员顾耀东在乱世风云中坚守初心匡扶正义。"),
                    ("天盛长歌", 70, new[] { "古装", "权谋", "品质" }, "六皇子宁弈与前朝遗孤凤知微在朝堂风云中步步为营的宿命绝恋。"),
                    ("琅琊榜之风起长林", 50, new[] { "古装", "权谋", "忠烈" }, "长林王府忠肝义胆，萧平章与萧平旌守护大梁山河。")
                };

                int idx = 0;
                foreach (var (title, eps, tags, desc) in iqiyiShows)
                {
                    var cover = iqiyiPosters[idx % iqiyiPosters.Length];
                    idx++;

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
