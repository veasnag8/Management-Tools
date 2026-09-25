using System;
using System.Windows;
using Tool.Updates;

namespace Tool.Views
{
    public partial class UpdateDialog : Window
    {
        private readonly VersionResponseData _updateInfo;
        private readonly IUpdateManager _updateManager;

        public UpdateDialog(VersionResponseData updateInfo, IUpdateManager updateManager)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _updateManager = updateManager;

            TxtVersionInfo.Text = $"Version {updateInfo.LatestVersion} is available (Current: {updateInfo.CurrentVersion})";
            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
                ? "Performance improvements and bug fixes."
                : updateInfo.ReleaseNotes;

            if (updateInfo.Mandatory)
            {
                TxtTitle.Text = "Mandatory Update Required";
                BtnLater.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdateNow.IsEnabled = false;
            BtnLater.IsEnabled = false;
            PnlProgress.Visibility = Visibility.Visible;

            var progress = new Progress<int>(pct =>
            {
                ProgressBarDownload.Value = pct;
                TxtProgressStatus.Text = $"Downloading update ({pct}%)...";
            });

            try
            {
                var installerPath = await _updateManager.DownloadAndVerifyInstallerAsync(_updateInfo, progress);
                if (!string.IsNullOrEmpty(installerPath))
                {
                    _updateManager.LaunchInstaller(installerPath);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Failed to download update installer.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnUpdateNow.IsEnabled = true;
                    if (!_updateInfo.Mandatory) BtnLater.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update Error: {ex.Message}", "Verification Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnUpdateNow.IsEnabled = true;
                if (!_updateInfo.Mandatory) BtnLater.IsEnabled = true;
            }
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
