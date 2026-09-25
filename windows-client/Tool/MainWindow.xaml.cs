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

        private void OnLicenseStateChanged(object? sender, LicenseState state)
        {
            Dispatcher.Invoke(() =>
            {
                if (state != LicenseState.Active && state != LicenseState.OfflineGrace)
                {
                    MessageBox.Show(
                        $"License session ended: {_licenseManager.LastErrorMessage ?? state.ToString()}",
                        "Authentication Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    var actWin = new ActivationWindow(_licenseManager);
                    actWin.Show();
                    this.Close();
                }
            });
        }
    }
}
