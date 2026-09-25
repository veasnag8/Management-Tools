using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tool.License;

namespace Tool.Views
{
    public partial class ActivationWindow : Window
    {
        private readonly ILicenseManager _licenseManager;

        public ActivationWindow(ILicenseManager licenseManager)
        {
            InitializeComponent();
            _licenseManager = licenseManager;

            TxtPcId.Text = _licenseManager.CurrentDeviceId;
            UpdateStatusUI();
        }

        private void BtnCopyPcId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtPcId.Text);
                BtnCopyPcId.Content = "Copied!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy: {ex.Message}", "Clipboard", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            var key = TxtLicenseKey.Text.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                ShowError("Please enter a valid license key.");
                return;
            }

            BtnActivate.IsEnabled = false;
            BtnActivate.Content = "Activating with Server...";
            PnlError.Visibility = Visibility.Collapsed;

            try
            {
                var success = await _licenseManager.ActivateLicenseAsync(key);
                if (success)
                {
                    // Open main window and close activation
                    var mainWindow = new MainWindow(_licenseManager);
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    ShowError(_licenseManager.LastErrorMessage ?? "License activation rejected by server.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Connection Error: {ex.Message}");
            }
            finally
            {
                BtnActivate.IsEnabled = true;
                BtnActivate.Content = "Activate License";
                UpdateStatusUI();
            }
        }

        private void ShowError(string message)
        {
            PnlError.Visibility = Visibility.Visible;
            TxtErrorMessage.Text = message;
        }

        private void TxtLicenseKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PnlError.Visibility == Visibility.Visible)
            {
                PnlError.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateStatusUI()
        {
            switch (_licenseManager.CurrentState)
            {
                case LicenseState.Active:
                    TxtStatus.Text = "Status: License Activated";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    break;
                case LicenseState.Expired:
                    TxtStatus.Text = "Status: License Expired";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60));
                    break;
                case LicenseState.Disabled:
                    TxtStatus.Text = "Status: License Disabled by Admin";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                    break;
                case LicenseState.DeviceRevoked:
                    TxtStatus.Text = "Status: PC Device Revoked";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                    break;
                case LicenseState.DeviceLimitReached:
                    TxtStatus.Text = "Status: Maximum Device Limit Reached";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60));
                    break;
                default:
                    TxtStatus.Text = "Status: Not Activated";
                    TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                    break;
            }
        }
    }
}
