using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tool.Models
{
    public partial class DramaModel : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _chineseTitle = string.Empty;

        [ObservableProperty]
        private string _coverUrl = "https://vcover-vt-pic.wetvinfo.com/vcover_vt_pic/0/94jt6sxiwsjw5n61786414600805/350";

        [ObservableProperty]
        private string _resolution = "1080p FHD";

        [ObservableProperty]
        private PlatformType _platform = PlatformType.HongGuo;

        [ObservableProperty]
        private int _totalEpisodes = 0;

        [ObservableProperty]
        private string _episodeBadge = "全40集";

        [ObservableProperty]
        private string _summary = string.Empty;

        [ObservableProperty]
        private string _saveDirectory = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _tags = new();

        [ObservableProperty]
        private bool _isFavorite;

        [ObservableProperty]
        private ObservableCollection<EpisodeModel> _episodes = new();

        [ObservableProperty]
        private bool _isSelectedInLibrary;
    }
}
