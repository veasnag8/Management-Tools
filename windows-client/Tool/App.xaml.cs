using System;
using System.Windows;
using Tool.License;
using Tool.Services;
using Tool.Views;

namespace Tool
{
    public partial class App : Application
    {
        private ILicenseManager? _licenseManager;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize license validation services with production Cloudflare Worker API
            var apiClient = new LicenseApiClient(LicenseApiClient.ProductionApiUrl);
            var storageService = new SecureStorageService();
            var deviceIdService = new DeviceIdService();
            var signatureVerifier = new LicenseSignatureVerifier();
            var clockService = new ClockService();

            _licenseManager = new LicenseManager(
                apiClient,
                storageService,
                deviceIdService,
                signatureVerifier,
                clockService
            );

            // 2. Perform startup validation
            var state = await _licenseManager.InitializeAndValidateAsync();

            // 3. Coordinate navigation
            if (state == LicenseState.Active || state == LicenseState.OfflineGrace)
            {
                var mainWindow = new MainWindow(_licenseManager);
                mainWindow.Show();
            }
            else
            {
                var activationWindow = new ActivationWindow(_licenseManager);
                activationWindow.Show();
            }
        }
    }
}
