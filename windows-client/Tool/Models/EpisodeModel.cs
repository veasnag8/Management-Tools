using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tool.Models
{
    public enum EpisodeDownloadStatus
    {
        Idle,
        Queued,
        Downloading,
        Completed,
        Failed,
        Cancelled
    }

    public partial class EpisodeModel : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private int _episodeNumber;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _thumbnailUrl = "https://images.unsplash.com/photo-1578022761797-b8636ac1773c?w=400";

        [ObservableProperty]
        private string _duration = "45m 10s";

        [ObservableProperty]
        private string _streamUrl = string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private EpisodeDownloadStatus _status = EpisodeDownloadStatus.Idle;

        [ObservableProperty]
        private double _progress = 0.0;

        [ObservableProperty]
        private string _speed = "0 KB/s";

        [ObservableProperty]
        private string _eta = "--:--";

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _outputFilePath = string.Empty;
    }
}
