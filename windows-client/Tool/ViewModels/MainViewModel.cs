using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tool.License;
using Tool.Models;
using Tool.Services;
using Tool.Updates;

namespace Tool.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDownloaderService _downloaderService;
        private readonly IDramaCrawlerService _crawlerService;
        private readonly ILicenseManager _licenseManager;
        private readonly IUpdateManager _updateManager;

        private CancellationTokenSource? _downloadCts;

        [ObservableProperty]
        private PlatformType _selectedPlatform = PlatformType.HongGuo;

        [ObservableProperty]
        private string _searchUrl = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private ObservableCollection<DramaModel> _dramaLibrary = new();

        [ObservableProperty]
        private DramaModel? _selectedDrama;

        [ObservableProperty]
        private ObservableCollection<EpisodeModel> _currentEpisodes = new();

        [ObservableProperty]
        private string _saveDirectory;

        [ObservableProperty]
        private int _workerCount = 3;

        [ObservableProperty]
        private ObservableCollection<int> _workerOptions = new() { 1, 2, 3, 4, 6, 8 };

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private double _overallProgress = 0.0;

        [ObservableProperty]
        private string _overallSpeed = "0 KB/s";

        [ObservableProperty]
        private int _completedCount = 0;

        [ObservableProperty]
        private int _totalSelectedCount = 0;

        // Licensing Telemetry Binding
        [ObservableProperty]
        private string _licenseStatusText = "Active License";

        [ObservableProperty]
        private string _pcId = string.Empty;

        [ObservableProperty]
        private string _expiryText = "Lifetime";

        public MainViewModel(
            IDownloaderService downloaderService,
            IDramaCrawlerService crawlerService,
            ILicenseManager licenseManager,
            IUpdateManager updateManager)
        {
            _downloaderService = downloaderService;
            _crawlerService = crawlerService;
            _licenseManager = licenseManager;
            _updateManager = updateManager;

            // Default save folder in Videos / Downloads
            var myVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            _saveDirectory = Path.Combine(myVideos, "DramaDownloads");
            Directory.CreateDirectory(_saveDirectory);

            _pcId = _licenseManager.CurrentDeviceId;
            UpdateLicenseInfo();

            // Load initial library
            _ = LoadFeaturedLibraryAsync();
        }

        private void UpdateLicenseInfo()
        {
            var session = _licenseManager.CurrentSession;
            if (session != null)
            {
                ExpiryText = session.ExpiresAt.HasValue
                    ? session.ExpiresAt.Value.ToLocalTime().ToString("MMM dd, yyyy")
                    : "Lifetime Entitlement";
            }
            LicenseStatusText = _licenseManager.CurrentState == LicenseState.OfflineGrace
                ? "Offline Grace Mode"
                : "● Active License";
        }

        [RelayCommand]
        public async Task SwitchPlatformAsync(PlatformType platform)
        {
            SelectedPlatform = platform;
            await LoadFeaturedLibraryAsync();
        }

        public async Task LoadFeaturedLibraryAsync()
        {
            IsBusy = true;
            StatusMessage = $"Loading {SelectedPlatform} library...";
            try
            {
                var library = await _crawlerService.GetFeaturedLibraryAsync(SelectedPlatform);
                DramaLibrary.Clear();
                foreach (var item in library)
                {
                    DramaLibrary.Add(item);
                }

                if (DramaLibrary.Count > 0)
                {
                    SelectDrama(DramaLibrary[0]);
                }
                StatusMessage = $"Loaded {DramaLibrary.Count} featured dramas from {SelectedPlatform}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading library: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task FetchEpisodesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchUrl))
            {
                StatusMessage = "Please enter a valid Drama URL or Album ID.";
                return;
            }

            IsBusy = true;
            StatusMessage = $"Fetching {SelectedPlatform} drama details...";
            try
            {
                var drama = await _crawlerService.FetchDramaDetailsAsync(SearchUrl, SelectedPlatform);
                DramaLibrary.Insert(0, drama);
                SelectDrama(drama);
                StatusMessage = $"Successfully loaded {drama.Title} ({drama.Episodes.Count} episodes).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to fetch episodes: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SelectDrama(DramaModel drama)
        {
            if (SelectedDrama != null)
            {
                SelectedDrama.IsSelectedInLibrary = false;
            }

            SelectedDrama = drama;
            SelectedDrama.IsSelectedInLibrary = true;

            CurrentEpisodes.Clear();
            foreach (var ep in drama.Episodes)
            {
                CurrentEpisodes.Add(ep);
            }

            UpdateSelectionCount();
        }

        [RelayCommand]
        public void SelectAll()
        {
            foreach (var ep in CurrentEpisodes)
            {
                ep.IsSelected = true;
            }
            UpdateSelectionCount();
        }

        [RelayCommand]
        public void DeselectAll()
        {
            foreach (var ep in CurrentEpisodes)
            {
                ep.IsSelected = false;
            }
            UpdateSelectionCount();
        }

        public void UpdateSelectionCount()
        {
            TotalSelectedCount = CurrentEpisodes.Count(e => e.IsSelected);
        }

        [RelayCommand]
        public void BrowseSaveDirectory()
        {
            // Simple folder dialog or standard folder selection
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Output Save Directory",
                InitialDirectory = SaveDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                SaveDirectory = dialog.FolderName;
            }
        }

        [RelayCommand]
        public async Task StartDownloadAsync()
        {
            var selectedEps = CurrentEpisodes.Where(e => e.IsSelected && e.Status != EpisodeDownloadStatus.Completed).ToList();
            if (selectedEps.Count == 0)
            {
                StatusMessage = "No episodes selected for download.";
                return;
            }

            if (SelectedDrama == null)
            {
                StatusMessage = "Please select a drama first.";
                return;
            }

            IsDownloading = true;
            _downloadCts = new CancellationTokenSource();
            var ct = _downloadCts.Token;

            TotalSelectedCount = selectedEps.Count;
            CompletedCount = 0;
            OverallProgress = 0.0;

            // Target output subfolder
            var dramaSubfolder = Path.Combine(SaveDirectory, SelectedDrama.Title);
            Directory.CreateDirectory(dramaSubfolder);

            // Concurrency control via SemaphoreSlim
            var semaphore = new SemaphoreSlim(WorkerCount, WorkerCount);
            var tasks = new List<Task>();

            foreach (var ep in selectedEps)
            {
                ep.Status = EpisodeDownloadStatus.Queued;
            }

            StatusMessage = $"Downloading {selectedEps.Count} episodes with {WorkerCount} parallel workers...";

            foreach (var episode in selectedEps)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        if (ct.IsCancellationRequested) return;

                        var epProgress = new Progress<(double Progress, string Speed, string Eta)>(data =>
                        {
                            // Trigger dynamic progress calculation
                            RecalculateOverallProgress(selectedEps);
                        });

                        await _downloaderService.DownloadEpisodeAsync(episode, SelectedDrama.Title, dramaSubfolder, epProgress, ct);

                        if (episode.Status == EpisodeDownloadStatus.Completed)
                        {
                            Interlocked.Increment(ref _completedCount);
                            OnPropertyChanged(nameof(CompletedCount));
                        }
                    }
                    catch (Exception ex)
                    {
                        episode.Status = EpisodeDownloadStatus.Failed;
                        episode.ErrorMessage = ex.Message;
                    }
                    finally
                    {
                        semaphore.Release();
                        RecalculateOverallProgress(selectedEps);
                    }
                }, ct));
            }

            try
            {
                await Task.WhenAll(tasks);
                StatusMessage = ct.IsCancellationRequested
                    ? "Download paused/cancelled."
                    : $"Completed all {selectedEps.Count} episodes successfully!";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Downloads cancelled by user.";
            }
            finally
            {
                IsDownloading = false;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }

        [RelayCommand]
        public void CancelDownload()
        {
            _downloadCts?.Cancel();
            StatusMessage = "Cancelling downloads...";
        }

        private void RecalculateOverallProgress(List<EpisodeModel> activeList)
        {
            if (activeList.Count == 0) return;
            var totalPct = activeList.Sum(e => e.Progress);
            OverallProgress = Math.Round(totalPct / activeList.Count, 1);
        }
    }
}
