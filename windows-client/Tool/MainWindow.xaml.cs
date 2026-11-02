using System.Windows;
using Tool.License;
using Tool.Services;
using Tool.Updates;
using Tool.ViewModels;
using Tool.Views;

namespace Tool
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ILicenseManager _licenseManager;

        public MainWindow(ILicenseManager licenseManager)
        {
            InitializeComponent();
            _licenseManager = licenseManager;

            var downloaderService = new DownloaderService();
            var crawlerService = new DramaCrawlerService();
            var updateManager = new UpdateManager(new LicenseApiClient());

            _viewModel = new MainViewModel(downloaderService, crawlerService, licenseManager, updateManager);
            this.DataContext = _viewModel;

            _licenseManager.StateChanged += OnLicenseStateChanged;
        }

        private bool _isRevokedPendingLockout = false;

        private void OnLicenseStateChanged(object? sender, LicenseState state)
        {
            Dispatcher.Invoke(() =>
            {
                if (state != LicenseState.Active && state != LicenseState.OfflineGrace)
                {
                    // If video downloading or export is in progress, allow it to complete this 1 last time!
                    if (_viewModel.IsDownloading)
                    {
                        if (!_isRevokedPendingLockout)
                        {
                            _isRevokedPendingLockout = true;
                            _viewModel.StatusMessage = "⚠️ Admin បាន Revoke/Disable Tool នេះ! ការទាញយក/Export បច្ចុប្បន្នត្រូវបានអនុញ្ញាតឱ្យបញ្ចប់១លើកចុងក្រោយ។";
                            _viewModel.PropertyChanged += OnViewModelPropertyChangedWhileRevoked;
                        }
                        return;
                    }

                    EnforceLockout(state, wasRunningTask: false);
                }
            });
        }

        private void OnViewModelPropertyChangedWhileRevoked(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsDownloading) && !_viewModel.IsDownloading)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChangedWhileRevoked;
                Dispatcher.Invoke(() =>
                {
                    EnforceLockout(_licenseManager.CurrentState, wasRunningTask: true);
                });
            }
        }

        private void EnforceLockout(LicenseState state, bool wasRunningTask)
        {
            string msg = wasRunningTask
                ? "ការ Generate/Export/ទាញយក វីដេអូចុងក្រោយត្រូវបានបញ្ចប់!\n\nTool របស់អ្នកត្រូវបាន Disable ឬ Revoke ដោយ Admin។ មិនអាចបន្តការប្រើប្រាស់បានទៀតទេ។"
                : $"Tool របស់អ្នកត្រូវបាន Disable ឬ Revoke ដោយ Admin!\n\n({_licenseManager.LastErrorMessage ?? state.ToString()})\nTool ត្រូវបានផ្តាច់ចេញភ្លាមៗ (Disconnected) មិនអាចបន្តការប្រើប្រាស់បានទៀតទេ។";

            MessageBox.Show(
                this,
                msg,
                "Access Revoked by Admin",
                MessageBoxButton.OK,
                MessageBoxImage.Stop);

            var actWin = new ActivationWindow(_licenseManager);
            actWin.Show();
            this.Close();
        }
    }
}
