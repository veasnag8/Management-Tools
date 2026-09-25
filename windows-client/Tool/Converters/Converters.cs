using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Tool.Models;

namespace Tool.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EpisodeDownloadStatus status)
            {
                return status switch
                {
                    EpisodeDownloadStatus.Downloading => new SolidColorBrush(Color.FromRgb(6, 182, 212)), // Cyan
                    EpisodeDownloadStatus.Completed => new SolidColorBrush(Color.FromRgb(16, 185, 129)),  // Emerald
                    EpisodeDownloadStatus.Failed => new SolidColorBrush(Color.FromRgb(244, 63, 94)),      // Rose
                    EpisodeDownloadStatus.Cancelled => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                    EpisodeDownloadStatus.Queued => new SolidColorBrush(Color.FromRgb(129, 140, 248)),   // Indigo
                    _ => new SolidColorBrush(Color.FromRgb(71, 85, 105))                                 // Slate
                };
            }
            return new SolidColorBrush(Color.FromRgb(71, 85, 105));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value is bool flag && flag;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
