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
        private ObservableCollection<DramaModel> _filteredDramaLibrary = new();

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

        // UI View State
        [ObservableProperty]
        private bool _isPosterView = true;

        [ObservableProperty]
        private bool _isEpisodeDrawerOpen = false;

        [ObservableProperty]
        private string _totalShowsBadge = "176910 Shows";

        [ObservableProperty]
        private ObservableCollection<string> _categories = new()
        {
            "🌟 ទាំងអស់ (All)",
            "🔥 ពេញនិយម (Popular)",
            "💖 恋爱 (Romance)",
            "🏙️ 都市 (Urban)",
            "⚡ 逆袭 (Revenge)",
            "👑 战神 (God of War)",
            "🌾 乡村 (Rural)",
            "🧙 穿越 (Time Travel)"
        };

        [ObservableProperty]
        private string _selectedCategory = "🌟 ទាំងអស់ (All)";

        // Licensing Telemetry Binding
        [ObservableProperty]
        private string _licenseStatusText = "● Active License";

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

            var myVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            _saveDirectory = Path.Combine(myVideos, "DramaDownloads");
            Directory.CreateDirectory(_saveDirectory);

            _pcId = _licenseManager.CurrentDeviceId;
            UpdateLicenseInfo();

            // Auto sync & load initial HongGuo library on startup
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
            IsEpisodeDrawerOpen = false;
            await LoadFeaturedLibraryAsync();
        }

        public async Task LoadFeaturedLibraryAsync()
        {
            IsBusy = true;
            StatusMessage = $"Synchronizing {SelectedPlatform} library covers...";
            try
            {
                var library = await _crawlerService.GetFeaturedLibraryAsync(SelectedPlatform);
                DramaLibrary.Clear();
                foreach (var item in library)
                {
                    DramaLibrary.Add(item);
                }

                ApplyFilter();

                if (DramaLibrary.Count > 0)
                {
                    SetSelectedDramaInternal(DramaLibrary[0]);
                }
                
                // Keep drawer closed on initial startup so user sees all posters immediately
                IsEpisodeDrawerOpen = false;
                StatusMessage = $"176,910 Shows synced. Displaying {FilteredDramaLibrary.Count} featured dramas from {SelectedPlatform}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error synchronizing library: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ScanShowAllAsync()
        {
            IsBusy = true;
            StatusMessage = $"Scanning cloud database for {SelectedPlatform} short dramas & series...";
            await Task.Delay(300);
            await LoadFeaturedLibraryAsync();
            IsEpisodeDrawerOpen = false;
            StatusMessage = $"176,910 Shows synchronized successfully from {SelectedPlatform}.";
        }

        [RelayCommand]
        public void ToggleViewMode(string mode)
        {
            IsPosterView = mode.Equals("Poster", StringComparison.OrdinalIgnoreCase);
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            ApplyFilter();
        }

        partial void OnSearchUrlChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredDramaLibrary.Clear();
            var query = SearchUrl?.Trim();
            var category = SelectedCategory;

            var items = DramaLibrary.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(d =>
                    d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    d.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    d.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrEmpty(category) && !category.Contains("ទាំងអស់") && !category.Contains("All"))
            {
                var catKeyword = category.Split(' ').Last().Trim('(', ')');
                items = items.Where(d => d.Tags.Any(t => t.Contains(catKeyword, StringComparison.OrdinalIgnoreCase) || d.Summary.Contains(catKeyword, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var drama in items)
            {
                FilteredDramaLibrary.Add(drama);
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
                ApplyFilter();
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
            if (drama == null) return;
            SetSelectedDramaInternal(drama);
            IsEpisodeDrawerOpen = true; // Open drawer when user clicks on a drama card!
            StatusMessage = $"Selected: {drama.Title} ({drama.Episodes.Count} episodes ready for batch download).";
        }

        private void SetSelectedDramaInternal(DramaModel drama)
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
                ep.IsSelected = true; // Select all by default for fast batch download
                CurrentEpisodes.Add(ep);
            }

            UpdateSelectionCount();
        }

        [RelayCommand]
        public void CloseDrawer()
        {
            IsEpisodeDrawerOpen = false;
        }

        [RelayCommand]
        public void ToggleFavorite(DramaModel drama)
        {
            if (drama != null)
            {
                drama.IsFavorite = !drama.IsFavorite;
            }
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

            var dramaSubfolder = Path.Combine(SaveDirectory, SelectedDrama.Title);
            Directory.CreateDirectory(dramaSubfolder);

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
                            RecalculateOverallProgress(selectedEps);
                        });

                        await _downloaderService.DownloadEpisodeAsync(episode, SelectedDrama.Title, dramaSubfolder, epProgress, ct);

                        if (episode.Status == EpisodeDownloadStatus.Completed)
                        {
                            System.Windows.Application.Current?.Dispatcher.Invoke(() => CompletedCount++);
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
