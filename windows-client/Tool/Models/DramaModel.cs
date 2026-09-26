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
        private string _coverUrl = "https://images.unsplash.com/photo-1536440136628-849c177e76a1?w=400";

        [ObservableProperty]
        private string _resolution = "1080p FHD";

        [ObservableProperty]
        private PlatformType _platform = PlatformType.HongGuo;

        [ObservableProperty]
        private int _totalEpisodes = 0;

        [ObservableProperty]
        private string _summary = string.Empty;

        [ObservableProperty]
        private string _saveDirectory = string.Empty;

        [ObservableProperty]
        private ObservableCollection<EpisodeModel> _episodes = new();

        [ObservableProperty]
        private bool _isSelectedInLibrary;
    }
}
