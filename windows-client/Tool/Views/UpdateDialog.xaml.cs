using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Tool.Updates;

namespace Tool.Views
{
    public partial class UpdateDialog : Window
    {
        private VersionResponseData? _updateInfo;
        private readonly IUpdateManager _updateManager;
        private readonly string _productCode;
        private readonly string _currentVersion;
        private CancellationTokenSource? _downloadCts;

        public UpdateDialog(
            VersionResponseData? initialUpdateInfo,
            IUpdateManager updateManager,
            string productCode = "SNA_DUBBER",
            string currentVersion = "1.2.0")
        {
            InitializeComponent();
            _updateInfo = initialUpdateInfo;
            _updateManager = updateManager;
            _productCode = productCode;
            _currentVersion = currentVersion;

            TxtCurrentVersion.Text = _currentVersion;
            TxtLastChecked.Text = DateTime.Now.ToString("g");

            if (_updateInfo != null)
            {
                ApplyUpdateInfo(_updateInfo);
            }
            else
            {
                // Automatically run check on load
                Loaded += async (s, e) => await CheckForUpdatesAsync();
            }
        }

        private void ApplyUpdateInfo(VersionResponseData update)
        {
            _updateInfo = update;
            TxtLatestVersion.Text = update.LatestVersion;
            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                ? "Performance improvements, enhanced stability and Khmer dubbing accuracy."
                : update.ReleaseNotes;

            if (update.HasUpdate)
            {
                TxtStatusBadge.Text = update.Mandatory ? "Mandatory Update" : "Update Available";
                TxtProgressStatus.Text = "New update available.";
                BtnUpdateNow.IsEnabled = true;

                if (update.Mandatory)
                {
                    BtnLater.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                TxtStatusBadge.Text = "Up to Date";
                TxtProgressStatus.Text = "No updates available.";
                BtnUpdateNow.IsEnabled = false;
                BtnRetry.Visibility = Visibility.Collapsed;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            BtnCheckUpdates.IsEnabled = false;
            BtnUpdateNow.IsEnabled = false;
            BtnRetry.Visibility = Visibility.Collapsed;
            TxtProgressStatus.Text = "Checking for updates...";
            PnlProgress.Visibility = Visibility.Visible;
            ProgressBarDownload.IsIndeterminate = true;
            TxtDownloadStats.Text = "Connecting to update server...";

            try
            {
                var info = await _updateManager.CheckForUpdateAsync(_productCode, _currentVersion);
                TxtLastChecked.Text = DateTime.Now.ToString("g");
                ProgressBarDownload.IsIndeterminate = false;

                if (info != null && info.HasUpdate)
                {
                    ApplyUpdateInfo(info);
                    PnlProgress.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TxtLatestVersion.Text = _currentVersion;
                    TxtReleaseNotes.Text = "You are using the latest version of Tool Dubber.";
                    TxtStatusBadge.Text = "Up to Date";
                    TxtProgressStatus.Text = "No updates available.";
                    PnlProgress.Visibility = Visibility.Collapsed;
                    MessageBox.Show("You are using the latest version of Tool Dubber.\nលោកអ្នកកំពុងប្រើប្រាស់ជំនាន់ចុងក្រោយបង្អស់ហើយ។", "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ProgressBarDownload.IsIndeterminate = false;
                TxtProgressStatus.Text = "Update failed.";
                TxtDownloadStats.Text = "Network error: " + ex.Message;
                BtnRetry.Visibility = Visibility.Visible;
                MessageBox.Show($"Could not check for updates:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                BtnCheckUpdates.IsEnabled = true;
            }
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync();
        }

        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync();
        }

        private async Task StartDownloadAsync()
        {
            if (_updateInfo == null || string.IsNullOrWhiteSpace(_updateInfo.DownloadUrl))
            {
                await CheckForUpdatesAsync();
                return;
            }

            BtnCheckUpdates.IsEnabled = false;
            BtnUpdateNow.IsEnabled = false;
            BtnRetry.Visibility = Visibility.Collapsed;
            BtnLater.IsEnabled = !_updateInfo.Mandatory;
            PnlProgress.Visibility = Visibility.Visible;
            ProgressBarDownload.IsIndeterminate = false;
            ProgressBarDownload.Value = 0;

            TxtProgressStatus.Text = "Preparing download...";
            TxtDownloadStats.Text = "Initializing secure connection...";

            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<DownloadProgressReport>(report =>
            {
                ProgressBarDownload.Value = report.Percentage;
                TxtProgressStatus.Text = report.StatusText;
                TxtDownloadStats.Text = $"{report.DownloadedMB:0.0} MB / {report.TotalMB:0.0} MB ({report.SpeedMBps:0.0} MB/s)";
            });

            try
            {
                var installerPath = await _updateManager.DownloadAndVerifyInstallerAsync(_updateInfo, progress, _downloadCts.Token);

                if (!string.IsNullOrEmpty(installerPath))
                {
                    TxtProgressStatus.Text = "Ready to install.";
                    TxtDownloadStats.Text = "Verification successful. Prompting confirmation...";

                    var res = MessageBox.Show(
                        "Tool Dubber is ready to install the new update.\n\nPlease save your work before continuing. Tool Dubber will close, run the installer, and restart automatically.\n\nតើអ្នកចង់ដំឡើងឥឡូវនេះទេ?",
                        "Install Update & Restart",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (res == MessageBoxResult.Yes)
                    {
                        TxtProgressStatus.Text = "Installing update...";
                        TxtDownloadStats.Text = "Launching installer and restarting Tool Dubber...";
                        await Task.Delay(400);

                        TxtProgressStatus.Text = "Restarting Tool Dubber...";
                        _updateManager.LaunchInstaller(installerPath, autoRestart: true);

                        Application.Current.Shutdown();
                    }
                    else
                    {
                        TxtProgressStatus.Text = "Ready to install.";
                        BtnUpdateNow.IsEnabled = true;
                        BtnLater.IsEnabled = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TxtProgressStatus.Text = "Update failed.";
                TxtDownloadStats.Text = "Download cancelled by user.";
                BtnUpdateNow.IsEnabled = true;
                BtnRetry.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                TxtProgressStatus.Text = "Update failed.";
                TxtDownloadStats.Text = ex.Message;
                BtnRetry.Visibility = Visibility.Visible;
                BtnUpdateNow.IsEnabled = true;
                MessageBox.Show($"Update download failed:\n{ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnCheckUpdates.IsEnabled = true;
            }
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            Close();
        }
    }
}
