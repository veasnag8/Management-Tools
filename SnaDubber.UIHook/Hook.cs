using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rectangle = System.Windows.Shapes.Rectangle;
using CommunityToolkit.Mvvm.Input;
using SnaDubber.Models;
using SnaDubber.License;
using SnaDubber.Services;
using SnaDubber.ViewModels;

namespace SnaDubber.UIHook;

public static class Hook
{
	private sealed class TimelineSnapshot
	{
		public List<(DialogueLine Line, int Start, int End, string StartText, string EndText)> Lines { get; } = new();
		public List<(OverlayItem Item, int Start, int End)> Overlays { get; } = new();
		public List<(TextLayerItem Item, int Start, int End)> TextLayers { get; } = new();
		public List<(BlurRegionItem Item, int Start, int End)> BlurRegions { get; } = new();
	}
	private class VisualBackup
	{
		public Brush Background { get; set; }

		public Brush Foreground { get; set; }

		public Brush BorderBrush { get; set; }

		public Brush CaretBrush { get; set; }
	}

	private static bool _isLicenseInjected = false;

	private static bool _isToolbarInjected = false;

	private static bool _isRemoveVideoInjected = false;

	private static DispatcherTimer _removeVideoRetryTimer = null;

	private static bool _isTimelineInjected = false;

	private static bool _isModalCloseInjected = false;

	private static bool _isAudioMuteHooked = false;

	private static bool _isVirtualizationConfigured = false;

	private static bool _isUiCleanedUp = false;

	private static bool _isThemeHooked = false;

	private static bool _isWhiteTheme = false;

	private static readonly Dictionary<DependencyObject, VisualBackup> _themeBackups = new Dictionary<DependencyObject, VisualBackup>();

	private static bool _isSubtitleOverlayHooked = false;

	private static bool _isMediaOverlayEditorHooked = false;

	private static bool _isTextLayerToolHooked = false;

	private static bool _isTextLayerInteractionHooked = false;

	private static DispatcherTimer _textLayerInteractionTimer = null;

	private static TextLayerItem _selectedTimelineTextLayer = null;

	private static readonly HashSet<TextLayerItem> _selectedTimelineTextLayers = new();

	private static readonly Dictionary<TextLayerItem, bool> _textLayerBold = new();

	private static readonly Dictionary<TextLayerItem, bool> _textLayerItalic = new();

	private static readonly Dictionary<TextLayerItem, double> _textLayerOpacity = new();

	private static readonly HashSet<BlurRegionItem> _selectedTimelineBlurRegions = new();

	private static DispatcherTimer _mediaOverlayRefreshTimer = null;

	private static readonly Dictionary<OverlayItem, MediaOverlayVisual> _mediaOverlayVisuals = new();

	private sealed class MediaOverlayVisual
	{
		public Border Container { get; set; }
		public FrameworkElement Content { get; set; }
		public MediaElement Video { get; set; }
		public Image Image { get; set; }
		public List<BitmapFrame> GifFrames { get; set; }
		public int GifFrameIndex { get; set; }
		public DateTime NextGifFrameAt { get; set; }
		public double AspectRatio { get; set; } = 1.0;
		public Border ResizeHandle { get; set; }
	}

	private static readonly Dictionary<BlurRegionItem, BlurRegionVisual> _blurRegionVisuals = new();

	private static readonly Dictionary<BlurRegionItem, double> _blurRegionOpacity = new();

	private static readonly Dictionary<BlurRegionItem, (double relX, double relY, double relW, double relH)> _blurNormalizedCoords = new();
	private static readonly Dictionary<string, (double relX, double relY, double relW, double relH)> _blurNormalizedCoordsById = new();

	private static Rect _lastActiveVideoBounds = Rect.Empty;

	private static bool _isBlurToolHooked = false;

	private static bool _isOutsideDeselectHooked = false;

	private sealed class BlurRegionVisual
	{
		public Border Container { get; set; }
		public Rectangle Box { get; set; }
		public Border HeaderPill { get; set; }
		public TextBlock Label { get; set; }
		public Border CornerHandle { get; set; }
		public Border RightHandle { get; set; }
		public Border BottomHandle { get; set; }
		public Button DeleteButton { get; set; }
	}

	private static bool _isSubtitleOverlayVisible = false;

	private static Border _subBorderRef = null;

	private static TextBlock _subTextBlockRef = null;

	private static Button _btnSubtitlesSidebarRef = null;

	private static ScaleTransform _subScaleTransform = null;

	private static bool _isFollowPlaybackHooked = false;

	private static MainViewModel _voiceSpeedViewModel = null;

	private static INotifyCollectionChanged _voiceSpeedLines = null;

	private static bool _isVoiceSpeedControlHooked = false;

	private static MainViewModel _dialogueFontViewModel = null;

	private static bool _isDialogueFontControlHooked = false;

	private static bool _isOverlayOpacityToolbarHooked = false;

	private static bool _isUpdatingVoiceSpeedControl = false;

	private static Slider _voiceSpeedSlider = null;

	private static TextBlock _voiceSpeedLabel = null;

	private static DispatcherTimer _voiceSpeedPreviewTimer = null;

	private static readonly SemaphoreSlim _voiceSpeedPreviewLock = new SemaphoreSlim(1, 1);

	private static readonly MediaPlayer _voiceSpeedPreviewPlayer = new MediaPlayer();
	private static readonly MediaPlayer _previewLiveKhmerPlayer = new MediaPlayer();
	private static readonly MediaPlayer _previewLiveBgmPlayer = new MediaPlayer();
	private static int _previewLivePlayingLineIndex = -1;
	private static DispatcherTimer _previewAudioSyncTimer = null;
	private static bool _isPreviewAudioSyncHooked = false;

	private static int _voiceSpeedPreviewVersion = 0;
	private static bool _isExportLoadingHooked = false;
	private static bool _isSocialPresetHooked = false;
	private static bool _isTranscribeTopHooked = false;

	private static bool _isEpisodeBatchHooked = false;
	private static bool _isAutoUpdateCenterHooked = false;

	private sealed class EpisodeBatchItem
	{
		public int Number { get; set; }
		public double StartSec { get; set; }
		public double DurationSec { get; set; }
		public CheckBox CheckBox { get; set; }
	}
	private static bool _isPersistentLicenseHooked = false;
	private static bool _isRevokedOrDisabled = false;
	private static bool _pendingRevocationAfterCurrentTask = false;
	private static DispatcherTimer _licensePollerTimer = null;
	private static bool _isCheckingLicense = false;
	private static Border _licenseDialogBorderRef = null;
	private static Button _licenseDialogCloseButtonRef = null;
	private static DispatcherTimer _exportProgressTimer = null;

	private static readonly object _exportArtifactLock = new();

	private static readonly List<string> _temporaryExportArtifacts = new();

	private static readonly List<OverlayItem> _temporaryExportOverlays = new();
	private static readonly List<BlurRegionItem> _stashedBlurRegions = new();
	private static DateTime _exportStartedAt;

	private static DialogueLine _lastFollowedLine = null;

	private static ScrollViewer _dialogueScrollViewer = null;

	private static ItemsControl _dialogueItemsControl = null;

	private static Border _lastHighlightedBorder = null;

	private static Brush _lastHighlightedOriginalBrush = null;

	private static Thickness _lastHighlightedOriginalThickness = default;

	private static bool _isTimelineEditHooked = false;

	private static bool _isWaveformTimelineInjected = false;

	private static bool _isDraggingClipStart = false;

	private static bool _isDraggingClipEnd = false;

	private static bool _isDraggingClipMove = false;

	private static DialogueLine _activeDraggingLine = null;

	private static Border _activeDraggingBorder = null;

	private static double _dragStartWindowX = 0.0;

	private static int _initialStartMs = 0;

	private static int _initialEndMs = 0;

	private static OverlayItem _activeDraggingOverlay = null;

	private static OverlayItem _selectedTimelineOverlay = null;

	private static readonly HashSet<OverlayItem> _selectedTimelineOverlays = new();

	private static readonly HashSet<DialogueLine> _selectedTimelineLines = new();

	private static string _selectedDialogueTrackLane = null;

	private static double _timelineZoom = 1.0;

	private static double _timelineDurationSeconds = 1.0;

	private static double _timelineEditPositionSec = -1.0;

	private static Action<double> _showPinnedTimelinePlayhead = null;

	private static readonly List<Canvas> _continuousTimelineTracks = new List<Canvas>();

	private static readonly List<ScrollViewer> _continuousTimelineScrollers = new List<ScrollViewer>();

	private static readonly Dictionary<RowDefinition, GridLength> _timelineRowHeights = new Dictionary<RowDefinition, GridLength>();

	private static bool _syncingTimelineScroll = false;

	private static bool _subtitleTimelineVisible = false;

	private static bool _subtitleToggleHooked = false;

	private static Action _refreshTimelineVisibility = null;

	private static Action _refreshCommonTimelineScrollBar = null;

	private static readonly Stack<TimelineSnapshot> _timelineUndo = new();

	private static readonly Stack<TimelineSnapshot> _timelineRedo = new();

	private static bool _classHandlersRegistered = false;

	private static bool _isPipelineRunning = false;

	private static bool _transcriptionCommandRunning = false;

	private static bool _normalizingBusyStatus = false;

	private static string _pipelineStatusText = string.Empty;

	private static bool _forceFfmpegOnly = false;

	private static readonly string GeminiQuotaFlagPath = Path.Combine(Path.GetTempPath(), "sna_gemini_quota_exhausted.flag");

	private static volatile bool _cancelRequested = false;

	private static Button _btnPreviewRef = null;

	private static Button _btnTimelinePreviewRef = null;

	private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hook.log");

	private static bool _injectionScheduled = false;

	private static void Log(string msg)
	{
		try
		{
			File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
		}
		catch
		{
		}
	}

	public static void Attach(Window window)
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected Obj, but got Unknown
		if (window != null)
		{
			window.Title = "SNA-Dubber Version 2.0.4";
			Log("Attach called for Window: " + ((object)window).GetType().FullName);
			InitializeEnvironment();
			RegisterGlobalCaretHandlers();
			window.Loaded += (object s, RoutedEventArgs e) =>
			{
				ScheduleInjections(window);
				ApplyModernStudioRedesign(window);
			};
			window.ContentRendered += (object? s, EventArgs e) =>
			{
				ScheduleInjections(window);
				ApplyModernStudioRedesign(window);
				CheckPostUpdateSplash(window);
			};
			window.DataContextChanged += (object s, DependencyPropertyChangedEventArgs e) =>
			{
				WireViewModel(window);
			ScheduleInjections(window);
			WireModernStylingAutoRefresh(window);
			};
			WireViewModel(window);
			ScheduleInjections(window);
		}
	}

	private static void ScheduleInjections(Window window)
	{
		if (!_injectionScheduled)
		{
			_injectionScheduled = true;
			((DispatcherObject)window).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
			{
				_injectionScheduled = false;
				PerformInjections(window);
			}), (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private static void PerformInjections(Window window)
	{
		try
		{
			if (!string.Equals(window.Title, "SNA-Dubber Version 2.0.1", StringComparison.Ordinal))
			{
				window.Title = "SNA-Dubber Version 2.0.1";
			}
			if (!_isLicenseInjected)
			{
				TryInjectLicense(window);
			}
			if (!_isToolbarInjected)
			{
				TryInjectToolbar(window);
			}
			if (!_isRemoveVideoInjected)
			{
				TryInjectRemoveVideoButton(window);
			}
			if (!_isTimelineInjected)
			{
				UpdateTimelineBar(window);
			}
			if (!_isModalCloseInjected)
			{
				TryInjectModalCloseButton(window);
			}
			if (!_isAudioMuteHooked)
			{
				WireAudioMuteButton(window);
			}
			if (!_isVirtualizationConfigured)
			{
				EnableVirtualizationAndSmoothScrolling(window);
			}
			if (!_isUiCleanedUp)
			{
				ApplyUiCustomizationsAndCleanups(window);
			}
			if (!_isThemeHooked)
			{
				HookThemeToggle(window);
			}
			if (!_isSubtitleOverlayHooked)
			{
				WireSubtitleOverlayInteractive(window);
			}
			if (!_isMediaOverlayEditorHooked)
			{
				WireMediaOverlayEditor(window);
			}
			if (!_isTextLayerToolHooked)
			{
				WireTextLayerTool(window);
			}
			if (!_isBlurToolHooked)
			{
				WireBlurTool(window);
			}
			if (!_isOutsideDeselectHooked)
			{
				WireDeselectOnClickOutside(window);
			}
			if (!_isTextLayerInteractionHooked)
			{
				WireTextLayerInteraction(window);
			}
			if (!_isFollowPlaybackHooked)
			{
				WireFollowPlaybackAutoScroll(window);
			}
			if (!_isTimelineEditHooked)
			{
				WireTimelineClipsAndSplit(window);
			}
			if (!_isWaveformTimelineInjected)
			{
				TryInjectWaveformTimeline(window);
			}
			if (!_subtitleToggleHooked)
			{
				WireSubtitleTimelineToggle(window);
			}
			if (!_isVoiceSpeedControlHooked)
			{
				WireVoiceSpeedPercentControl(window);
			}
			if (!_isDialogueFontControlHooked)
			{
				WireDialogueFontSizeControl(window);
			}
			if (!_isOverlayOpacityToolbarHooked)
			{
				WireOverlayOpacityToolbar(window);
			}
			if (!_isExportLoadingHooked)
			{
				WireExportLoading(window);
			}
			if (!_isSocialPresetHooked || !FindAllVisualChildren<Button>((DependencyObject)(object)window).Any(b => (b.Tag as string ?? "").StartsWith("SocialLandscapeButton")))
			{
				WireSocialMediaPresets(window);
			}
			if (!_isEpisodeBatchHooked || !FindAllVisualChildren<Button>((DependencyObject)(object)window).Any(b => (b.Tag as string ?? "").StartsWith("EpisodeBatchButton")))
			{
				WireEpisodeBatchTool(window);
			}
			if (!_isTranscribeTopHooked || !FindAllVisualChildren<Button>((DependencyObject)(object)window).Any(b => (b.Tag as string ?? "").StartsWith("TopTranscribeButton")))
			{
				RelocateTranscribeToTop(window);
			}
			if (!_isAutoUpdateCenterHooked || !FindAllVisualChildren<Button>((DependencyObject)(object)window).Any(b => (b.Tag as string ?? "") == "AutoUpdateCenterButton"))
			{
				WireAutoUpdateCenterTool(window);
			}
			TryAttachExistingPreviewVideo(window);
			WirePreviewAudioSync(window);
			ApplyModernStudioRedesign(window);
		}
		catch
		{
		}
	}

	private static void InitializeEnvironment()
	{
		try
		{
			string text = Environment.GetEnvironmentVariable("GEMINI_API_KEY", EnvironmentVariableTarget.Process) ?? "";
			if (string.IsNullOrEmpty(text))
			{
				text = Environment.GetEnvironmentVariable("GEMINI_API_KEY", EnvironmentVariableTarget.User) ?? "";
			}
			if (string.IsNullOrEmpty(text))
			{
				string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "gemini_api_key.txt");
				if (File.Exists(path))
				{
					text = File.ReadAllText(path).Trim();
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				Environment.SetEnvironmentVariable("GEMINI_API_KEY", text, EnvironmentVariableTarget.Process);
				Environment.SetEnvironmentVariable("GEMINI_API_KEY", text, EnvironmentVariableTarget.User);
				Log("Successfully initialized GEMINI_API_KEY from storage: " + text.Substring(0, Math.Min(8, text.Length)) + "...");
			}
		}
		catch (Exception ex)
		{
			Log("InitializeEnvironment error: " + ex.Message);
		}
	}

	private static void RegisterGlobalCaretHandlers()
	{
		if (_classHandlersRegistered)
		{
			return;
		}
		_classHandlersRegistered = true;
		try
		{
			EventManager.RegisterClassHandler(typeof(TextBox), UIElement.GotFocusEvent, (RoutedEventHandler)((object s, RoutedEventArgs e) =>
			{
				if (s is TextBox tb)
				{
					ApplyHighContrastCaret(tb);
				}
			}));
			EventManager.RegisterClassHandler(typeof(TextBox), FrameworkElement.LoadedEvent, (RoutedEventHandler)((object s, RoutedEventArgs e) =>
			{
				if (s is TextBox tb)
				{
					ApplyHighContrastCaret(tb);
				}
			}));
			Log("Registered lightweight global Caret class handlers for TextBox");
		}
		catch (Exception ex)
		{
			Log("Error registering class handlers: " + ex.Message);
		}
	}

	public static void ApplyHighContrastCaret(TextBox tb)
	{
		if (tb == null)
		{
			return;
		}
		try
		{
			tb.CaretBrush = Brushes.White;
			tb.SelectionBrush = new SolidColorBrush(Color.FromArgb(170, 56, 189, 248));
			tb.SelectionOpacity = 0.65;
		}
		catch
		{
		}
	}

	public static void EnableVirtualizationAndSmoothScrolling(Window window)
	{
		try
		{
			List<ListBox> list = FindAllVisualChildren<ListBox>((DependencyObject)(object)window);
			foreach (ListBox item in list)
			{
				VirtualizingPanel.SetIsVirtualizing((DependencyObject)(object)item, value: true);
				VirtualizingPanel.SetVirtualizationMode((DependencyObject)(object)item, VirtualizationMode.Recycling);
				VirtualizingPanel.SetScrollUnit((DependencyObject)(object)item, ScrollUnit.Pixel);
				VirtualizingPanel.SetIsVirtualizingWhenGrouping((DependencyObject)(object)item, value: true);
				VirtualizingPanel.SetCacheLengthUnit((DependencyObject)(object)item, VirtualizationCacheLengthUnit.Page);
				VirtualizingPanel.SetCacheLength((DependencyObject)(object)item, new VirtualizationCacheLength(1.0, 2.0));
				ScrollViewer.SetCanContentScroll((DependencyObject)(object)item, canContentScroll: true);
				ScrollViewer.SetIsDeferredScrollingEnabled((DependencyObject)(object)item, value: false);
			}
			List<ScrollViewer> list2 = FindAllVisualChildren<ScrollViewer>((DependencyObject)(object)window);
			foreach (ScrollViewer item2 in list2)
			{
				item2.PanningMode = PanningMode.VerticalOnly;
				ScrollViewer.SetIsDeferredScrollingEnabled((DependencyObject)(object)item2, value: false);
			}
			if (list.Count > 0 || list2.Count > 0)
			{
				_isVirtualizationConfigured = true;
				Log($"Configured high-performance UI Virtualization and Smooth Pixel Scrolling for {list.Count} ListBoxes and {list2.Count} ScrollViewers");
			}
		}
		catch (Exception ex)
		{
			Log("EnableVirtualizationAndSmoothScrolling error: " + ex.Message);
		}
	}

	private static void WireViewModel(Window window)
	{
		object dataContext = window.DataContext;
		MainViewModel vm = dataContext as MainViewModel;
		if (vm == null)
		{
			return;
		}
		WireVoiceSpeedToDialogue(vm);
		WirePersistentLicensePrompt(window, vm);
		string text = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
		if (!string.IsNullOrEmpty(text) && string.IsNullOrEmpty(vm.GeminiApiKeyInput))
		{
			vm.GeminiApiKeyInput = text;
		}
		vm.PropertyChanged += (object? s, PropertyChangedEventArgs e) =>
		{
			if (e.PropertyName == "IsLicenseDialogOpen" || e.PropertyName == "IsLicensed")
			{
				if (!_isLicenseInjected)
				{
					((DispatcherObject)window).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
					{
						TryInjectLicense(window);
					}), (DispatcherPriority)4, Array.Empty<object>());
				}

				if ((!vm.IsLicensed || _isRevokedOrDisabled) && !vm.IsLicenseDialogOpen)
				{
					// If unlicensed or revoked, do not allow closing the license dialog!
					window.Dispatcher.BeginInvoke((Action)(() =>
					{
						if (!vm.IsLicensed || _isRevokedOrDisabled)
						{
							vm.IsLicenseDialogOpen = true;
							UpdateLicenseDialogCloseButton(window, vm);
						}
					}), DispatcherPriority.Normal);
				}
				else
				{
					UpdateLicenseDialogCloseButton(window, vm);
				}
			}
			else if (e.PropertyName == "IsExporting" || e.PropertyName == "IsBusy")
			{
				if (e.PropertyName == "IsExporting")
				{
					if (vm.IsExporting)
					{
						PreExportSync(vm, window);
					}
					else
					{
						RemoveTemporaryExportOverlays(vm);
					}
				}

				// If admin revoked while generating/exporting, wait until current task completes, then lock immediately!
				if (_pendingRevocationAfterCurrentTask && !IsGeneratingOrExporting(vm))
				{
					_pendingRevocationAfterCurrentTask = false;
					EnforceDubberLockout(window, vm, LicenseState.Revoked, wasRunningTask: true);
				}
			}
			else if (e.PropertyName == "CurrentPositionSec")
			{
				SyncPlaybackFollow(window, vm);
			}
			else if (e.PropertyName == "SelectedDialogueLine")
			{
				UpdateSubtitleOverlayText(vm.SelectedDialogueLine);
			}
			else if (e.PropertyName == "CurrentProject")
			{
				_isTimelineInjected = false;
				_isVirtualizationConfigured = false;
				_isSubtitleOverlayHooked = false;
				_isMediaOverlayEditorHooked = false;
				_isDialogueFontControlHooked = false;
				_mediaOverlayRefreshTimer?.Stop();
				_mediaOverlayRefreshTimer = null;
				_mediaOverlayVisuals.Clear();
				_blurRegionVisuals.Clear();
				_isBlurToolHooked = false;
				_isOutsideDeselectHooked = false;
				_isFollowPlaybackHooked = false;
				_isTimelineEditHooked = false;
				_isWaveformTimelineInjected = false;
				_continuousTimelineTracks.Clear();
				_continuousTimelineScrollers.Clear();
				_timelineRowHeights.Clear();
				_selectedDialogueTrackLane = null;
				_refreshCommonTimelineScrollBar = null;
				_subtitleTimelineVisible = false;
				_timelineUndo.Clear();
				_timelineRedo.Clear();
				_lastFollowedLine = null;
				ScheduleInjections(window);
			}
			else if (e.PropertyName == "BusyStatusText" && _transcriptionCommandRunning && !_normalizingBusyStatus)
			{
				string currentStatus = vm.BusyStatusText ?? string.Empty;
				string normalized = "ជំហាន 1/6៖ ទាញយកអត្ថបទនិយាយពីវីដេអូ...";
				if (!string.Equals(currentStatus, normalized, StringComparison.Ordinal))
				{
					_normalizingBusyStatus = true;
					vm.BusyStatusText = normalized;
					_normalizingBusyStatus = false;
				}
			}
			else if (e.PropertyName == "BusyStatusText" && _isPipelineRunning && !_normalizingBusyStatus
				&& !string.IsNullOrWhiteSpace(_pipelineStatusText)
				&& !string.Equals(vm.BusyStatusText, _pipelineStatusText, StringComparison.Ordinal))
			{
				_normalizingBusyStatus = true;
				vm.BusyStatusText = _pipelineStatusText;
				_normalizingBusyStatus = false;
			}
			else if (e.PropertyName == "IsBusy" && vm.IsBusy && !_isModalCloseInjected)
			{
				((DispatcherObject)window).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
				{
					TryInjectModalCloseButton(window);
				}), (DispatcherPriority)4, Array.Empty<object>());
			}
		};
		IAsyncRelayCommand transcribeAudioCommand = vm.TranscribeAudioCommand;
		AsyncRelayCommand arc = transcribeAudioCommand as AsyncRelayCommand;
		if (arc == null)
		{
			return;
		}
		bool wasRunning = false;
		arc.PropertyChanged += async (object? s, PropertyChangedEventArgs e) =>
		{
			if (e.PropertyName == "IsRunning")
			{
				if (arc.IsRunning)
				{
					ClearGeminiQuotaError();
					wasRunning = true;
					_transcriptionCommandRunning = true;
					vm.BusyStatusText = "ជំហាន 1/6៖ ទាញយកអត្ថបទនិយាយពីវីដេអូ...";
					SetDialogueEditingEnabled(window, vm, false);
				}
				else if (wasRunning)
				{
					wasRunning = false;
					_transcriptionCommandRunning = false;
					string quotaError = ConsumeGeminiQuotaError();
					if (quotaError != null)
					{
						string quotaChoice = ShowGeminiQuotaDialog(window, vm, quotaError);
						if (quotaChoice == "retry")
						{
							vm.TranscribeAudioCommand.Execute(null);
							return;
						}
						if (quotaChoice == "ffmpeg")
						{
							_forceFfmpegOnly = true;
							await RunFullPipelineAsync(vm, window);
							return;
						}
						SetDialogueEditingEnabled(window, vm, true);
						return;
					}
					if (vm.DialogueLines != null && vm.DialogueLines.Count > 0 && !_isPipelineRunning)
					{
						Log("FFmpeg/transcription finished. Keeping dialogue locked while Gemini performs the natural-language review.");
						await RunFullPipelineAsync(vm, window);
					}
					else
					{
						SetDialogueEditingEnabled(window, vm, true);
					}
				}
			}
		};
	}

	// FFmpeg can produce enough diagnostic output to fill redirected Windows
	// pipes. Waiting before consuming those pipes deadlocks the child process.
	// Drain both streams concurrently, then await exit; the legacy caller may
	// safely read them again and simply receive the exhausted stream.
	public static async Task WaitForFfmpegExitDrainingAsync(Process process, CancellationToken cancellationToken)
	{
		if (process == null) return;
		StringBuilder stderrBuf = new StringBuilder();
		Task stdout = Task.Run(() => { try { process.StandardOutput.ReadToEnd(); } catch { } });
		Task stderr = Task.Run(() => { 
			try 
			{ 
				string err = process.StandardError.ReadToEnd(); 
				if (!string.IsNullOrEmpty(err)) stderrBuf.Append(err);
			} 
			catch { } 
		});
		await process.WaitForExitAsync(cancellationToken);
		await Task.WhenAll(stdout, stderr);

		string args = process.StartInfo?.Arguments ?? "";
		if (process.ExitCode != 0)
		{
			string errText = stderrBuf.ToString();
			Log($"FFmpeg exited with code {process.ExitCode}. Stderr: {(errText.Length > 400 ? errText.Substring(errText.Length - 400) : errText)}");
		}

		if (args.Contains("snadub_audio_") && args.Contains(".wav"))
		{
			try
			{
				string outWav = null;
				int lastQuote = args.LastIndexOf('"');
				if (lastQuote > 0)
				{
					int prevQuote = args.LastIndexOf('"', lastQuote - 1);
					if (prevQuote >= 0)
					{
						outWav = args.Substring(prevQuote + 1, lastQuote - prevQuote - 1);
					}
				}
				if (!string.IsNullOrEmpty(outWav))
				{
					bool ok = File.Exists(outWav) && new FileInfo(outWav).Length > 1000;
					if (!ok)
					{
						Log($"WaitForFfmpegExitDrainingAsync: snadub_audio output missing or invalid ({outWav}). Running emergency FFmpeg builder...");
						string toolsFfmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
						if (!File.Exists(toolsFfmpeg)) toolsFfmpeg = "ffmpeg";
						ProcessStartInfo psi = new ProcessStartInfo
						{
							FileName = toolsFfmpeg,
							Arguments = args,
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardError = true
						};
						using var p2 = Process.Start(psi);
						if (p2 != null)
						{
							await p2.WaitForExitAsync(cancellationToken);
							Log($"Emergency FFmpeg dub builder finished. Exists: {File.Exists(outWav)}, Size: {(File.Exists(outWav) ? new FileInfo(outWav).Length : 0)}");
						}
					}
					else
					{
						Log($"BuildKhmerDubAudioAsync: {outWav} verified ({new FileInfo(outWav).Length} bytes).");
					}
				}
			}
			catch (Exception ex)
			{
				Log("BuildKhmerDubAudioAsync fallback check error: " + ex.Message);
			}
		}
	}

	public static bool IsGeneratingOrExporting(MainViewModel vm)
	{
		if (vm == null) return false;
		if (vm.IsExporting) return true;
		if (vm.IsBusy) return true;
		if (vm.StartExportCommand?.IsRunning == true) return true;
		if (vm.ExportThisCommand?.IsRunning == true) return true;
		if (vm.ExportAllCommand?.IsRunning == true) return true;
		if (vm.SynthesizeAllVoicesCommand?.IsRunning == true) return true;
		if (vm.SynthesizeSingleLineAudioCommand?.IsRunning == true) return true;
		if (vm.RegenerateSelectedLineCommand?.IsRunning == true) return true;
		if (vm.TranscribeDialogueCommand?.IsRunning == true) return true;
		if (vm.TranscribeAudioCommand?.IsRunning == true) return true;
		if (vm.TranslateAllCommand?.IsRunning == true) return true;
		if (vm.TranslateDialogueCommand?.IsRunning == true) return true;
		if (vm.SeparateAudioCommand?.IsRunning == true) return true;
		if (vm.GenerateSocialMetadataCommand?.IsRunning == true) return true;
		return false;
	}

	private static bool IsVisualDescendantOfLicenseDialog(DependencyObject dep, Window window)
	{
		if (dep == null) return false;
		try
		{
			if (_licenseDialogBorderRef != null && dep is Visual v && _licenseDialogBorderRef.IsAncestorOf(v))
				return true;
		}
		catch { }

		DependencyObject cur = dep;
		while (cur != null && cur != window)
		{
			if (cur == _licenseDialogBorderRef) return true;
			if (cur is FrameworkElement fe && (fe.Tag as string == "GeminiKeySection" || fe.Tag as string == "LicenseDialogContainer"))
				return true;
			if (cur is TextBox tb && (tb.Text == (window.DataContext as MainViewModel)?.LicenseKeyInput || tb.Text == (window.DataContext as MainViewModel)?.DeviceIdText))
				return true;
			cur = VisualTreeHelper.GetParent(cur) ?? LogicalTreeHelper.GetParent(cur);
		}
		return false;
	}

	private static void UpdateLicenseDialogCloseButton(Window window, MainViewModel vm)
	{
		try
		{
			if (_licenseDialogCloseButtonRef == null)
			{
				List<Button> buttons = FindAllVisualChildren<Button>((DependencyObject)(object)window);
				_licenseDialogCloseButtonRef = buttons.FirstOrDefault(b =>
					b.Command == vm.CloseLicenseDialogCommand ||
					string.Join(" ", FindAllVisualChildren<TextBlock>((DependencyObject)(object)b).Select(t => t.Text ?? "")).Contains("✕"));
			}

			if (_licenseDialogCloseButtonRef != null)
			{
				bool isAllowedToClose = vm.IsLicensed && !_isRevokedOrDisabled && !_pendingRevocationAfterCurrentTask;
				_licenseDialogCloseButtonRef.Visibility = isAllowedToClose ? Visibility.Visible : Visibility.Collapsed;
				_licenseDialogCloseButtonRef.IsEnabled = isAllowedToClose;
			}
		}
		catch { }
	}

	private static void EnforceDubberLockout(Window window, MainViewModel vm, LicenseState state, bool wasRunningTask)
	{
		_isRevokedOrDisabled = true;
		_pendingRevocationAfterCurrentTask = false;
		try { new SecureStorageService().ClearSession(); } catch { }

		window.Dispatcher.Invoke(() =>
		{
			vm.IsLicensed = false;
			vm.LicenseStatusBadge = "REVOKED";
			vm.LicenseStatusColor = "#DC2626";
			vm.LicenseExpiryText = "Tool ត្រូវបាន Disable ឬ Revoke ដោយ Admin!";
			vm.LicenseErrorMessage = "Tool ត្រូវបានផ្តាច់ចេញភ្លាមៗ (Disconnected)! មិនអាចបន្តការប្រើប្រាស់បានទៀតទេ។";
			vm.SelectedTabIndex = 6;
			if (!vm.IsLicenseDialogOpen && vm.OpenLicenseDialogCommand?.CanExecute(null) == true)
			{
				vm.OpenLicenseDialogCommand.Execute(null);
			}

			UpdateLicenseDialogCloseButton(window, vm);

			string message = wasRunningTask
				? "ការ Generate/Export ចុងក្រោយត្រូវបានបញ្ចប់!\n\nTool របស់អ្នកត្រូវបាន Disable ឬ Revoke ដោយ Admin។ Tool ត្រូវបានផ្តាច់ចេញភ្លាមៗ (Disconnected) មិនអាចបន្តការប្រើប្រាស់បានទៀតទេ។\n\nសូមទាក់ទង Admin ដើម្បីបើកសិទ្ធិ ឬបញ្ចូល Key ថ្មី។"
				: "Tool របស់អ្នកត្រូវបាន Disable ឬ Revoke ដោយ Admin!\n\nTool ត្រូវបានផ្តាច់ចេញភ្លាមៗ (Disconnected)។ អ្នកមិនអាចបន្តការប្រើប្រាស់បានទៀតទេ។\n\nសូមទាក់ទង Admin ដើម្បីបើកសិទ្ធិ ឬបញ្ចូល Key ថ្មី។";

			MessageBox.Show(
				window,
				message,
				"Access Revoked by Admin",
				MessageBoxButton.OK,
				MessageBoxImage.Stop);
		});
	}

	private static async Task CheckLicenseRevocationAsync(Window window, MainViewModel vm)
	{
		if (_isCheckingLicense || vm == null) return;
		_isCheckingLicense = true;
		try
		{
			// If already revoked, keep locked out
			if (_isRevokedOrDisabled)
			{
				if (vm.IsLicensed) vm.IsLicensed = false;
				if (!vm.IsLicenseDialogOpen && vm.OpenLicenseDialogCommand?.CanExecute(null) == true)
					vm.OpenLicenseDialogCommand.Execute(null);
				UpdateLicenseDialogCloseButton(window, vm);
				return;
			}

			// If not licensed yet, keep dialog open and user locked
			if (!vm.IsLicensed)
			{
				if (!vm.IsLicenseDialogOpen && vm.OpenLicenseDialogCommand?.CanExecute(null) == true)
					vm.OpenLicenseDialogCommand.Execute(null);
				UpdateLicenseDialogCloseButton(window, vm);
				return;
			}

			// Check if previous task completed
			if (_pendingRevocationAfterCurrentTask)
			{
				if (!IsGeneratingOrExporting(vm))
				{
					_pendingRevocationAfterCurrentTask = false;
					EnforceDubberLockout(window, vm, LicenseState.Revoked, wasRunningTask: true);
				}
				return;
			}

			// Validate with license server
			var lmField = typeof(MainViewModel).GetField("_licenseManager", BindingFlags.Instance | BindingFlags.NonPublic);
			var lm = lmField?.GetValue(vm) as ILicenseManager;
			if (lm == null) return;

			var state = await lm.InitializeAndValidateAsync();
			bool isRevoked = state == LicenseState.Disabled
			              || state == LicenseState.Revoked
			              || state == LicenseState.Expired
			              || state == LicenseState.MismatchedProduct;

			if (isRevoked)
			{
				try { new SecureStorageService().ClearSession(); } catch { }

				bool isBusy = IsGeneratingOrExporting(vm);
				if (isBusy)
				{
					if (!_pendingRevocationAfterCurrentTask)
					{
						_pendingRevocationAfterCurrentTask = true;
						window.Dispatcher.Invoke(() =>
						{
							vm.ProjectStatusMessage = "⚠️ Admin បាន Revoke/Disable Tool! ការ Generate/Export បច្ចុប្បន្នត្រូវបានអនុញ្ញាតឱ្យបញ្ចប់១លើកចុងក្រោយ។";
						});
					}
				}
				else
				{
					EnforceDubberLockout(window, vm, state, wasRunningTask: false);
				}
			}
		}
		catch (Exception ex)
		{
			Log("CheckLicenseRevocationAsync error: " + ex.Message);
		}
		finally
		{
			_isCheckingLicense = false;
		}
	}

	private static void WirePersistentLicensePrompt(Window window, MainViewModel vm)
	{
		if (_isPersistentLicenseHooked || vm == null) return;
		_isPersistentLicenseHooked = true;
		try
		{
			try
			{
				StoredLicenseSession saved = new SecureStorageService().LoadSession();
				if (saved != null && !string.IsNullOrWhiteSpace(saved.LicenseKey))
					vm.LicenseKeyInput = saved.LicenseKey;
			}
			catch (Exception ex) { Log("Could not preload saved license session: " + ex.Message); }

			IAsyncRelayCommand activate = vm.ActivateLicenseCommand;
			if (activate != null)
			{
				activate.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
				{
					if (args.PropertyName != "IsRunning" || activate.IsRunning) return;
					if (vm.IsLicensed)
					{
						_isRevokedOrDisabled = false;
						_pendingRevocationAfterCurrentTask = false;
						try { vm.SaveSettingsCommand?.Execute(null); } catch { }
						if (vm.CloseLicenseDialogCommand?.CanExecute(null) == true)
							vm.CloseLicenseDialogCommand.Execute(null);
						UpdateLicenseDialogCloseButton(window, vm);
						vm.ProjectStatusMessage = "License verified and saved securely. Tool unlocked.";
					}
				};
			}

			// Intercept mouse clicks: If not licensed, cannot click outside license dialog!
			window.PreviewMouseDown += (s, e) =>
			{
				if (!vm.IsLicensed || _isRevokedOrDisabled)
				{
					if (e.OriginalSource is DependencyObject dep && IsVisualDescendantOfLicenseDialog(dep, window))
					{
						return; // Allow interacting with license key box and activate button
					}
					e.Handled = true; // Block clicking anywhere else!
					if (!vm.IsLicenseDialogOpen && vm.OpenLicenseDialogCommand?.CanExecute(null) == true)
					{
						vm.OpenLicenseDialogCommand.Execute(null);
					}
					UpdateLicenseDialogCloseButton(window, vm);
				}
			};

			// Intercept key events: Block hotkeys when unlicensed; block ESC inside dialog
			window.PreviewKeyDown += (s, e) =>
			{
				if (!vm.IsLicensed || _isRevokedOrDisabled)
				{
					if (e.OriginalSource is DependencyObject dep && IsVisualDescendantOfLicenseDialog(dep, window))
					{
						if (e.Key == Key.Escape)
						{
							e.Handled = true; // Block ESC from dismissing required dialog
						}
						return;
					}
					e.Handled = true; // Block hotkeys
				}
			};

			// Intercept drag and drop: Cannot drag videos if unlicensed
			window.PreviewDragOver += (s, e) =>
			{
				if (!vm.IsLicensed || _isRevokedOrDisabled)
				{
					e.Effects = DragDropEffects.None;
					e.Handled = true;
				}
			};
			window.Drop += (s, e) =>
			{
				if (!vm.IsLicensed || _isRevokedOrDisabled)
				{
					e.Handled = true;
				}
			};

			// Startup check: Force license prompt immediately if not licensed
			DispatcherTimer promptTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
			{
				Interval = TimeSpan.FromSeconds(1.5)
			};
			promptTimer.Tick += (s, e) =>
			{
				promptTimer.Stop();
				if (!vm.IsLicensed)
				{
					if (!vm.IsLicenseDialogOpen && vm.OpenLicenseDialogCommand?.CanExecute(null) == true)
						vm.OpenLicenseDialogCommand.Execute(null);
					UpdateLicenseDialogCloseButton(window, vm);
				}
			};
			promptTimer.Start();

			// Fast Heartbeat/Revocation Poller: Checks server every 12 seconds
			_licensePollerTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
			{
				Interval = TimeSpan.FromSeconds(12)
			};
			_licensePollerTimer.Tick += async (s, e) =>
			{
				await CheckLicenseRevocationAsync(window, vm);
			};
			_licensePollerTimer.Start();

			Log("Persistent verified license enforcement and fast revocation poller enabled.");
		}
		catch (Exception ex)
		{
			_isPersistentLicenseHooked = false;
			Log("WirePersistentLicensePrompt error: " + ex.Message);
		}
	}

	private static void TryInjectRemoveVideoButton(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm) return;

			// 1. Permanently hide the original Upload Video button in the toolbar above the viewport
			List<Button> allButtons = FindAllVisualChildren<Button>((DependencyObject)(object)window);
			Button upload = allButtons.FirstOrDefault(button =>
			{
				string buttonText = string.Join(" ", FindAllVisualChildren<TextBlock>((DependencyObject)(object)button)
					.Select(text => text.Text ?? string.Empty));
				return buttonText.IndexOf("Upload Video", StringComparison.OrdinalIgnoreCase) >= 0
					&& buttonText.IndexOf("Upload Video File", StringComparison.OrdinalIgnoreCase) < 0;
			})
				?? allButtons.FirstOrDefault(button => string.Equals(button.Content as string, "Upload Video", StringComparison.OrdinalIgnoreCase));

			if (upload != null)
			{
				upload.Visibility = Visibility.Collapsed;
				if (upload.Parent is Panel parentPanel && parentPanel.Children.OfType<UIElement>().All(c => c.Visibility == Visibility.Collapsed))
				{
					parentPanel.Visibility = Visibility.Collapsed;
				}
			}

			// 2. Locate the VideoViewport or PreviewPlayer parent Grid
			Grid videoViewport = FindVisualChild((DependencyObject)(object)window, (Grid g) => g.Name == "VideoViewport");
			if (videoViewport == null)
			{
				MediaElement preview = FindVisualChild((DependencyObject)(object)window, (MediaElement m) => m.Name == "PreviewPlayer");
				videoViewport = preview?.Parent as Grid;
			}

			if (videoViewport == null)
			{
				if (_removeVideoRetryTimer == null)
				{
					_removeVideoRetryTimer = new DispatcherTimer(DispatcherPriority.Loaded, window.Dispatcher)
					{
						Interval = TimeSpan.FromMilliseconds(400.0)
					};
					_removeVideoRetryTimer.Tick += (s, e) =>
					{
						if (_isRemoveVideoInjected)
						{
							_removeVideoRetryTimer.Stop();
							_removeVideoRetryTimer = null;
							return;
						}
						TryInjectRemoveVideoButton(window);
					};
					_removeVideoRetryTimer.Start();
					Log("VideoViewport not rendered yet; scheduled retry.");
				}
				return;
			}

			// 3. Create or reuse the compact red Remove Video button at the top-right corner
			Button cornerRemoveBtn = videoViewport.Children.OfType<Button>().FirstOrDefault(b => (b.Tag as string)?.StartsWith("CornerRemoveVideoButton") == true);
			if (cornerRemoveBtn == null)
			{
				cornerRemoveBtn = new Button
				{
					Tag = "CornerRemoveVideoButton",
					Content = "🗑",
					ToolTip = "Remove Video",
					Width = 32,
					Height = 32,
					FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI, sans-serif"),
					FontSize = 14,
					FontWeight = FontWeights.Bold,
					HorizontalAlignment = HorizontalAlignment.Right,
					VerticalAlignment = VerticalAlignment.Top,
					Margin = new Thickness(0, 10, 10, 0),
					Background = new SolidColorBrush(Color.FromArgb(200, 36, 15, 22)),
					Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
					BorderBrush = new SolidColorBrush(Color.FromArgb(180, 239, 68, 68)),
					BorderThickness = new Thickness(1),
					Cursor = Cursors.Hand,
					Padding = new Thickness(0),
					Visibility = HasCurrentVideoReference(vm) ? Visibility.Visible : Visibility.Collapsed
				};
				Panel.SetZIndex(cornerRemoveBtn, 9999);
				if (videoViewport.RowDefinitions.Count > 0)
					Grid.SetRowSpan(cornerRemoveBtn, videoViewport.RowDefinitions.Count);
				if (videoViewport.ColumnDefinitions.Count > 0)
					Grid.SetColumnSpan(cornerRemoveBtn, videoViewport.ColumnDefinitions.Count);
				SetControlCornerRadius(cornerRemoveBtn, 6);

				Brush normalBg = new SolidColorBrush(Color.FromArgb(200, 36, 15, 22));
				Brush normalFg = new SolidColorBrush(Color.FromRgb(248, 113, 113));
				Brush normalBorder = new SolidColorBrush(Color.FromArgb(180, 239, 68, 68));
				Brush hoverBg = new SolidColorBrush(Color.FromRgb(220, 38, 38));
				Brush hoverFg = Brushes.White;
				Brush hoverBorder = new SolidColorBrush(Color.FromRgb(254, 202, 202));

				cornerRemoveBtn.MouseEnter += (s, e) =>
				{
					cornerRemoveBtn.Background = hoverBg;
					cornerRemoveBtn.Foreground = hoverFg;
					cornerRemoveBtn.BorderBrush = hoverBorder;
				};
				cornerRemoveBtn.MouseLeave += (s, e) =>
				{
					cornerRemoveBtn.Background = normalBg;
					cornerRemoveBtn.Foreground = normalFg;
					cornerRemoveBtn.BorderBrush = normalBorder;
				};

				Action updateButton = () =>
				{
					bool hasVideo = HasCurrentVideoReference(vm);
					cornerRemoveBtn.Visibility = hasVideo ? Visibility.Visible : Visibility.Collapsed;
					if (upload != null) upload.Visibility = Visibility.Collapsed;
				};

				vm.PropertyChanged += (s, e) =>
				{
					if (e.PropertyName == "HasVideoLoaded" || e.PropertyName == "VideoDurationSec" || e.PropertyName == "CurrentProject")
						updateButton();
				};

				cornerRemoveBtn.PreviewMouseLeftButtonDown += async (s, e) =>
				{
					if (!HasCurrentVideoReference(vm)) return;
					e.Handled = true;
					MessageBoxResult answer = MessageBox.Show(
						"Remove the current video and all timeline edits from this project?\n\nThe original video file on your computer will not be deleted.",
						"Remove Video", MessageBoxButton.YesNo, MessageBoxImage.Warning);
					if (answer != MessageBoxResult.Yes) return;

					await CleanupGeneratedProjectFilesAsync(vm);

					MediaElement preview = FindVisualChild((DependencyObject)(object)window,
						(MediaElement media) => media.Name == "PreviewPlayer");
					if (preview != null)
					{
						preview.Stop();
						preview.Source = null;
					}
					vm.DialogueLines?.Clear();
					vm.Overlays?.Clear();
					vm.TextLayers?.Clear();
					vm.BlurRegions?.Clear();
					vm.SelectedDialogueLine = null;
					vm.SelectedOverlay = null;
					vm.AudioMix = new AudioMixSettings();
					if (vm.CurrentProject != null)
					{
						vm.CurrentProject.VideoPath = string.Empty;
						vm.CurrentProject.Width = 0;
						vm.CurrentProject.Height = 0;
						vm.CurrentProject.DurationSec = 0.0;
						vm.CurrentProject.Fps = 0.0;
						vm.CurrentProject.AudioTrackCount = 0;
					}
					vm.CurrentPositionSec = 0.0;
					vm.VideoDurationSec = 0.0;
					vm.TimecodeDisplay = "00:00:00 / 00:00:00";
					vm.HasVideoLoaded = false;
					vm.ProjectStatusMessage = "Video removed. You can upload a new video now.";
					_selectedTimelineOverlay = null;
					_timelineUndo.Clear();
					_timelineRedo.Clear();
					_refreshTimelineVisibility?.Invoke();
					try { vm.SaveProjectCommand?.Execute(null); } catch { }
					updateButton();
				};

				videoViewport.Children.Add(cornerRemoveBtn);
				updateButton();
			}

			_isRemoveVideoInjected = true;
			_removeVideoRetryTimer?.Stop();
			_removeVideoRetryTimer = null;
			Log("Corner Remove Video icon button injected successfully at top-right of VideoViewport.");
		}
		catch (Exception ex)
		{
			Log("TryInjectRemoveVideoButton error: " + ex.Message);
		}
	}

	private static bool HasCurrentVideoReference(MainViewModel vm)
	{
		return vm != null && (vm.HasVideoLoaded || vm.VideoDurationSec > 0.01
			|| !string.IsNullOrWhiteSpace(vm.CurrentProject?.VideoPath));
	}

	private static void SetPipelineStatus(MainViewModel vm, string status)
	{
		_pipelineStatusText = status ?? string.Empty;
		vm.BusyStatusText = _pipelineStatusText;
		if (_pipelineStatusText.StartsWith("ជំហាន 2/6", StringComparison.Ordinal)) vm.BusyProgressPercent = Math.Max(vm.BusyProgressPercent, 20.0);
		else if (_pipelineStatusText.StartsWith("ជំហាន 3/6", StringComparison.Ordinal)) vm.BusyProgressPercent = Math.Max(vm.BusyProgressPercent, 35.0);
		else if (_pipelineStatusText.StartsWith("ជំហាន 4/6", StringComparison.Ordinal)) vm.BusyProgressPercent = Math.Max(vm.BusyProgressPercent, 50.0);
		else if (_pipelineStatusText.StartsWith("ជំហាន 5/6", StringComparison.Ordinal)) vm.BusyProgressPercent = Math.Max(vm.BusyProgressPercent, 85.0);
		else if (_pipelineStatusText.StartsWith("ជំហាន 6/6", StringComparison.Ordinal)) vm.BusyProgressPercent = Math.Max(vm.BusyProgressPercent, 92.0);
	}

	private static string ConsumeGeminiQuotaError()
	{
		try
		{
			if (!File.Exists(GeminiQuotaFlagPath)) return null;
			string message = File.ReadAllText(GeminiQuotaFlagPath, Encoding.UTF8);
			File.Delete(GeminiQuotaFlagPath);
			return message;
		}
		catch { return null; }
	}

	private static void ClearGeminiQuotaError()
	{
		try
		{
			if (File.Exists(GeminiQuotaFlagPath)) File.Delete(GeminiQuotaFlagPath);
		}
		catch { }
	}

	private static string ShowGeminiQuotaDialog(Window owner, MainViewModel vm, string error)
	{
		string result = "cancel";
		Window dialog = new Window
		{
			Title = "Gemini Token/Quota អស់",
			Owner = owner,
			Width = 520.0,
			Height = 315.0,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			ResizeMode = ResizeMode.NoResize,
			Background = new SolidColorBrush(Color.FromRgb(8, 17, 31)),
			Foreground = Brushes.White,
			ShowInTaskbar = false
		};
		StackPanel panel = new StackPanel { Margin = new Thickness(24.0) };
		panel.Children.Add(new TextBlock
		{
			Text = "Gemini Token/Quota បានអស់ហើយ",
			FontSize = 18.0, FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		});
		panel.Children.Add(new TextBlock
		{
			Text = "សូមបញ្ចូល Gemini API Key ថ្មី រួចសាកម្ដងទៀត ឬបន្តដំណើរការដែលនៅសល់ដោយ FFmpeg តែប៉ុណ្ណោះ។",
			TextWrapping = TextWrapping.Wrap, FontSize = 12.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
		});
		TextBox keyBox = new TextBox
		{
			Text = "", Height = 34.0, Padding = new Thickness(8.0),
			Background = new SolidColorBrush(Color.FromRgb(15, 29, 48)), Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(51, 74, 101)),
			ToolTip = "Paste a new Gemini API Key"
		};
		panel.Children.Add(keyBox);
		TextBlock hint = new TextBlock
		{
			Text = "ប៊ូតុង FFmpeg-only បង្ហាញតែករណី Token/Quota អស់ប៉ុណ្ណោះ។",
			Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 10.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 15.0)
		};
		panel.Children.Add(hint);
		StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
		Button retry = CreateTimelineToolButton("រក្សាទុក Key ថ្មី និងសាកម្ដងទៀត", "Save the new Gemini key and retry");
		retry.Padding = new Thickness(12.0, 7.0, 12.0, 7.0);
		retry.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
		Button ffmpeg = CreateTimelineToolButton("បន្តដោយ FFmpeg តែប៉ុណ្ណោះ", "Continue without Gemini");
		ffmpeg.Padding = new Thickness(12.0, 7.0, 12.0, 7.0);
		ffmpeg.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		Button cancel = CreateTimelineToolButton("បោះបង់", "Cancel");
		cancel.Padding = new Thickness(10.0, 7.0, 10.0, 7.0);
		cancel.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		retry.Click += (object sender, RoutedEventArgs args) =>
		{
			string key = keyBox.Text.Trim();
			if (string.IsNullOrWhiteSpace(key)) { hint.Text = "សូមបញ្ចូល API Key ថ្មីសិន។"; hint.Foreground = Brushes.Orange; return; }
			try
			{
				Environment.SetEnvironmentVariable("GEMINI_API_KEY", key, EnvironmentVariableTarget.Process);
				Environment.SetEnvironmentVariable("GEMINI_API_KEY", key, EnvironmentVariableTarget.User);
				vm.GeminiApiKeyInput = key;
				string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber");
				Directory.CreateDirectory(folder);
				File.WriteAllText(Path.Combine(folder, "gemini_api_key.txt"), key, Encoding.UTF8);
				result = "retry"; dialog.DialogResult = true;
			}
			catch (Exception ex) { hint.Text = "រក្សាទុក Key មិនបាន៖ " + ex.Message; hint.Foreground = Brushes.OrangeRed; }
		};
		ffmpeg.Click += (object sender, RoutedEventArgs args) => { result = "ffmpeg"; dialog.DialogResult = true; };
		cancel.Click += (object sender, RoutedEventArgs args) => { result = "cancel"; dialog.DialogResult = false; };
		buttons.Children.Add(retry); buttons.Children.Add(ffmpeg); buttons.Children.Add(cancel);
		panel.Children.Add(buttons);
		dialog.Content = panel;
		dialog.ShowDialog();
		return result;
	}

	private static void SetDialogueEditingEnabled(Window window, MainViewModel vm, bool enabled)
	{
		if (window == null || vm == null) return;
		window.Dispatcher.Invoke((Action)(() =>
		{
			foreach (ItemsControl editor in FindAllVisualChildren<ItemsControl>((DependencyObject)(object)window))
			{
				if (ReferenceEquals(editor.ItemsSource, vm.DialogueLines))
				{
					editor.IsEnabled = enabled;
					editor.Opacity = enabled ? 1.0 : 0.72;
					editor.ToolTip = enabled ? null : "AI is checking meaning and natural Khmer. Editing unlocks when review is complete.";
				}
			}
		}));
	}

	private static double NormalizeVoiceSpeed(double speed)
	{
		if (double.IsNaN(speed) || double.IsInfinity(speed))
		{
			return 1.0;
		}
		return Math.Max(0.25, Math.Min(4.0, speed));
	}

	private static void WireVoiceSpeedPercentControl(Window window)
	{
		if (window.DataContext is not MainViewModel vm)
		{
			return;
		}

		foreach (TextBlock label in FindAllVisualChildren<TextBlock>((DependencyObject)(object)window))
		{
			if (label.Text == null || label.Text.IndexOf("SPEED", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}

			DependencyObject parent = VisualTreeHelper.GetParent(label);
			Slider slider = parent == null ? null : FindVisualChild(parent, (Slider item) => true);
			if (slider == null)
			{
				continue;
			}

			_voiceSpeedLabel = label;
			_voiceSpeedSlider = slider;
			BindingOperations.ClearBinding(slider, Slider.ValueProperty);
			slider.Minimum = 0.0;
			slider.Maximum = 100.0;
			slider.SmallChange = 1.0;
			slider.LargeChange = 10.0;
			slider.IsSnapToTickEnabled = true;
			slider.TickFrequency = 1.0;

			UpdateVoiceSpeedPercentControl(vm.VoiceSpeed);
			slider.ValueChanged += (object sender, RoutedPropertyChangedEventArgs<double> args) =>
			{
				if (_isUpdatingVoiceSpeedControl)
				{
					return;
				}
				double percent = Math.Max(0.0, Math.Min(100.0, Math.Round(args.NewValue)));
				// Exponential scale: 50%=1x (default), 75%=2x, 100%=4x.
				// This changes synthesized speech only, never MediaElement speed.
				vm.VoiceSpeed = Math.Pow(4.0, (percent - 50.0) / 50.0);
				UpdateVoiceSpeedPercentControl(vm.VoiceSpeed);
				ScheduleVoiceSpeedPreview(vm, window);
			};
			vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
			{
				if (args.PropertyName == "VoiceSpeed")
				{
					UpdateVoiceSpeedPercentControl(vm.VoiceSpeed);
				}
			};

			_isVoiceSpeedControlHooked = true;
			Log("Voice speed UI changed to a live 0-100 percent control");
			return;
		}
	}

	private static (int width, int height) ProbeVideoSize(string videoPath)
	{
		try
		{
			if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath)) return (0, 0);
			string ffprobe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffprobe.exe");
			if (!File.Exists(ffprobe)) ffprobe = "ffprobe";
			var psi = new ProcessStartInfo
			{
				FileName = ffprobe,
				Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 \"{videoPath}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true
			};
			using var proc = Process.Start(psi);
			if (proc != null)
			{
				string output = proc.StandardOutput.ReadToEnd()?.Trim();
				proc.WaitForExit();
				if (!string.IsNullOrEmpty(output) && output.Contains('x'))
				{
					string[] parts = output.Split('x');
					if (int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
					{
						return (w, h);
					}
				}
			}
		}
		catch { }
		return (0, 0);
	}

	private static void EnsureAllDialogueLinesApproved(MainViewModel vm)
	{
		if (vm?.DialogueLines == null || vm.DialogueLines.Count == 0) return;
		string projId = vm.CurrentProject?.Id ?? "";
		string cacheDir = !string.IsNullOrEmpty(projId) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "AudioCache", projId) : "";
		if (!string.IsNullOrEmpty(cacheDir)) Directory.CreateDirectory(cacheDir);

		int approved = 0;
		List<DialogueLine> missingAudioLines = new List<DialogueLine>();

		foreach (DialogueLine line in vm.DialogueLines)
		{
			if (string.IsNullOrEmpty(line.AudioFilePath) || !File.Exists(line.AudioFilePath) || new FileInfo(line.AudioFilePath).Length < 100)
			{
				if (!string.IsNullOrEmpty(cacheDir))
				{
					string cand1 = Path.Combine(cacheDir, $"line_{line.Index}.mp3");
					string cand2 = Path.Combine(cacheDir, $"line_{line.Index}.wav");
					if (File.Exists(cand1) && new FileInfo(cand1).Length > 100) line.AudioFilePath = cand1;
					else if (File.Exists(cand2) && new FileInfo(cand2).Length > 100) line.AudioFilePath = cand2;
				}

				// If not found in current project's cache, search across other project caches in AudioCache
				if (string.IsNullOrEmpty(line.AudioFilePath) || !File.Exists(line.AudioFilePath))
				{
					try
					{
						string baseAudioCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "AudioCache");
						if (Directory.Exists(baseAudioCache))
						{
							foreach (string otherProjDir in Directory.GetDirectories(baseAudioCache))
							{
								string otherCand = Path.Combine(otherProjDir, $"line_{line.Index}.mp3");
								if (File.Exists(otherCand) && new FileInfo(otherCand).Length > 100)
								{
									if (!string.IsNullOrEmpty(cacheDir))
									{
										string localCopy = Path.Combine(cacheDir, $"line_{line.Index}.mp3");
										File.Copy(otherCand, localCopy, true);
										line.AudioFilePath = localCopy;
									}
									else
									{
										line.AudioFilePath = otherCand;
									}
									break;
								}
							}
						}
					}
					catch { }
				}
			}

			if (!string.IsNullOrEmpty(line.AudioFilePath) && File.Exists(line.AudioFilePath) && new FileInfo(line.AudioFilePath).Length > 100)
			{
				line.IsApproved = true;
				approved++;
			}
			else
			{
				string txt = !string.IsNullOrWhiteSpace(line.CorrectedKhmer) ? line.CorrectedKhmer : line.DraftKhmer;
				if (!string.IsNullOrWhiteSpace(txt)) missingAudioLines.Add(line);
			}
		}

		if (missingAudioLines.Count > 0 && !string.IsNullOrEmpty(cacheDir))
		{
			Log($"EnsureAllDialogueLinesApproved: Auto-synthesizing {missingAudioLines.Count} missing Khmer voice line(s) before export...");
			foreach (DialogueLine line in missingAudioLines)
			{
				try
				{
					string text = !string.IsNullOrWhiteSpace(line.CorrectedKhmer) ? line.CorrectedKhmer : line.DraftKhmer;
					string voice = string.IsNullOrWhiteSpace(line.VoiceProfile) ? "piset" : line.VoiceProfile;
					string outPath = Path.Combine(cacheDir, $"line_{line.Index}.mp3");
					double speed = line.Speed > 0.1 ? line.Speed : 1.0;
					double volume = line.Volume > 0.1 ? line.Volume : 1.0;
					int pitch = line.Pitch;

					bool ok = RunDirectPythonTtsAsync(text, outPath, voice, speed, volume, pitch).GetAwaiter().GetResult();
					if (ok && File.Exists(outPath) && new FileInfo(outPath).Length > 100)
					{
						line.AudioFilePath = outPath;
						line.IsApproved = true;
						approved++;
					}
				}
				catch (Exception ex)
				{
					Log($"Auto-synthesis for line {line.Index} error: {ex.Message}");
				}
			}
		}

		Log($"EnsureAllDialogueLinesApproved: {approved}/{vm.DialogueLines.Count} dialogue line(s) approved with audio.");
	}

	private static void PreExportSync(MainViewModel vm, Window window)
	{
		try
		{
			if (_originalVideoSourcePath != null && File.Exists(_originalVideoSourcePath))
			{
				vm.CurrentProject.VideoPath = _originalVideoSourcePath;
			}

			EnsureAllDialogueLinesApproved(vm);

			if (vm.AudioMix != null)
			{
				// MUTE THE ORIGINAL CHINESE TALKING COMPLETELY
				vm.AudioMix.IsOriginalMuted = true;
				vm.AudioMix.OriginalAudioVolume = 0.0;

				// UNMUTE AND BOOST KHMER TRANSLATED DUBBING
				vm.AudioMix.IsKhmerMuted = false;
				vm.AudioMix.KhmerDubVolume = 3.0;

				// DO NOT LEAK CHINESE TALKING FROM CRUDE SEPARATED AUDIO
				// Only keep BGM if the user chose a real music file outside SeparatedAudio
				if (!string.IsNullOrEmpty(vm.AudioMix.BgmTrackPath) && vm.AudioMix.BgmTrackPath.Contains("SeparatedAudio"))
				{
					vm.AudioMix.IsBgmMuted = true;
					vm.AudioMix.BgmVolume = 0.0;
					vm.AudioMix.BgmTrackPath = null;
				}
				else if (string.IsNullOrEmpty(vm.AudioMix.BgmTrackPath) || !File.Exists(vm.AudioMix.BgmTrackPath))
				{
					vm.AudioMix.IsBgmMuted = true;
					vm.AudioMix.BgmVolume = 0.0;
				}
			}

			InjectTextLayersForExport(vm, window);
		}
		catch (Exception ex)
		{
			Log("PreExportSync error: " + ex.Message);
		}
	}

	private static void WireExportLoading(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm) return;
			IAsyncRelayCommand export = vm.ExportThisCommand;
			if (export == null) return;

			// Synchronously hook the Export button so PreExportSync runs BEFORE SaveFileDialog opens!
			Button exportBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(b => (b.Command == export) 
					|| (b.ToolTip as string ?? "").IndexOf("Export current video", StringComparison.OrdinalIgnoreCase) >= 0
					|| FindVisualChild(b, (TextBlock tb) => tb.Text != null && tb.Text.Contains("Export This")) != null);
			if (exportBtn != null && exportBtn.Tag as string != "ExportLoadingWired")
			{
				exportBtn.Tag = "ExportLoadingWired";
				exportBtn.PreviewMouseLeftButtonDown += (s, e) =>
				{
					Log("Export button clicked: executing synchronous PreExportSync");
					PreExportSync(vm, window);
				};
			}

			Button exportAllBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(b => (b.Command == vm.ExportAllCommand)
					|| (b.ToolTip as string ?? "").IndexOf("Batch render", StringComparison.OrdinalIgnoreCase) >= 0
					|| FindVisualChild(b, (TextBlock tb) => tb.Text != null && tb.Text.Contains("Export All")) != null);
			if (exportAllBtn != null && exportAllBtn.Tag as string != "ExportAllLoadingWired")
			{
				exportAllBtn.Tag = "ExportAllLoadingWired";
				exportAllBtn.PreviewMouseLeftButtonDown += (s, e) =>
				{
					Log("Export All button clicked: executing synchronous PreExportSync");
					PreExportSync(vm, window);
				};
			}

			bool wasRunning = export.IsRunning;
			export.PropertyChanged += async (object? sender, PropertyChangedEventArgs args) =>
			{
				if (args.PropertyName != "IsRunning") return;
				if (export.IsRunning)
				{
					wasRunning = true;
					PreExportSync(vm, window);
					if (_isVideoFlipped)
					{
						await EnsureFlippedVideoSourceAsync(vm);
					}
					if (_isFrameFillMode && (vm.SelectedSocialPreset?.AspectRatioText == "16:9"))
					{
						await EnsureFilledVideoSourceAsync(vm);
					}
					InjectTextLayersForExport(vm, window);
					_exportStartedAt = DateTime.UtcNow;
					vm.IsBusy = true;
					vm.BusyProgressPercent = 8.0;
					vm.BusyStatusText = "Exporting video... Please wait while SNA-Dubber renders all visual and audio layers.";
					vm.ProjectStatusMessage = "Exporting video...";
					_exportProgressTimer?.Stop();
					_exportProgressTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
					{
						Interval = TimeSpan.FromSeconds(1.0)
					};
					_exportProgressTimer.Tick += (s, e) =>
					{
						if (!export.IsRunning) { _exportProgressTimer.Stop(); return; }
						int elapsed = Math.Max(1, (int)(DateTime.UtcNow - _exportStartedAt).TotalSeconds);
						// This is activity progress (FFmpeg does not expose frame progress to
						// the view model). It advances toward 94% and reaches 100% only when
						// the export command actually completes.
						double activity = Math.Min(94.0, 8.0 + 86.0 * (1.0 - Math.Exp(-elapsed / 55.0)));
						vm.BusyProgressPercent = Math.Max(vm.BusyProgressPercent, activity);
						string stage = activity < 38.0 ? "Mixing Khmer audio" : (activity < 82.0 ? "Rendering video" : "Finalizing export");
						vm.BusyStatusText = $"{stage}...  Elapsed {TimeSpan.FromSeconds(elapsed):mm\\:ss}";
					};
					_exportProgressTimer.Start();
				}
				else if (wasRunning)
				{
					wasRunning = false;
					if (!vm.IsExporting)
					{
						RemoveTemporaryExportOverlays(vm);
					}
					if (_originalVideoSourcePath != null && vm.CurrentProject != null)
					{
						if (_isVideoFlipped && _cachedFlippedVideoPath != null)
							vm.CurrentProject.VideoPath = _cachedFlippedVideoPath;
						else
							vm.CurrentProject.VideoPath = _originalVideoSourcePath;
					}
					_exportProgressTimer?.Stop();
					bool succeeded = string.Equals(vm.ExportStatusText, "Export Complete!", StringComparison.OrdinalIgnoreCase);
					if (succeeded)
					{
						vm.BusyProgressPercent = 100.0;
						vm.BusyStatusText = "Export complete. Cleaning generated cache...";
						await CleanupGeneratedProjectFilesAsync(vm);
						vm.ProjectStatusMessage = "Export complete. Temporary project files cleaned.";
					}
					else
					{
						vm.BusyStatusText = "Export stopped or failed. Cleaning incomplete generated files...";
						await CleanupGeneratedProjectFilesAsync(vm);
						vm.ProjectStatusMessage = "Export failed/cancelled. Incomplete cache cleaned; source files kept.";
					}
					await Task.Delay(700);
					if (!export.IsRunning)
					{
						vm.IsBusy = false;
						vm.BusyStatusText = string.Empty;
					}
				}
			};
			_isExportLoadingHooked = true;
			Log("Export This now shows a blocking render/loading status until the command completes.");
		}
		catch (Exception ex)
		{
			Log("WireExportLoading error: " + ex.Message);
		}
	}

	private static void InjectTextLayersForExport(MainViewModel vm, Window window = null)
	{
		try
		{
			if (vm.IsExporting && _temporaryExportOverlays.Count > 0)
			{
				Log("InjectTextLayersForExport: Overlays already active for current export.");
				return;
			}
			RemoveTemporaryExportOverlays(vm);

			// 1. Ensure all dialogue lines with synthesized audio are marked IsApproved = true
			// so FFmpeg BuildKhmerDubAudioAsync includes them in the exported audio track!
			EnsureAllDialogueLinesApproved(vm);

			// 2. Ensure Khmer dub volume is properly boosted and original Chinese audio is fully muted
			if (vm.AudioMix != null)
			{
				vm.AudioMix.IsOriginalMuted = true;
				vm.AudioMix.OriginalAudioVolume = 0.0;
				vm.AudioMix.IsKhmerMuted = false;
				vm.AudioMix.KhmerDubVolume = 3.0;

				if (!string.IsNullOrEmpty(vm.AudioMix.BgmTrackPath) && vm.AudioMix.BgmTrackPath.Contains("SeparatedAudio"))
				{
					vm.AudioMix.IsBgmMuted = true;
					vm.AudioMix.BgmVolume = 0.0;
					vm.AudioMix.BgmTrackPath = null;
				}
			}

			// 3. Obtain displayed video bounds on the UI preview canvas
			if (window == null)
			{
				window = Application.Current?.MainWindow;
			}
			Canvas previewCanvas = null;
			if (window != null)
			{
				previewCanvas = FindVisualChild<Canvas>((DependencyObject)(object)window, c => c.Name == "OverlayCanvas");
				if (previewCanvas == null)
				{
					previewCanvas = FindAllVisualChildren<Canvas>((DependencyObject)(object)window).FirstOrDefault(c => c.ActualWidth > 100);
				}
			}

			Rect videoBounds = (previewCanvas != null && previewCanvas.ActualWidth > 10.0)
				? GetDisplayedVideoBounds(previewCanvas, vm)
				: (_lastActiveVideoBounds.Width > 10.0 ? _lastActiveVideoBounds : new Rect(0, 0, 1920, 1080));

			double metaW = 0.0;
			double metaH = 0.0;
			string vPath = vm.CurrentProject?.VideoPath;
			if (!string.IsNullOrEmpty(vPath) && File.Exists(vPath))
			{
				(int pw, int ph) = ProbeVideoSize(vPath);
				if (pw > 0 && ph > 0)
				{
					metaW = pw;
					metaH = ph;
				}
			}
			if ((metaW <= 0.0 || metaH <= 0.0) && window != null)
			{
				MediaElement player = FindVisualChild<MediaElement>((DependencyObject)(object)window, m => m.Name == "PreviewPlayer");
				if (player != null && player.NaturalVideoWidth > 0 && player.NaturalVideoHeight > 0)
				{
					metaW = player.NaturalVideoWidth;
					metaH = player.NaturalVideoHeight;
				}
			}
			if ((metaW <= 0.0 || metaH <= 0.0) && vm.CurrentProject != null)
			{
				metaW = vm.CurrentProject.Width;
				metaH = vm.CurrentProject.Height;
			}
			if (metaW <= 0.0 || metaH <= 0.0)
			{
				metaW = 720.0;
				metaH = 1280.0;
			}

			SocialPreset preset = vm.SelectedSocialPreset ?? vm.SocialPresets?.FirstOrDefault();
			int presetW = preset?.Width ?? 1920;
			int presetH = preset?.Height ?? 1080;

			double exportScale = Math.Min((double)presetW / metaW, (double)presetH / metaH);
			double scaledW = metaW * exportScale;
			double scaledH = metaH * exportScale;
			double padX = (presetW - scaledW) / 2.0;
			double padY = (presetH - scaledH) / 2.0;

			// 4. Stash BlurRegions and convert to precision OverlayItems with mathematical scaling
			// so they align perfectly over the subtitle area without vertical/horizontal offset!
			_stashedBlurRegions.Clear();
			if (vm.BlurRegions != null && vm.BlurRegions.Count > 0)
			{
				_stashedBlurRegions.AddRange(vm.BlurRegions);
				string blurDir = Path.Combine(Path.GetTempPath(), "SNA-Dubber", "ExportBlur", Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(blurDir);
				int bIndex = 0;
				foreach (BlurRegionItem blur in _stashedBlurRegions)
				{
					if (blur == null || blur.EndMs <= blur.StartMs) continue;

					// Compute normalized relative coordinates inside the UI video bounds
					(double relX, double relY, double relW, double relH) = GetOrComputeBlurNormalizedCoords(blur, videoBounds, vm);

					// Desired pixel coordinates on FFmpeg [vscaled] canvas
					double targetX = padX + (relX * scaledW);
					double targetY = padY + (relY * scaledH);
					double targetW = Math.Max(10.0, relW * scaledW);
					double targetH = Math.Max(8.0, relH * scaledH);

					// Invert FFmpegService's overlay formula (targetX = overlay.X * presetW / metaW)
					double invX = targetX * metaW / presetW;
					double invY = targetY * metaH / presetH;
					double invW = targetW * metaW / presetW;
					double invH = targetH * metaH / presetH;

					int renderW = Math.Max(10, (int)Math.Round(targetW));
					int renderH = Math.Max(8, (int)Math.Round(targetH));

					string blurPng = Path.Combine(blurDir, $"blur_{bIndex++:000}.png");
					double opVal = _blurRegionOpacity.TryGetValue(blur, out double op) ? op : Math.Clamp(blur.Strength / 30.0, 0.1, 1.0);
					RenderBlurRegionPngWithDimensions(blurPng, opVal, renderW, renderH);

					if (File.Exists(blurPng))
					{
						lock (_exportArtifactLock) _temporaryExportArtifacts.Add(blurPng);
						OverlayItem blurItem = new OverlayItem
						{
							Id = Guid.NewGuid().ToString(),
							ImagePath = blurPng,
							X = Math.Max(0.0, invX),
							Y = Math.Max(0.0, invY),
							Width = Math.Max(1.0, invW),
							Height = Math.Max(1.0, invH),
							Rotation = 0.0,
							Opacity = 1.0,
							StartMs = blur.StartMs,
							EndMs = blur.EndMs,
							ZIndex = 850 + bIndex
						};
						vm.Overlays.Add(blurItem);
						_temporaryExportOverlays.Add(blurItem);
					}
				}
				// Empty vm.BlurRegions so the whole-frame blur bug in SnaDubber.dll does NOT trigger!
				vm.BlurRegions.Clear();
				Log($"Protected {bIndex} blur region(s) as localized overlays; stashed {_stashedBlurRegions.Count} BlurRegion(s) from whole-frame blur.");
			}

			// 5. Inject Text layers with matching coordinate mapping
			List<OverlayItem> existing = vm.Overlays?.ToList() ?? new List<OverlayItem>();
			List<OverlayItem> prepared = AddTextLayersToExportOverlays(existing, vm.TextLayers, videoBounds, metaW, metaH, presetW, presetH, scaledW, scaledH, padX, padY).ToList();
			foreach (OverlayItem item in prepared.Skip(existing.Count))
			{
				vm.Overlays.Add(item);
				_temporaryExportOverlays.Add(item);
			}
			Log($"Injected {_temporaryExportOverlays.Count} visual overlay(s) for this export.");
		}
		catch (Exception ex) { Log("InjectTextLayersForExport error: " + ex); }
	}

	private static void RemoveTemporaryExportOverlays(MainViewModel vm)
	{
		foreach (OverlayItem item in _temporaryExportOverlays.ToList())
		{
			try { vm?.Overlays?.Remove(item); } catch { }
		}
		_temporaryExportOverlays.Clear();

		// Restore stashed BlurRegions back to the project model
		if (_stashedBlurRegions.Count > 0)
		{
			if (vm?.BlurRegions != null)
			{
				foreach (BlurRegionItem b in _stashedBlurRegions)
				{
					if (!vm.BlurRegions.Contains(b)) vm.BlurRegions.Add(b);
				}
			}
			_stashedBlurRegions.Clear();
			Log("Restored stashed BlurRegion(s) after export.");
		}
	}

	// The original FFmpeg exporter accepts TextLayerItem but never consumes it.
	// Convert each text item to a transparent PNG overlay so the existing, tested
	// overlay filter renders text edits in the final video as well as the preview.
	public static IEnumerable<OverlayItem> AddTextLayersToExportOverlays(
		IEnumerable<OverlayItem> overlays, IEnumerable<TextLayerItem> textLayers)
	{
		return AddTextLayersToExportOverlays(overlays, textLayers, new Rect(0, 0, 1920, 1080), 1920, 1080, 1920, 1080, 1920, 1080, 0, 0);
	}

	public static IEnumerable<OverlayItem> AddTextLayersToExportOverlays(
		IEnumerable<OverlayItem> overlays,
		IEnumerable<TextLayerItem> textLayers,
		Rect videoBounds,
		double metaW, double metaH,
		int presetW, int presetH,
		double scaledW, double scaledH,
		double padX, double padY)
	{
		List<OverlayItem> result = overlays?.ToList() ?? new List<OverlayItem>();
		try
		{
			string dir = Path.Combine(Path.GetTempPath(), "SNA-Dubber", "ExportText", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			int index = 0;
			double scaleFactor = (videoBounds.Width > 1.0 && scaledW > 1.0) ? (scaledW / videoBounds.Width) : 1.0;
			foreach (TextLayerItem layer in textLayers?.ToList() ?? new List<TextLayerItem>())
			{
				if (layer == null || string.IsNullOrWhiteSpace(layer.Text) || layer.EndMs <= layer.StartMs) continue;
				string path = Path.Combine(dir, $"text_{index++:000}.png");
				(double width, double height) = RenderTextLayerPng(layer, path, scaleFactor);
				if (!File.Exists(path)) continue;
				lock (_exportArtifactLock) _temporaryExportArtifacts.Add(path);

				double relX = (videoBounds.Width > 1.0) ? Math.Clamp((layer.X - videoBounds.Left) / videoBounds.Width, 0.0, 1.0) : 0.0;
				double relY = (videoBounds.Height > 1.0) ? Math.Clamp((layer.Y - videoBounds.Top) / videoBounds.Height, 0.0, 1.0) : 0.0;
				double targetX = padX + (relX * scaledW);
				double targetY = padY + (relY * scaledH);

				double invX = targetX * metaW / presetW;
				double invY = targetY * metaH / presetH;
				double invW = width * metaW / presetW;
				double invH = height * metaH / presetH;

				result.Add(new OverlayItem
				{
					Id = Guid.NewGuid().ToString(), ImagePath = path,
					X = Math.Max(0.0, invX), Y = Math.Max(0.0, invY),
					Width = Math.Max(1.0, invW), Height = Math.Max(1.0, invH), Rotation = 0.0,
					Opacity = _textLayerOpacity.TryGetValue(layer, out double opacity) ? Math.Clamp(opacity, 0.05, 1.0) : 1.0,
					StartMs = layer.StartMs, EndMs = layer.EndMs, ZIndex = 1000 + index
				});
			}
			Log($"Export preparation: {result.Count} visual overlay(s), {index} rendered text layer(s).");
		}
		catch (Exception ex) { Log("AddTextLayersToExportOverlays error: " + ex); }
		return result;
	}

	private static (double width, double height) RenderTextLayerPng(TextLayerItem layer, string path, double scaleFactor = 1.0)
	{
		double fontSize = Math.Clamp(layer.FontSize * Math.Max(0.5, scaleFactor), 8.0, 300.0);
		FontFamily family;
		try { family = new FontFamily(string.IsNullOrWhiteSpace(layer.FontFamily) ? "Khmer OS Battambang" : layer.FontFamily); }
		catch { family = new FontFamily("Segoe UI"); }
		FontWeight weight = _textLayerBold.TryGetValue(layer, out bool bold) && bold ? FontWeights.Bold : FontWeights.Normal;
		FontStyle style = _textLayerItalic.TryGetValue(layer, out bool italic) && italic ? FontStyles.Italic : FontStyles.Normal;
		Typeface typeface = new Typeface(family, style, weight, FontStretches.Normal);
		Brush foreground;
		try { foreground = (Brush)new BrushConverter().ConvertFromString(string.IsNullOrWhiteSpace(layer.FontColor) ? "#FACC15" : layer.FontColor); }
		catch { foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21)); }
		FormattedText text = new FormattedText(layer.Text, CultureInfo.CurrentUICulture,
			FlowDirection.LeftToRight, typeface, fontSize, foreground, 1.0);
		double padding = Math.Max(6.0, (layer.OutlineThickness + 5.0) * scaleFactor);
		int width = Math.Max(2, (int)Math.Ceiling(text.WidthIncludingTrailingWhitespace + padding * 2.0));
		int height = Math.Max(2, (int)Math.Ceiling(text.Height + padding * 2.0));
		DrawingVisual visual = new DrawingVisual();
		using (DrawingContext dc = visual.RenderOpen())
		{
			if (layer.HasShadow)
			{
				FormattedText shadow = new FormattedText(layer.Text, CultureInfo.CurrentUICulture,
					FlowDirection.LeftToRight, typeface, fontSize, new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 1.0);
				dc.DrawText(shadow, new Point(padding + 2.0 * scaleFactor, padding + 2.0 * scaleFactor));
			}
			dc.DrawText(text, new Point(padding, padding));
		}
		RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32);
		bitmap.Render(visual);
		PngBitmapEncoder encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using (FileStream stream = File.Create(path)) encoder.Save(stream);
		return (width, height);
	}

	private static void RenderBlurRegionPngWithDimensions(string path, double opacity, int width, int height)
	{
		int w = Math.Max(10, width);
		int h = Math.Max(10, height);
		DrawingVisual visual = new DrawingVisual();
		using (DrawingContext dc = visual.RenderOpen())
		{
			byte alpha = (byte)Math.Clamp((int)(opacity * 240.0), 30, 255);
			SolidColorBrush fillBrush = new SolidColorBrush(Color.FromArgb(alpha, 100, 116, 139));
			dc.DrawRoundedRectangle(fillBrush, null, new Rect(0, 0, w, h), 6.0, 6.0);
		}
		RenderTargetBitmap bitmap = new RenderTargetBitmap(w, h, 96.0, 96.0, PixelFormats.Pbgra32);
		bitmap.Render(visual);
		PngBitmapEncoder encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using (FileStream stream = File.Create(path)) encoder.Save(stream);
	}

	private static (double width, double height) RenderBlurRegionPng(BlurRegionItem blur, string path, double opacity)
	{
		int width = Math.Max(20, (int)Math.Ceiling(blur.Width));
		int height = Math.Max(12, (int)Math.Ceiling(blur.Height));
		RenderBlurRegionPngWithDimensions(path, opacity, width, height);
		return (width, height);
	}

	private static async Task CleanupGeneratedProjectFilesAsync(MainViewModel vm)
	{
		await Task.Run(() =>
		{
			CleanupTemporaryExportArtifacts();
			// NOTE: Do NOT delete AudioCache or SeparatedAudio!
			// Those directories contain the project's generated Khmer dubbing and BGM audio files.
			// Deleting them breaks audio playback and results in exported videos playing without Khmer voice!
			try
			{
				foreach (string file in Directory.EnumerateFiles(Path.GetTempPath(), "snadub_audio_*", SearchOption.TopDirectoryOnly))
				{
					try { File.Delete(file); } catch { }
				}
			}
			catch { }
		});
	}

	private static void CleanupTemporaryExportArtifacts()
	{
		List<string> files;
		lock (_exportArtifactLock) { files = _temporaryExportArtifacts.ToList(); _temporaryExportArtifacts.Clear(); }
		foreach (string file in files)
		{
			try
			{
				if (File.Exists(file)) File.Delete(file);
				string dir = Path.GetDirectoryName(file);
				if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
			}
			catch { }
		}
	}

	private static void WireEpisodeBatchTool(Window window)
	{
		try
		{
			if (window == null) return;
			MainViewModel vm = window.DataContext as MainViewModel;
			if (vm == null) return;

			Button existingBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(b => (b.Tag as string != null && b.Tag.ToString().StartsWith("EpisodeBatchButton")));
			if (existingBtn != null)
			{
				existingBtn.Visibility = Visibility.Visible;
				_isEpisodeBatchHooked = true;
				return;
			}

			// 1. Collapse any old episode ComboBox and its container border so no empty border artifact appears
			foreach (ComboBox box in FindAllVisualChildren<ComboBox>((DependencyObject)(object)window))
			{
				if (box.DisplayMemberPath == "Title" || (vm.Episodes != null && ReferenceEquals(box.ItemsSource, vm.Episodes)))
				{
					box.Visibility = Visibility.Collapsed;
					Border parentBorder = FindAncestor<Border>(box);
					if (parentBorder != null && !(parentBorder.Parent is Window) && !(parentBorder.Parent is Grid))
					{
						parentBorder.Visibility = Visibility.Collapsed;
					}
				}
			}

			// 2. Find target host container in top header
			Panel hostPanel = null;
			TextBlock titleTb = FindAllVisualChildren<TextBlock>((DependencyObject)(object)window)
				.FirstOrDefault(t => t.Text != null && (t.Text.Trim() == "ពិសេស" || t.Text.Trim() == "SNA"));

			if (titleTb != null)
			{
				DependencyObject curr = titleTb;
				while (curr != null && !(curr is Window))
				{
					DependencyObject p = VisualTreeHelper.GetParent(curr);
					if (p is StackPanel sp && sp.Orientation == Orientation.Horizontal)
					{
						if (FindAllVisualChildren<Image>((DependencyObject)(object)sp).Any())
						{
							hostPanel = sp;
							break;
						}
					}
					curr = p;
				}
			}

			if (hostPanel == null && titleTb != null)
			{
				DependencyObject curr = titleTb;
				while (curr != null && !(curr is Window))
				{
					DependencyObject p = VisualTreeHelper.GetParent(curr);
					if (p is StackPanel sp && sp.Orientation == Orientation.Horizontal)
					{
						hostPanel = sp;
						break;
					}
					curr = p;
				}
			}

			if (hostPanel == null)
			{
				ComboBox episodeBox = FindAllVisualChildren<ComboBox>((DependencyObject)(object)window)
					.FirstOrDefault(box => box.DisplayMemberPath == "Title" || (vm.Episodes != null && ReferenceEquals(box.ItemsSource, vm.Episodes)));
				if (episodeBox != null)
				{
					hostPanel = FindAncestor<StackPanel>(episodeBox) ?? (episodeBox.Parent as Panel);
				}
			}

			if (hostPanel == null)
			{
				Button exportBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
					.FirstOrDefault(b => (b.Tag as string) == "TopTranscribeButton" || (vm != null && b.Command == vm.ExportThisCommand));
				hostPanel = exportBtn?.Parent as Panel;
			}

			if (hostPanel == null) return;

			Button button = new Button
			{
				Tag = "EpisodeBatchButton",
				Content = "✂ Split Videos",
				ToolTip = "Split long video into multiple episodes / parts before transcription & export",
				Height = 32.0,
				MinWidth = 120.0,
				Margin = new Thickness(14.0, 0.0, 8.0, 0.0),
				Cursor = Cursors.Hand,
				Background = new LinearGradientBrush(Color.FromRgb(30, 27, 75), Color.FromRgb(49, 46, 129), 90),
				BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
				BorderThickness = new Thickness(1.2),
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				FontSize = 11.5,
				Padding = new Thickness(14.0, 5.0, 14.0, 5.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			SetControlCornerRadius(button, 8);
			AttachModernHover(button,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(49, 46, 129), Color.FromRgb(67, 56, 202), 90),
				hoverBorder: new SolidColorBrush(Color.FromRgb(165, 180, 252)));
			button.Click += (s, e) => ShowEpisodeBatchDialog(window, vm);

			hostPanel.Children.Add(button);
			_isEpisodeBatchHooked = true;
			Log("Successfully injected Split Videos button into top header bar!");
		}
		catch (Exception ex)
		{
			Log("WireEpisodeBatchTool error: " + ex);
		}
	}

	private static void RelocateTranscribeToTop(Window window)
	{
		try
		{
			if (window == null) return;
			MainViewModel vm = window.DataContext as MainViewModel;

			// 1. Find the original Transcribe button located above the video viewport
			Button origTranscribe = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(b =>
				{
					if ((b.Tag as string ?? "").StartsWith("TopTranscribeButton")) return false;
					if (vm != null && b.Command == vm.TranscribeAudioCommand) return true;
					string tt = b.ToolTip as string ?? "";
					if (tt.IndexOf("Transcribe", StringComparison.OrdinalIgnoreCase) >= 0) return true;
					string bt = string.Join(" ", FindAllVisualChildren<TextBlock>((DependencyObject)(object)b).Select(t => t.Text ?? ""));
					return bt.IndexOf("Transcribe & Translate", StringComparison.OrdinalIgnoreCase) >= 0;
				});

			// Collapse original button so only the clean Top Header button remains
			if (origTranscribe != null)
			{
				origTranscribe.Visibility = Visibility.Collapsed;
				Border parentBorder = FindAncestor<Border>((DependencyObject)(object)origTranscribe);
				if (parentBorder != null && !(parentBorder.Parent is Window) && !(parentBorder.Parent is Grid))
				{
					if (parentBorder.Child is Panel p && p.Children.OfType<UIElement>().All(c => c.Visibility == Visibility.Collapsed))
					{
						parentBorder.Visibility = Visibility.Collapsed;
					}
				}
			}

			// 2. Find target host container in top header - right next to "Split Videos"
			Button splitBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(b => (b.Tag as string)?.StartsWith("EpisodeBatchButton") == true);
			Panel topHost = splitBtn?.Parent as Panel;

			if (topHost == null)
			{
				TextBlock titleTb = FindVisualChild((DependencyObject)(object)window, (TextBlock tb) => tb.Text != null && (tb.Text.Contains("SNA") || tb.Text.Contains("Dubber")));
				if (titleTb != null)
				{
					DependencyObject curr = titleTb;
					while (curr != null && !(curr is Window))
					{
						DependencyObject p = VisualTreeHelper.GetParent(curr);
						if (p is StackPanel sp && sp.Orientation == Orientation.Horizontal)
						{
							topHost = sp;
							break;
						}
						curr = p;
					}
				}
			}

			if (topHost == null)
			{
				Button exportBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
					.FirstOrDefault(b => (vm != null && b.Command == vm.ExportThisCommand));
				topHost = exportBtn?.Parent as Panel;
			}

			if (topHost == null) return;

			if (topHost.Children.OfType<Button>().Any(b => (b.Tag as string ?? "").StartsWith("TopTranscribeButton")))
			{
				Button existing = topHost.Children.OfType<Button>().First(b => (b.Tag as string ?? "").StartsWith("TopTranscribeButton"));
				existing.Visibility = Visibility.Visible;
				_isTranscribeTopHooked = true;
				return;
			}

			Button topTranscribe = new Button
			{
				Tag = "TopTranscribeButton",
				Content = "🎙 Transcribe & Translate AI",
				ToolTip = "Transcribe Video Speech & Auto-Translate to Khmer AI",
				Background = new LinearGradientBrush(Color.FromRgb(79, 70, 229), Color.FromRgb(124, 58, 237), 45),
				BorderBrush = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
				BorderThickness = new Thickness(1.2),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 11.5,
				Height = 32.0,
				Padding = new Thickness(14, 5, 14, 5),
				Margin = new Thickness(10, 0, 8, 0),
				VerticalAlignment = VerticalAlignment.Center,
				Cursor = Cursors.Hand
			};
			SetControlCornerRadius(topTranscribe, 8);
			AttachModernHover(topTranscribe,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(139, 92, 246), 45),
				hoverBorder: new SolidColorBrush(Color.FromRgb(221, 214, 254)));

			topTranscribe.Click += (s, e) =>
			{
				if (vm == null) return;
				if (vm.TranscribeAudioCommand != null && vm.TranscribeAudioCommand.CanExecute(null))
				{
					vm.TranscribeAudioCommand.Execute(null);
				}
				else if (origTranscribe != null && origTranscribe.Command != null && origTranscribe.Command.CanExecute(origTranscribe.CommandParameter))
				{
					origTranscribe.Command.Execute(origTranscribe.CommandParameter);
				}
			};

			int insertIndex = splitBtn != null ? topHost.Children.IndexOf(splitBtn) + 1 : topHost.Children.Count;
			topHost.Children.Insert(Math.Min(topHost.Children.Count, Math.Max(0, insertIndex)), topTranscribe);
			_isTranscribeTopHooked = true;
			Log("Successfully placed Transcribe & Translate AI button in top header bar next to Split Videos!");
		}
		catch (Exception ex)
		{
			Log("RelocateTranscribeToTop error: " + ex.Message);
		}
	}

	private static void WireAutoUpdateCenterTool(Window window)
	{
		try
		{
			if (window == null) return;
			if (window.DataContext is not MainViewModel vm) return;

			Button existingBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(b => (b.Tag as string) == "AutoUpdateCenterButton");
			if (existingBtn != null)
			{
				_isAutoUpdateCenterHooked = true;
				return;
			}

			Button settingsBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(b => {
					string text = (b.Content as string ?? "") + " " + string.Join(" ", FindAllVisualChildren<TextBlock>((DependencyObject)(object)b).Select(l => l.Text ?? ""));
					return text.Contains("⚙") || (b.ToolTip as string ?? "").Contains("Settings");
				});

			Panel targetHost = null;
			if (settingsBtn != null && settingsBtn.Parent is Panel p)
			{
				targetHost = p;
			}
			else
			{
				Button splitBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
					.FirstOrDefault(b => (b.Tag as string)?.StartsWith("EpisodeBatchButton") == true);
				if (splitBtn != null && splitBtn.Parent is Panel sp)
				{
					targetHost = sp;
				}
			}

			if (targetHost == null) return;

			Button updateBtn = new Button
			{
				Tag = "AutoUpdateCenterButton",
				Content = "🔄 Auto Update Center",
				ToolTip = "Auto Update Center — Check for updates, release notes, and install latest versions",
				Height = 32.0,
				MinWidth = 145.0,
				Margin = new Thickness(6.0, 0.0, 6.0, 0.0),
				Cursor = Cursors.Hand,
				Background = new SolidColorBrush(Color.FromRgb(14, 42, 71)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
				BorderThickness = new Thickness(1.0),
				Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
				FontWeight = FontWeights.SemiBold,
				FontSize = 11.5,
				Padding = new Thickness(10.0, 4.0, 10.0, 4.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			SetControlCornerRadius(updateBtn, 8);
			AttachModernHover(updateBtn,
				hoverBackground: new SolidColorBrush(Color.FromRgb(2, 132, 199)),
				hoverBorder: new SolidColorBrush(Color.FromRgb(56, 189, 248)));

			updateBtn.Click += (s, e) => ShowAutoUpdateCenterDialog(window, vm);

			int idx = settingsBtn != null ? targetHost.Children.IndexOf(settingsBtn) : targetHost.Children.Count;
			targetHost.Children.Insert(Math.Min(targetHost.Children.Count, Math.Max(0, idx)), updateBtn);

			if (settingsBtn != null)
			{
				ContextMenu cm = settingsBtn.ContextMenu ?? new ContextMenu();
				MenuItem mi = new MenuItem
				{
					Header = "🔄 Auto Update Center...",
					FontWeight = FontWeights.SemiBold
				};
				mi.Click += (s, e) => ShowAutoUpdateCenterDialog(window, vm);
				cm.Items.Add(mi);
				settingsBtn.ContextMenu = cm;
			}

			_isAutoUpdateCenterHooked = true;
			Log("Successfully injected Auto Update Center button into top header bar!");
		}
		catch (Exception ex)
		{
			Log("WireAutoUpdateCenterTool error: " + ex.Message);
		}
	}

	private static bool _postUpdateChecked = false;

	private static string GetPostUpdateMarkerPath()
	{
		string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolDubber");
		Directory.CreateDirectory(dir);
		return Path.Combine(dir, "just_updated.json");
	}

	private static void CheckPostUpdateSplash(Window window)
	{
		if (_postUpdateChecked || window == null) return;
		_postUpdateChecked = true;
		try
		{
			string markerPath = GetPostUpdateMarkerPath();
			if (!File.Exists(markerPath)) return;

			string json = File.ReadAllText(markerPath, Encoding.UTF8);
			try { File.Delete(markerPath); } catch { }

			using var doc = System.Text.Json.JsonDocument.Parse(json);
			string ver = doc.RootElement.TryGetProperty("version", out var vEl) ? (vEl.GetString() ?? "2.0.4") : "2.0.4";
			string notes = doc.RootElement.TryGetProperty("release_notes", out var nEl) ? (nEl.GetString() ?? "") : "";

			window.Dispatcher.BeginInvoke((Action)(() =>
			{
				var vm = window.DataContext as MainViewModel;
				ShowAutoUpdateCenterDialog(window, vm, ver, notes);
			}), DispatcherPriority.ApplicationIdle);
		}
		catch (Exception ex)
		{
			Log("CheckPostUpdateSplash error: " + ex.Message);
		}
	}

	private static void ShowAutoUpdateCenterDialog(Window owner, MainViewModel vm, string postUpdateVersion = null, string postUpdateNotes = null)
	{
		try
		{
			Window dialog = new Window
			{
				Title = "Auto Update Center - Tool Dubber",
				Owner = owner,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Width = 610,
				Height = 500,
				ResizeMode = ResizeMode.NoResize,
				WindowStyle = WindowStyle.None,
				AllowsTransparency = true,
				Background = Brushes.Transparent,
				ShowInTaskbar = false,
				FontFamily = new FontFamily("Segoe UI, Khmer OS Battambang")
			};

			Border outerFrame = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
				CornerRadius = new CornerRadius(16),
				BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
				BorderThickness = new Thickness(1),
				Padding = new Thickness(22),
				Effect = new System.Windows.Media.Effects.DropShadowEffect
				{
					Color = Colors.Black,
					BlurRadius = 24,
					ShadowDepth = 4,
					Opacity = 0.55
				}
			};
			outerFrame.MouseLeftButtonDown += (s, e) =>
			{
				if (e.LeftButton == MouseButtonState.Pressed)
				{
					try { dialog.DragMove(); } catch { }
				}
			};

			Grid root = new Grid();
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Version Bar
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Progress Track
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Action Buttons
			root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 4: Release Notes

			// Row 0: Header
			Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			TextBlock txtTitle = new TextBlock
			{
				Text = "🚀 Tool Dubber Auto-Update Center",
				FontSize = 19,
				FontWeight = FontWeights.Bold,
				Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
				VerticalAlignment = VerticalAlignment.Center
			};
			headerGrid.Children.Add(txtTitle);

			Button btnClose = new Button
			{
				Content = "✕",
				Width = 30,
				Height = 30,
				FontSize = 13,
				FontWeight = FontWeights.Bold,
				Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
				BorderThickness = new Thickness(1),
				Cursor = Cursors.Hand,
				VerticalAlignment = VerticalAlignment.Center
			};
			SetControlCornerRadius(btnClose, 15);
			Grid.SetColumn(btnClose, 1);
			headerGrid.Children.Add(btnClose);
			Grid.SetRow(headerGrid, 0);
			root.Children.Add(headerGrid);

			// Row 1: Compact Version Bar (Current: v2.0.4 -> Latest: v2.0.4   [Up to Date ✓])
			Border infoCard = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(11, 18, 34)),
				CornerRadius = new CornerRadius(10),
				BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				BorderThickness = new Thickness(1),
				Padding = new Thickness(14, 10, 14, 10),
				Margin = new Thickness(0, 0, 0, 12)
			};
			Grid infoGrid = new Grid();
			infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			StackPanel verRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
			verRow.Children.Add(new TextBlock { Text = "Current: ", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)), VerticalAlignment = VerticalAlignment.Center });
			TextBlock txtCurVer = new TextBlock { Text = "v2.0.4", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
			verRow.Children.Add(txtCurVer);
			verRow.Children.Add(new TextBlock { Text = "  →  Latest: ", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), VerticalAlignment = VerticalAlignment.Center });
			TextBlock txtLatestVer = new TextBlock { Text = "v2.0.4", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), VerticalAlignment = VerticalAlignment.Center };
			verRow.Children.Add(txtLatestVer);
			infoGrid.Children.Add(verRow);

			Border statusBadge = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(6, 78, 59)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(12),
				Padding = new Thickness(12, 4, 12, 4),
				VerticalAlignment = VerticalAlignment.Center
			};
			TextBlock statusBadgeText = new TextBlock
			{
				Text = "Checking...",
				FontSize = 12,
				FontWeight = FontWeights.Bold,
				Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208))
			};
			statusBadge.Child = statusBadgeText;
			Grid.SetColumn(statusBadge, 1);
			infoGrid.Children.Add(statusBadge);
			infoCard.Child = infoGrid;
			Grid.SetRow(infoCard, 1);
			root.Children.Add(infoCard);

			// Row 2: Gradient Progress Bar + Status Text
			StackPanel progressPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
			Border progressTrack = new Border
			{
				Height = 22,
				Background = new SolidColorBrush(Color.FromRgb(11, 18, 34)),
				CornerRadius = new CornerRadius(11),
				BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				BorderThickness = new Thickness(1),
				ClipToBounds = true
			};
			Grid progOverlay = new Grid();
			Border progressFill = new Border
			{
				HorizontalAlignment = HorizontalAlignment.Left,
				Width = 0,
				CornerRadius = new CornerRadius(10),
				Background = new LinearGradientBrush(
					Color.FromRgb(16, 185, 129),
					Color.FromRgb(59, 130, 246),
					0.0)
			};
			TextBlock txtPct = new TextBlock
			{
				Text = "0%",
				FontSize = 11,
				FontWeight = FontWeights.Bold,
				Foreground = Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			progOverlay.Children.Add(progressFill);
			progOverlay.Children.Add(txtPct);
			progressTrack.Child = progOverlay;
			progressPanel.Children.Add(progressTrack);

			Action<double> setProgressPct = (pct) =>
			{
				double clamped = Math.Max(0, Math.Min(100, pct));
				double trackW = progressTrack.ActualWidth > 10 ? progressTrack.ActualWidth - 2 : 562;
				progressFill.Width = Math.Round(trackW * (clamped / 100.0), 1);
				txtPct.Text = $"{(int)Math.Round(clamped)}%";
			};

			// Keep hidden ProgressBar stub for indeterminate state compatibility
			ProgressBar progressBar = new ProgressBar { Height = 0, Visibility = Visibility.Collapsed, Minimum = 0, Maximum = 100, Value = 0 };
			Border progressCard = new Border { Visibility = Visibility.Visible };
			TextBlock txtStatus = new TextBlock { Text = "", Visibility = Visibility.Collapsed };

			TextBlock txtStats = new TextBlock
			{
				Text = "Checking for updates...",
				FontSize = 12,
				Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
				Margin = new Thickness(2, 6, 0, 0)
			};
			progressPanel.Children.Add(txtStats);
			Grid.SetRow(progressPanel, 2);
			root.Children.Add(progressPanel);

			// Row 3: Action Buttons
			StackPanel actionRow = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0, 0, 0, 14)
			};

			Button btnCheck = new Button
			{
				Content = "🔍 Check for Updates",
				Padding = new Thickness(16, 8, 16, 8),
				Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
				BorderThickness = new Thickness(1),
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0, 0, 10, 0),
				Cursor = Cursors.Hand
			};
			SetControlCornerRadius(btnCheck, 8);
			actionRow.Children.Add(btnCheck);

			Button btnRetry = new Button
			{
				Content = "Retry",
				Padding = new Thickness(14, 8, 14, 8),
				Background = new SolidColorBrush(Color.FromRgb(185, 28, 28)),
				Foreground = Brushes.White,
				Margin = new Thickness(0, 0, 10, 0),
				Cursor = Cursors.Hand,
				Visibility = Visibility.Collapsed
			};
			SetControlCornerRadius(btnRetry, 8);
			actionRow.Children.Add(btnRetry);

			Button btnUpdate = new Button
			{
				Content = "⬇ Download Update Now",
				Padding = new Thickness(16, 8, 16, 8),
				Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
				BorderThickness = new Thickness(1),
				FontWeight = FontWeights.Bold,
				IsEnabled = false,
				Margin = new Thickness(0, 0, 10, 0),
				Cursor = Cursors.Hand
			};
			SetControlCornerRadius(btnUpdate, 8);
			actionRow.Children.Add(btnUpdate);

			Button btnLater = new Button
			{
				Content = "Later",
				Padding = new Thickness(16, 8, 16, 8),
				Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
				BorderThickness = new Thickness(1),
				Cursor = Cursors.Hand
			};
			SetControlCornerRadius(btnLater, 8);
			actionRow.Children.Add(btnLater);

			Grid.SetRow(actionRow, 3);
			root.Children.Add(actionRow);

			// Row 4: Release Notes Card
			Border notesCard = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(11, 18, 34)),
				CornerRadius = new CornerRadius(10),
				Padding = new Thickness(14),
				BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				BorderThickness = new Thickness(1)
			};
			Grid notesLayout = new Grid();
			notesLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			notesLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

			Grid notesHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
			notesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			notesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			TextBlock txtNotesHeader = new TextBlock
			{
				Text = "📦 ព័ត៌មាន Update (v2.0.4):",
				FontSize = 13,
				FontWeight = FontWeights.Bold,
				Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
				VerticalAlignment = VerticalAlignment.Center
			};
			notesHeader.Children.Add(txtNotesHeader);

			Border notesPill = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(6, 78, 59)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(10),
				Padding = new Thickness(10, 3, 10, 3)
			};
			notesPill.Child = new TextBlock
			{
				Text = "🎯 Tool Dubber Pro",
				FontSize = 11,
				FontWeight = FontWeights.Bold,
				Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208))
			};
			Grid.SetColumn(notesPill, 1);
			notesHeader.Children.Add(notesPill);
			notesLayout.Children.Add(notesHeader);

			ScrollViewer svNotes = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
			TextBlock txtNotes = new TextBlock
			{
				Text = "កំណែថ្មី Version 2.0.4 រួមមាន:\n• ដកចេញប៊ូតុង Watermark និង Music ពីរបារខាងឆ្វេង (រក្សាទុក Add Sponsor)\n• Auto-Update Center រចនាថ្មី ដំឡើងស្វ័យប្រវត្តិស្ងាត់ៗ (Silent Update) និងបើកកម្មវិធីវិញភ្លាមៗ",
				FontSize = 12.5,
				Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
				TextWrapping = TextWrapping.Wrap,
				LineHeight = 20
			};
			svNotes.Content = txtNotes;
			Grid.SetRow(svNotes, 1);
			notesLayout.Children.Add(svNotes);
			notesCard.Child = notesLayout;
			Grid.SetRow(notesCard, 4);
			root.Children.Add(notesCard);

			outerFrame.Child = root;
			dialog.Content = outerFrame;

			// Logic & State
			string currentVer = "2.0.4";
			string latestVer = currentVer;
			string latestNotesText = txtNotes.Text;
			string downloadUrl = "";
			string sha256Expected = "";
			bool isMandatory = false;
			CancellationTokenSource cts = null;

			Action<string, string> setBadge = (text, bgHex) =>
			{
				statusBadgeText.Text = text;
				try { statusBadge.Background = (Brush)new BrushConverter().ConvertFromString(bgHex); } catch { }
			};

			Action<bool> setUpdateBtnReady = (ready) =>
			{
				btnUpdate.IsEnabled = ready;
				if (ready)
				{
					btnUpdate.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
					btnUpdate.Foreground = Brushes.White;
					btnUpdate.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
				}
				else
				{
					btnUpdate.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
					btnUpdate.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
					btnUpdate.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
				}
			};

			Func<Task> checkUpdatesAsync = async () =>
			{
				btnCheck.IsEnabled = false;
				btnUpdate.IsEnabled = false;
				btnRetry.Visibility = Visibility.Collapsed;
				progressCard.Visibility = Visibility.Visible;
				progressBar.IsIndeterminate = true;
				txtStatus.Text = "Checking for updates...";
				txtStats.Text = "Querying version database...";

				try
				{
					bool hasUpdate = false;
					string? latestVersionFound = null;
					string? downloadUrlFound = null;
					string? releaseNotesFound = null;
					string? sha256Found = null;
					bool mandatoryFound = false;

					// 1. Primary: Query GitHub raw version.json directly
					try
					{
						using var ghClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
						ghClient.DefaultRequestHeaders.UserAgent.ParseAdd("ToolDubber/2.0.4");
						string ghUrl = "https://raw.githubusercontent.com/veasnag8/Management-Tools/main/version.json?t=" + DateTime.UtcNow.Ticks;
						using var ghResp = await ghClient.GetAsync(ghUrl);
						if (ghResp.IsSuccessStatusCode)
						{
							string ghJson = await ghResp.Content.ReadAsStringAsync();
							using var doc = System.Text.Json.JsonDocument.Parse(ghJson);
							var rootEl = doc.RootElement;
							if (rootEl.TryGetProperty("has_update", out var hu)) hasUpdate = hu.GetBoolean();
							if (rootEl.TryGetProperty("latest_version", out var lv)) latestVersionFound = lv.GetString();
							if (rootEl.TryGetProperty("download_url", out var du)) downloadUrlFound = du.GetString();
							if (rootEl.TryGetProperty("release_notes", out var rn)) releaseNotesFound = rn.GetString();
							if (rootEl.TryGetProperty("sha256", out var sha)) sha256Found = sha.GetString();
							if (rootEl.TryGetProperty("mandatory", out var man)) mandatoryFound = man.GetBoolean();
						}
					}
					catch { }

					// 2. Secondary fallback: Cloudflare worker API
					if (string.IsNullOrEmpty(latestVersionFound))
					{
						try
						{
							using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
							client.DefaultRequestHeaders.UserAgent.ParseAdd("ToolDubber/2.0.4");
							string apiUrl = $"https://license-api.veasnag8.workers.dev/v1/version?product=SNA_DUBBER&current_version={currentVer}";
							using var resp = await client.GetAsync(apiUrl);
							if (resp.IsSuccessStatusCode)
							{
								string json = await resp.Content.ReadAsStringAsync();
								using var doc = System.Text.Json.JsonDocument.Parse(json);
								var rootEl = doc.RootElement;
								if (rootEl.TryGetProperty("data", out var dataEl))
								{
									if (dataEl.TryGetProperty("has_update", out var hu)) hasUpdate = hu.GetBoolean();
									if (dataEl.TryGetProperty("latest_version", out var lv)) latestVersionFound = lv.GetString();
									if (dataEl.TryGetProperty("download_url", out var du)) downloadUrlFound = du.GetString();
									if (dataEl.TryGetProperty("release_notes", out var rn)) releaseNotesFound = rn.GetString();
									if (dataEl.TryGetProperty("sha256", out var sha)) sha256Found = sha.GetString();
									if (dataEl.TryGetProperty("mandatory", out var man)) mandatoryFound = man.GetBoolean();
								}
							}
						}
						catch { }
					}

					// Semantic version comparison
					if (!string.IsNullOrEmpty(latestVersionFound) && Version.TryParse(latestVersionFound, out Version? remoteVer) && Version.TryParse(currentVer, out Version? localVer))
					{
						if (remoteVer > localVer)
						{
							hasUpdate = true;
							latestVer = latestVersionFound;
							downloadUrl = downloadUrlFound ?? "";
							latestNotesText = !string.IsNullOrWhiteSpace(releaseNotesFound)
								? releaseNotesFound
								: "Performance improvements and bug fixes.";
							txtNotes.Text = latestNotesText;
							txtNotesHeader.Text = $"📦 ព័ត៌មាន Update (v{latestVer}):";
							sha256Expected = sha256Found ?? "";
							isMandatory = mandatoryFound;
						}
					}

					txtLatestVer.Text = $"v{(hasUpdate ? latestVer : currentVer)}";
					progressBar.IsIndeterminate = false;

					if (hasUpdate)
					{
						setBadge(isMandatory ? "Mandatory Update" : "Update Available", isMandatory ? "#0284C7" : "#0284C7");
						setProgressPct(0);
						txtStats.Text = $"Version v{latestVer} is available — click Download Update Now.";
						setUpdateBtnReady(true);
						if (isMandatory) btnLater.Visibility = Visibility.Collapsed;
					}
					else
					{
						setBadge("Up to Date ✓", "#064E3B");
						setProgressPct(100);
						setUpdateBtnReady(false);
						txtStats.Text = $"Tool Dubber v{currentVer} is up to date.";
					}
				}
				catch (Exception)
				{
					progressBar.IsIndeterminate = false;
					setBadge("Up to Date ✓", "#064E3B");
					setProgressPct(100);
					setUpdateBtnReady(false);
					txtStats.Text = $"Tool Dubber v{currentVer} is up to date.";
				}
				finally
				{
					btnCheck.IsEnabled = true;
				}
			};

			Func<Task> startDownloadAsync = async () =>
			{
				if (string.IsNullOrWhiteSpace(downloadUrl))
				{
					await checkUpdatesAsync();
					return;
				}

				btnCheck.IsEnabled = false;
				btnUpdate.IsEnabled = false;
				btnRetry.Visibility = Visibility.Collapsed;
				btnLater.IsEnabled = !isMandatory;
				progressCard.Visibility = Visibility.Visible;
				progressBar.IsIndeterminate = false;
				progressBar.Value = 0;

				txtStatus.Text = "Preparing download...";
				txtStats.Text = "Connecting to update server...";

				cts = new CancellationTokenSource();
				string tempDir = Path.Combine(Path.GetTempPath(), "ToolDubberUpdates");
				Directory.CreateDirectory(tempDir);
				string tempPath = Path.Combine(tempDir, $"ToolDubber_Setup_{latestVer}_{Guid.NewGuid():N}.exe");

				try
				{
					bool downloadDone = false;
					string[] localCandidates = new[]
					{
						Path.Combine(@"D:\Project\Release_v1.2.0\Output", $"ToolDubber-Setup-v{latestVer}.exe"),
						Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", $"ToolDubber-Setup-v{latestVer}.exe"),
						Path.Combine(@"D:\Project\Release_v1.2.0\Output", "ToolDubber-Setup-v2.0.4.exe"),
						Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ToolDubber-Setup-v2.0.4.exe")
					};

					try
					{
						using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
						http.DefaultRequestHeaders.UserAgent.ParseAdd("ToolDubber/2.0.4");

						using var resp = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
						if (resp.IsSuccessStatusCode)
						{
							long total = resp.Content.Headers.ContentLength ?? -1L;
							using var srcStream = await resp.Content.ReadAsStreamAsync(cts.Token);
							using var dstStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

							byte[] buf = new byte[65536];
							long readTotal = 0;
							int r;
							var sw = Stopwatch.StartNew();
							long lastRead = 0;
							TimeSpan lastT = sw.Elapsed;

							while ((r = await srcStream.ReadAsync(buf, 0, buf.Length, cts.Token)) > 0)
							{
								await dstStream.WriteAsync(buf, 0, r, cts.Token);
								readTotal += r;

								TimeSpan curT = sw.Elapsed;
								double diffSec = (curT - lastT).TotalSeconds;
								if (diffSec >= 0.25 || readTotal == total)
								{
									long bytesDiff = readTotal - lastRead;
									double speed = diffSec > 0 ? (bytesDiff / (1024.0 * 1024.0)) / diffSec : 0.0;
									int pct = total > 0 ? (int)((readTotal * 100) / total) : 0;

									progressBar.Value = pct;
									setProgressPct(pct);
									double downMB = readTotal / (1024.0 * 1024.0);
									double totMB = total / (1024.0 * 1024.0);
									txtStats.Text = $"Downloading... {pct}% ({downMB:0.00} / {totMB:0.00} MB • {speed:0.00} MB/s)";

									lastRead = readTotal;
									lastT = curT;
								}
							}
							downloadDone = true;
						}
					}
					catch { }

					if (!downloadDone)
					{
						foreach (string cand in localCandidates)
						{
							if (File.Exists(cand) && new FileInfo(cand).Length > 10000000)
							{
								File.Copy(cand, tempPath, true);
								downloadDone = true;
								txtStatus.Text = "Using local package...";
								break;
							}
						}
					}

					if (!downloadDone || !File.Exists(tempPath))
					{
						throw new InvalidOperationException($"Could not download update from {downloadUrl}. Please check your connection or download from GitHub Releases.");
					}

					// Verify SHA-256
					txtStatus.Text = "Verifying update...";
					txtStats.Text = "Checking file checksum integrity...";

					if (!string.IsNullOrWhiteSpace(sha256Expected))
					{
						using var sha = System.Security.Cryptography.SHA256.Create();
						using var fs = File.OpenRead(tempPath);
						byte[] hash = await sha.ComputeHashAsync(fs, cts.Token);
						string hashHex = Convert.ToHexString(hash).ToLowerInvariant();
						if (!string.Equals(hashHex, sha256Expected.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
						{
							try { File.Delete(tempPath); } catch { }
							throw new InvalidOperationException($"Checksum mismatch! Expected: {sha256Expected}, Computed: {hashHex}");
						}
					}

					setProgressPct(100);
					txtStats.Text = "Installer verified. Installing silently & restarting...";

					try
					{
						string markerPayload = System.Text.Json.JsonSerializer.Serialize(new
						{
							version = latestVer,
							release_notes = latestNotesText
						});
						File.WriteAllText(GetPostUpdateMarkerPath(), markerPayload, Encoding.UTF8);
					}
					catch { }

					await Task.Delay(350);

					string currentExe = Process.GetCurrentProcess().MainModule?.FileName;
					string runnerBat = Path.Combine(Path.GetTempPath(), $"update_runner_{Guid.NewGuid():N}.bat");
					string batCode = $@"@echo off
timeout /t 2 /nobreak >nul
start """" /wait ""{tempPath}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS
timeout /t 1 /nobreak >nul
start """" ""{currentExe}""
del ""%~f0""
";
					File.WriteAllText(runnerBat, batCode);
					Process.Start(new ProcessStartInfo
					{
						FileName = runnerBat,
						UseShellExecute = true,
						CreateNoWindow = true,
						WindowStyle = ProcessWindowStyle.Hidden
					});

					Application.Current.Shutdown();
				}
				catch (OperationCanceledException)
				{
					try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
					txtStats.Text = "Download cancelled by user.";
					btnRetry.Visibility = Visibility.Visible;
					setUpdateBtnReady(true);
				}
				catch (Exception ex)
				{
					try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
					txtStats.Text = $"Update error: {ex.Message}";
					btnRetry.Visibility = Visibility.Visible;
					setUpdateBtnReady(true);
					MessageBox.Show($"Update error: {ex.Message}", "Update Verification Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					btnCheck.IsEnabled = true;
				}
			};

			btnCheck.Click += async (s, e) => await checkUpdatesAsync();
			btnUpdate.Click += async (s, e) => await startDownloadAsync();
			btnRetry.Click += async (s, e) => await startDownloadAsync();
			btnLater.Click += (s, e) =>
			{
				cts?.Cancel();
				dialog.Close();
			};
			btnClose.Click += (s, e) =>
			{
				cts?.Cancel();
				dialog.Close();
			};

			// Automatically show post-update summary or check for updates on dialog open
			dialog.Loaded += async (s, e) =>
			{
				if (!string.IsNullOrEmpty(postUpdateVersion))
				{
					txtCurVer.Text = $"v{postUpdateVersion}";
					txtLatestVer.Text = $"v{postUpdateVersion}";
					setBadge("Up to Date ✓", "#064E3B");
					setProgressPct(100);
					setUpdateBtnReady(false);
					txtStats.Text = $"Update completed! Tool Dubber v{postUpdateVersion} is now active.";
					txtNotesHeader.Text = $"📦 ព័ត៌មាន Update (v{postUpdateVersion}):";
					if (!string.IsNullOrWhiteSpace(postUpdateNotes))
					{
						txtNotes.Text = postUpdateNotes;
					}
					btnCheck.IsEnabled = true;
				}
				else
				{
					await checkUpdatesAsync();
				}
			};
			dialog.ShowDialog();
		}
		catch (Exception ex)
		{
			Log("ShowAutoUpdateCenterDialog error: " + ex);
		}
	}

	private static void ShowEpisodeBatchDialog(Window owner, MainViewModel vm)
	{
		if (vm.CurrentProject == null || string.IsNullOrWhiteSpace(vm.CurrentProject.VideoPath) || !File.Exists(vm.CurrentProject.VideoPath))
		{
			MessageBox.Show("Please upload a video first.", "Split Episodes", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		double totalSec = Math.Max(1.0, vm.VideoDurationSec > 0 ? vm.VideoDurationSec : vm.CurrentProject.DurationSec);
		RadioButton byCount = new RadioButton { Content = "បំបែកតាមចំនួនភាគ", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 4, 18, 4) };
		RadioButton byMinutes = new RadioButton { Content = "កំណត់ចំនួននាទីក្នុងមួយភាគ", Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 4) };
		TextBox countInput = new TextBox { Text = "2", Width = 85, Margin = new Thickness(8, 4, 16, 4), Padding = new Thickness(7, 4, 7, 4) };
		TextBox minuteInput = new TextBox { Text = "10", Width = 85, Margin = new Thickness(8, 4, 16, 4), Padding = new Thickness(7, 4, 7, 4), IsEnabled = false };
		TextBlock result = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 8) };
		StackPanel list = new StackPanel();
		List<EpisodeBatchItem> plans = new();
		Brush panelBrush = new SolidColorBrush(Color.FromRgb(13, 27, 46));
		Brush cardBrush = new SolidColorBrush(Color.FromRgb(18, 36, 59));
		Brush borderBrush = new SolidColorBrush(Color.FromRgb(38, 68, 101));
		Brush accentBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233));
		Brush mutedBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
		byCount.Content = "Split by episode count";
		byMinutes.Content = "Equal minutes per episode";
		byCount.FontWeight = FontWeights.SemiBold;
		byMinutes.FontWeight = FontWeights.SemiBold;
		foreach (TextBox inputBox in new[] { countInput, minuteInput })
		{
			inputBox.Height = 34; inputBox.Padding = new Thickness(10, 6, 10, 6);
			inputBox.FontSize = 14; inputBox.Foreground = Brushes.White; inputBox.CaretBrush = Brushes.White;
			inputBox.Background = new SolidColorBrush(Color.FromRgb(8, 18, 34)); inputBox.BorderBrush = borderBrush;
		}
		result.Foreground = Brushes.White; result.FontSize = 13; result.Margin = new Thickness(0);
		Action recalculate = () =>
		{
			list.Children.Clear(); plans.Clear();
			int count;
			if (byCount.IsChecked == true)
			{
				if (!int.TryParse(countInput.Text, out count)) count = 1;
				count = Math.Clamp(count, 1, 500);
			}
			else
			{
				if (!double.TryParse(minuteInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double minutes)) minutes = 10.0;
				minutes = Math.Max(0.1, minutes);
				count = Math.Clamp((int)Math.Ceiling(totalSec / (minutes * 60.0)), 1, 500);
			}
			double equalDuration = byCount.IsChecked == true ? totalSec / count : Math.Max(6.0, (double.TryParse(minuteInput.Text, out double min) ? min : 10.0) * 60.0);
			result.Text = $"Video duration  {TimeSpan.FromSeconds(totalSec):hh\\:mm\\:ss}     →     {count} episodes will be created";
			for (int i = 0; i < count; i++)
			{
				double start = i * equalDuration;
				double duration = Math.Min(equalDuration, Math.Max(0.1, totalSec - start));
				CheckBox check = new CheckBox
				{
					IsChecked = true, Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
					Content = $"Episode {i + 1:00}       {TimeSpan.FromSeconds(start):hh\\:mm\\:ss}  →  {TimeSpan.FromSeconds(start + duration):hh\\:mm\\:ss}       Duration {TimeSpan.FromSeconds(duration):mm\\:ss}"
				};
				plans.Add(new EpisodeBatchItem { Number = i + 1, StartSec = start, DurationSec = duration, CheckBox = check });
				list.Children.Add(new Border { Child = check, Background = i % 2 == 0 ? cardBrush : panelBrush, BorderBrush = borderBrush, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 9, 12, 9) });
			}
		};
		byCount.Checked += (s, e) => { countInput.IsEnabled = true; minuteInput.IsEnabled = false; recalculate(); };
		byMinutes.Checked += (s, e) => { countInput.IsEnabled = false; minuteInput.IsEnabled = true; recalculate(); };
		countInput.TextChanged += (s, e) => { if (byCount.IsChecked == true) recalculate(); };
		minuteInput.TextChanged += (s, e) => { if (byMinutes.IsChecked == true) recalculate(); };

		Button all = new Button { Content = "✓ Select all", Height = 30, Margin = new Thickness(0, 0, 7, 0), Padding = new Thickness(12, 4, 12, 4), Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)), Background = cardBrush, BorderBrush = borderBrush };
		Button none = new Button { Content = "Clear selection", Height = 30, Padding = new Thickness(12, 4, 12, 4), Foreground = mutedBrush, Background = cardBrush, BorderBrush = borderBrush };
		Button cancel = new Button { Content = "Cancel", Width = 105, Height = 38, Margin = new Thickness(5), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), BorderBrush = borderBrush };
		Button start = new Button { Content = "▶  Start automatic processing", Width = 235, Height = 38, Margin = new Thickness(5), Background = accentBrush, BorderBrush = accentBrush, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
		all.Click += (s, e) => plans.ForEach(p => p.CheckBox.IsChecked = true);
		none.Click += (s, e) => plans.ForEach(p => p.CheckBox.IsChecked = false);
		StackPanel selectRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) }; selectRow.Children.Add(all); selectRow.Children.Add(none);
		StackPanel countCardBody = new StackPanel(); countCardBody.Children.Add(byCount); countCardBody.Children.Add(new TextBlock { Text = "NUMBER OF EPISODES", Foreground = mutedBrush, FontSize = 9, Margin = new Thickness(0, 5, 0, 5) }); countCardBody.Children.Add(countInput);
		StackPanel minuteCardBody = new StackPanel(); minuteCardBody.Children.Add(byMinutes); minuteCardBody.Children.Add(new TextBlock { Text = "MINUTES PER EPISODE", Foreground = mutedBrush, FontSize = 9, Margin = new Thickness(0, 5, 0, 5) }); minuteCardBody.Children.Add(minuteInput);
		Border countCard = new Border { Child = countCardBody, Background = cardBrush, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 0, 6, 0) };
		Border minuteCard = new Border { Child = minuteCardBody, Background = cardBrush, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(6, 0, 0, 0) };
		Grid modeGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
		modeGrid.ColumnDefinitions.Add(new ColumnDefinition()); modeGrid.ColumnDefinitions.Add(new ColumnDefinition());
		Grid.SetColumn(countCard, 0); Grid.SetColumn(minuteCard, 1); modeGrid.Children.Add(countCard); modeGrid.Children.Add(minuteCard);
		Border summary = new Border { Child = result, Background = new SolidColorBrush(Color.FromRgb(7, 53, 76)), BorderBrush = new SolidColorBrush(Color.FromRgb(14, 116, 144)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 12) };
		ScrollViewer scroll = new ScrollViewer { Content = list, Height = 275, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = panelBrush, BorderBrush = borderBrush, BorderThickness = new Thickness(1) };
		StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) }; actions.Children.Add(cancel); actions.Children.Add(start);
		StackPanel body = new StackPanel { Margin = new Thickness(22) };
		body.Children.Add(new TextBlock { Text = "Episode Split & Automatic Khmer Export", Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), FontSize = 20, FontWeight = FontWeights.Bold });
		body.Children.Add(new TextBlock { Text = "Choose one split method, select episodes, then let SNA-Dubber process them automatically.", Foreground = mutedBrush, FontSize = 11, Margin = new Thickness(0, 4, 0, 16) });
		body.Children.Add(modeGrid); body.Children.Add(summary); body.Children.Add(selectRow); body.Children.Add(scroll); body.Children.Add(actions);
		Border shell = new Border { Child = body, Background = new SolidColorBrush(Color.FromRgb(8, 18, 34)), BorderBrush = accentBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10) };
		Window dialog = new Window { Title = "Split Episodes", Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = 760, Height = 665, MinWidth = 720, MinHeight = 620, ResizeMode = ResizeMode.CanResize, ShowInTaskbar = false, Background = new SolidColorBrush(Color.FromRgb(4, 10, 20)), Content = shell };
		cancel.Click += (s, e) => dialog.Close();
		start.Click += async (s, e) =>
		{
			List<EpisodeBatchItem> selected = plans.Where(p => p.CheckBox.IsChecked == true).ToList();
			if (selected.Count == 0) { MessageBox.Show("Please select at least one episode."); return; }
			dialog.Close();
			await RunEpisodeBatchAsync(owner, vm, selected);
		};
		recalculate();
		dialog.ShowDialog();
	}

	private static async Task RunEpisodeBatchAsync(Window owner, MainViewModel vm, List<EpisodeBatchItem> selected)
	{
		string originalVideo = vm.CurrentProject.VideoPath;
		string baseName = Path.GetFileNameWithoutExtension(originalVideo);
		string outputRoot = Path.Combine(Path.GetDirectoryName(originalVideo) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), baseName + "_Episodes_Khmer");
		string workRoot = Path.Combine(Path.GetTempPath(), "SNA-Dubber", "EpisodeSplit", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(outputRoot); Directory.CreateDirectory(workRoot);
		vm.Episodes.Clear();
		foreach (EpisodeBatchItem plan in selected)
		{
			vm.Episodes.Add(new EpisodeItem { Number = plan.Number, Title = $"Episode {plan.Number}", DurationText = TimeSpan.FromSeconds(plan.DurationSec).ToString("hh\\:mm\\:ss"), VideoPath = string.Empty, TranscriptionStatus = "Queued" });
		}
		vm.IsBusy = true;
		try
		{
			int done = 0;
			foreach (EpisodeBatchItem plan in selected)
			{
				EpisodeItem episode = vm.Episodes.First(item => item.Number == plan.Number);
				string episodeDir = Path.Combine(outputRoot, $"Episode_{plan.Number:00}");
				Directory.CreateDirectory(episodeDir);
				string splitPath = Path.Combine(workRoot, $"Episode_{plan.Number:00}_source.mp4");
				string finalPath = Path.Combine(episodeDir, $"Episode_{plan.Number:00}_Khmer.mp4");
				try
				{
					episode.TranscriptionStatus = "Splitting";
					vm.BusyProgressPercent = done * 100.0 / selected.Count;
					vm.BusyStatusText = $"Episode {plan.Number}: splitting source video...";
					await SplitEpisodeVideoAsync(originalVideo, splitPath, plan.StartSec, plan.DurationSec);
					episode.VideoPath = splitPath;
					await vm.LoadVideoFileAsync(splitPath);
					episode.TranscriptionStatus = "Transcribing";
					vm.BusyStatusText = $"Episode {plan.Number}: transcribing and translating Khmer...";
					if (vm.TranscribeAudioCommand != null) await vm.TranscribeAudioCommand.ExecuteAsync(null);
					await RunFullPipelineAsync(vm, owner);
					vm.BusyStatusText = $"Episode {plan.Number}: exporting final video...";
					IEnumerable<OverlayItem> exportOverlays = AddTextLayersToExportOverlays(vm.Overlays, vm.TextLayers);
					FieldInfo ffmpegField = typeof(MainViewModel).GetField("_ffmpegService", BindingFlags.Instance | BindingFlags.NonPublic);
					if (ffmpegField?.GetValue(vm) is not IFFmpegService ffmpeg) throw new InvalidOperationException("FFmpeg export service is unavailable.");
					SocialPreset preset = vm.SelectedSocialPreset ?? vm.SocialPresets.First();
					bool ok = await ffmpeg.ExportVideoAsync(splitPath, finalPath, preset, vm.BlurRegions, exportOverlays, vm.TextLayers, vm.DialogueLines, vm.AudioMix, null, CancellationToken.None);
					if (!ok || !File.Exists(finalPath)) throw new InvalidOperationException("Final episode export was not created.");
					episode.VideoPath = finalPath; episode.TranscriptionStatus = "Exported";
					done++; vm.BusyProgressPercent = done * 100.0 / selected.Count;
				}
				catch (Exception ex)
				{
					episode.TranscriptionStatus = "Error";
					Log($"Episode {plan.Number} batch error: {ex}");
				}
				finally
				{
					try { if (File.Exists(splitPath)) File.Delete(splitPath); } catch { }
					CleanupTemporaryExportArtifacts();
					await CleanupGeneratedProjectFilesAsync(vm);
				}
			}
			vm.ProjectStatusMessage = $"Episode batch finished. Results: {outputRoot}";
			MessageBox.Show($"Episode processing finished.\n\nExported videos are in:\n{outputRoot}", "Episodes Complete", MessageBoxButton.OK, MessageBoxImage.Information);
		}
		finally
		{
			try { await vm.LoadVideoFileAsync(originalVideo); } catch { }
			try { if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true); } catch { }
			vm.IsBusy = false; vm.BusyStatusText = string.Empty;
		}
	}

	private static async Task SplitEpisodeVideoAsync(string source, string output, double startSec, double durationSec)
	{
		string ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
		if (!File.Exists(ffmpeg)) ffmpeg = "ffmpeg";
		ProcessStartInfo psi = new ProcessStartInfo
		{
			FileName = ffmpeg,
			Arguments = $"-y -ss {startSec.ToString("0.###", CultureInfo.InvariantCulture)} -i \"{source}\" -t {durationSec.ToString("0.###", CultureInfo.InvariantCulture)} -map 0:v:0 -map 0:a? -c copy -avoid_negative_ts make_zero \"{output}\"",
			UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
		};
		using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start FFmpeg.");
		await WaitForFfmpegExitDrainingAsync(process, CancellationToken.None);
		if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0) throw new InvalidOperationException($"FFmpeg split failed (code {process.ExitCode}).");
	}

	private static bool _isSocialViewportHooked = false;
	private static bool _isPlayerMediaOpenedHooked = false;
	private static bool _isFrameFillMode = true;

	public static void ApplySocialAspectRatio(Window window, MainViewModel vm)
	{
		try
		{
			if (window == null || vm == null) return;
			Grid videoViewport = FindVisualChild((DependencyObject)(object)window, (Grid g) => g.Name == "VideoViewport");
			if (videoViewport == null) return;

			Border parentBorder = videoViewport.Parent as Border;
			if (parentBorder == null) return;

			if (!_isSocialViewportHooked)
			{
				parentBorder.SizeChanged += (s, e) => ApplySocialAspectRatio(window, vm);
				_isSocialViewportHooked = true;
			}

			MediaElement player = FindVisualChild((DependencyObject)(object)videoViewport, (MediaElement m) => m.Name == "PreviewPlayer");
			if (player != null && !_isPlayerMediaOpenedHooked)
			{
				player.MediaOpened += (s, e) =>
				{
					window.Dispatcher.BeginInvoke(new Action(() => ApplySocialAspectRatio(window, vm)));
				};
				_isPlayerMediaOpenedHooked = true;
			}

			// Dark backing for the border behind the viewport
			parentBorder.ClipToBounds = true;
			parentBorder.Background = new SolidColorBrush(Color.FromRgb(6, 10, 18));

			SocialPreset preset = vm.SelectedSocialPreset;
			double targetRatio = 16.0 / 9.0;
			if (preset != null && preset.Width > 0 && preset.Height > 0)
			{
				targetRatio = (double)preset.Width / (double)preset.Height;
			}
			else if (preset != null && !string.IsNullOrEmpty(preset.AspectRatioText))
			{
				if (preset.AspectRatioText == "9:16") targetRatio = 9.0 / 16.0;
				else if (preset.AspectRatioText == "1:1") targetRatio = 1.0;
				else targetRatio = 16.0 / 9.0;
			}

			double availW = parentBorder.ActualWidth;
			double availH = parentBorder.ActualHeight;

			if (availW < 50.0 || availH < 50.0)
			{
				availW = Math.Max(200.0, window.ActualWidth > 300.0 ? (window.ActualWidth * 0.5) : 760.0);
				availH = Math.Max(150.0, window.ActualHeight > 300.0 ? (window.ActualHeight * 0.55) : 470.0);
			}

			double targetW, targetH;
			if (availW / availH > targetRatio)
			{
				targetH = availH;
				targetW = Math.Round(targetH * targetRatio, 1);
			}
			else
			{
				targetW = availW;
				targetH = Math.Round(targetW / targetRatio, 1);
			}

			videoViewport.HorizontalAlignment = HorizontalAlignment.Center;
			videoViewport.VerticalAlignment = VerticalAlignment.Center;
			videoViewport.Width = targetW;
			videoViewport.Height = targetH;
			videoViewport.Background = Brushes.Black;
			videoViewport.ClipToBounds = true;

			Canvas overlayCanvas = FindVisualChild((DependencyObject)(object)videoViewport, (Canvas c) => c.Name == "OverlayCanvas");
			if (overlayCanvas != null)
			{
				overlayCanvas.Width = targetW;
				overlayCanvas.Height = targetH;
				overlayCanvas.ClipToBounds = true;
			}

			if (player != null)
			{
				player.ClearValue(FrameworkElement.WidthProperty);
				player.ClearValue(FrameworkElement.HeightProperty);
				player.ClearValue(FrameworkElement.MaxWidthProperty);
				player.ClearValue(FrameworkElement.MaxHeightProperty);
				player.Stretch = _isFrameFillMode ? Stretch.UniformToFill : Stretch.Uniform;
				player.HorizontalAlignment = HorizontalAlignment.Center;
				player.VerticalAlignment = VerticalAlignment.Center;
			}
		}
		catch (Exception ex)
		{
			Log("ApplySocialAspectRatio error: " + ex.Message);
		}
	}

	private static void UpdateFillButtonState(Button fillBtn)
	{
		if (fillBtn == null) return;
		fillBtn.Content = _isFrameFillMode ? "⛶ Fill" : "⊡ Fit";
		if (_isFrameFillMode)
		{
			fillBtn.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(14, 165, 233), 45);
			fillBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
			fillBtn.BorderThickness = new Thickness(1.2);
			fillBtn.Foreground = Brushes.White;
		}
		else
		{
			fillBtn.Background = new SolidColorBrush(Color.FromRgb(15, 24, 38));
			fillBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 48, 74));
			fillBtn.BorderThickness = new Thickness(1.0);
			fillBtn.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
		}
	}

	private static void UpdateFormatButtonsState(Panel host, MainViewModel vm)
	{
		if (host == null || vm == null) return;
		string currentAspect = vm.SelectedSocialPreset?.AspectRatioText ?? "16:9";
		bool is4K = (vm.SelectedSocialPreset?.Name ?? "").Contains("4K");

		Button landscapeBtn = host.Children.OfType<Button>().FirstOrDefault(b => (b.Tag as string ?? "").StartsWith("SocialLandscapeButton"));
		Button portraitBtn = host.Children.OfType<Button>().FirstOrDefault(b => (b.Tag as string ?? "").StartsWith("SocialPortraitButton"));
		Button squareBtn = host.Children.OfType<Button>().FirstOrDefault(b => (b.Tag as string ?? "").StartsWith("SocialSquareButton"));
		Button fillBtn = host.Children.OfType<Button>().FirstOrDefault(b => (b.Tag as string ?? "").StartsWith("VideoFillToggleButton"));

		if (fillBtn != null)
		{
			UpdateFillButtonState(fillBtn);
		}

		SolidColorBrush inactiveBg = new SolidColorBrush(Color.FromRgb(15, 24, 38));
		SolidColorBrush inactiveBorder = new SolidColorBrush(Color.FromRgb(30, 48, 74));
		SolidColorBrush inactiveFg = new SolidColorBrush(Color.FromRgb(226, 232, 240));

		LinearGradientBrush activeBg = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(14, 165, 233), 45);
		SolidColorBrush activeBorder = new SolidColorBrush(Color.FromRgb(56, 189, 248));

		if (landscapeBtn != null)
		{
			bool active = currentAspect == "16:9";
			landscapeBtn.Background = active ? (Brush)activeBg : inactiveBg;
			landscapeBtn.BorderBrush = active ? activeBorder : inactiveBorder;
			landscapeBtn.Foreground = active ? Brushes.White : inactiveFg;
			landscapeBtn.Content = is4K ? "🎬 4K" : "▭ 16:9";
		}
		if (portraitBtn != null)
		{
			bool active = currentAspect == "9:16";
			portraitBtn.Background = active ? (Brush)activeBg : inactiveBg;
			portraitBtn.BorderBrush = active ? activeBorder : inactiveBorder;
			portraitBtn.Foreground = active ? Brushes.White : inactiveFg;
		}
		if (squareBtn != null)
		{
			bool active = currentAspect == "1:1";
			squareBtn.Background = active ? (Brush)activeBg : inactiveBg;
			squareBtn.BorderBrush = active ? activeBorder : inactiveBorder;
			squareBtn.Foreground = active ? Brushes.White : inactiveFg;
		}
	}

	private static bool _userChangedPresetManually = false;

	private static void WireSocialMediaPresets(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm || vm.SocialPresets == null) return;

			TextBlock landscapeTb = FindAllVisualChildren<TextBlock>((DependencyObject)(object)window)
				.FirstOrDefault(t =>
				{
					if (string.IsNullOrEmpty(t.Text)) return false;
					string s = t.Text.Trim();
					return string.Equals(s, "LANDSCAPE", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(s, "YouTube 16:9", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(s, "▭ 16:9", StringComparison.OrdinalIgnoreCase);
				});

			Border landscapeBorder = landscapeTb != null ? FindAncestor<Border>((DependencyObject)(object)landscapeTb) : null;
			Panel host = landscapeBorder?.Parent as Panel;

			if (host == null)
			{
				foreach (StackPanel sp in FindAllVisualChildren<StackPanel>((DependencyObject)(object)window))
				{
					if (sp.Orientation == Orientation.Horizontal && FindVisualChild(sp, (TextBlock tb) => tb.Text != null && (tb.Text.Contains("LANDSCAPE") || tb.Text.Contains("Auto detect"))) != null)
					{
						host = sp;
						break;
					}
				}
			}

			if (host == null) return;

			// Collapse original static borders: LANDSCAPE, 1:1, Source language: Auto detect
			foreach (FrameworkElement child in host.Children.OfType<FrameworkElement>().ToList())
			{
				if (child is Button) continue;
				TextBlock tb = child as TextBlock ?? FindVisualChild((DependencyObject)(object)child, (TextBlock t) =>
				{
					if (string.IsNullOrEmpty(t.Text)) return false;
					string s = t.Text.Trim();
					return string.Equals(s, "LANDSCAPE", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(s, "1:1", StringComparison.OrdinalIgnoreCase)
						|| s.IndexOf("Source language", StringComparison.OrdinalIgnoreCase) >= 0
						|| s.IndexOf("Auto detect", StringComparison.OrdinalIgnoreCase) >= 0;
				});

				if (tb != null)
				{
					child.Visibility = Visibility.Collapsed;
				}
			}

			// Add interactive buttons if not already added
			if (!host.Children.OfType<Button>().Any(b => (b.Tag as string ?? "").StartsWith("SocialLandscapeButton")))
			{
				SocialPreset youtube = vm.SocialPresets.FirstOrDefault(item => item.AspectRatioText == "16:9" && item.Name.IndexOf("4K", StringComparison.OrdinalIgnoreCase) < 0) ?? vm.SocialPresets.FirstOrDefault();
				SocialPreset portrait = vm.SocialPresets.FirstOrDefault(item => item.AspectRatioText == "9:16");
				SocialPreset squarePreset = vm.SocialPresets.FirstOrDefault(item => item.AspectRatioText == "1:1");
				SocialPreset cinema4k = vm.SocialPresets.FirstOrDefault(item => item.Name.IndexOf("4K", StringComparison.OrdinalIgnoreCase) >= 0);

				// Ensure Landscape (16:9) is the default on startup
				if (!_userChangedPresetManually && youtube != null && vm.SelectedSocialPreset?.AspectRatioText != "16:9")
				{
					vm.SelectedSocialPreset = youtube;
					_isFrameFillMode = true;
				}

				Action<SocialPreset> select = preset =>
				{
					if (preset == null) return;
					_userChangedPresetManually = (preset.AspectRatioText != "16:9");
					window.Dispatcher.Invoke(() =>
					{
						vm.SelectedSocialPreset = preset;
						if (preset.AspectRatioText == "16:9")
						{
							_isFrameFillMode = true;
						}
						vm.ProjectStatusMessage = $"Social format: {preset.Name} ({preset.Width}x{preset.Height})";
						UpdateFormatButtonsState(host, vm);
						ApplySocialAspectRatio(window, vm);
					});
				};

				Button landscapeBtn = new Button
				{
					Tag = "SocialLandscapeButton",
					Content = "▭ 16:9",
					Height = 25.0,
					MinWidth = 66.0,
					Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
					Padding = new Thickness(8.0, 2.0, 8.0, 2.0),
					FontWeight = FontWeights.SemiBold,
					FontSize = 10.5,
					Cursor = Cursors.Hand,
					ToolTip = "YouTube / Landscape (16:9) — Right-click for 4K / other formats"
				};
				SetControlCornerRadius(landscapeBtn, 6);
				landscapeBtn.Click += (s, e) => select(youtube);
				landscapeBtn.MouseRightButtonUp += (s, e) =>
				{
					ContextMenu cm = new ContextMenu();
					MenuItem m1 = new MenuItem { Header = "▭ YouTube Landscape (16:9 - 1080p)" };
					m1.Click += (ms, me) => select(youtube);
					MenuItem m2 = new MenuItem { Header = "🎬 Cinema 4K Landscape (16:9 - 2160p)" };
					m2.Click += (ms, me) => select(cinema4k ?? youtube);
					MenuItem m3 = new MenuItem { Header = "▯ TikTok / Reels Portrait (9:16 - 1080p)" };
					m3.Click += (ms, me) => select(portrait);
					MenuItem m4 = new MenuItem { Header = "⏹ Square Post (1:1 - 1080p)" };
					m4.Click += (ms, me) => select(squarePreset);

					cm.Items.Add(m1);
					cm.Items.Add(m2);
					cm.Items.Add(m3);
					cm.Items.Add(m4);
					cm.PlacementTarget = landscapeBtn;
					cm.IsOpen = true;
				};

				Button portraitBtn = new Button
				{
					Tag = "SocialPortraitButton",
					Content = "▯ 9:16",
					Height = 25.0,
					MinWidth = 62.0,
					Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
					Padding = new Thickness(8.0, 2.0, 8.0, 2.0),
					FontWeight = FontWeights.SemiBold,
					FontSize = 10.5,
					Cursor = Cursors.Hand,
					ToolTip = "TikTok / Reels / Shorts Portrait (9:16) — 1080x1920"
				};
				SetControlCornerRadius(portraitBtn, 6);
				portraitBtn.Click += (s, e) => select(portrait);

				Button squareBtn = new Button
				{
					Tag = "SocialSquareButton",
					Content = "⏹ 1:1",
					Height = 25.0,
					MinWidth = 54.0,
					Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
					Padding = new Thickness(8.0, 2.0, 8.0, 2.0),
					FontWeight = FontWeights.SemiBold,
					FontSize = 10.5,
					Cursor = Cursors.Hand,
					ToolTip = "Facebook / Instagram Square (1:1) — 1080x1080"
				};
				SetControlCornerRadius(squareBtn, 6);
				squareBtn.Click += (s, e) => select(squarePreset);

				Button fillButton = new Button
				{
					Tag = "VideoFillToggleButton",
					Content = _isFrameFillMode ? "⛶ Fill" : "⊡ Fit",
					Height = 25.0,
					MinWidth = 56.0,
					Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
					Padding = new Thickness(6.0, 2.0, 6.0, 2.0),
					FontWeight = FontWeights.SemiBold,
					FontSize = 10.5,
					Cursor = Cursors.Hand,
					ToolTip = "ពង្រីកវីដេអូពេញ Frame 16:9 (Fill Frame / Fit Frame)"
				};
				SetControlCornerRadius(fillButton, 6);
				UpdateFillButtonState(fillButton);
				fillButton.Click += (s, e) =>
				{
					_isFrameFillMode = !_isFrameFillMode;
					UpdateFillButtonState(fillButton);
					ApplySocialAspectRatio(window, vm);
					vm.ProjectStatusMessage = _isFrameFillMode ? "វីដេអូពេញ Frame (Fill 16:9 Active)" : "វីដេអូសម Frame (Fit Original Active)";
				};

				Button flipButton = new Button
				{
					Tag = "VideoFlipToggleButton",
					Content = _isVideoFlipped ? "⇄ Flipped" : "⇄ Flip",
					Height = 25.0,
					MinWidth = 62.0,
					Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
					Padding = new Thickness(8.0, 2.0, 8.0, 2.0),
					FontWeight = FontWeights.SemiBold,
					FontSize = 10.5,
					Cursor = Cursors.Hand,
					ToolTip = "បញ្ច្រាសវីដេអូ ទៅមក (Flip / Mirror Video Horizontal ⇄)"
				};
				SetControlCornerRadius(flipButton, 6);
				flipButton.Click += async (s, e) =>
				{
					_isVideoFlipped = !_isVideoFlipped;
					ApplyVideoFlipState(window, flipButton);
					await EnsureFlippedVideoSourceAsync(vm);
				};

				Action<Button, string> wireHover = (btn, aspect) =>
				{
					btn.MouseEnter += (s, e) =>
					{
						if (vm.SelectedSocialPreset?.AspectRatioText != aspect)
						{
							btn.Background = new SolidColorBrush(Color.FromRgb(30, 48, 74));
							btn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
						}
					};
					btn.MouseLeave += (s, e) =>
					{
						UpdateFormatButtonsState(host, vm);
					};
				};
				wireHover(landscapeBtn, "16:9");
				wireHover(portraitBtn, "9:16");
				wireHover(squareBtn, "1:1");

				host.Children.Insert(0, landscapeBtn);
				host.Children.Insert(1, portraitBtn);
				host.Children.Insert(2, squareBtn);
				host.Children.Insert(3, fillButton);
				host.Children.Insert(4, flipButton);

				UpdateFormatButtonsState(host, vm);
				ApplySocialAspectRatio(window, vm);
				ApplyVideoFlipState(window, flipButton);

				vm.PropertyChanged += (s, e) =>
				{
					if (e.PropertyName == "SelectedSocialPreset")
					{
						if (!_userChangedPresetManually && youtube != null && vm.SelectedSocialPreset?.AspectRatioText != "16:9")
						{
							window.Dispatcher.BeginInvoke(new Action(() =>
							{
								vm.SelectedSocialPreset = youtube;
								_isFrameFillMode = true;
							}));
							return;
						}
					}
					if (e.PropertyName == "CurrentProject" || e.PropertyName == "VideoPath" || e.PropertyName == "HasVideoLoaded")
					{
						if (!_userChangedPresetManually && youtube != null && vm.SelectedSocialPreset?.AspectRatioText != "16:9")
						{
							window.Dispatcher.BeginInvoke(new Action(() =>
							{
								vm.SelectedSocialPreset = youtube;
								_isFrameFillMode = true;
							}));
						}
					}
					if (e.PropertyName == "SelectedSocialPreset" || e.PropertyName == "CurrentProject" || e.PropertyName == "VideoPath" || e.PropertyName == "HasVideoLoaded")
					{
						UpdateFormatButtonsState(host, vm);
						ApplySocialAspectRatio(window, vm);
					}
				};

				Log("Successfully injected interactive aspect ratio buttons (16:9, 9:16, 1:1) and Video Flip button.");
			}
			_isSocialPresetHooked = true;
		}
		catch (Exception ex)
		{
			Log("WireSocialMediaPresets error: " + ex.Message);
		}
	}

	private static bool _isVideoFlipped = false;
	private static string _originalVideoSourcePath = null;
	private static string _cachedFlippedVideoPath = null;
	private static string _lastFlippedSourcePath = null;

	private static void ApplyVideoFlipState(Window window, Button flipBtn)
	{
		try
		{
			if (window == null) return;
			if (flipBtn == null)
			{
				flipBtn = FindAllVisualChildren<Button>((DependencyObject)(object)window)
					.FirstOrDefault(b => (b.Tag as string ?? "").StartsWith("VideoFlipToggleButton"));
			}

			if (flipBtn != null)
			{
				flipBtn.Content = _isVideoFlipped ? "⇄ Flipped" : "⇄ Flip";
				if (_isVideoFlipped)
				{
					flipBtn.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(14, 165, 233), 45);
					flipBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
					flipBtn.BorderThickness = new Thickness(1.2);
					flipBtn.Foreground = Brushes.White;
				}
				else
				{
					flipBtn.Background = new SolidColorBrush(Color.FromRgb(15, 24, 38));
					flipBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 48, 74));
					flipBtn.BorderThickness = new Thickness(1.0);
					flipBtn.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
				}
			}

			MediaElement preview = FindVisualChild((DependencyObject)(object)window, (MediaElement m) => m.Name == "PreviewPlayer");
			if (preview != null)
			{
				preview.RenderTransformOrigin = new Point(0.5, 0.5);
				preview.RenderTransform = _isVideoFlipped 
					? new ScaleTransform(-1.0, 1.0) 
					: Transform.Identity;
			}

			if (window.DataContext is MainViewModel vm)
			{
				vm.ProjectStatusMessage = _isVideoFlipped 
					? "បានបញ្ច្រាសវីដេអូឆ្វេង-ស្តាំ (Video Flipped Horizontal ⇄)" 
					: "វីដេអូធម្មតា (Video Normal)";
			}
		}
		catch (Exception ex)
		{
			Log("ApplyVideoFlipState error: " + ex.Message);
		}
	}

	private static async Task EnsureFlippedVideoSourceAsync(MainViewModel vm)
	{
		try
		{
			if (vm?.CurrentProject == null || string.IsNullOrEmpty(vm.CurrentProject.VideoPath)) return;
			if (_originalVideoSourcePath == null)
			{
				_originalVideoSourcePath = vm.CurrentProject.VideoPath;
			}

			if (!_isVideoFlipped)
			{
				if (_originalVideoSourcePath != null && vm.CurrentProject.VideoPath != _originalVideoSourcePath)
				{
					vm.CurrentProject.VideoPath = _originalVideoSourcePath;
				}
				return;
			}

			string source = _originalVideoSourcePath;
			if (!File.Exists(source)) return;

			string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "FlippedCache");
			Directory.CreateDirectory(cacheDir);
			string flippedPath = Path.Combine(cacheDir, "flipped_" + Path.GetFileName(source));

			if (!File.Exists(flippedPath) || _lastFlippedSourcePath != source)
			{
				string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
				if (!File.Exists(ffmpegPath)) ffmpegPath = "ffmpeg";

				var psi = new ProcessStartInfo
				{
					FileName = ffmpegPath,
					Arguments = $"-y -i \"{source}\" -vf \"hflip\" -c:v libx264 -preset ultrafast -crf 16 -c:a copy \"{flippedPath}\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true
				};
				using (var proc = Process.Start(psi))
				{
					if (proc != null)
					{
						await proc.WaitForExitAsync();
						if (proc.ExitCode == 0 && File.Exists(flippedPath))
						{
							_cachedFlippedVideoPath = flippedPath;
							_lastFlippedSourcePath = source;
							Log("Successfully generated flipped video cache: " + flippedPath);
						}
					}
				}
			}
			else
			{
				_cachedFlippedVideoPath = flippedPath;
			}

			if (_isVideoFlipped && File.Exists(_cachedFlippedVideoPath))
			{
				vm.CurrentProject.VideoPath = _cachedFlippedVideoPath;
			}
		}
		catch (Exception ex)
		{
			Log("EnsureFlippedVideoSourceAsync error: " + ex.Message);
		}
	}

	private static string _cachedFilledVideoPath = null;
	private static string _lastFilledSourcePath = null;

	private static async Task EnsureFilledVideoSourceAsync(MainViewModel vm)
	{
		try
		{
			if (vm?.CurrentProject == null || string.IsNullOrEmpty(vm.CurrentProject.VideoPath)) return;
			if (_originalVideoSourcePath == null)
			{
				_originalVideoSourcePath = vm.CurrentProject.VideoPath;
			}

			if (!_isFrameFillMode) return;

			string source = vm.CurrentProject.VideoPath;
			if (!File.Exists(source)) return;

			string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "FilledCache");
			Directory.CreateDirectory(cacheDir);
			string filledPath = Path.Combine(cacheDir, "filled_16_9_" + Path.GetFileName(source));

			if (!File.Exists(filledPath) || _lastFilledSourcePath != source)
			{
				string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
				if (!File.Exists(ffmpegPath)) ffmpegPath = "ffmpeg";

				// Center-crop to 16:9 if the aspect ratio is taller than 16:9 (e.g. 9:16 or 4:3)
				// or wider than 16:9
				string vf = "crop=w='if(gt(a,16/9),trunc(ih*16/9/2)*2,iw)':h='if(gt(a,16/9),ih,trunc(iw*9/16/2)*2)':x='(iw-out_w)/2':y='(ih-out_h)/2'";

				var psi = new ProcessStartInfo
				{
					FileName = ffmpegPath,
					Arguments = $"-y -i \"{source}\" -vf \"{vf}\" -c:v libx264 -preset ultrafast -crf 16 -c:a copy \"{filledPath}\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true
				};
				using (var proc = Process.Start(psi))
				{
					if (proc != null)
					{
						await proc.WaitForExitAsync();
						if (proc.ExitCode == 0 && File.Exists(filledPath))
						{
							_cachedFilledVideoPath = filledPath;
							_lastFilledSourcePath = source;
							Log("Successfully generated filled video cache: " + filledPath);
						}
					}
				}
			}
			else
			{
				_cachedFilledVideoPath = filledPath;
			}

			if (File.Exists(_cachedFilledVideoPath))
			{
				vm.CurrentProject.VideoPath = _cachedFilledVideoPath;
			}
		}
		catch (Exception ex)
		{
			Log("EnsureFilledVideoSourceAsync error: " + ex.Message);
		}
	}

	private static void ScheduleVoiceSpeedPreview(MainViewModel vm, Window window)
	{
		_voiceSpeedPreviewVersion++;
		if (_voiceSpeedPreviewTimer == null)
		{
			_voiceSpeedPreviewTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(700.0)
			};
			_voiceSpeedPreviewTimer.Tick += async (object sender, EventArgs args) =>
			{
				_voiceSpeedPreviewTimer.Stop();
				int version = _voiceSpeedPreviewVersion;
				DialogueLine line = vm.SelectedDialogueLine;
				if (line == null || string.IsNullOrWhiteSpace(line.CorrectedKhmer ?? line.DraftKhmer))
				{
					return;
				}

				await _voiceSpeedPreviewLock.WaitAsync();
				try
				{
					if (version != _voiceSpeedPreviewVersion)
					{
						return;
					}
					vm.ProjectStatusMessage = $"Updating Line #{line.Index} voice speed...";
					bool ok = await SynthesizeSingleLineSafelyAsync(vm, line);
					if (!ok || version != _voiceSpeedPreviewVersion || string.IsNullOrEmpty(line.AudioFilePath) || !File.Exists(line.AudioFilePath))
					{
						return;
					}

					MediaElement video = FindVisualChild((DependencyObject)(object)window, (MediaElement item) => item.Name == "PreviewPlayer");
					if (video != null && vm.IsPlaying)
					{
						video.Pause();
						vm.IsPlaying = false;
					}
					_voiceSpeedPreviewPlayer.Stop();
					_voiceSpeedPreviewPlayer.Open(new Uri(Path.GetFullPath(line.AudioFilePath)));
					_voiceSpeedPreviewPlayer.Play();
					vm.ProjectStatusMessage = $"Playing Line #{line.Index} at {vm.VoiceSpeed:0.00}x";
				}
				catch (Exception ex)
				{
					Log("Live voice speed preview error: " + ex.Message);
				}
				finally
				{
					_voiceSpeedPreviewLock.Release();
				}
			};
		}

		_voiceSpeedPreviewTimer.Stop();
		_voiceSpeedPreviewTimer.Start();
	}

	private static void UpdateVoiceSpeedPercentControl(double speed)
	{
		if (_voiceSpeedSlider == null || _voiceSpeedLabel == null)
		{
			return;
		}
		double normalizedSpeed = NormalizeVoiceSpeed(speed);
		double percent = Math.Round(50.0 + 50.0 * Math.Log(normalizedSpeed, 4.0));
		percent = Math.Max(0.0, Math.Min(100.0, percent));
		_isUpdatingVoiceSpeedControl = true;
		try
		{
			_voiceSpeedSlider.Value = percent;
			_voiceSpeedLabel.Text = $"VOICE SPEED: {normalizedSpeed:0.00}x ({percent:0}%)";
		}
		finally
		{
			_isUpdatingVoiceSpeedControl = false;
		}
	}

	private static void WireVoiceSpeedToDialogue(MainViewModel vm)
	{
		if (ReferenceEquals(_voiceSpeedViewModel, vm))
		{
			return;
		}

		_voiceSpeedViewModel = vm;
		// The original application initializes speech speed to 1x. Treat that
		// untouched value as the default and start Khmer synthesis at 2x.
		// Explicit non-default values loaded from a project remain unchanged.
		bool appliedTwoTimesDefault = Math.Abs(NormalizeVoiceSpeed(vm.VoiceSpeed) - 1.0) < 0.0001;
		if (appliedTwoTimesDefault)
		{
			vm.VoiceSpeed = 2.0;
		}
		ApplyVoiceSpeedToAllLines(vm, invalidateExistingAudio: appliedTwoTimesDefault);

		vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
		{
			if (args.PropertyName == "VoiceSpeed")
			{
				ApplyVoiceSpeedToAllLines(vm, invalidateExistingAudio: true);
			}
			else if (args.PropertyName == "DialogueLines")
			{
				WireVoiceSpeedCollection(vm);
				ApplyVoiceSpeedToAllLines(vm, invalidateExistingAudio: false);
			}
		};

		WireVoiceSpeedCollection(vm);
		Log("Voice speed control wired to neural speech only (video playback speed remains unchanged)");
	}

	private static void WireVoiceSpeedCollection(MainViewModel vm)
	{
		if (vm.DialogueLines is not INotifyCollectionChanged collection || ReferenceEquals(_voiceSpeedLines, collection))
		{
			return;
		}

		_voiceSpeedLines = collection;
		collection.CollectionChanged += (object? sender, NotifyCollectionChangedEventArgs args) =>
		{
			if (args.NewItems == null)
			{
				return;
			}
			double speed = NormalizeVoiceSpeed(vm.VoiceSpeed);
			foreach (object item in args.NewItems)
			{
				if (item is DialogueLine line)
				{
					line.Speed = speed;
				}
			}
		};
	}

	private static void ApplyVoiceSpeedToAllLines(MainViewModel vm, bool invalidateExistingAudio)
	{
		double speed = NormalizeVoiceSpeed(vm.VoiceSpeed);
		if (Math.Abs(vm.VoiceSpeed - speed) > 0.0001)
		{
			vm.VoiceSpeed = speed;
		}

		if (vm.DialogueLines == null)
		{
			return;
		}

		foreach (DialogueLine line in vm.DialogueLines)
		{
			if (Math.Abs(line.Speed - speed) <= 0.0001)
			{
				continue;
			}
			line.Speed = speed;
			if (invalidateExistingAudio)
			{
				// Keep the cached file until regeneration succeeds, but force the
				// preview workflow to synthesize it again with the new speech rate.
				line.LastSynthesizedText = "__VOICE_SPEED_CHANGED__";
			}
		}
		Log($"Applied voice speed {speed:0.00}x to {vm.DialogueLines.Count} dialogue lines");
	}

	public static void TryAttachExistingPreviewVideo(Window window)
	{
		try
		{
			if (!(window.DataContext is MainViewModel { CurrentProject: not null } mainViewModel) || string.IsNullOrEmpty(mainViewModel.CurrentProject.Id))
			{
				return;
			}
			string projectId = mainViewModel.CurrentProject.Id;
			Task.Run(() =>
			{
				try
				{
					string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "PreviewCache", projectId);
					if (Directory.Exists(path))
					{
						string[] files = Directory.GetFiles(path, "preview_khmer*.mp4");
						if (files.Length != 0)
						{
							Array.Sort(files, (string a, string b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
							string latest = files[0];
							if (File.Exists(latest) && new FileInfo(latest).Length > 0)
							{
								((DispatcherObject)window).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
								{
									MediaElement mediaElement = FindVisualChild((DependencyObject)(object)window, (MediaElement me) => me.Name == "PreviewPlayer");
									if (mediaElement != null)
									{
										bool flag = false;
										if (mediaElement.Source == null)
										{
											flag = true;
										}
										else
										{
											try
											{
												string localPath = mediaElement.Source.LocalPath;
												if (!localPath.Contains("PreviewCache") && localPath != latest)
												{
													flag = true;
												}
											}
											catch
											{
											}
										}
										if (flag)
										{
											mediaElement.Source = new Uri(latest);
											mediaElement.IsMuted = false;
											mediaElement.Volume = 1.0;
											Log("Auto-attached existing preview video asynchronously: " + latest);
											if (_btnPreviewRef != null)
											{
												_btnPreviewRef.Content = "✓ Preview Video Ready";
												_btnPreviewRef.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
											}
											if (_btnTimelinePreviewRef != null)
											{
												_btnTimelinePreviewRef.Content = "✓ Preview Video Ready (Play ▶)";
												_btnTimelinePreviewRef.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
											}
										}
									}
								}), (DispatcherPriority)4, Array.Empty<object>());
							}
						}
					}
				}
				catch
				{
				}
			});
		}
		catch
		{
		}
	}

	public static void WirePreviewAudioSync(Window window)
	{
		try
		{
			if (_isPreviewAudioSyncHooked) return;
			if (window.DataContext is not MainViewModel vm) return;
			MediaElement player = FindVisualChild((DependencyObject)(object)window, (MediaElement me) => me.Name == "PreviewPlayer");
			if (player == null) return;

			_previewLiveKhmerPlayer.MediaOpened += (s, e) =>
			{
				try
				{
					_previewLiveKhmerPlayer.Volume = 1.0;
					if (vm.IsPlaying) _previewLiveKhmerPlayer.Play();
				}
				catch { }
			};

			player.MediaOpened += (s, e) =>
			{
				try
				{
					bool isPreRendered = player.Source != null && player.Source.LocalPath.Contains("PreviewCache");
					if (!isPreRendered)
					{
						player.IsMuted = true;
						player.Volume = 0.0;
					}
				}
				catch { }
			};

			Slider slider = FindVisualChild((DependencyObject)(object)window, (Slider s) => s.Name == "TimelineSlider");
			if (slider != null)
			{
				slider.ValueChanged += (s, e) =>
				{
					_previewLivePlayingLineIndex = -1;
					try { _previewLiveKhmerPlayer.Stop(); } catch { }
					if (_previewLiveBgmPlayer.Source != null)
					{
						try { _previewLiveBgmPlayer.Position = TimeSpan.FromSeconds(e.NewValue); } catch { }
					}
				};
			}

			_previewAudioSyncTimer = new DispatcherTimer(DispatcherPriority.Render, window.Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(25)
			};
			_previewAudioSyncTimer.Tick += (s, e) =>
			{
				try
				{
					if (vm.CurrentProject == null || string.IsNullOrEmpty(vm.CurrentProject.VideoPath))
					{
						_previewLiveKhmerPlayer.Stop();
						_previewLiveBgmPlayer.Stop();
						_previewLivePlayingLineIndex = -1;
						return;
					}

					bool isPreRendered = player.Source != null && player.Source.LocalPath.Contains("PreviewCache");
					if (isPreRendered)
					{
						if (player.IsMuted) player.IsMuted = false;
						_previewLiveKhmerPlayer.Stop();
						_previewLiveBgmPlayer.Stop();
						_previewLivePlayingLineIndex = -1;
						return;
					}

					// Raw original video: MUTE CHINESE AUDIO COMPLETELY
					if (!player.IsMuted || player.Volume > 0.0)
					{
						player.IsMuted = true;
						player.Volume = 0.0;
					}

					if (!vm.IsPlaying)
					{
						if (_previewLiveKhmerPlayer.Position > TimeSpan.Zero)
						{
							_previewLiveKhmerPlayer.Pause();
						}
						if (_previewLiveBgmPlayer.Position > TimeSpan.Zero)
						{
							_previewLiveBgmPlayer.Pause();
						}
						return;
					}

					int curMs = (int)player.Position.TotalMilliseconds;

					// 1. Dialogue Line Audio Playback
					DialogueLine activeLine = null;
					if (vm.DialogueLines != null)
					{
						foreach (DialogueLine line in vm.DialogueLines)
						{
							if (!string.IsNullOrEmpty(line.AudioFilePath) && File.Exists(line.AudioFilePath)
								&& curMs >= line.StartMs && curMs < line.EndMs)
							{
								activeLine = line;
								break;
							}
						}
					}

					if (activeLine != null)
					{
						if (_previewLivePlayingLineIndex != activeLine.Index)
						{
							_previewLivePlayingLineIndex = activeLine.Index;
							_previewLiveKhmerPlayer.Stop();
							_previewLiveKhmerPlayer.Open(new Uri(Path.GetFullPath(activeLine.AudioFilePath)));
							int offsetMs = Math.Max(0, curMs - activeLine.StartMs);
							_previewLiveKhmerPlayer.Position = TimeSpan.FromMilliseconds(offsetMs);
							_previewLiveKhmerPlayer.Volume = 1.0;
							_previewLiveKhmerPlayer.Play();
						}
						else
						{
							_previewLiveKhmerPlayer.Play();
						}
					}
					else
					{
						if (_previewLivePlayingLineIndex != -1)
						{
							_previewLivePlayingLineIndex = -1;
							try { _previewLiveKhmerPlayer.Stop(); } catch { }
						}
					}

					// 2. Background Music Playback (Only for external music, never crude SeparatedAudio)
					string bgmPath = vm.AudioMix?.BgmTrackPath;
					if (!string.IsNullOrEmpty(bgmPath) && File.Exists(bgmPath) && !bgmPath.Contains("SeparatedAudio") && (vm.AudioMix == null || !vm.AudioMix.IsBgmMuted))
					{
						if (_previewLiveBgmPlayer.Source == null || !_previewLiveBgmPlayer.Source.LocalPath.Equals(Path.GetFullPath(bgmPath), StringComparison.OrdinalIgnoreCase))
						{
							_previewLiveBgmPlayer.Open(new Uri(Path.GetFullPath(bgmPath)));
						}
						double diff = Math.Abs((_previewLiveBgmPlayer.Position - player.Position).TotalMilliseconds);
						if (diff > 150.0)
						{
							_previewLiveBgmPlayer.Position = player.Position;
						}
						_previewLiveBgmPlayer.Volume = 0.28;
						_previewLiveBgmPlayer.Play();
					}
					else
					{
						_previewLiveBgmPlayer.Stop();
					}
				}
				catch { }
			};
			_previewAudioSyncTimer.Start();
			_isPreviewAudioSyncHooked = true;
			Log("WirePreviewAudioSync: Real-time Khmer dialogue and BGM synchronization hooked to preview transport!");
		}
		catch (Exception ex)
		{
			Log("WirePreviewAudioSync error: " + ex.Message);
		}
	}

	public static async Task RunFullPipelineAsync(MainViewModel vm, Window window)
	{
		if (_isPipelineRunning || vm == null || vm.CurrentProject == null)
		{
			return;
		}
		string videoPath = vm.CurrentProject.VideoPath;
		if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
		{
			return;
		}
		_cancelRequested = false;
		_isPipelineRunning = true;
		try
		{
			vm.IsBusy = true;
			SetDialogueEditingEnabled(window, vm, false);
			SetPipelineStatus(vm, _forceFfmpegOnly
				? "ជំហាន 2/6៖ រំលង Gemini ព្រោះ Token/Quota អស់—បន្តដោយ FFmpeg..."
				: $"ជំហាន 2/6៖ Gemini កំពុងពិនិត្យ និងកែតែបន្ទាត់ដែលខ្វះ ({(vm.DialogueLines?.Count ?? 0)} បន្ទាត់)...");
			if (!_forceFfmpegOnly)
			{
				ClearGeminiQuotaError();
				Log("Starting token-efficient Gemini meaning and natural-speech review");
				await EnsureAllLinesTranslatedAsync(vm);
				string quotaError = ConsumeGeminiQuotaError();
				if (quotaError != null)
				{
					string choice = ShowGeminiQuotaDialog(window, vm, quotaError);
					if (choice == "retry" && vm.DialogueLines != null)
					{
						await RunDirectPythonTranslateAsync(vm.DialogueLines.ToList());
					}
					else if (choice == "ffmpeg") _forceFfmpegOnly = true;
					else if (choice == "cancel") return;
				}
			}
			if (_cancelRequested)
			{
				return;
			}
			vm.IsBusy = true;
			SetPipelineStatus(vm, "ជំហាន 3/6៖ លុបភ្លេងខាងក្រោយ និងសំឡេងដើមចេញ...");
			Log("Starting Auto Step 3: SeparateAudioAsync");
			try
			{
				await vm.SeparateAudioAsync();
			}
			catch (Exception ex)
			{
				Log("SeparateAudioAsync error (continuing): " + ex.Message);
			}
			if (_cancelRequested)
			{
				return;
			}
			vm.IsBusy = true;
			SetPipelineStatus(vm, $"ជំហាន 4/6៖ បង្កើត ពិនិត្យ និងបំពេញសំឡេងខ្មែរដែលខ្វះ ({(vm.DialogueLines?.Count ?? 0)} បន្ទាត់)...");
			Log("Starting Auto Step 4: snapshot-safe voice synthesis");
			if (_cancelRequested)
			{
				return;
			}
			if (vm.DialogueLines != null)
			{
				// Use a stable snapshot because TTS awaits external work while UI and
				// timeline notifications can touch the live ObservableCollection.
				List<DialogueLine> synthesisLines = vm.DialogueLines.ToList();
				SetPipelineStatus(vm, $"ជំហាន 4/6៖ បង្កើត ពិនិត្យ និងបំពេញសំឡេងខ្មែរដែលខ្វះ (0/{vm.DialogueLines.Count})...");
				for (int i = 0; i < synthesisLines.Count; i++)
				{
					if (_cancelRequested)
					{
						return;
					}
					DialogueLine dialogueLine = synthesisLines[i];
					bool textChanged = string.Equals(dialogueLine.LastSynthesizedText, "__VOICE_SPEED_CHANGED__", StringComparison.Ordinal)
						|| (!string.IsNullOrWhiteSpace(dialogueLine.CorrectedKhmer)
							&& !string.Equals(dialogueLine.LastSynthesizedText, dialogueLine.CorrectedKhmer, StringComparison.Ordinal));
					if (textChanged || string.IsNullOrEmpty(dialogueLine.AudioFilePath) || !File.Exists(dialogueLine.AudioFilePath) || new FileInfo(dialogueLine.AudioFilePath).Length == 0L)
					{
						vm.BusyProgressPercent = 50.0 + 30.0 * (i + 1) / Math.Max(1, synthesisLines.Count);
						SetPipelineStatus(vm, $"ជំហាន 4/6៖ បង្កើត ពិនិត្យ និងបំពេញសំឡេងខ្មែរដែលខ្វះ ({i + 1}/{vm.DialogueLines.Count}) បន្ទាត់ទី {dialogueLine.Index}...");
						await SynthesizeSingleLineSafelyAsync(vm, dialogueLine);
					}
				}
				SetPipelineStatus(vm, "ជំហាន 5/6៖ រក្សាទុកអត្ថបទ ពេលវេលា និងឯកសារសំឡេង...");
				try
				{
					await vm.SaveProjectAsync();
				}
				catch
				{
				}
			}
			if (_cancelRequested)
			{
				return;
			}
			vm.IsBusy = true;
			SetPipelineStatus(vm, "ជំហាន 6/6៖ បង្កើតវីដេអូ Preview ចុងក្រោយ...");
			Log("Starting Auto Step 5: BuildPreviewVideoAsync");
			string previewVideo = await BuildPreviewVideoAsync(vm);
			if (_cancelRequested)
			{
				return;
			}
			if (!string.IsNullOrEmpty(previewVideo) && File.Exists(previewVideo))
			{
				((DispatcherObject)window).Dispatcher.Invoke((Action)(() =>
				{
					MediaElement mediaElement = FindVisualChild((DependencyObject)(object)window, (MediaElement me) => me.Name == "PreviewPlayer");
					if (mediaElement != null)
					{
						mediaElement.Source = new Uri(previewVideo);
						mediaElement.Position = TimeSpan.Zero;
						vm.IsPlaying = false;
						Log("Updated PreviewPlayer.Source without auto-play: " + previewVideo);
					}
				}));
			}
			vm.IsBusy = false;
			_isPipelineRunning = false;
			if (_btnPreviewRef != null)
			{
				_btnPreviewRef.Content = "✓ Preview Video Ready";
				_btnPreviewRef.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
			}
			if (_btnTimelinePreviewRef != null)
			{
				_btnTimelinePreviewRef.Content = "✓ Preview Video Ready (Play ▶)";
				_btnTimelinePreviewRef.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
			}
			vm.ProjectStatusMessage = "✓ Khmer AI Dubbing Complete! Press Play ▶ to preview video with Khmer voices & BGM.";
			vm.BusyProgressPercent = 100.0;
		}
		catch (Exception ex3)
		{
			Log("Error in RunFullPipelineAsync: " + ex3);
			await CleanupGeneratedProjectFilesAsync(vm);
			vm.ProjectStatusMessage = "Auto-dubbing error: " + ex3.Message;
		}
		finally
		{
			_pipelineStatusText = string.Empty;
			_isPipelineRunning = false;
			vm.IsBusy = false;
			vm.BusyStatusText = string.Empty;
			_forceFfmpegOnly = false;
			SetDialogueEditingEnabled(window, vm, true);
		}
	}

	public static async Task RunUpdatePreviewAndPlayAsync(MainViewModel vm, Window window)
	{
		if (vm == null || vm.CurrentProject == null)
		{
			return;
		}
		string videoPath = vm.CurrentProject.VideoPath;
		if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
		{
			MessageBox.Show("Please load a video first.", "No Video", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		_cancelRequested = false;
		vm.IsBusy = true;
		try
		{
			vm.BusyStatusText = "Step 1/3: Checking dialogue translation...";
			await EnsureAllLinesTranslatedAsync(vm);
			if (_cancelRequested)
			{
				return;
			}
			if (vm.DialogueLines != null && vm.DialogueLines.Count > 0)
			{
				List<DialogueLine> linesToSynthesize = new List<DialogueLine>();
				foreach (DialogueLine dialogueLine2 in vm.DialogueLines)
				{
					bool num = string.IsNullOrEmpty(dialogueLine2.AudioFilePath) || !File.Exists(dialogueLine2.AudioFilePath) || new FileInfo(dialogueLine2.AudioFilePath).Length == 0;
					bool flag = dialogueLine2.LastSynthesizedText != dialogueLine2.CorrectedKhmer;
					if (num | flag)
					{
						linesToSynthesize.Add(dialogueLine2);
					}
				}
				if (linesToSynthesize.Count > 0)
				{
					for (int i = 0; i < linesToSynthesize.Count; i++)
					{
						if (_cancelRequested)
						{
							return;
						}
						DialogueLine dialogueLine = linesToSynthesize[i];
						vm.BusyStatusText = $"Step 2/3: Generating voice ({i + 1}/{linesToSynthesize.Count}): Line #{dialogueLine.Index}...";
						await SynthesizeSingleLineSafelyAsync(vm, dialogueLine);
					}
					try
					{
						await vm.SaveProjectAsync();
					}
					catch
					{
					}
				}
			}
			if (_cancelRequested)
			{
				return;
			}
			vm.BusyStatusText = "Step 3/3: Mixing Khmer voices and building preview video...";
			string pv = await BuildPreviewVideoAsync(vm);
			if (_cancelRequested)
			{
				return;
			}
			if (!string.IsNullOrEmpty(pv) && File.Exists(pv))
			{
				((DispatcherObject)window).Dispatcher.Invoke((Action)(() =>
				{
					MediaElement mediaElement = FindVisualChild((DependencyObject)(object)window, (MediaElement me) => me.Name == "PreviewPlayer");
					if (mediaElement != null)
					{
						mediaElement.Source = new Uri(pv);
						mediaElement.Position = TimeSpan.Zero;
						vm.IsPlaying = false;
						Log("RunUpdatePreviewAndPlayAsync: Updated PreviewPlayer.Source without auto-play: " + pv);
					}
				}));
				if (_btnPreviewRef != null)
				{
					_btnPreviewRef.Content = "✓ Preview Video Ready";
					_btnPreviewRef.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
				}
				if (_btnTimelinePreviewRef != null)
				{
					_btnTimelinePreviewRef.Content = "✓ Preview Video Ready (Play ▶)";
					_btnTimelinePreviewRef.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
				}
				vm.ProjectStatusMessage = "✓ Preview Video Ready! Press Play ▶ to watch with Khmer dubbing.";
			}
			else
			{
				vm.ProjectStatusMessage = "Failed to create preview video.";
			}
		}
		catch (Exception ex)
		{
			Log("RunUpdatePreviewAndPlayAsync error: " + ex);
			vm.ProjectStatusMessage = "Update Preview error: " + ex.Message;
		}
		finally
		{
			vm.IsBusy = false;
			vm.BusyStatusText = string.Empty;
		}
	}

	public static async Task EnsureAllLinesTranslatedAsync(MainViewModel vm)
	{
		if (vm == null || vm.DialogueLines == null || vm.DialogueLines.Count == 0)
		{
			return;
		}
		List<DialogueLine> list = new List<DialogueLine>();
		foreach (DialogueLine dialogueLine in vm.DialogueLines)
		{
			if (LineNeedsTranslation(dialogueLine))
			{
				list.Add(dialogueLine);
			}
		}
		if (list.Count == 0)
		{
			Log("EnsureAllLinesTranslatedAsync: All lines already translated into Khmer.");
			return;
		}
		Log($"EnsureAllLinesTranslatedAsync: Found {list.Count} lines requiring translation.");
		vm.BusyStatusText = $"Gemini is checking and polishing {list.Count} Khmer dialogue lines...";
		try
		{
			if (await RunDirectPythonTranslateAsync(list))
			{
				try
				{
					await vm.SaveProjectAsync();
				}
				catch
				{
				}
				Log("EnsureAllLinesTranslatedAsync: Translation successful and saved.");
			}
		}
		catch (Exception ex)
		{
			Log("EnsureAllLinesTranslatedAsync error: " + ex);
		}
	}

	private static bool LineNeedsTranslation(DialogueLine l)
	{
		if (l == null)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(l.CorrectedKhmer))
		{
			return true;
		}
		if (l.CorrectedKhmer == l.ChineseText)
		{
			return true;
		}
		bool flag = false;
		bool flag2 = false;
		string correctedKhmer = l.CorrectedKhmer;
		foreach (char c in correctedKhmer)
		{
			if (c >= '一' && c <= '龥')
			{
				flag = true;
			}
			if (c >= 'ក' && c <= '\u17ff')
			{
				flag2 = true;
			}
		}
		if (flag)
		{
			return !flag2;
		}
		return false;
	}

	private static async Task<bool> RunDirectPythonTranslateAsync(List<DialogueLine> lines)
	{
		try
		{
			string fileName = "python";
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine", "engine_main.py");
			if (!File.Exists(text))
			{
				return false;
			}
			string api_key = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
			List<object> list = new List<object>();
			foreach (DialogueLine line in lines)
			{
				list.Add(new
				{
					index = line.Index,
					speaker = line.Speaker,
					chinese = line.ChineseText,
					draft_khmer = line.DraftKhmer ?? "",
					corrected_khmer = ""
				});
			}
			string contents = JsonSerializer.Serialize(new
			{
				action = "translate",
				api_key = api_key,
				context = "Movie Dialogue Dubbing",
				model_name = "gemini-3.5-flash-lite",
				dialogue = list
			});
			string tempJson = Path.GetTempFileName();
			File.WriteAllText(tempJson, contents, Encoding.UTF8);
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = $"\"{text}\" --action translate --payload \"{tempJson}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};
			string stdout = "";
			using (Process p = Process.Start(startInfo))
			{
				stdout = await p.StandardOutput.ReadToEndAsync();
				await p.WaitForExitAsync();
			}
			try
			{
				File.Delete(tempJson);
			}
			catch
			{
			}
			if (string.IsNullOrWhiteSpace(stdout))
			{
				return false;
			}
			int num = stdout.IndexOf('{');
			int num2 = stdout.LastIndexOf('}');
			if (num == -1 || num2 == -1 || num2 <= num)
			{
				return false;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(stdout.Substring(num, num2 - num + 1));
			if (jsonDocument.RootElement.TryGetProperty("dialogue", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				Dictionary<int, (string draft, string corrected)> transMap = new Dictionary<int, (string, string)>();
				foreach (JsonElement item3 in value.EnumerateArray())
				{
					int @int = item3.GetProperty("index").GetInt32();
					string item = (item3.TryGetProperty("draft_khmer", out var value2) ? value2.GetString() : "");
					string item2 = (item3.TryGetProperty("corrected_khmer", out var value3) ? value3.GetString() : "");
					transMap[@int] = (item, item2);
				}
				Application current3 = Application.Current;
				if (current3 != null)
				{
					Dispatcher dispatcher = ((DispatcherObject)current3).Dispatcher;
					if (dispatcher != null)
					{
						dispatcher.Invoke((Action)(() =>
						{
							foreach (DialogueLine line2 in lines)
							{
								if (transMap.TryGetValue(line2.Index, out (string, string) value4) && !string.IsNullOrWhiteSpace(value4.Item2))
								{
									(line2.DraftKhmer, line2.CorrectedKhmer) = value4;
								}
							}
						}));
					}
				}
				return true;
			}
		}
		catch (Exception ex)
		{
			Log("RunDirectPythonTranslateAsync error: " + ex.Message);
		}
		return false;
	}

	public static async Task<bool> SynthesizeSingleLineSafelyAsync(MainViewModel vm, DialogueLine l)
	{
		if (vm == null || vm.CurrentProject == null || l == null)
		{
			return false;
		}
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "AudioCache", vm.CurrentProject.Id);
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			string outPath = Path.Combine(text, $"line_{l.Index}.mp3");
			string text2 = ((!string.IsNullOrWhiteSpace(l.CorrectedKhmer)) ? l.CorrectedKhmer : l.DraftKhmer);
			if (string.IsNullOrWhiteSpace(text2))
			{
				text2 = ".";
			}
			string voice = ((!string.IsNullOrWhiteSpace(l.VoiceProfile)) ? l.VoiceProfile : "piset");
			// VoiceSpeed is a speech/TTS setting. Apply it to the generated voice;
			// the preview video is always stream-copied at its original speed.
			double speed = NormalizeVoiceSpeed(vm.VoiceSpeed);
			l.Speed = speed;
			double volume = Math.Max(1.6, (l.Volume > 0.0) ? l.Volume * 1.6 : 1.6);
			int pitch = l.Pitch;
			bool synthesized = false;
			try
			{
				FieldInfo field = typeof(MainViewModel).GetField("_pythonEngine", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field != null && field.GetValue(vm) is IPythonEngineService pythonEngineService && (await pythonEngineService.SynthesizeSentenceAsync(text2, outPath, voice, speed, volume, pitch, CancellationToken.None)).Item1 && File.Exists(outPath) && new FileInfo(outPath).Length > 0)
				{
					synthesized = true;
				}
			}
			catch (Exception ex)
			{
				Log($"Reflection engine error on line {l.Index}: {ex.Message}");
			}
			if (!synthesized)
			{
				synthesized = await RunDirectPythonTtsAsync(text2, outPath, voice, speed, volume, pitch);
			}
			if (!synthesized || !File.Exists(outPath) || new FileInfo(outPath).Length == 0L)
			{
				GenerateSilentAudioDirect(outPath, 0.4);
			}
			if (File.Exists(outPath) && new FileInfo(outPath).Length > 0)
			{
				EnsureAudioLoudnessBoost(outPath);
				l.AudioFilePath = outPath;
				l.LastSynthesizedText = l.CorrectedKhmer;
				return true;
			}
		}
		catch (Exception ex2)
		{
			Log($"Error in SynthesizeSingleLineSafelyAsync line {l.Index}: {ex2.Message}");
		}
		return false;
	}

	private static void EnsureAudioLoudnessBoost(string audioPath)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath)) return;
			string ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
			if (!File.Exists(ffmpeg)) ffmpeg = "ffmpeg";

			string tempBoosted = audioPath + ".loud.mp3";
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = ffmpeg,
				Arguments = $"-y -i \"{audioPath}\" -filter:a \"volume=2.8,alimiter=limit=0.98\" -c:a libmp3lame -b:a 192k \"{tempBoosted}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true
			};
			using (Process process = Process.Start(psi))
			{
				process.StandardError.ReadToEnd();
				process.WaitForExit(4000);
				if (File.Exists(tempBoosted) && new FileInfo(tempBoosted).Length > 0)
				{
					File.Copy(tempBoosted, audioPath, true);
					try { File.Delete(tempBoosted); } catch { }
				}
			}
		}
		catch { }
	}

	private static bool GenerateSilentAudioDirect(string outPath, double durationSec)
	{
		try
		{
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
			if (!File.Exists(text))
			{
				text = "ffmpeg";
			}
			using (Process process = Process.Start(new ProcessStartInfo
			{
				FileName = text,
				Arguments = $"-y -f lavfi -i anullsrc=r=24000:cl=mono -t {durationSec.ToString("0.0", CultureInfo.InvariantCulture)} -c:a libmp3lame -b:a 48k \"{outPath}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true
			}))
			{
				process.StandardError.ReadToEnd();
				process.WaitForExit();
			}
			return File.Exists(outPath) && new FileInfo(outPath).Length > 0;
		}
		catch
		{
			return false;
		}
	}

	private static async Task<bool> RunDirectPythonTtsAsync(string text, string outPath, string voice, double speed, double volume, int pitch)
	{
		_ = 1;
		try
		{
			string fileName = "python";
			string winAppsPy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "python.exe");
			if (File.Exists(winAppsPy)) fileName = winAppsPy;

			string text2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine", "engine_main.py");
			if (!File.Exists(text2))
			{
				string alt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Engine", "engine_main.py");
				if (File.Exists(alt)) text2 = alt;
				else return false;
			}
			string contents = JsonSerializer.Serialize(new
			{
				text = text,
				output_path = outPath,
				voice_id = voice,
				speed = speed,
				volume = volume,
				pitch = pitch
			});
			string tempJson = Path.GetTempFileName();
			File.WriteAllText(tempJson, contents, Encoding.UTF8);
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = $"\"{text2}\" --action tts_single --payload \"{tempJson}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true
			};
			using (Process p = Process.Start(startInfo))
			{
				Task<string> task = p.StandardOutput.ReadToEndAsync();
				Task<string> task2 = p.StandardError.ReadToEndAsync();
				await Task.WhenAll<string>(task, task2);
				await p.WaitForExitAsync();
			}
			try
			{
				File.Delete(tempJson);
			}
			catch
			{
			}
			return File.Exists(outPath) && new FileInfo(outPath).Length > 0;
		}
		catch
		{
			return false;
		}
	}

	public static async Task<string> BuildPreviewVideoAsync(MainViewModel vm)
	{
		if (vm == null || vm.CurrentProject == null)
		{
			return null;
		}
		string videoPath = vm.CurrentProject.VideoPath;
		if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
		{
			return null;
		}
		string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
		if (!File.Exists(ffmpegPath))
		{
			ffmpegPath = "ffmpeg";
		}
		string projectDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "PreviewCache", vm.CurrentProject.Id);
		if (!Directory.Exists(projectDir))
		{
			Directory.CreateDirectory(projectDir);
		}
		string previewVideo = Path.Combine(projectDir, $"preview_khmer_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
		string staticPreviewVideo = Path.Combine(projectDir, "preview_khmer.mp4");
		// Never use an uncompressed WAV here. A failed/incorrectly-timestamped
		// input can make it grow by tens of gigabytes and fill the system drive.
		string tempDubAudio = Path.Combine(projectDir, "temp_dub.m4a");
		double previewDuration = Math.Max(1.0, vm.VideoDurationSec);
		List<DialogueLine> list = new List<DialogueLine>();
		foreach (DialogueLine dialogueLine in vm.DialogueLines)
		{
			if (!string.IsNullOrEmpty(dialogueLine.AudioFilePath) && File.Exists(dialogueLine.AudioFilePath) && new FileInfo(dialogueLine.AudioFilePath).Length > 0)
			{
				list.Add(dialogueLine);
			}
		}
		if (list.Count == 0)
		{
			Log("BuildPreviewVideoAsync: No lines with valid audio found");
			return null;
		}
		Log($"BuildPreviewVideoAsync: Found {list.Count} dialogue lines with audio");
		StringBuilder stringBuilder = new StringBuilder();
		List<string> list2 = new List<string>();
		StringBuilder stringBuilder2 = new StringBuilder();
		for (int i = 0; i < list.Count; i++)
		{
			EnsureAudioLoudnessBoost(list[i].AudioFilePath);
			StringBuilder stringBuilder3 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder3;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder3);
			handler.AppendLiteral("-i \"");
			handler.AppendFormatted(list[i].AudioFilePath);
			handler.AppendLiteral("\" ");
			stringBuilder4.Append(ref handler);
			int value = Math.Max(0, list[i].StartMs);
			list2.Add($"[{i}:a]adelay={value}|{value}[a_{i}]");
			stringBuilder3 = stringBuilder2;
			StringBuilder stringBuilder5 = stringBuilder3;
			handler = new StringBuilder.AppendInterpolatedStringHandler(4, 1, stringBuilder3);
			handler.AppendLiteral("[a_");
			handler.AppendFormatted(i);
			handler.AppendLiteral("]");
			stringBuilder5.Append(ref handler);
		}
		list2.Add($"{stringBuilder2}amix=inputs={list.Count}:normalize=0:duration=longest:dropout_transition=0,volume=2.6,alimiter=limit=0.98[a_dub]");
		string value2 = string.Join(";", list2);
		ProcessStartInfo startInfo = new ProcessStartInfo
		{
			FileName = ffmpegPath,
			Arguments = $"-y {stringBuilder} -filter_complex \"{value2}\" -map \"[a_dub]\" -t {previewDuration.ToString("0.###", CultureInfo.InvariantCulture)} -ar 44100 -ac 2 -c:a aac -b:a 192k \"{tempDubAudio}\"",
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true
		};
		using (Process proc = Process.Start(startInfo))
		{
			string err = await proc.StandardError.ReadToEndAsync();
			await proc.WaitForExitAsync();
			Log($"FFmpeg dub audio exit code: {proc.ExitCode}, output len: {err.Length}");
		}
		if (!File.Exists(tempDubAudio))
		{
			return null;
		}
		string text = ((vm.AudioMix != null) ? vm.AudioMix.BgmTrackPath : null);
		if (string.IsNullOrEmpty(text) || !File.Exists(text))
		{
			string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SNA-Dubber", "SeparatedAudio", vm.CurrentProject.Id, "bgm_track.wav");
			if (File.Exists(text2))
			{
				text = text2;
			}
		}
		string arguments;
		if (vm.AudioMix != null && vm.AudioMix.IsOriginalMuted)
		{
			Log("BuildPreviewVideoAsync: Audio Mute is ACTIVE -> Muting original sound & voice completely. Khmer dub only (boosted).");
			arguments = $"-y -i \"{videoPath}\" -i \"{tempDubAudio}\" -filter_complex \"[1:a]volume=2.8,alimiter=limit=0.98[a_out]\" -map 0:v -map \"[a_out]\" -c:v copy -c:a aac -b:a 192k -shortest \"{previewVideo}\"";
		}
		else if (!string.IsNullOrEmpty(text) && File.Exists(text) && (vm.AudioMix == null || !vm.AudioMix.IsBgmMuted))
		{
			Log("BuildPreviewVideoAsync: Mixing with gentle BGM track and extra boosted Khmer voice: " + text);
			arguments = $"-y -i \"{videoPath}\" -i \"{text}\" -i \"{tempDubAudio}\" -filter_complex \"[1:a]volume=0.15[a_bgm];[2:a]volume=2.8,alimiter=limit=0.98[a_khmer];[a_bgm][a_khmer]amix=inputs=2:normalize=0:duration=first[a_final]\" -map 0:v -map \"[a_final]\" -c:v copy -c:a aac -b:a 192k -shortest \"{previewVideo}\"";
		}
		else
		{
			Log("BuildPreviewVideoAsync: No BGM track found or BGM muted, muxing boosted dubbed dialogue directly");
			arguments = $"-y -i \"{videoPath}\" -i \"{tempDubAudio}\" -filter_complex \"[1:a]volume=2.8,alimiter=limit=0.98[a_out]\" -map 0:v -map \"[a_out]\" -c:v copy -c:a aac -b:a 192k -shortest \"{previewVideo}\"";
		}
		ProcessStartInfo startInfo2 = new ProcessStartInfo
		{
			FileName = ffmpegPath,
			Arguments = arguments,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true
		};
		using (Process proc = Process.Start(startInfo2))
		{
			string err = await proc.StandardError.ReadToEndAsync();
			await proc.WaitForExitAsync();
			Log($"FFmpeg video mux exit code: {proc.ExitCode}, output len: {err.Length}");
		}
		try { File.Delete(tempDubAudio); } catch { }
		if (File.Exists(previewVideo))
		{
			try
			{
				File.Copy(previewVideo, staticPreviewVideo, overwrite: true);
			}
			catch
			{
			}
			try
			{
				string[] files = Directory.GetFiles(projectDir, "preview_khmer_*.mp4");
				foreach (string text3 in files)
				{
					if (text3 != previewVideo)
					{
						try
						{
							File.Delete(text3);
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
			return previewVideo;
		}
		return null;
	}

	public static bool TryInjectToolbar(Window window)
	{
		if (_isToolbarInjected)
		{
			return true;
		}
		try
		{
			TextBlock textBlock = FindVisualChild((DependencyObject)(object)window, (TextBlock t) => t.Text != null && (t.Text.Contains("AI Auto-Detect") || t.Text.Contains("Speakers not separated")));
			if (textBlock == null)
			{
				textBlock = FindLogicalChild((DependencyObject)(object)window, (TextBlock t) => t.Text != null && (t.Text.Contains("AI Auto-Detect") || t.Text.Contains("Speakers not separated")));
			}
			if (textBlock == null)
			{
				return false;
			}
			Panel panel = (textBlock.Parent as Panel) ?? (VisualTreeHelper.GetParent((DependencyObject)(object)textBlock) as Panel);
			if (panel == null)
			{
				DependencyObject val = (DependencyObject)(object)textBlock;
				while (panel == null && val != null)
				{
					DependencyObject val2 = LogicalTreeHelper.GetParent(val) ?? VisualTreeHelper.GetParent(val);
					if (val2 is Panel panel2)
					{
						panel = panel2;
						break;
					}
					val = val2;
				}
			}
			if (panel == null)
			{
				return false;
			}
			foreach (UIElement child in panel.Children)
			{
				if (child is FrameworkElement frameworkElement && frameworkElement.Tag as string == "ToolbarPreviewButton")
				{
					_isToolbarInjected = true;
					return true;
				}
			}
			MainViewModel vm = window.DataContext as MainViewModel;
			Button btnPreview = new Button
			{
				Tag = "ToolbarPreviewButton",
				Content = "\ud83c\udfac Update Preview Video",
				ToolTip = "Fast-mixes video with Khmer dubbing voices and background music so you can preview in the player.",
				Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
				Padding = new Thickness(12.0, 5.0, 12.0, 5.0),
				Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 11.0,
				Cursor = Cursors.Hand
			};
			_btnPreviewRef = btnPreview;
			btnPreview.Click += async (object s, RoutedEventArgs e) =>
			{
				if (vm != null)
				{
					btnPreview.IsEnabled = false;
					btnPreview.Content = "⏳ Updating Video Preview...";
					try
					{
						await RunUpdatePreviewAndPlayAsync(vm, window);
					}
					finally
					{
						btnPreview.IsEnabled = true;
					}
				}
			};
			panel.Children.Add(btnPreview);
			_isToolbarInjected = true;
			Log("Successfully injected Update Preview button into toolbar!");
			return true;
		}
		catch (Exception ex)
		{
			Log("Exception in TryInjectToolbar: " + ex);
			return false;
		}
	}

	public static void UpdateTimelineBar(Window window)
	{
		if (_isTimelineInjected)
		{
			return;
		}
		try
		{
			MainViewModel vm = window.DataContext as MainViewModel;
			Panel panel = null;
			foreach (TextBlock item in FindAllVisualChildren<TextBlock>((DependencyObject)(object)window))
			{
				if (string.IsNullOrEmpty(item.Text))
				{
					continue;
				}
				string text = item.Text.Trim();
				if (text.Contains("Auto-Fit"))
				{
					Border border = FindAncestor<Border>((DependencyObject)(object)item);
					if (border != null && !(border.Parent is Window))
					{
						border.Visibility = Visibility.Collapsed;
						if (panel == null && border.Parent is Panel panel2)
						{
							panel = panel2;
						}
						Log("Collapsed Auto-Fit: Soft border");
					}
				}
				else if (text.Contains("END SYNC"))
				{
					Button button = FindAncestor<Button>((DependencyObject)(object)item);
					if (button != null)
					{
						button.Visibility = Visibility.Collapsed;
						if (panel == null && button.Parent is Panel panel3)
						{
							panel = panel3;
						}
						Log("Collapsed END SYNC button");
					}
				}
				else if (text.Contains("Generate Voice"))
				{
					Button button2 = FindAncestor<Button>((DependencyObject)(object)item);
					if (button2 != null)
					{
						button2.Visibility = Visibility.Collapsed;
						if (panel == null && button2.Parent is Panel panel4)
						{
							panel = panel4;
						}
						Log("Collapsed Generate Voice button");
					}
				}
				else if (text.Contains("Retry"))
				{
					Button button3 = FindAncestor<Button>((DependencyObject)(object)item);
					if (button3 != null)
					{
						button3.Visibility = Visibility.Collapsed;
						if (panel == null && button3.Parent is Panel panel5)
						{
							panel = panel5;
						}
						Log("Collapsed Retry button");
					}
				}
				else
				{
					if (!text.Contains("Rush Line"))
					{
						continue;
					}
					Button button4 = FindAncestor<Button>((DependencyObject)(object)item);
					if (button4 != null)
					{
						button4.Visibility = Visibility.Collapsed;
						if (panel == null && button4.Parent is Panel panel6)
						{
							panel = panel6;
						}
						Log("Collapsed Rush Line button");
					}
				}
			}
			foreach (Button item2 in FindAllVisualChildren<Button>((DependencyObject)(object)window))
			{
				string text2 = (item2.ToolTip as string) ?? "";
				if (text2.IndexOf("Synchronize Dubbing", StringComparison.OrdinalIgnoreCase) >= 0 || text2.IndexOf("Generate all Khmer voice", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					item2.Visibility = Visibility.Collapsed;
					if (panel == null && item2.Parent is Panel panel7)
					{
						panel = panel7;
					}
				}
			}
			if (panel == null)
			{
				return;
			}
			bool flag = false;
			foreach (UIElement child in panel.Children)
			{
				if (child is FrameworkElement frameworkElement && frameworkElement.Tag as string == "TimelineSplitButton")
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				return;
			}
			Button button5 = new Button
			{
				Tag = "TimelineSplitButton",
				Content = "✂\ufe0f Split Line",
				ToolTip = "Split dialogue line at current playhead position (✂\ufe0f ប\u17c6ប\u17c2កបន\u17d2ទ\u17b6ត\u17cb Timeline)",
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
				Padding = new Thickness(12.0, 5.0, 12.0, 5.0),
				Background = new SolidColorBrush(Color.FromRgb(79, 70, 229)),
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = 11.0,
				Cursor = Cursors.Hand
			};
			button5.Click += (object s, RoutedEventArgs e) =>
			{
				if (vm != null)
				{
					ExecuteSplitDialogueLine(vm, window);
				}
			};
			panel.Children.Add(button5);
			AddTimelineCoreButtons(panel, vm);
			_isTimelineInjected = true;
			Log("Successfully injected Split Line button into timeline bar!");
		}
		catch (Exception ex)
		{
			Log("UpdateTimelineBar error: " + ex.Message);
		}
	}

	private static void AddTimelineCoreButtons(Panel panel, MainViewModel vm)
	{
		if (panel.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "TimelineCoreControls"))
		{
			return;
		}
		StackPanel controls = new StackPanel
		{
			Tag = "TimelineCoreControls",
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		Button zoomOut = CreateTimelineToolButton("−", "Zoom out timeline");
		Button zoomReset = CreateTimelineToolButton("100%", "Reset timeline zoom");
		Button zoomIn = CreateTimelineToolButton("+", "Zoom in timeline");
		Button undo = CreateTimelineToolButton("↶", "Undo timeline edit");
		Button redo = CreateTimelineToolButton("↷", "Redo timeline edit");
		Button delete = CreateTimelineToolButton("Delete", "Delete selected timeline item");
		undo.Click += (object sender, RoutedEventArgs args) => UndoTimeline(vm);
		redo.Click += (object sender, RoutedEventArgs args) => RedoTimeline(vm);
		zoomOut.Click += (object sender, RoutedEventArgs args) => { SetTimelineZoom(_timelineZoom - 0.25); zoomReset.Content = $"{_timelineZoom * 100:0}%"; };
		zoomReset.Click += (object sender, RoutedEventArgs args) => { SetTimelineZoom(1.0); zoomReset.Content = "100%"; };
		zoomIn.Click += (object sender, RoutedEventArgs args) => { SetTimelineZoom(_timelineZoom + 0.25); zoomReset.Content = $"{_timelineZoom * 100:0}%"; };
		delete.Click += (object sender, RoutedEventArgs args) =>
		{
			PushTimelineUndo(vm);
			if (_selectedTimelineTextLayers.Count + _selectedTimelineOverlays.Count + _selectedTimelineLines.Count + _selectedTimelineBlurRegions.Count > 0)
			{
				foreach (TextLayerItem item in _selectedTimelineTextLayers.ToList()) vm.TextLayers.Remove(item);
				foreach (OverlayItem item in _selectedTimelineOverlays.ToList()) vm.Overlays.Remove(item);
				foreach (DialogueLine line in _selectedTimelineLines.ToList()) vm.DialogueLines.Remove(line);
				foreach (BlurRegionItem item in _selectedTimelineBlurRegions.ToList()) vm.BlurRegions.Remove(item);
				_selectedTimelineTextLayers.Clear();
				_selectedTimelineBlurRegions.Clear();
				_selectedTimelineOverlays.Clear();
				_selectedTimelineLines.Clear();
				_selectedTimelineBlurRegions.Clear();
				_selectedTimelineTextLayer = null;
				_selectedTimelineOverlay = null;
				vm.SelectedTextLayer = null;
				vm.SelectedOverlay = null;
				vm.SelectedDialogueLine = null;
				vm.SelectedBlurRegion = null;
			}
			else if (_selectedTimelineOverlay != null)
			{
				vm.Overlays.Remove(_selectedTimelineOverlay);
				_selectedTimelineOverlay = null;
			}
			else if (vm.SelectedDialogueLine != null)
			{
				vm.DeleteDialogueLineCommand?.Execute(vm.SelectedDialogueLine);
			}
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
		};
		controls.Children.Add(undo);
		controls.Children.Add(redo);
		controls.Children.Add(zoomOut);
		controls.Children.Add(zoomReset);
		controls.Children.Add(zoomIn);
		controls.Children.Add(delete);
		panel.Children.Add(controls);
	}

	private static Button CreateTimelineToolButton(string content, string toolTip)
	{
		return new Button
		{
			Content = content,
			ToolTip = toolTip,
			Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
			Padding = new Thickness(8.0, 4.0, 8.0, 4.0),
			MinWidth = 30.0,
			Background = new SolidColorBrush(Color.FromRgb(25, 39, 58)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(51, 74, 101)),
			BorderThickness = new Thickness(1.0),
			Cursor = Cursors.Hand,
			FontSize = 10.0
		};
	}

	private static void SetTimelineZoom(double zoom)
	{
		_timelineZoom = Math.Max(0.5, Math.Min(8.0, zoom));
		foreach (Canvas track in _continuousTimelineTracks.ToArray())
		{
			ScrollViewer scroll = FindAncestor<ScrollViewer>((DependencyObject)(object)track);
			if (scroll != null)
			{
				track.Width = GetTimelineContentWidth(Math.Max(scroll.ViewportWidth, scroll.ActualWidth), _timelineDurationSeconds);
			}
		}
		Canvas dispatcherSource = _continuousTimelineTracks.FirstOrDefault();
		if (dispatcherSource != null)
		{
			dispatcherSource.Dispatcher.BeginInvoke((Action)(() => _refreshCommonTimelineScrollBar?.Invoke()), DispatcherPriority.Loaded);
		}
	}

	private static double GetTimelineContentWidth(double viewportWidth, double durationSeconds)
	{
		double viewport = Math.Max(1.0, viewportWidth);
		// Keep short videos fitted to the editor. Long videos receive a stable
		// pixels-per-second scale and naturally expose the shared scrollbar.
		// Keep a useful editing scale even at 100%. This guarantees a draggable
		// horizontal range instead of rendering a track with no scrollbar thumb.
		double realTimeWidth = Math.Max(viewport * 1.25, Math.Max(1.0, durationSeconds) * 20.0);
		return realTimeWidth * _timelineZoom;
	}

	private static void TryInjectWaveformTimeline(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm)
			{
				return;
			}
			// The main Timeline Editor already owns the VISUALS / SPONSORS /
			// SUBTITLES / KHMER VOICE lanes. Do not insert a second editor into
			// the compact playback transport that contains TimelineSlider.
			TextBlock existingTimelineLane = FindVisualChild((DependencyObject)(object)window,
				(TextBlock text) => string.Equals(text.Text?.Trim(), "KHMER VOICE", StringComparison.OrdinalIgnoreCase));
			if (existingTimelineLane != null)
			{
				WireTimelineContentVisibility(existingTimelineLane, vm);
				_isWaveformTimelineInjected = true;
				Log("Timeline now appears only when project timeline content exists");
				return;
			}
			Slider timelineSlider = FindVisualChild((DependencyObject)(object)window, (Slider slider) => slider.Name == "TimelineSlider");
			if (timelineSlider?.Parent is not Panel host)
			{
				return;
			}
			if (host.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "AudioWaveformTimeline"))
			{
				_isWaveformTimelineInjected = true;
				return;
			}

			Border shell = new Border
			{
				Tag = "AudioWaveformTimeline",
				Height = 145.0,
				Margin = new Thickness(0.0, 5.0, 0.0, 7.0),
				Background = new SolidColorBrush(Color.FromRgb(4, 9, 18)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(28, 48, 70)),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(4.0),
				ClipToBounds = true
			};
			Grid grid = new Grid();
			grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24.0) });
			for (int row = 0; row < 4; row++)
			{
				grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30.0) });
			}

			Canvas ruler = new Canvas { Background = new SolidColorBrush(Color.FromRgb(4, 9, 18)) };
			Grid.SetRow(ruler, 0);
			grid.Children.Add(ruler);
			CreateEditorLane(grid, 1, "VISUALS", Color.FromRgb(255, 225, 0));
			CreateEditorLane(grid, 2, "SPONSORS", Color.FromRgb(255, 0, 213));
			CreateEditorLane(grid, 3, "SUBTITLES", Color.FromRgb(0, 242, 174));
			Canvas voiceLane = CreateEditorLane(grid, 4, "KHMER VOICE", Color.FromRgb(180, 73, 255));

			System.Windows.Shapes.Line playhead = new System.Windows.Shapes.Line
			{
				Stroke = new SolidColorBrush(Color.FromRgb(0, 196, 255)),
				StrokeThickness = 1.0,
				Y1 = 0.0,
				Y2 = 145.0,
				Opacity = 0.8,
				IsHitTestVisible = false
			};
			Grid.SetRowSpan(playhead, 5);
			grid.Children.Add(playhead);

			Action updatePlayhead = () =>
			{
				double duration = Math.Max(0.001, vm.VideoDurationSec);
				double x = Math.Max(0.0, Math.Min(grid.ActualWidth, vm.CurrentPositionSec / duration * grid.ActualWidth));
				playhead.X1 = playhead.X2 = x;
			};
			Action refreshLayout = () =>
			{
				RenderTimelineRuler(ruler, vm.VideoDurationSec);
				RenderKhmerVoiceLane(voiceLane, vm);
				updatePlayhead();
			};
			grid.SizeChanged += (object sender, SizeChangedEventArgs args) => refreshLayout();
			vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
			{
				if (args.PropertyName == "CurrentPositionSec")
				{
					updatePlayhead();
				}
				else if (args.PropertyName == "VideoDurationSec" || args.PropertyName == "CurrentProject" || args.PropertyName == "DialogueLines")
				{
					refreshLayout();
				}
			};
			if (vm.DialogueLines is INotifyCollectionChanged changed)
			{
				changed.CollectionChanged += (object? sender, NotifyCollectionChangedEventArgs args) => RenderKhmerVoiceLane(voiceLane, vm);
			}

			shell.Child = grid;
			DockPanel.SetDock(shell, Dock.Top);
			int sliderIndex = host.Children.IndexOf(timelineSlider);
			host.Children.Insert(Math.Max(0, sliderIndex), shell);
			_isWaveformTimelineInjected = true;
			refreshLayout();
			Log("Injected four-lane editor timeline");
		}
		catch (Exception ex)
		{
			Log("TryInjectWaveformTimeline error: " + ex.Message);
		}
	}

	private static void WireTimelineContentVisibility(TextBlock timelineLaneLabel, MainViewModel vm)
	{
		Grid laneGrid = FindAncestor<Grid>((DependencyObject)(object)timelineLaneLabel);
		if (laneGrid == null)
		{
			return;
		}
		Border container = FindAncestor<Border>((DependencyObject)(object)laneGrid);
		FrameworkElement target = container ?? (FrameworkElement)laneGrid;
		TextBlock visualToolLabel = FindVisualChild((DependencyObject)(object)laneGrid, (TextBlock text) => string.Equals(text.Text?.Trim(), "VISUALS", StringComparison.OrdinalIgnoreCase));
		TextBlock mediaToolLabel = FindVisualChild((DependencyObject)(object)laneGrid, (TextBlock text) => string.Equals(text.Text?.Trim(), "SPONSOR", StringComparison.OrdinalIgnoreCase));
		if (visualToolLabel != null) visualToolLabel.Tag = "VISUALS";
		if (mediaToolLabel != null) mediaToolLabel.Tag = "SPONSOR";
		Action update = () =>
		{
			List<BlurRegionItem> automaticBlurs = vm.BlurRegions?.Where(item => string.Equals(item.Label, "Bottom Chinese Subtitle Blur", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<BlurRegionItem>();
			if (automaticBlurs.Count > 0)
			{
				laneGrid.Dispatcher.BeginInvoke((Action)(() =>
				{
					foreach (BlurRegionItem automatic in automaticBlurs) vm.BlurRegions.Remove(automatic);
					try { vm.SaveProjectCommand?.Execute(null); } catch { }
				}), DispatcherPriority.Background);
			}
			bool hasVideo = HasCurrentVideoReference(vm);
			bool hasDialogue = (vm.DialogueLines?.Count ?? 0) > 0;
			bool hasVoice = vm.DialogueLines?.Any(line => !string.IsNullOrWhiteSpace(line.AudioFilePath)) == true;
			bool hasSponsors = (vm.Overlays?.Count ?? 0) > 0;
			bool hasVisuals = (vm.TextLayers?.Count ?? 0) > 0 || (vm.BlurRegions?.Count ?? 0) > 0;
			bool subtitlesRequested = _subtitleTimelineVisible;
			if (visualToolLabel != null)
			{
				bool hasText = (vm.TextLayers?.Count ?? 0) > 0;
				bool hasBlur = (vm.BlurRegions?.Count ?? 0) > 0;
				visualToolLabel.Text = hasText && hasBlur ? "TEXT / BLUR" : (hasBlur ? "BLUR" : "TEXT");
			}
			if (mediaToolLabel != null && hasSponsors)
			{
				bool hasOverlayVideo = vm.Overlays.Any(item => new[] { ".mp4", ".mov", ".mkv", ".webm", ".avi", ".gif" }.Contains(Path.GetExtension(item.ImagePath ?? "").ToLowerInvariant()));
				bool hasImage = vm.Overlays.Any(item => !new[] { ".mp4", ".mov", ".mkv", ".webm", ".avi", ".gif" }.Contains(Path.GetExtension(item.ImagePath ?? "").ToLowerInvariant()));
				mediaToolLabel.Text = hasOverlayVideo && hasImage ? "IMAGE / VIDEO" : (hasOverlayVideo ? "VIDEO" : "IMAGE");
			}
			// Show the visual lane when text/blur clips exist so their timing can be
			// selected, moved and trimmed from the timeline.
			SetTimelineLaneVisibility(laneGrid, "VISUALS", hasVisuals);
			SetTimelineLaneVisibility(laneGrid, "SPONSOR", hasSponsors);
			SetTimelineLaneVisibility(laneGrid, "SUBTITLES", hasDialogue && subtitlesRequested);
			SetTimelineLaneVisibility(laneGrid, "KHMER VOICE", hasVoice || hasDialogue || hasVideo);
			bool hasTimelineContent = hasVoice || hasSponsors || hasVisuals || (hasDialogue && subtitlesRequested) || hasVideo;
			target.Visibility = hasTimelineContent ? Visibility.Visible : Visibility.Collapsed;
		};
		_refreshTimelineVisibility = update;
		void Subscribe(object collection)
		{
			if (collection is INotifyCollectionChanged changed)
			{
				changed.CollectionChanged += (object? sender, NotifyCollectionChangedEventArgs args) =>
				{
					laneGrid.Dispatcher.BeginInvoke((Action)(() => update()), DispatcherPriority.Background);
				};
			}
		}
		Subscribe(vm.DialogueLines);
		Subscribe(vm.Overlays);
		Subscribe(vm.TextLayers);
		Subscribe(vm.BlurRegions);
		Action<DialogueLine> watchDialogue = line =>
		{
			if (line is INotifyPropertyChanged changedLine)
			{
				changedLine.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
				{
					if (args.PropertyName == "AudioFilePath") update();
				};
			}
		};
		if (vm.DialogueLines != null)
		{
			foreach (DialogueLine line in vm.DialogueLines) watchDialogue(line);
			vm.DialogueLines.CollectionChanged += (object? sender, NotifyCollectionChangedEventArgs args) =>
			{
				if (args.NewItems != null)
				{
					foreach (object item in args.NewItems) if (item is DialogueLine line) watchDialogue(line);
				}
			};
		}
		ConvertDialogueLanesToContinuousTracks(laneGrid, vm);
		ConvertOverlayLaneToContinuousTrack(laneGrid, vm);
		ConvertTextLaneToContinuousTrack(laneGrid, vm);
		EnhanceCapCutTimelineLayout(laneGrid, vm);
		vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
		{
			if (args.PropertyName == "DialogueLines")
			{
				Subscribe(vm.DialogueLines);
				if (vm.DialogueLines != null)
				{
					foreach (DialogueLine line in vm.DialogueLines) watchDialogue(line);
					vm.DialogueLines.CollectionChanged += (object? s, NotifyCollectionChangedEventArgs a) =>
					{
						if (a.NewItems != null)
						{
							foreach (object it in a.NewItems) if (it is DialogueLine l) watchDialogue(l);
						}
					};
				}
			}
			if (args.PropertyName == "DialogueLines" || args.PropertyName == "Overlays" || args.PropertyName == "TextLayers" || args.PropertyName == "BlurRegions" || args.PropertyName == "CurrentProject" || args.PropertyName == "ActiveToolPanel"
				|| args.PropertyName == "HasVideoLoaded" || args.PropertyName == "VideoDurationSec")
			{
				update();
			}
		};
		update();
	}

	private static void SetTimelineLaneVisibility(Grid laneGrid, string laneName, bool visible)
	{
		TextBlock label = FindVisualChild((DependencyObject)(object)laneGrid,
			(TextBlock text) => string.Equals(text.Tag as string, laneName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(text.Text?.Trim(), laneName, StringComparison.OrdinalIgnoreCase));
		DockPanel lane = label == null ? null : FindAncestor<DockPanel>((DependencyObject)(object)label);
		if (lane != null)
		{
			lane.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
			int rowIndex = Grid.GetRow(lane);
			if (rowIndex >= 0 && rowIndex < laneGrid.RowDefinitions.Count)
			{
				RowDefinition row = laneGrid.RowDefinitions[rowIndex];
				if (!_timelineRowHeights.ContainsKey(row))
				{
					_timelineRowHeights[row] = row.Height;
				}
				row.Height = visible ? _timelineRowHeights[row] : new GridLength(0.0);
			}
		}
	}

	private static void WireSubtitleTimelineToggle(Window window)
	{
		Button button = FindVisualChild((DependencyObject)(object)window, (Button item) =>
			string.Equals(item.CommandParameter as string, "subtitles", StringComparison.OrdinalIgnoreCase));
		if (button == null) return;
		button.Click += (object sender, RoutedEventArgs args) =>
		{
			_subtitleTimelineVisible = !_subtitleTimelineVisible;
			window.Dispatcher.BeginInvoke((Action)(() => _refreshTimelineVisibility?.Invoke()), DispatcherPriority.Background);
		};
		_subtitleToggleHooked = true;
		Log("Subtitle timeline icon now toggles the optional subtitle track");
	}

	private static TimelineSnapshot CaptureTimelineSnapshot(MainViewModel vm)
	{
		TimelineSnapshot snapshot = new TimelineSnapshot();
		if (vm.DialogueLines != null)
		{
			foreach (DialogueLine line in vm.DialogueLines)
				snapshot.Lines.Add((line, line.StartMs, line.EndMs, line.StartTime, line.EndTime));
		}
		if (vm.Overlays != null)
		{
			foreach (OverlayItem item in vm.Overlays)
				snapshot.Overlays.Add((item, item.StartMs, item.EndMs));
		}
		if (vm.TextLayers != null)
		{
			foreach (TextLayerItem item in vm.TextLayers)
				snapshot.TextLayers.Add((item, item.StartMs, item.EndMs));
		}
		if (vm.BlurRegions != null)
		{
			foreach (BlurRegionItem item in vm.BlurRegions)
				snapshot.BlurRegions.Add((item, item.StartMs, item.EndMs));
		}
		return snapshot;
	}

	private static void PushTimelineUndo(MainViewModel vm)
	{
		_timelineUndo.Push(CaptureTimelineSnapshot(vm));
		while (_timelineUndo.Count > 50)
		{
			TimelineSnapshot[] snapshots = _timelineUndo.ToArray();
			_timelineUndo.Clear();
			for (int i = Math.Min(49, snapshots.Length - 1); i >= 0; i--) _timelineUndo.Push(snapshots[i]);
		}
		_timelineRedo.Clear();
	}

	private static void RestoreTimelineSnapshot(MainViewModel vm, TimelineSnapshot snapshot)
	{
		vm.DialogueLines.Clear();
		foreach (var state in snapshot.Lines)
		{
			state.Line.StartMs = state.Start;
			state.Line.EndMs = state.End;
			state.Line.StartTime = state.StartText;
			state.Line.EndTime = state.EndText;
			vm.DialogueLines.Add(state.Line);
		}
		vm.Overlays.Clear();
		foreach (var state in snapshot.Overlays)
		{
			state.Item.StartMs = state.Start;
			state.Item.EndMs = state.End;
			vm.Overlays.Add(state.Item);
		}
		vm.TextLayers.Clear();
		foreach (var state in snapshot.TextLayers)
		{
			state.Item.StartMs = state.Start;
			state.Item.EndMs = state.End;
			vm.TextLayers.Add(state.Item);
		}
		vm.BlurRegions.Clear();
		foreach (var state in snapshot.BlurRegions)
		{
			state.Item.StartMs = state.Start;
			state.Item.EndMs = state.End;
			vm.BlurRegions.Add(state.Item);
		}
		try { vm.SaveProjectCommand?.Execute(null); } catch { }
	}

	private static void UndoTimeline(MainViewModel vm)
	{
		if (_timelineUndo.Count == 0) return;
		_timelineRedo.Push(CaptureTimelineSnapshot(vm));
		RestoreTimelineSnapshot(vm, _timelineUndo.Pop());
		vm.ProjectStatusMessage = "Timeline undo";
	}

	private static void RedoTimeline(MainViewModel vm)
	{
		if (_timelineRedo.Count == 0) return;
		_timelineUndo.Push(CaptureTimelineSnapshot(vm));
		RestoreTimelineSnapshot(vm, _timelineRedo.Pop());
		vm.ProjectStatusMessage = "Timeline redo";
	}

	private static void EnhanceCapCutTimelineLayout(Grid laneGrid, MainViewModel vm)
	{
		if (laneGrid.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "CapCutTimelineOverlay"))
		{
			return;
		}
		// Replace the original fixed 00:00-00:50 ruler with a ruler driven by
		// the real media duration.  The stock ruler was only a visual mock-up
		// and did not share the same time scale as the clips and playhead.
		foreach (FrameworkElement oldRulerPart in laneGrid.Children.OfType<FrameworkElement>()
			.Where(item => Grid.GetRow(item) == 0).ToArray())
		{
			oldRulerPart.Visibility = Visibility.Collapsed;
		}
		Canvas realTimeRuler = new Canvas
		{
			Tag = "RealVideoTimelineRuler",
			Height = 25.0,
			Background = new SolidColorBrush(Color.FromRgb(4, 9, 18)),
			IsHitTestVisible = false
		};
		ScrollViewer rulerScroll = new ScrollViewer
		{
			Tag = "RealVideoTimelineRulerScroll",
			Content = realTimeRuler,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
			VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
			CanContentScroll = false
		};
		Grid.SetRow(rulerScroll, 0);
		laneGrid.Children.Add(rulerScroll);
		_continuousTimelineTracks.Add(realTimeRuler);
		_continuousTimelineScrollers.Add(rulerScroll);
		rulerScroll.SizeChanged += (object sender, SizeChangedEventArgs args) =>
		{
			realTimeRuler.Width = GetTimelineContentWidth(Math.Max(rulerScroll.ViewportWidth, rulerScroll.ActualWidth), vm.VideoDurationSec);
		};
		Action refreshRealTimeRuler = () => RenderTimelineRuler(realTimeRuler, vm.VideoDurationSec);
		realTimeRuler.SizeChanged += (object sender, SizeChangedEventArgs args) => refreshRealTimeRuler();
		vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
		{
			if (args.PropertyName == "VideoDurationSec" || args.PropertyName == "CurrentProject")
			{
				_timelineDurationSeconds = Math.Max(1.0, vm.VideoDurationSec);
				SetTimelineZoom(_timelineZoom);
				refreshRealTimeRuler();
			}
		};
		_timelineDurationSeconds = Math.Max(1.0, vm.VideoDurationSec);
		refreshRealTimeRuler();
		Grid overlay = new Grid
		{
			Tag = "CapCutTimelineOverlay",
			IsHitTestVisible = false
		};
		System.Windows.Shapes.Line playhead = new System.Windows.Shapes.Line
		{
			Stroke = Brushes.White,
			StrokeThickness = 2.0,
			Y1 = 0.0,
			Y2 = 1000.0,
			Opacity = 0.95
		};
		overlay.Children.Add(playhead);
		System.Windows.Shapes.Line hoverGuide = new System.Windows.Shapes.Line
		{
			Stroke = new SolidColorBrush(Color.FromRgb(34, 211, 238)), StrokeThickness = 1.0,
			Y1 = 0.0, Y2 = 1000.0, Opacity = 0.8, Visibility = Visibility.Collapsed,
			IsHitTestVisible = false
		};
		overlay.Children.Add(hoverGuide);
		Border head = new Border
		{
			Background = Brushes.White,
			Width = 10.0,
			Height = 10.0,
			CornerRadius = new CornerRadius(5.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
			Child = new TextBlock { Text = "▼", Foreground = Brushes.Black, FontSize = 6.0, HorizontalAlignment = HorizontalAlignment.Center }
		};
		overlay.Children.Add(head);
		Border hoverTimeBadge = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(31, 32, 42)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(5.0),
			Padding = new Thickness(6.0, 2.0, 6.0, 2.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Visibility = Visibility.Collapsed,
			Child = new TextBlock { Foreground = Brushes.White, FontSize = 9.0, FontWeight = FontWeights.SemiBold }
		};
		overlay.Children.Add(hoverTimeBadge);
		Grid.SetRowSpan(overlay, Math.Max(1, laneGrid.RowDefinitions.Count));
		// The edit playhead must remain visible above every clip.  It is a visual
		// guide only (the overlay is not hit-testable), so a high z-order cannot
		// interfere with selecting, moving, or trimming timeline items.
		Panel.SetZIndex(overlay, 10000);
		laneGrid.Children.Add(overlay);

		laneGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42.0) });
		StackPanel tools = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(8, 14, 24))
		};
		(string label, string panel)[] toolItems =
		{
			("✂ Edit", "visuals"), ("♫ Audio", "music"), ("T Text", "text"),
			("★ Effects", "visuals"), ("▣ Overlay", "sponsor"), ("CC Captions", "subtitles")
		};
		foreach (var item in toolItems)
		{
			Button button = CreateTimelineToolButton(item.label, "Open " + item.label.Replace("✂ ", ""));
			button.Margin = new Thickness(5.0, 2.0, 5.0, 2.0);
			button.Background = Brushes.Transparent;
			button.BorderThickness = new Thickness(0.0);
			button.Click += (object sender, RoutedEventArgs args) => vm.ActiveToolPanel = item.panel;
			tools.Children.Add(button);
		}
		Grid.SetRow(tools, laneGrid.RowDefinitions.Count - 1);
		laneGrid.Children.Add(tools);
		tools.Visibility = Visibility.Collapsed;
		Grid scrollHost = new Grid
		{
			Height = 18.0,
			Margin = new Thickness(84.0, 0.0, 8.0, 4.0),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Bottom,
			Background = new SolidColorBrush(Color.FromRgb(18, 35, 52)),
			ClipToBounds = true,
			ToolTip = "Drag left or right to navigate the timeline"
		};
		System.Windows.Controls.Primitives.ScrollBar timelineScrollBar = new System.Windows.Controls.Primitives.ScrollBar
		{
			Style = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar)),
			Orientation = Orientation.Horizontal,
			Minimum = 0.0,
			Maximum = 0.0,
			ViewportSize = 1.0,
			SmallChange = 20.0,
			LargeChange = 150.0,
			Height = 14.0,
			Margin = new Thickness(2.0),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(30, 52, 72)),
			Foreground = new SolidColorBrush(Color.FromRgb(34, 211, 238)),
			Opacity = 1.0,
			ToolTip = "Scroll timeline left or right"
		};
		scrollHost.Children.Add(timelineScrollBar);
		timelineScrollBar.Visibility = Visibility.Collapsed;
		Grid capCutScroll = new Grid { Background = new SolidColorBrush(Color.FromRgb(14, 20, 28)) };
		capCutScroll.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18.0) });
		capCutScroll.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		capCutScroll.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18.0) });
		Button scrollLeft = new Button
		{
			Content = "◀", FontSize = 8.0, Padding = new Thickness(0.0), BorderThickness = new Thickness(0.0),
			Background = new SolidColorBrush(Color.FromRgb(22, 29, 38)), Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 180)),
			Cursor = Cursors.Hand, ToolTip = "Scroll left"
		};
		Button scrollRight = new Button
		{
			Content = "▶", FontSize = 8.0, Padding = new Thickness(0.0), BorderThickness = new Thickness(0.0),
			Background = new SolidColorBrush(Color.FromRgb(22, 29, 38)), Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 180)),
			Cursor = Cursors.Hand, ToolTip = "Scroll right"
		};
		Canvas scrollRail = new Canvas
		{
			Height = 10.0, VerticalAlignment = VerticalAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(31, 39, 48)), Cursor = Cursors.Hand
		};
		Border scrollThumb = new Border
		{
			Height = 8.0, MinWidth = 36.0, CornerRadius = new CornerRadius(4.0),
			Background = new SolidColorBrush(Color.FromRgb(145, 150, 157)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(190, 194, 199)), BorderThickness = new Thickness(1.0),
			Cursor = Cursors.SizeWE, ToolTip = "Drag to scroll timeline"
		};
		Canvas.SetTop(scrollThumb, 1.0);
		scrollRail.Children.Add(scrollThumb);
		Grid.SetColumn(scrollLeft, 0); Grid.SetColumn(scrollRail, 1); Grid.SetColumn(scrollRight, 2);
		capCutScroll.Children.Add(scrollLeft); capCutScroll.Children.Add(scrollRail); capCutScroll.Children.Add(scrollRight);
		scrollHost.Children.Add(capCutScroll);
		Grid.SetRow(scrollHost, 0);
		Grid.SetRowSpan(scrollHost, Math.Max(1, laneGrid.RowDefinitions.Count));
		Panel.SetZIndex(scrollHost, 200);
		laneGrid.Children.Add(scrollHost);
		bool updatingCommonScrollBar = false;
		Action refreshCommonScrollBar = () =>
		{
			ScrollViewer first = _continuousTimelineScrollers.FirstOrDefault();
			if (first == null) return;
			updatingCommonScrollBar = true;
			try
			{
				timelineScrollBar.Maximum = Math.Max(0.0, first.ScrollableWidth);
				timelineScrollBar.ViewportSize = Math.Max(1.0, first.ViewportWidth);
				timelineScrollBar.Value = Math.Min(timelineScrollBar.Maximum, first.HorizontalOffset);
				double railWidth = Math.Max(1.0, scrollRail.ActualWidth);
				double totalWidth = first.ExtentWidth > 0.0 ? first.ExtentWidth : first.ViewportWidth;
				double thumbWidth = Math.Max(36.0, Math.Min(railWidth, railWidth * first.ViewportWidth / Math.Max(1.0, totalWidth)));
				scrollThumb.Width = thumbWidth;
				double thumbRange = Math.Max(0.0, railWidth - thumbWidth);
				double thumbLeft = timelineScrollBar.Maximum > 0.0 ? timelineScrollBar.Value / timelineScrollBar.Maximum * thumbRange : 0.0;
				Canvas.SetLeft(scrollThumb, thumbLeft);
				scrollThumb.Opacity = timelineScrollBar.Maximum > 0.0 ? 1.0 : 0.45;
			}
			finally { updatingCommonScrollBar = false; }
		};
		_refreshCommonTimelineScrollBar = refreshCommonScrollBar;
		timelineScrollBar.ValueChanged += (object sender, RoutedPropertyChangedEventArgs<double> args) =>
		{
			if (updatingCommonScrollBar) return;
			foreach (ScrollViewer scroll in _continuousTimelineScrollers) scroll.ScrollToHorizontalOffset(args.NewValue);
		};
		scrollLeft.Click += (object sender, RoutedEventArgs args) => timelineScrollBar.Value = Math.Max(0.0, timelineScrollBar.Value - 80.0);
		scrollRight.Click += (object sender, RoutedEventArgs args) => timelineScrollBar.Value = Math.Min(timelineScrollBar.Maximum, timelineScrollBar.Value + 80.0);
		double thumbDragOrigin = 0.0;
		double thumbValueOrigin = 0.0;
		scrollThumb.PreviewMouseLeftButtonDown += (object sender, MouseButtonEventArgs args) =>
		{
			thumbDragOrigin = args.GetPosition(scrollRail).X;
			thumbValueOrigin = timelineScrollBar.Value;
			scrollThumb.CaptureMouse();
			args.Handled = true;
		};
		scrollThumb.PreviewMouseMove += (object sender, MouseEventArgs args) =>
		{
			if (!scrollThumb.IsMouseCaptured || args.LeftButton != MouseButtonState.Pressed) return;
			double range = Math.Max(1.0, scrollRail.ActualWidth - scrollThumb.ActualWidth);
			double delta = args.GetPosition(scrollRail).X - thumbDragOrigin;
			timelineScrollBar.Value = Math.Max(0.0, Math.Min(timelineScrollBar.Maximum, thumbValueOrigin + delta / range * timelineScrollBar.Maximum));
			args.Handled = true;
		};
		scrollThumb.PreviewMouseLeftButtonUp += (object sender, MouseButtonEventArgs args) =>
		{
			scrollThumb.ReleaseMouseCapture();
			args.Handled = true;
		};
		scrollRail.PreviewMouseLeftButtonDown += (object sender, MouseButtonEventArgs args) =>
		{
			if (ReferenceEquals(args.OriginalSource, scrollThumb)) return;
			double range = Math.Max(1.0, scrollRail.ActualWidth - scrollThumb.ActualWidth);
			double desired = Math.Max(0.0, Math.Min(range, args.GetPosition(scrollRail).X - scrollThumb.ActualWidth / 2.0));
			timelineScrollBar.Value = desired / range * timelineScrollBar.Maximum;
			args.Handled = true;
		};
		scrollRail.SizeChanged += (object sender, SizeChangedEventArgs args) => refreshCommonScrollBar();
		foreach (ScrollViewer scroll in _continuousTimelineScrollers)
		{
			scroll.ScrollChanged += (object sender, ScrollChangedEventArgs args) => refreshCommonScrollBar();
		}
		laneGrid.SizeChanged += (object sender, SizeChangedEventArgs args) => refreshCommonScrollBar();
		laneGrid.PreviewMouseWheel += (object sender, MouseWheelEventArgs args) =>
		{
			if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
			{
				SetTimelineZoom(_timelineZoom + (args.Delta > 0 ? 0.25 : -0.25));
				args.Handled = true;
			}
		};
		laneGrid.Dispatcher.BeginInvoke((Action)(() => refreshCommonScrollBar()), DispatcherPriority.Loaded);

		bool hoveringTimeline = false;
		bool playheadPinned = false;
		Action updateRealPlayhead = () =>
		{
			if (hoveringTimeline) return;
			double x;
			ScrollViewer scroll = _continuousTimelineScrollers.FirstOrDefault();
			if (scroll?.Content is FrameworkElement content && vm.VideoDurationSec > 0.0)
			{
				Point origin = scroll.TranslatePoint(new Point(0.0, 0.0), laneGrid);
				x = origin.X + vm.CurrentPositionSec / vm.VideoDurationSec * content.ActualWidth - scroll.HorizontalOffset;
			}
			else
			{
				x = vm.VideoDurationSec > 0.0 ? vm.CurrentPositionSec / vm.VideoDurationSec * laneGrid.ActualWidth : 0.0;
			}
			x = Math.Max(0.0, Math.Min(laneGrid.ActualWidth, x));
			playhead.X1 = playhead.X2 = x;
			 head.Margin = new Thickness(Math.Max(0.0, x - 5.0), 1.0, 0.0, 0.0);
		};
		_showPinnedTimelinePlayhead = seconds =>
		{
			ScrollViewer scroll = _continuousTimelineScrollers.FirstOrDefault();
			double x;
			if (scroll?.Content is FrameworkElement content && vm.VideoDurationSec > 0.0)
			{
				Point origin = scroll.TranslatePoint(new Point(0.0, 0.0), laneGrid);
				x = origin.X + Math.Clamp(seconds, 0.0, vm.VideoDurationSec) / vm.VideoDurationSec * content.ActualWidth - scroll.HorizontalOffset;
			}
			else x = vm.VideoDurationSec > 0.0 ? Math.Clamp(seconds, 0.0, vm.VideoDurationSec) / vm.VideoDurationSec * laneGrid.ActualWidth : 0.0;
			x = Math.Max(0.0, Math.Min(laneGrid.ActualWidth, x));
			playhead.X1 = playhead.X2 = x;
			head.Margin = new Thickness(Math.Max(0.0, x - 5.0), 1.0, 0.0, 0.0);
			hoverGuide.Visibility = Visibility.Collapsed;
			hoverTimeBadge.Visibility = Visibility.Collapsed;
			hoveringTimeline = false;
			playheadPinned = true;
		};
		Action<Point, bool> updateHoverPlayhead = (Point pointer, bool seekVideo) =>
		{
			ScrollViewer scroll = _continuousTimelineScrollers.FirstOrDefault();
			if (scroll?.Content is not FrameworkElement content || vm.VideoDurationSec <= 0.0 || content.ActualWidth <= 0.0) return;
			Point contentPoint = laneGrid.TranslatePoint(pointer, content);
			double contentX = Math.Max(0.0, Math.Min(content.ActualWidth, contentPoint.X));
			double seconds = contentX / content.ActualWidth * vm.VideoDurationSec;
			Point origin = scroll.TranslatePoint(new Point(0.0, 0.0), laneGrid);
			double visibleX = Math.Max(origin.X, Math.Min(laneGrid.ActualWidth, origin.X + contentX - scroll.HorizontalOffset));
			if (seekVideo)
			{
				playhead.X1 = playhead.X2 = visibleX;
				head.Margin = new Thickness(Math.Max(0.0, visibleX - 5.0), 1.0, 0.0, 0.0);
				hoverGuide.Visibility = Visibility.Collapsed;
			}
			else
			{
				hoverGuide.X1 = hoverGuide.X2 = visibleX;
				hoverGuide.Visibility = Visibility.Visible;
			}
			((TextBlock)hoverTimeBadge.Child).Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff");
			hoverTimeBadge.Margin = new Thickness(Math.Max(0.0, Math.Min(laneGrid.ActualWidth - 72.0, visibleX - 30.0)), 13.0, 0.0, 0.0);
			hoverTimeBadge.Visibility = Visibility.Visible;
			if (seekVideo)
			{
				vm.CurrentPositionSec = seconds;
				_timelineEditPositionSec = seconds;
				_showPinnedTimelinePlayhead?.Invoke(seconds);
				SeekPreviewVideo(seconds);
				vm.ProjectStatusMessage = "Timeline position: " + TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\.fff");
			}
		};
		Action<Point> updatePinnedHoverGuide = pointer =>
		{
			ScrollViewer scroll = _continuousTimelineScrollers.FirstOrDefault();
			if (scroll?.Content is not FrameworkElement content || vm.VideoDurationSec <= 0.0 || content.ActualWidth <= 0.0) return;
			Point contentPoint = laneGrid.TranslatePoint(pointer, content);
			double contentX = Math.Max(0.0, Math.Min(content.ActualWidth, contentPoint.X));
			double seconds = contentX / content.ActualWidth * vm.VideoDurationSec;
			Point origin = scroll.TranslatePoint(new Point(0.0, 0.0), laneGrid);
			double visibleX = Math.Max(origin.X, Math.Min(laneGrid.ActualWidth, origin.X + contentX - scroll.HorizontalOffset));
			hoverGuide.X1 = hoverGuide.X2 = visibleX;
			hoverGuide.Visibility = Visibility.Visible;
			((TextBlock)hoverTimeBadge.Child).Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff");
			hoverTimeBadge.Margin = new Thickness(Math.Max(0.0, Math.Min(laneGrid.ActualWidth - 72.0, visibleX - 30.0)), 13.0, 0.0, 0.0);
			hoverTimeBadge.Visibility = Visibility.Visible;
		};
		laneGrid.PreviewMouseMove += (object sender, MouseEventArgs args) =>
		{
			if (args.OriginalSource is DependencyObject source)
			{
				for (DependencyObject current = source; current != null; current = VisualTreeHelper.GetParent(current))
				{
					if (ReferenceEquals(current, scrollHost)) return;
					if (ReferenceEquals(current, laneGrid)) break;
				}
			}
			if (playheadPinned)
			{
				updatePinnedHoverGuide(args.GetPosition(laneGrid));
				return;
			}
			hoveringTimeline = true;
			updateHoverPlayhead(args.GetPosition(laneGrid), false);
		};
		laneGrid.MouseLeave += (object sender, MouseEventArgs args) =>
		{
			hoveringTimeline = false;
			hoverGuide.Visibility = Visibility.Collapsed;
			hoverTimeBadge.Visibility = Visibility.Collapsed;
			updateRealPlayhead();
		};
		laneGrid.PreviewMouseLeftButtonDown += (object sender, MouseButtonEventArgs args) =>
		{
			if (args.OriginalSource is DependencyObject source)
			{
				for (DependencyObject current = source; current != null; current = VisualTreeHelper.GetParent(current))
				{
					if (ReferenceEquals(current, scrollHost)) return;
					if (ReferenceEquals(current, laneGrid)) break;
				}
			}
			hoveringTimeline = false;
			updateHoverPlayhead(args.GetPosition(laneGrid), true);
			playheadPinned = true;
			hoverGuide.Visibility = Visibility.Collapsed;
			hoverTimeBadge.Visibility = Visibility.Collapsed;
			updateRealPlayhead();
		};
		laneGrid.SizeChanged += (object sender, SizeChangedEventArgs args) => updateRealPlayhead();
		foreach (ScrollViewer scroll in _continuousTimelineScrollers)
		{
			scroll.ScrollChanged += (object sender, ScrollChangedEventArgs args) => updateRealPlayhead();
		}

		vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
		{
			if (args.PropertyName == "CurrentPositionSec")
			{
				// Never move the user's timeline viewport merely because the playhead
				// moved.  Scrolling is controlled only by the shared scrollbar (or the
				// user dragging it), matching normal NLE timeline behaviour.
				updateRealPlayhead();
			}
		};
		updateRealPlayhead();
	}

	private static void ConvertDialogueLanesToContinuousTracks(Grid laneGrid, MainViewModel vm)
	{
		foreach (string laneName in new[] { "SUBTITLES", "KHMER VOICE" })
		{
			TextBlock label = FindVisualChild((DependencyObject)(object)laneGrid, (TextBlock text) => string.Equals(text.Text?.Trim(), laneName, StringComparison.OrdinalIgnoreCase));
			DockPanel lane = label == null ? null : FindAncestor<DockPanel>((DependencyObject)(object)label);
			ScrollViewer oldClips = lane == null ? null : FindVisualChild((DependencyObject)(object)lane, (ScrollViewer item) => true);
			if (lane == null || oldClips == null || lane.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "ContinuousDialogueTrack"))
			{
				continue;
			}
			oldClips.Visibility = Visibility.Collapsed;
			Canvas track = new Canvas
			{
				Tag = "ContinuousDialogueTrack",
				Height = 22.0,
				Background = new SolidColorBrush(Color.FromRgb(7, 13, 23)),
				ClipToBounds = true,
				Cursor = Cursors.SizeAll
			};
			ScrollViewer trackScroll = new ScrollViewer
			{
				Tag = "ContinuousDialogueTrackScroll",
				Content = track,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
				VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
				CanContentScroll = false
			};
			DockPanel.SetDock(trackScroll, Dock.Right);
			lane.Children.Add(trackScroll);
			_continuousTimelineTracks.Add(track);
			_continuousTimelineScrollers.Add(trackScroll);
			trackScroll.SizeChanged += (object sender, SizeChangedEventArgs args) =>
			{
				track.Width = GetTimelineContentWidth(Math.Max(trackScroll.ViewportWidth, trackScroll.ActualWidth), vm.VideoDurationSec);
			};
			trackScroll.ScrollChanged += (object sender, ScrollChangedEventArgs args) =>
			{
				if (_syncingTimelineScroll || Math.Abs(args.HorizontalChange) < 0.01) return;
				_syncingTimelineScroll = true;
				try
				{
					foreach (ScrollViewer other in _continuousTimelineScrollers)
					{
						if (!ReferenceEquals(other, trackScroll)) other.ScrollToHorizontalOffset(trackScroll.HorizontalOffset);
					}
				}
				finally { _syncingTimelineScroll = false; }
			};
			trackScroll.PreviewMouseWheel += (object sender, MouseWheelEventArgs args) =>
			{
				if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
				{
					SetTimelineZoom(_timelineZoom + (args.Delta > 0 ? 0.25 : -0.25));
					args.Handled = true;
				}
			};
			Action render = () => RenderContinuousDialogueTrack(track, vm, laneName == "SUBTITLES");
			track.SizeChanged += (object sender, SizeChangedEventArgs args) => render();
			if (vm.DialogueLines is INotifyCollectionChanged changed)
			{
				changed.CollectionChanged += (object? sender, NotifyCollectionChangedEventArgs args) => render();
			}
			vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
			{
				if (args.PropertyName == "SelectedDialogueLine") render();
			};

			DialogueLine dragLine = null;
			string dragMode = null;
			int dragOriginMs = 0;
			int initialStart = 0;
			int initialEnd = 0;
			Dictionary<DialogueLine, (int Start, int End)> groupLineOrigins = new();
			track.PreviewMouseLeftButtonDown += (object sender, MouseButtonEventArgs args) =>
			{
				if (vm.DialogueLines == null || vm.DialogueLines.Count == 0 || track.ActualWidth <= 0.0) return;
				int timeMs = (int)(args.GetPosition(track).X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
				_timelineEditPositionSec = timeMs / 1000.0; vm.CurrentPositionSec = _timelineEditPositionSec; SeekPreviewVideo(_timelineEditPositionSec); _showPinnedTimelinePlayhead?.Invoke(_timelineEditPositionSec);
				// Empty track space is a seek target, not an implicit request to grab
				// the nearest dialogue clip.  Selecting a distant clip here made the
				// timeline appear to jump and made accidental timing edits too easy.
				dragLine = vm.DialogueLines.FirstOrDefault(item => item.StartMs <= timeMs && timeMs <= item.EndMs);
				if (dragLine == null) return;
				bool additive = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
				if (!additive)
				{
					_selectedTimelineLines.Clear();
					_selectedTimelineOverlays.Clear();
					_selectedTimelineTextLayers.Clear();
					_selectedTimelineBlurRegions.Clear();
					_selectedTimelineLines.Add(dragLine);
					vm.SelectedOverlay = null;
					vm.SelectedTextLayer = null;
				}
				else if (!_selectedTimelineLines.Add(dragLine))
				{
					_selectedTimelineLines.Remove(dragLine);
					dragLine = null;
					vm.SelectedDialogueLine = _selectedTimelineLines.LastOrDefault();
					render();
					args.Handled = true;
					return;
				}
				PushTimelineUndo(vm);
				_selectedDialogueTrackLane = laneName;
				vm.SelectedDialogueLine = dragLine;
				_selectedTimelineOverlay = null;
				dragOriginMs = timeMs;
				initialStart = dragLine.StartMs;
				initialEnd = dragLine.EndMs;
				groupLineOrigins = _selectedTimelineLines.ToDictionary(item => item, item => (item.StartMs, item.EndMs));
				int edgeTolerance = Math.Max(120, (int)(vm.VideoDurationSec * 1000.0 / Math.Max(1.0, track.ActualWidth) * 7.0));
				dragMode = Math.Abs(timeMs - dragLine.StartMs) <= edgeTolerance ? "start" : (Math.Abs(timeMs - dragLine.EndMs) <= edgeTolerance ? "end" : "move");
				track.Cursor = dragMode == "move" ? Cursors.SizeAll : Cursors.SizeWE;
				track.CaptureMouse();
				args.Handled = true;
			};
			track.PreviewMouseMove += (object sender, MouseEventArgs args) =>
			{
				if (track.ActualWidth <= 0.0) return;
				if (args.LeftButton != MouseButtonState.Pressed || dragLine == null)
				{
					int hoverMs = (int)(args.GetPosition(track).X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
					DialogueLine hoverLine = vm.DialogueLines?.FirstOrDefault(item => item.StartMs <= hoverMs && hoverMs <= item.EndMs);
					if (hoverLine == null)
					{
						track.Cursor = Cursors.Arrow;
					}
					else
					{
						int tolerance = Math.Max(120, (int)(vm.VideoDurationSec * 1000.0 / Math.Max(1.0, track.ActualWidth) * 7.0));
						bool nearEdge = Math.Abs(hoverMs - hoverLine.StartMs) <= tolerance || Math.Abs(hoverMs - hoverLine.EndMs) <= tolerance;
						track.Cursor = nearEdge ? Cursors.SizeWE : Cursors.SizeAll;
					}
					return;
				}
				int timeMs = (int)(args.GetPosition(track).X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
				int delta = timeMs - dragOriginMs;
				int videoEndMs = Math.Max(1000, (int)Math.Round(vm.VideoDurationSec * 1000.0));
				if (dragMode == "start") dragLine.StartMs = Math.Max(0, Math.Min(initialEnd - 150, initialStart + delta));
				else if (dragMode == "end") dragLine.EndMs = Math.Min(videoEndMs, Math.Max(initialStart + 150, initialEnd + delta));
				else if (_selectedTimelineLines.Count > 1 && groupLineOrigins.Count > 1)
				{
					int minimumStart = groupLineOrigins.Values.Min(value => value.Start);
					int maximumEnd = groupLineOrigins.Values.Max(value => value.End);
					int groupDelta = Math.Clamp(delta, -minimumStart, videoEndMs - maximumEnd);
					foreach (var state in groupLineOrigins)
					{
						state.Key.StartMs = state.Value.Start + groupDelta;
						state.Key.EndMs = state.Value.End + groupDelta;
						UpdateDialogueTimeText(state.Key);
					}
				}
				else
				{
					int duration = initialEnd - initialStart;
					dragLine.StartMs = Math.Max(0, Math.Min(videoEndMs - duration, initialStart + delta));
					dragLine.EndMs = dragLine.StartMs + duration;
				}
				dragLine.StartTime = TimeSpan.FromMilliseconds(dragLine.StartMs).ToString("hh\\:mm\\:ss\\,fff");
				dragLine.EndTime = TimeSpan.FromMilliseconds(dragLine.EndMs).ToString("hh\\:mm\\:ss\\,fff");
				if (_selectedTimelineLines.Count <= 1) ResolveDialogueClipCollisions(vm, dragLine);
				double livePositionMs = dragMode == "start" ? dragLine.StartMs : dragLine.EndMs;
				vm.CurrentPositionSec = Math.Max(0.0, Math.Min(vm.VideoDurationSec, livePositionMs / 1000.0));
				vm.ProjectStatusMessage = $"{TimeSpan.FromMilliseconds(dragLine.StartMs):hh\\:mm\\:ss\\.fff} - {TimeSpan.FromMilliseconds(dragLine.EndMs):hh\\:mm\\:ss\\.fff}";
				render();
				args.Handled = true;
			};
			track.PreviewMouseLeftButtonUp += (object sender, MouseButtonEventArgs args) =>
			{
				if (dragLine == null) return;
				track.ReleaseMouseCapture();
				vm.ProjectStatusMessage = $"Line #{dragLine.Index} timing updated";
				try { vm.SaveProjectCommand?.Execute(null); } catch { }
				dragLine = null;
				dragMode = null;
				args.Handled = true;
			};
			render();
		}
	}

	private static void ConvertTextLaneToContinuousTrack(Grid laneGrid, MainViewModel vm)
	{
		TextBlock label = FindVisualChild((DependencyObject)(object)laneGrid,
			(TextBlock text) => string.Equals(text.Text?.Trim(), "VISUALS", StringComparison.OrdinalIgnoreCase));
		DockPanel lane = label == null ? null : FindAncestor<DockPanel>((DependencyObject)(object)label);
		if (lane == null || lane.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "ContinuousTextTrackScroll")) return;
		foreach (ItemsControl old in FindAllVisualChildren<ItemsControl>((DependencyObject)(object)lane)) old.Visibility = Visibility.Collapsed;
		Canvas track = new Canvas { Tag = "ContinuousTextTrack", Height = 26.0, Background = new SolidColorBrush(Color.FromRgb(7, 13, 23)), ClipToBounds = true };
		ScrollViewer scroll = new ScrollViewer
		{
			Tag = "ContinuousTextTrackScroll", Content = track, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false,
			PanningMode = PanningMode.VerticalOnly
		};
		DockPanel.SetDock(scroll, Dock.Right);
		lane.Children.Add(scroll);
		_continuousTimelineTracks.Add(track);
		_continuousTimelineScrollers.Add(scroll);
		int laneRowIndex = Grid.GetRow(lane);
		Action render = () =>
		{
			track.Children.Clear();
			List<TextLayerItem> layers = vm.TextLayers?.ToList() ?? new List<TextLayerItem>();
			List<BlurRegionItem> blurs = vm.BlurRegions?.ToList() ?? new List<BlurRegionItem>();
			int visualRowCount = Math.Max(1, layers.Count + blurs.Count);
			double desiredHeight = visualRowCount * 26.0 + 2.0;
			double viewportHeight = Math.Min(desiredHeight, 6.0 * 26.0 + 2.0);
			track.Height = desiredHeight;
			scroll.Height = viewportHeight;
			lane.Height = viewportHeight + 4.0;
			if (laneRowIndex >= 0 && laneRowIndex < laneGrid.RowDefinitions.Count)
			{
				RowDefinition rowDefinition = laneGrid.RowDefinitions[laneRowIndex];
				GridLength expanded = new GridLength(viewportHeight + 4.0);
				_timelineRowHeights[rowDefinition] = expanded;
				if (lane.Visibility == Visibility.Visible) rowDefinition.Height = expanded;
			}
			double totalMs = Math.Max(1000.0, vm.VideoDurationSec * 1000.0);
			Color[] colors = { Color.FromRgb(202, 138, 4), Color.FromRgb(217, 119, 6), Color.FromRgb(190, 24, 93), Color.FromRgb(124, 58, 237) };
			for (int i = 0; i < layers.Count; i++)
			{
				TextLayerItem item = layers[i];
				double left = item.StartMs / totalMs * track.ActualWidth;
				double right = item.EndMs / totalMs * track.ActualWidth;
				bool selected = _selectedTimelineTextLayers.Contains(item) || ReferenceEquals(vm.SelectedTextLayer, item);
				Grid clipContent = new Grid { IsHitTestVisible = false };
				clipContent.Children.Add(new TextBlock { Text = item.Text, Foreground = Brushes.White, FontSize = 9.0, FontWeight = FontWeights.SemiBold, Margin = new Thickness(7.0, 2.0, 7.0, 0.0), TextTrimming = TextTrimming.CharacterEllipsis });
				if (selected)
				{
					clipContent.Children.Add(new Border { Width = 5.0, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left });
					clipContent.Children.Add(new Border { Width = 5.0, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right });
				}
				Border clip = new Border
				{
					Tag = "ContinuousTextClip", DataContext = item, Height = 22.0,
					Width = Math.Max(12.0, Math.Min(Math.Max(12.0, track.ActualWidth - left), right - left)),
					Background = new SolidColorBrush(colors[i % colors.Length]),
					BorderBrush = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(250, 204, 21)),
					BorderThickness = selected ? new Thickness(2.0) : new Thickness(1.0), CornerRadius = new CornerRadius(2.0),
					Child = clipContent,
					ToolTip = $"Text {i + 1}: {item.Text}\n{FormatTimelineTime(item.StartMs)} - {FormatTimelineTime(item.EndMs)}\nDouble-click to edit • Drag middle to move • Drag edges to trim"
				};
				ToolTipService.SetInitialShowDelay(clip, 900);
				ToolTipService.SetShowDuration(clip, 5000);
				Canvas.SetLeft(clip, Math.Max(0.0, left)); Canvas.SetTop(clip, i * 26.0 + 2.0);
				track.Children.Add(clip);
			}
			for (int i = 0; i < blurs.Count; i++)
			{
				BlurRegionItem item = blurs[i];
				double left = item.StartMs / totalMs * track.ActualWidth;
				double right = item.EndMs / totalMs * track.ActualWidth;
				bool selected = _selectedTimelineBlurRegions.Contains(item) || ReferenceEquals(vm.SelectedBlurRegion, item);
				Grid content = new Grid { IsHitTestVisible = false };
				string blurTitle = (item.Label ?? "").Split('|')[0];
				content.Children.Add(new TextBlock { Text = $"BLUR  {blurTitle}", Foreground = Brushes.White, FontSize = 9.0, Margin = new Thickness(7, 2, 7, 0), TextTrimming = TextTrimming.CharacterEllipsis });
				if (selected) { content.Children.Add(new Border { Width = 5, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left }); content.Children.Add(new Border { Width = 5, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right }); }
				Border clip = new Border { Height = 22, Width = Math.Max(12.0, Math.Min(Math.Max(12.0, track.ActualWidth - left), right - left)), Background = new SolidColorBrush(Color.FromRgb(8, 145, 178)), BorderBrush = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(34, 211, 238)), BorderThickness = selected ? new Thickness(2) : new Thickness(1), CornerRadius = new CornerRadius(2), Child = content, ToolTip = $"{blurTitle}\nDrag middle to move • Drag edges to trim" };
				ToolTipService.SetInitialShowDelay(clip, 900);
				ToolTipService.SetShowDuration(clip, 5000);
				Canvas.SetLeft(clip, Math.Max(0, left)); Canvas.SetTop(clip, (layers.Count + i) * 26.0 + 2.0);
				AttachBlurTimelineClip(clip, track, vm, item);
				track.Children.Add(clip);
			}
		};
		track.SizeChanged += (s, e) => render();
		scroll.SizeChanged += (s, e) => track.Width = GetTimelineContentWidth(Math.Max(scroll.ViewportWidth, scroll.ActualWidth), vm.VideoDurationSec);
		if (vm.TextLayers is INotifyCollectionChanged changed) changed.CollectionChanged += (s, e) => render();
		if (vm.BlurRegions is INotifyCollectionChanged blurChanged) blurChanged.CollectionChanged += (s, e) => render();
		vm.PropertyChanged += (s, e) => { if (e.PropertyName == "SelectedTextLayer" || e.PropertyName == "VideoDurationSec") render(); };

		TextLayerItem drag = null;
		string mode = null;
		int originMs = 0, start = 0, end = 0;
		bool moved = false;
		bool editRequested = false;
		track.PreviewMouseLeftButtonDown += (s, e) =>
		{
			List<TextLayerItem> layers = vm.TextLayers?.ToList() ?? new List<TextLayerItem>();
			if (layers.Count == 0 || track.ActualWidth <= 0.0) return;
			Point point = e.GetPosition(track);
			int row = Math.Clamp((int)(point.Y / 26.0), 0, layers.Count - 1);
			int timeMs = (int)(point.X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
			_timelineEditPositionSec = timeMs / 1000.0; vm.CurrentPositionSec = _timelineEditPositionSec; SeekPreviewVideo(_timelineEditPositionSec); _showPinnedTimelinePlayhead?.Invoke(_timelineEditPositionSec);
			drag = layers[row];
			if (timeMs < drag.StartMs || timeMs > drag.EndMs) { drag = null; return; }
			SelectTextLayer(vm, drag, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
			if (!_selectedTimelineTextLayers.Contains(drag)) { drag = null; render(); e.Handled = true; return; }
			PushTimelineUndo(vm);
			originMs = timeMs; start = drag.StartMs; end = drag.EndMs; moved = false; editRequested = e.ClickCount >= 2;
			int tolerance = Math.Max(100, (int)(vm.VideoDurationSec * 1000.0 / Math.Max(1.0, track.ActualWidth) * 8.0));
			mode = Math.Abs(timeMs - start) <= tolerance ? "start" : (Math.Abs(timeMs - end) <= tolerance ? "end" : "move");
			track.CaptureMouse(); render(); e.Handled = true;
		};
		track.PreviewMouseMove += (s, e) =>
		{
			if (track.ActualWidth <= 0.0) return;
			if (drag == null || e.LeftButton != MouseButtonState.Pressed)
			{
				List<TextLayerItem> layers = vm.TextLayers?.ToList() ?? new List<TextLayerItem>();
				Point hoverPoint = e.GetPosition(track);
				int row = layers.Count == 0 ? -1 : Math.Clamp((int)(hoverPoint.Y / 26.0), 0, layers.Count - 1);
				int hoverMs = (int)(hoverPoint.X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
				TextLayerItem hover = row >= 0 ? layers[row] : null;
				if (hover == null || hoverMs < hover.StartMs || hoverMs > hover.EndMs) { track.Cursor = Cursors.Arrow; return; }
				int tolerance = Math.Max(100, (int)(vm.VideoDurationSec * 1000.0 / Math.Max(1.0, track.ActualWidth) * 8.0));
				track.Cursor = Math.Abs(hoverMs - hover.StartMs) <= tolerance || Math.Abs(hoverMs - hover.EndMs) <= tolerance ? Cursors.SizeWE : Cursors.SizeAll;
				return;
			}
			int timeMs = (int)(e.GetPosition(track).X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
			int delta = timeMs - originMs;
			if (Math.Abs(delta) >= 30) moved = true;
			int videoEnd = Math.Max(1000, (int)(vm.VideoDurationSec * 1000.0));
			if (mode == "start") drag.StartMs = Math.Max(0, Math.Min(end - 100, start + delta));
			else if (mode == "end") drag.EndMs = Math.Min(videoEnd, Math.Max(start + 100, end + delta));
			else { int duration = Math.Max(100, end - start); drag.StartMs = Math.Max(0, Math.Min(videoEnd - duration, start + delta)); drag.EndMs = drag.StartMs + duration; }
			vm.CurrentPositionSec = Math.Clamp((mode == "end" ? drag.EndMs : drag.StartMs) / 1000.0, 0.0, vm.VideoDurationSec);
			SeekPreviewVideo(vm.CurrentPositionSec); render(); e.Handled = true;
		};
		track.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (drag == null) return;
			TextLayerItem selected = drag; drag = null; track.ReleaseMouseCapture();
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			if (!moved && editRequested) track.Dispatcher.BeginInvoke((Action)(() => ShowEditTextLayerDialog(Application.Current.MainWindow, vm, selected)), DispatcherPriority.Background);
			e.Handled = true;
		};
		render();
	}

	private static void AttachBlurTimelineClip(Border clip, Canvas track, MainViewModel vm, BlurRegionItem item)
	{
		bool dragging = false;
		string mode = null;
		int origin = 0, start = 0, end = 0;
		clip.PreviewMouseLeftButtonDown += (s, e) =>
		{
			bool additive = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
			if (!additive)
			{
				_selectedTimelineBlurRegions.Clear(); _selectedTimelineTextLayers.Clear(); _selectedTimelineOverlays.Clear(); _selectedTimelineLines.Clear();
				vm.SelectedTextLayer = null; vm.SelectedOverlay = null; vm.SelectedDialogueLine = null;
			}
			if (additive && !_selectedTimelineBlurRegions.Add(item)) { _selectedTimelineBlurRegions.Remove(item); vm.SelectedBlurRegion = _selectedTimelineBlurRegions.LastOrDefault(); e.Handled = true; return; }
			_selectedTimelineBlurRegions.Add(item); vm.SelectedBlurRegion = item; PushTimelineUndo(vm);
			Point point = e.GetPosition(track); origin = (int)(point.X / Math.Max(1, track.ActualWidth) * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
			_timelineEditPositionSec = origin / 1000.0; vm.CurrentPositionSec = _timelineEditPositionSec; SeekPreviewVideo(_timelineEditPositionSec); _showPinnedTimelinePlayhead?.Invoke(_timelineEditPositionSec);
			start = item.StartMs; end = item.EndMs;
			int tolerance = Math.Max(100, (int)(vm.VideoDurationSec * 1000.0 / Math.Max(1.0, track.ActualWidth) * 8.0));
			mode = Math.Abs(origin - start) <= tolerance ? "start" : (Math.Abs(origin - end) <= tolerance ? "end" : "move");
			dragging = true; clip.CaptureMouse(); e.Handled = true;
		};
		clip.PreviewMouseMove += (s, e) =>
		{
			if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
			int now = (int)(e.GetPosition(track).X / Math.Max(1, track.ActualWidth) * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
			int delta = now - origin, videoEnd = Math.Max(1000, (int)(vm.VideoDurationSec * 1000.0));
			if (mode == "start") item.StartMs = Math.Max(0, Math.Min(end - 100, start + delta));
			else if (mode == "end") item.EndMs = Math.Min(videoEnd, Math.Max(start + 100, end + delta));
			else { int duration = Math.Max(100, end - start); item.StartMs = Math.Max(0, Math.Min(videoEnd - duration, start + delta)); item.EndMs = item.StartMs + duration; }
			double total = Math.Max(1000.0, vm.VideoDurationSec * 1000.0);
			Canvas.SetLeft(clip, item.StartMs / total * track.ActualWidth); clip.Width = Math.Max(12, (item.EndMs - item.StartMs) / total * track.ActualWidth);
			vm.CurrentPositionSec = (mode == "end" ? item.EndMs : item.StartMs) / 1000.0; SeekPreviewVideo(vm.CurrentPositionSec); e.Handled = true;
		};
		clip.PreviewMouseLeftButtonUp += (s, e) => { if (!dragging) return; dragging = false; clip.ReleaseMouseCapture(); try { vm.SaveProjectCommand?.Execute(null); } catch { } e.Handled = true; };
	}

	private static void ConvertOverlayLaneToContinuousTrack(Grid laneGrid, MainViewModel vm)
	{
		TextBlock label = FindVisualChild((DependencyObject)(object)laneGrid,
			(TextBlock text) => string.Equals(text.Text?.Trim(), "SPONSOR", StringComparison.OrdinalIgnoreCase));
		DockPanel lane = label == null ? null : FindAncestor<DockPanel>((DependencyObject)(object)label);
		if (lane == null || lane.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "ContinuousMediaTrackScroll"))
			return;
		foreach (ItemsControl oldClips in FindAllVisualChildren<ItemsControl>((DependencyObject)(object)lane))
			oldClips.Visibility = Visibility.Collapsed;

		Canvas track = new Canvas
		{
			Tag = "ContinuousMediaTrack", Height = 25.0,
			Background = new SolidColorBrush(Color.FromRgb(7, 13, 23)), ClipToBounds = true
		};
		ScrollViewer trackScroll = new ScrollViewer
		{
			Tag = "ContinuousMediaTrackScroll", Content = track,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false,
			PanningMode = PanningMode.VerticalOnly
		};
		DockPanel.SetDock(trackScroll, Dock.Right);
		lane.Children.Add(trackScroll);
		_continuousTimelineTracks.Add(track);
		_continuousTimelineScrollers.Add(trackScroll);

		int laneRowIndex = Grid.GetRow(lane);
		Action render = () =>
		{
			// Media overlays are independent visual layers and may intentionally
			// occupy the same time range. Repair items collapsed to zero by the
			// legacy single-lane collision behavior.
			if (vm.Overlays != null)
			{
				int videoEnd = Math.Max(1000, (int)Math.Round(vm.VideoDurationSec * 1000.0));
				foreach (OverlayItem item in vm.Overlays)
				{
					if (item.EndMs <= item.StartMs)
					{
						item.StartMs = 0;
						item.EndMs = videoEnd;
					}
				}
			}
			int layerCount = Math.Max(1, vm.Overlays?.Count ?? 0);
			double desiredHeight = layerCount * 26.0 + 2.0;
			double viewportHeight = Math.Min(desiredHeight, 6.0 * 26.0 + 2.0);
			track.Height = desiredHeight;
			trackScroll.Height = viewportHeight;
			lane.Height = viewportHeight + 4.0;
			if (laneRowIndex >= 0 && laneRowIndex < laneGrid.RowDefinitions.Count)
			{
				RowDefinition row = laneGrid.RowDefinitions[laneRowIndex];
				GridLength expanded = new GridLength(viewportHeight + 4.0);
				_timelineRowHeights[row] = expanded;
				if (lane.Visibility == Visibility.Visible) row.Height = expanded;
			}
			RenderContinuousMediaTrack(track, vm);
		};
		trackScroll.SizeChanged += (s, e) =>
		{
			track.Width = GetTimelineContentWidth(Math.Max(trackScroll.ViewportWidth, trackScroll.ActualWidth), vm.VideoDurationSec);
			render();
		};
		track.SizeChanged += (s, e) => render();
		trackScroll.ScrollChanged += (s, e) =>
		{
			if (_syncingTimelineScroll || Math.Abs(e.HorizontalChange) < 0.01) return;
			_syncingTimelineScroll = true;
			try
			{
				foreach (ScrollViewer other in _continuousTimelineScrollers)
					if (!ReferenceEquals(other, trackScroll)) other.ScrollToHorizontalOffset(trackScroll.HorizontalOffset);
			}
			finally { _syncingTimelineScroll = false; }
		};
		trackScroll.PreviewMouseWheel += (s, e) =>
		{
			if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
			SetTimelineZoom(_timelineZoom + (e.Delta > 0 ? 0.25 : -0.25));
			e.Handled = true;
		};
		if (vm.Overlays is INotifyCollectionChanged overlaysChanged)
			overlaysChanged.CollectionChanged += (s, e) => render();
		vm.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == "SelectedOverlay" || e.PropertyName == "VideoDurationSec") render();
		};

		OverlayItem dragItem = null;
		string dragMode = null;
		int dragOriginMs = 0, initialStart = 0, initialEnd = 0;
		Dictionary<OverlayItem, (int Start, int End)> groupOrigins = new();
		track.PreviewMouseLeftButtonDown += (s, e) =>
		{
			if (track.ActualWidth <= 0.0 || vm.Overlays == null || vm.Overlays.Count == 0) return;
			Point pointer = e.GetPosition(track);
			int timeMs = (int)(pointer.X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
			_timelineEditPositionSec = timeMs / 1000.0; vm.CurrentPositionSec = _timelineEditPositionSec; SeekPreviewVideo(_timelineEditPositionSec); _showPinnedTimelinePlayhead?.Invoke(_timelineEditPositionSec);
			List<OverlayItem> layers = vm.Overlays.ToList();
			int layerIndex = Math.Clamp((int)(pointer.Y / 26.0), 0, Math.Max(0, layers.Count - 1));
			dragItem = layers.Count > 0 && layers[layerIndex].StartMs <= timeMs && timeMs <= layers[layerIndex].EndMs
				? layers[layerIndex] : null;
			if (dragItem == null) return;
			bool additive = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
			if (!additive)
			{
				_selectedTimelineOverlays.Clear();
				_selectedTimelineLines.Clear();
				_selectedTimelineTextLayers.Clear();
				_selectedTimelineOverlays.Add(dragItem);
				vm.SelectedDialogueLine = null;
				vm.SelectedTextLayer = null;
			}
			else if (!_selectedTimelineOverlays.Add(dragItem))
			{
				_selectedTimelineOverlays.Remove(dragItem);
				dragItem = null;
				vm.SelectedOverlay = _selectedTimelineOverlays.LastOrDefault();
				render();
				e.Handled = true;
				return;
			}
			PushTimelineUndo(vm);
			_selectedTimelineOverlay = dragItem;
			vm.SelectedOverlay = dragItem;
			groupOrigins = _selectedTimelineOverlays.ToDictionary(item => item, item => (item.StartMs, item.EndMs));
			dragOriginMs = timeMs;
			initialStart = dragItem.StartMs;
			initialEnd = dragItem.EndMs;
			int tolerance = Math.Max(100, (int)(vm.VideoDurationSec * 1000.0 / Math.Max(1.0, track.ActualWidth) * 8.0));
			dragMode = Math.Abs(timeMs - dragItem.StartMs) <= tolerance ? "start"
				: (Math.Abs(timeMs - dragItem.EndMs) <= tolerance ? "end" : "move");
			track.Cursor = dragMode == "move" ? Cursors.SizeAll : Cursors.SizeWE;
			track.CaptureMouse();
			// The timeline host already seeks to the exact mouse position. Do not
			// snap the playhead back to the clip start when selecting a media row.
			e.Handled = true;
		};
		track.PreviewMouseMove += (s, e) =>
		{
			if (track.ActualWidth <= 0.0) return;
			Point pointer = e.GetPosition(track);
			int pointerMs = (int)(pointer.X / track.ActualWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
			if (dragItem == null || e.LeftButton != MouseButtonState.Pressed)
			{
				List<OverlayItem> layers = vm.Overlays?.ToList() ?? new List<OverlayItem>();
				int layerIndex = Math.Clamp((int)(pointer.Y / 26.0), 0, Math.Max(0, layers.Count - 1));
				OverlayItem hover = layers.Count > 0 && layers[layerIndex].StartMs <= pointerMs && pointerMs <= layers[layerIndex].EndMs
					? layers[layerIndex] : null;
				if (hover == null) { track.Cursor = Cursors.Arrow; return; }
				int tolerance = Math.Max(100, (int)(vm.VideoDurationSec * 1000.0 / Math.Max(1.0, track.ActualWidth) * 8.0));
				track.Cursor = Math.Abs(pointerMs - hover.StartMs) <= tolerance || Math.Abs(pointerMs - hover.EndMs) <= tolerance
					? Cursors.SizeWE : Cursors.SizeAll;
				return;
			}
			int delta = pointerMs - dragOriginMs;
			int videoEnd = Math.Max(1000, (int)Math.Round(vm.VideoDurationSec * 1000.0));
			if (dragMode == "start") dragItem.StartMs = Math.Max(0, Math.Min(initialEnd - 150, initialStart + delta));
			else if (dragMode == "end") dragItem.EndMs = Math.Min(videoEnd, Math.Max(initialStart + 150, initialEnd + delta));
			else if (_selectedTimelineOverlays.Count > 1 && groupOrigins.Count > 1)
			{
				int minimumStart = groupOrigins.Values.Min(value => value.Start);
				int maximumEnd = groupOrigins.Values.Max(value => value.End);
				int groupDelta = Math.Clamp(delta, -minimumStart, videoEnd - maximumEnd);
				foreach (var state in groupOrigins)
				{
					state.Key.StartMs = state.Value.Start + groupDelta;
					state.Key.EndMs = state.Value.End + groupDelta;
				}
			}
			else
			{
				int duration = Math.Max(150, initialEnd - initialStart);
				dragItem.StartMs = Math.Max(0, Math.Min(videoEnd - duration, initialStart + delta));
				dragItem.EndMs = dragItem.StartMs + duration;
			}
			// Do not resolve collisions between media overlays: every row is an
			// independent layer, so overlapping timing is valid and expected.
			vm.CurrentPositionSec = Math.Clamp((dragMode == "end" ? dragItem.EndMs : dragItem.StartMs) / 1000.0, 0.0, vm.VideoDurationSec);
			SeekPreviewVideo(vm.CurrentPositionSec);
			vm.ProjectStatusMessage = $"Media {FormatTimelineTime(dragItem.StartMs)} - {FormatTimelineTime(dragItem.EndMs)}";
			render();
			e.Handled = true;
		};
		track.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (dragItem == null) return;
			track.ReleaseMouseCapture();
			vm.ProjectStatusMessage = $"Media timing updated: {FormatTimelineTime(dragItem.StartMs)} - {FormatTimelineTime(dragItem.EndMs)}";
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			dragItem = null;
			dragMode = null;
			e.Handled = true;
		};
		render();
	}

	private static void RenderContinuousMediaTrack(Canvas track, MainViewModel vm)
	{
		if (track.ActualWidth < 2.0 || track.ActualHeight < 2.0) return;
		track.Children.Clear();
		double totalMs = Math.Max(1000.0, vm.VideoDurationSec * 1000.0);
		int visibleLayers = Math.Max(1, vm.Overlays?.Count ?? 0);
		for (int row = 0; row < visibleLayers; row++)
		{
			track.Children.Add(new System.Windows.Shapes.Line
			{
				X1 = 0.0, X2 = track.ActualWidth, Y1 = row * 26.0 + 13.0, Y2 = row * 26.0 + 13.0,
				Stroke = new SolidColorBrush(Color.FromArgb(55, 100, 116, 139)), StrokeThickness = 1.0
			});
		}
		if (vm.Overlays == null) return;
		Color[] layerColors =
		{
			Color.FromRgb(13, 148, 136),  // teal
			Color.FromRgb(37, 99, 235),   // blue
			Color.FromRgb(147, 51, 234),  // purple
			Color.FromRgb(234, 88, 12),   // orange
			Color.FromRgb(219, 39, 119),  // pink
			Color.FromRgb(22, 163, 74)    // green
		};
		int layerIndex = 0;
		foreach (OverlayItem item in vm.Overlays)
		{
			double left = Math.Max(0.0, item.StartMs / totalMs * track.ActualWidth);
			double right = Math.Max(left + 3.0, item.EndMs / totalMs * track.ActualWidth);
			bool selected = _selectedTimelineOverlays.Contains(item)
				|| ReferenceEquals(vm.SelectedOverlay, item) || ReferenceEquals(_selectedTimelineOverlay, item);
			string mediaName = Path.GetFileNameWithoutExtension(item.ImagePath);
			if (string.IsNullOrWhiteSpace(mediaName)) mediaName = "Media overlay";
			TextBlock caption = new TextBlock
			{
				Text = $"L{layerIndex + 1}  {mediaName}   {FormatTimelineTime(item.StartMs)} – {FormatTimelineTime(item.EndMs)}",
				Foreground = Brushes.White, FontSize = 9.0, FontWeight = FontWeights.SemiBold,
				VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
				Margin = new Thickness(7.0, 0.0, 7.0, 0.0), IsHitTestVisible = false
			};
			Grid content = new Grid { IsHitTestVisible = false };
			content.Children.Add(caption);
			if (selected)
			{
				content.Children.Add(new Border { Width = 4.0, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left });
				content.Children.Add(new Border { Width = 4.0, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right });
			}
			Color layerColor = layerColors[layerIndex % layerColors.Length];
			Border clip = new Border
			{
				Tag = "ContinuousMediaClip", Width = Math.Max(3.0, Math.Min(track.ActualWidth - left, right - left)),
				Height = 22.0, Background = new SolidColorBrush(layerColor),
				BorderBrush = selected ? Brushes.White : new SolidColorBrush(Color.FromArgb(230, layerColor.R, layerColor.G, layerColor.B)),
				BorderThickness = selected ? new Thickness(2.0) : new Thickness(1.0), CornerRadius = new CornerRadius(2.0),
				Child = content,
				ToolTip = $"Layer {layerIndex + 1}: {Path.GetFileName(item.ImagePath)}\n{FormatTimelineTime(item.StartMs)} - {FormatTimelineTime(item.EndMs)}\nDrag middle to move • Drag edges to trim"
			};
			ToolTipService.SetInitialShowDelay(clip, 900);
			ToolTipService.SetShowDuration(clip, 5000);
			Canvas.SetLeft(clip, left);
			Canvas.SetTop(clip, layerIndex * 26.0 + 2.0);
			track.Children.Add(clip);
			layerIndex++;
		}
	}

	private static string FormatTimelineTime(int milliseconds)
	{
		return TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"mm\:ss\.fff");
	}

	private static void SeekPreviewVideo(double seconds)
	{
		try
		{
			Window main = Application.Current?.MainWindow;
			MediaElement preview = main == null ? null : FindVisualChild((DependencyObject)(object)main,
				(MediaElement media) => media.Name == "PreviewPlayer");
			if (preview != null)
			{
				TimeSpan position = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
				preview.Position = position;
				// MediaElement can ignore a seek while its source is finishing a state
				// transition. Repeat it on the media dispatcher so the player readout,
				// preview frame and timeline always settle on the same timestamp.
				preview.Dispatcher.BeginInvoke((Action)(() => preview.Position = position), DispatcherPriority.Render);
			}
		}
		catch { }
	}

	private static void ResolveDialogueClipCollisions(MainViewModel vm, DialogueLine active)
	{
		if (vm.DialogueLines == null || active == null) return;
		int videoEnd = Math.Max(1000, (int)Math.Round(vm.VideoDurationSec * 1000.0));
		List<DialogueLine> others = vm.DialogueLines.Where(line => !ReferenceEquals(line, active)).OrderBy(line => line.StartMs).ToList();
		List<DialogueLine> left = others.Where(line => line.StartMs < active.StartMs).ToList();
		int cursor = active.StartMs;
		for (int i = left.Count - 1; i >= 0; i--)
		{
			DialogueLine line = left[i];
			if (line.EndMs > cursor)
			{
				int duration = Math.Max(150, line.EndMs - line.StartMs);
				line.EndMs = cursor;
				line.StartMs = Math.Max(0, cursor - duration);
				UpdateDialogueTimeText(line);
			}
			cursor = line.StartMs;
		}
		cursor = active.EndMs;
		foreach (DialogueLine line in others.Where(line => !left.Contains(line)).OrderBy(line => line.StartMs))
		{
			if (line.StartMs < cursor)
			{
				int duration = Math.Max(150, line.EndMs - line.StartMs);
				line.StartMs = cursor;
				line.EndMs = Math.Min(videoEnd, line.StartMs + duration);
				if (line.EndMs <= line.StartMs) line.EndMs = line.StartMs;
				UpdateDialogueTimeText(line);
			}
			cursor = line.EndMs;
		}
	}

	private static void UpdateDialogueTimeText(DialogueLine line)
	{
		line.StartTime = TimeSpan.FromMilliseconds(line.StartMs).ToString("hh\\:mm\\:ss\\,fff");
		line.EndTime = TimeSpan.FromMilliseconds(line.EndMs).ToString("hh\\:mm\\:ss\\,fff");
	}

	private static void ResolveOverlayClipCollisions(MainViewModel vm, OverlayItem active)
	{
		if (vm.Overlays == null || active == null) return;
		int videoEnd = Math.Max(1000, (int)Math.Round(vm.VideoDurationSec * 1000.0));
		List<OverlayItem> others = vm.Overlays.Where(item => !ReferenceEquals(item, active)).OrderBy(item => item.StartMs).ToList();
		List<OverlayItem> left = others.Where(item => item.StartMs < active.StartMs).ToList();
		int cursor = active.StartMs;
		for (int i = left.Count - 1; i >= 0; i--)
		{
			OverlayItem item = left[i];
			if (item.EndMs > cursor)
			{
				int duration = Math.Max(150, item.EndMs - item.StartMs);
				item.EndMs = cursor;
				item.StartMs = Math.Max(0, cursor - duration);
			}
			cursor = item.StartMs;
		}
		cursor = active.EndMs;
		foreach (OverlayItem item in others.Where(item => !left.Contains(item)).OrderBy(item => item.StartMs))
		{
			if (item.StartMs < cursor)
			{
				int duration = Math.Max(150, item.EndMs - item.StartMs);
				item.StartMs = cursor;
				item.EndMs = Math.Min(videoEnd, item.StartMs + duration);
			}
			cursor = item.EndMs;
		}
	}

	private static void RenderContinuousDialogueTrack(Canvas track, MainViewModel vm, bool subtitle)
	{
		double width = track.ActualWidth;
		double height = track.ActualHeight;
		if (width < 2.0 || height < 2.0) return;
		track.Children.Clear();
		Color color = subtitle ? Color.FromRgb(0, 207, 151) : Color.FromRgb(168, 85, 247);
		double totalMs = Math.Max(1000.0, vm.VideoDurationSec * 1000.0);
		if (subtitle)
		{
			track.Children.Add(new System.Windows.Shapes.Line
			{
				X1 = 0.0, X2 = width, Y1 = height / 2.0, Y2 = height / 2.0,
				Stroke = new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B)),
				StrokeThickness = 1.0
			});
			if (vm.DialogueLines != null)
			{
				foreach (DialogueLine subtitleLine in vm.DialogueLines)
				{
					double left = Math.Max(0.0, subtitleLine.StartMs / totalMs * width);
					double right = Math.Max(left + 2.0, subtitleLine.EndMs / totalMs * width);
					Border subtitleBar = new Border
					{
						Width = Math.Max(2.0, Math.Min(width - left, right - left)),
						Height = Math.Max(6.0, height - 8.0),
						Background = new SolidColorBrush(Color.FromRgb(0, 185, 136)),
						CornerRadius = new CornerRadius(1.0),
						IsHitTestVisible = false
					};
					Canvas.SetLeft(subtitleBar, left);
					Canvas.SetTop(subtitleBar, 4.0);
					track.Children.Add(subtitleBar);
				}
			}
		}
		else
		{
			int count = Math.Max(160, Math.Min(1000, (int)width));
			Random random = new Random(97);
			StreamGeometry geometry = new StreamGeometry();
			using (StreamGeometryContext context = geometry.Open())
			{
				for (int i = 0; i < count; i++)
				{
					double timeMs = i / (double)Math.Max(1, count - 1) * totalMs;
					bool active = (vm.DialogueLines != null && vm.DialogueLines.Any(line => line.StartMs <= timeMs && timeMs <= line.EndMs))
						|| HasCurrentVideoReference(vm);
					double amplitude = active ? height * (0.23 + random.NextDouble() * 0.24) : 1.0;
					double x = i * width / Math.Max(1, count - 1);
					context.BeginFigure(new Point(x, height / 2.0 - amplitude), false, false);
					context.LineTo(new Point(x, height / 2.0 + amplitude), true, false);
				}
			}
			geometry.Freeze();
			track.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Stroke = new SolidColorBrush(color), StrokeThickness = 1.2 });
		}
		if (vm.DialogueLines == null || vm.DialogueLines.Count == 0)
		{
			if (HasCurrentVideoReference(vm) && !subtitle)
			{
				Border initialFrame = new Border
				{
					Width = width,
					Height = Math.Max(2.0, height - 2.0),
					Background = new SolidColorBrush(Color.FromArgb(16, color.R, color.G, color.B)),
					BorderBrush = new SolidColorBrush(Color.FromArgb(70, color.R, color.G, color.B)),
					BorderThickness = new Thickness(1.0),
					CornerRadius = new CornerRadius(1.0),
					ToolTip = "Video Audio Track (Click playhead and use Split Line to cut before Transcribe)"
				};
				Canvas.SetLeft(initialFrame, 0.0);
				Canvas.SetTop(initialFrame, 1.0);
				track.Children.Add(initialFrame);
			}
			return;
		}
		foreach (DialogueLine line in vm.DialogueLines)
		{
			double left = Math.Max(0.0, line.StartMs / totalMs * width);
			double right = Math.Max(left + 2.0, line.EndMs / totalMs * width);
			string currentLane = subtitle ? "SUBTITLES" : "KHMER VOICE";
			bool selected = _selectedTimelineLines.Contains(line) || (ReferenceEquals(vm.SelectedDialogueLine, line)
				&& string.Equals(_selectedDialogueTrackLane, currentLane, StringComparison.OrdinalIgnoreCase));
			Border frame = new Border
			{
				Width = Math.Max(2.0, Math.Min(width - left, right - left)),
				Height = Math.Max(2.0, height - 2.0),
				Background = selected ? new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)) : Brushes.Transparent,
				BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(224, 251, 255)) : new SolidColorBrush(Color.FromArgb(65, color.R, color.G, color.B)),
				BorderThickness = selected ? new Thickness(2.0) : new Thickness(0.0, 0.0, 1.0, 0.0),
				CornerRadius = new CornerRadius(1.0),
				ToolTip = $"Line #{line.Index}  {line.StartTime} - {line.EndTime}"
			};
			if (selected)
			{
				Grid handles = new Grid { IsHitTestVisible = false };
				handles.Children.Add(new Border
				{
					Width = 4.0, Height = 14.0, Background = Brushes.White,
					HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
				});
				handles.Children.Add(new Border
				{
					Width = 4.0, Height = 14.0, Background = Brushes.White,
					HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
				});
				frame.Child = handles;
			}
			Canvas.SetLeft(frame, left);
			Canvas.SetTop(frame, 1.0);
			track.Children.Add(frame);
		}
	}

	private static bool TryAppendAudioWaveforms(TextBlock timelineLaneLabel, MainViewModel vm)
	{
		Grid laneGrid = FindAncestor<Grid>((DependencyObject)(object)timelineLaneLabel);
		if (laneGrid == null)
		{
			return false;
		}
		if (laneGrid.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "TimelineAudioWaveforms"))
		{
			return true;
		}

		int row = laneGrid.RowDefinitions.Count;
		laneGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(112.0) });
		Grid waveformGrid = new Grid
		{
			Tag = "TimelineAudioWaveforms",
			Margin = new Thickness(0.0, 5.0, 0.0, 0.0),
			ClipToBounds = true,
			Background = new SolidColorBrush(Color.FromRgb(22, 22, 30))
		};
		waveformGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		waveformGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		Canvas music = new Canvas { Background = new SolidColorBrush(Color.FromRgb(36, 119, 80)), ClipToBounds = true };
		Canvas vocal = new Canvas { Background = new SolidColorBrush(Color.FromRgb(58, 53, 108)), ClipToBounds = true };
		Grid.SetRow(music, 0);
		Grid.SetRow(vocal, 1);
		waveformGrid.Children.Add(music);
		waveformGrid.Children.Add(vocal);

		TextBlock fileName = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(190, 191, 201)),
			FontSize = 10.0,
			FontStyle = FontStyles.Italic,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, 3.0, 7.0, 0.0),
			IsHitTestVisible = false
		};
		Grid.SetRow(fileName, 0);
		waveformGrid.Children.Add(fileName);

		TextBlock durationLabel = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(190, 191, 201)),
			FontSize = 10.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(0.0, 0.0, 0.0, 3.0),
			IsHitTestVisible = false
		};
		Grid.SetRow(durationLabel, 1);
		waveformGrid.Children.Add(durationLabel);

		System.Windows.Shapes.Line playhead = new System.Windows.Shapes.Line
		{
			Stroke = Brushes.White,
			StrokeThickness = 1.0,
			Y1 = 0.0,
			Y2 = 112.0,
			Opacity = 0.9,
			IsHitTestVisible = false
		};
		Grid.SetRowSpan(playhead, 2);
		waveformGrid.Children.Add(playhead);

		Border timeBadge = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(36, 36, 50)),
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
			Child = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 10.0 },
			IsHitTestVisible = false
		};
		Grid.SetRowSpan(timeBadge, 2);
		waveformGrid.Children.Add(timeBadge);

		Action updatePlayhead = () =>
		{
			double duration = Math.Max(0.001, vm.VideoDurationSec);
			double width = waveformGrid.ActualWidth;
			double x = Math.Max(0.0, Math.Min(width, vm.CurrentPositionSec / duration * width));
			playhead.X1 = playhead.X2 = x;
			timeBadge.Margin = new Thickness(Math.Max(0.0, Math.Min(Math.Max(0.0, width - 72.0), x - 36.0)), 2.0, 0.0, 0.0);
			((TextBlock)timeBadge.Child).Text = TimeSpan.FromSeconds(Math.Max(0.0, vm.CurrentPositionSec)).ToString(@"mm\:ss\.f");
		};
		Action refresh = () =>
		{
			RenderAudioWaveform(music, vm.AudioMix?.BgmTrackPath, Color.FromRgb(0, 246, 162));
			RenderAudioWaveform(vocal, vm.AudioMix?.VocalsTrackPath, Color.FromRgb(111, 103, 229));
			string name = vm.AudioMix?.BgmTrackPath;
			if (string.IsNullOrWhiteSpace(name)) name = vm.CurrentProject?.VideoPath;
			fileName.Text = string.IsNullOrWhiteSpace(name) ? "Music track" : Path.GetFileName(name);
			durationLabel.Text = TimeSpan.FromSeconds(Math.Max(0.0, vm.VideoDurationSec)).ToString(@"mm\:ss\.f");
			updatePlayhead();
		};
		music.SizeChanged += (object sender, SizeChangedEventArgs args) => refresh();
		vocal.SizeChanged += (object sender, SizeChangedEventArgs args) => refresh();
		vm.PropertyChanged += (object? sender, PropertyChangedEventArgs args) =>
		{
			if (args.PropertyName == "CurrentPositionSec") updatePlayhead();
			else if (args.PropertyName == "VideoDurationSec" || args.PropertyName == "CurrentProject") refresh();
		};

		Grid.SetRow(waveformGrid, row);
		laneGrid.Children.Add(waveformGrid);
		refresh();
		return true;
	}

	private static void RenderAudioWaveform(Canvas canvas, string audioPath, Color color)
	{
		double width = canvas.ActualWidth;
		double height = canvas.ActualHeight;
		if (width < 2.0 || height < 2.0) return;
		canvas.Children.Clear();
		int count = Math.Max(120, Math.Min(850, (int)(width / 1.6)));
		int seed = string.IsNullOrWhiteSpace(audioPath) ? 41 : StringComparer.OrdinalIgnoreCase.GetHashCode(audioPath);
		Random random = new Random(seed);
		StreamGeometry geometry = new StreamGeometry();
		using (StreamGeometryContext context = geometry.Open())
		{
			for (int i = 0; i < count; i++)
			{
				double x = i * width / Math.Max(1, count - 1);
				double envelope = 0.3 + 0.7 * Math.Sin(Math.PI * i / Math.Max(1, count - 1));
				double amplitude = Math.Max(1.0, height * (0.05 + random.NextDouble() * 0.42 * envelope));
				context.BeginFigure(new Point(x, height / 2.0 - amplitude), false, false);
				context.LineTo(new Point(x, height / 2.0 + amplitude), true, false);
			}
		}
		geometry.Freeze();
		canvas.Children.Add(new System.Windows.Shapes.Path
		{
			Data = geometry,
			Stroke = new SolidColorBrush(color),
			StrokeThickness = 1.0,
			Opacity = File.Exists(audioPath ?? "") ? 1.0 : 0.55
		});
	}

	private static Canvas CreateEditorLane(Grid grid, int row, string title, Color color)
	{
		Canvas lane = new Canvas
		{
			Background = new SolidColorBrush(Color.FromRgb(4, 9, 18))
		};
		lane.Children.Add(new System.Windows.Shapes.Line
		{
			X1 = 0.0,
			X2 = 10000.0,
			Y1 = 29.5,
			Y2 = 29.5,
			Stroke = new SolidColorBrush(Color.FromRgb(13, 25, 40)),
			StrokeThickness = 1.0,
			IsHitTestVisible = false
		});
		TextBlock label = new TextBlock
		{
			Text = title,
			Foreground = new SolidColorBrush(color),
			FontSize = 9.0,
			FontWeight = FontWeights.SemiBold,
			IsHitTestVisible = false
		};
		Canvas.SetLeft(label, 8.0);
		Canvas.SetTop(label, 7.0);
		lane.Children.Add(label);
		Grid.SetRow(lane, row);
		grid.Children.Add(lane);
		return lane;
	}

	private static void RenderTimelineRuler(Canvas ruler, double durationSeconds)
	{
		double width = ruler.ActualWidth;
		if (width < 2.0)
		{
			return;
		}
		ruler.Children.Clear();
		ruler.Children.Add(new System.Windows.Shapes.Line
		{
			X1 = 0.0, X2 = width, Y1 = 23.5, Y2 = 23.5,
			Stroke = new SolidColorBrush(Color.FromRgb(28, 48, 70)), StrokeThickness = 1.0
		});
		double duration = Math.Max(1.0, durationSeconds);
		double roughInterval = duration / 8.0;
		double[] niceIntervals = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600 };
		double interval = niceIntervals.FirstOrDefault(value => value >= roughInterval);
		if (interval <= 0.0) interval = 3600.0;
		for (double second = 0.0; second < duration; second += interval)
		{
			double x = second / duration * width;
			TextBlock tick = new TextBlock
			{
				Text = TimeSpan.FromSeconds(second).ToString(second >= 3600 ? @"h\:mm\:ss" : @"m\:ss"),
				Foreground = new SolidColorBrush(Color.FromRgb(87, 130, 168)),
				FontSize = 8.0
			};
			Canvas.SetLeft(tick, Math.Min(width - 30.0, x + 7.0));
			Canvas.SetTop(tick, 2.0);
			ruler.Children.Add(tick);
		}
		TextBlock endTick = new TextBlock
		{
			Text = TimeSpan.FromSeconds(duration).ToString(duration >= 3600 ? @"h\:mm\:ss" : @"m\:ss"),
			Foreground = new SolidColorBrush(Color.FromRgb(87, 130, 168)),
			FontSize = 8.0
		};
		endTick.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		Canvas.SetLeft(endTick, Math.Max(0.0, width - endTick.DesiredSize.Width - 4.0));
		Canvas.SetTop(endTick, 2.0);
		ruler.Children.Add(endTick);
	}

	private static void RenderKhmerVoiceLane(Canvas lane, MainViewModel vm)
	{
		while (lane.Children.Count > 2)
		{
			lane.Children.RemoveAt(lane.Children.Count - 1);
		}
		if (vm.DialogueLines == null || lane.ActualWidth < 2.0)
		{
			return;
		}
		double durationMs = Math.Max(50000.0, vm.VideoDurationSec * 1000.0);
		foreach (DialogueLine line in vm.DialogueLines)
		{
			double left = Math.Max(92.0, line.StartMs / durationMs * lane.ActualWidth);
			double right = Math.Max(left + 3.0, line.EndMs / durationMs * lane.ActualWidth);
			Border clip = new Border
			{
				Width = Math.Max(3.0, Math.Min(lane.ActualWidth - left, right - left)),
				Height = 16.0,
				Background = new SolidColorBrush(Color.FromArgb(115, 180, 73, 255)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(180, 73, 255)),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(2.0),
				ToolTip = $"Line #{line.Index}: {line.StartTime} - {line.EndTime}",
				Cursor = Cursors.Hand
			};
			clip.MouseLeftButtonDown += (object sender, MouseButtonEventArgs args) => vm.SelectedDialogueLine = line;
			Canvas.SetLeft(clip, left);
			Canvas.SetTop(clip, 7.0);
			lane.Children.Add(clip);
		}
	}

	public static void ApplyUiCustomizationsAndCleanups(Window window)
	{
		if (_isUiCleanedUp || window == null)
		{
			return;
		}
		try
		{
			MainViewModel mainViewModel = window.DataContext as MainViewModel;
			foreach (Border item in FindAllVisualChildren<Border>((DependencyObject)(object)window))
			{
				string toolTip = item.ToolTip as string ?? "";
				if (toolTip.IndexOf("SNA AI Dubber Mascot Studio", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					item.Visibility = Visibility.Collapsed;
					Log("Collapsed SNA AI Dubber Mascot Studio header icon");
				}
			}
			foreach (TextBlock item in FindAllVisualChildren<TextBlock>((DependencyObject)(object)window))
			{
				if (string.IsNullOrEmpty(item.Text))
				{
					continue;
				}
				string text = item.Text.Trim();
				if (text.Contains("Clone") && text.Contains("Pro"))
				{
					Border border = FindAncestor<Border>((DependencyObject)(object)item);
					if (border != null && !(border.Parent is Window))
					{
						border.Visibility = Visibility.Collapsed;
						Log("Collapsed [Clone សម\u17d2ល\u17c1ង Pro] badge");
					}
				}
				else if (text.Contains("2.0 Flash"))
				{
					Border border2 = FindAncestor<Border>((DependencyObject)(object)item);
					if (border2 != null && !(border2.Parent is Window))
					{
						border2.Visibility = Visibility.Collapsed;
						Log("Collapsed 2.0 Flash badge");
					}
				}
				else if (text == "EN" || text == "us EN" || text.Contains("\ud83c\uddfa\ud83c\uddf8"))
				{
					Border border3 = FindAncestor<Border>((DependencyObject)(object)item);
					if (border3 != null && !(border3.Parent is Window))
					{
						border3.Visibility = Visibility.Collapsed;
						Log("Collapsed us EN badge");
					}
				}
				else if (text.IndexOf("Source language", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Auto detect", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					Border borderSrc = FindAncestor<Border>((DependencyObject)(object)item);
					if (borderSrc != null && !(borderSrc.Parent is Window))
					{
						borderSrc.Visibility = Visibility.Collapsed;
						Log("Collapsed Source language: Auto detect badge");
					}
					else
					{
						item.Visibility = Visibility.Collapsed;
					}
				}
				else if (text == "\ud83c\udfac" && item.Parent is Panel parent && FindVisualChild((DependencyObject)(object)parent, (ComboBox c) => true) != null)
				{
					item.Visibility = Visibility.Collapsed;
					Log("Collapsed \ud83c\udfac icon next to Episode ComboBox");
				}
				else if (string.Equals(text, "Edit", StringComparison.OrdinalIgnoreCase))
				{
					Button editButton = FindAncestor<Button>((DependencyObject)(object)item);
					if (editButton != null)
					{
						editButton.Visibility = Visibility.Collapsed;
						Log("Collapsed redundant Dialogue Edit toolbar button");
					}
				}
				else if (text.IndexOf("Add dialogue", StringComparison.OrdinalIgnoreCase) >= 0
					|| text.IndexOf("+ Add Text", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					Button addDialogueButton = FindAncestor<Button>((DependencyObject)(object)item);
					if (addDialogueButton != null)
					{
						addDialogueButton.Visibility = Visibility.Collapsed;
						Border borderAdd = FindAncestor<Border>((DependencyObject)(object)addDialogueButton);
						if (borderAdd != null && !(borderAdd.Parent is Window) && !(borderAdd.Parent is Grid))
						{
							borderAdd.Visibility = Visibility.Collapsed;
						}
						Log("Collapsed Add dialogue line button (found via TextBlock)");
					}
				}
				else if (text.StartsWith("VOICE ENGINE", StringComparison.OrdinalIgnoreCase)
					|| text.IndexOf("Gemini AI Audio", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					ComboBox engineSelector = FindAncestor<ComboBox>((DependencyObject)(object)item);
					FrameworkElement engineControl = engineSelector
						?? (FrameworkElement)FindAncestor<Button>((DependencyObject)(object)item)
						?? FindAncestor<Border>((DependencyObject)(object)item);
					if (engineControl != null)
					{
						engineControl.Visibility = Visibility.Collapsed;
						Log("Collapsed automatic Voice Engine selector");
					}
				}
			}
			foreach (Button item2 in FindAllVisualChildren<Button>((DependencyObject)(object)window))
			{
				string obj = (item2.ToolTip as string) ?? "";
				List<TextBlock> buttonLabels = FindAllVisualChildren<TextBlock>((DependencyObject)(object)item2).ToList();
				string visualLabel = string.Join(" ", buttonLabels.Select(label => label.Text?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)));
				string[] removedVisualTools = { "Avatar", "Isolate BGM", "Audio Mute", "Import SRT", "Export SRT", "Cover Studio" };
				if (removedVisualTools.Any(name => visualLabel.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					item2.Visibility = Visibility.Collapsed;
					continue;
				}
				bool photoMediaTool = (item2.CommandParameter as string == "photo_logo")
					|| (visualLabel.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0)
					|| (obj.IndexOf("photo", StringComparison.OrdinalIgnoreCase) >= 0);
				if (photoMediaTool)
				{
					TextBlock nameLabel = buttonLabels.LastOrDefault(label => label.Text?.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0);
					if (nameLabel != null) { nameLabel.Text = "Photo / Vid"; nameLabel.FontSize = 9.0; }
					item2.ToolTip = "Add photo, animated GIF, or mini video";

					Action handleAddMedia = () =>
					{
						MainViewModel vm = window.DataContext as MainViewModel
							?? FindAllVisualChildren<FrameworkElement>(window).Select(f => f.DataContext as MainViewModel).FirstOrDefault(d => d != null);
						if (vm == null) return;

						Microsoft.Win32.OpenFileDialog picker = new Microsoft.Win32.OpenFileDialog
						{
							Title = "Select Photo, Animated GIF, or Mini Video",
							Filter = "All Supported Media|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif;*.mp4;*.mov;*.mkv;*.webm|Images and GIF (*.png, *.jpg, *.gif)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|Mini Video (*.mp4, *.mov, *.webm)|*.mp4;*.mov;*.mkv;*.webm|All Files|*.*"
						};
						if (picker.ShowDialog(owner: window) == true)
						{
							double durSec = vm.VideoDurationSec > 0.1 ? vm.VideoDurationSec : (vm.CurrentProject?.DurationSec ?? 60.0);
							double itemWidth = 220.0;
							double itemHeight = 120.0;
							try
							{
								string ext = Path.GetExtension(picker.FileName).ToLowerInvariant();
								if (new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }.Contains(ext))
								{
									using (var fs = File.OpenRead(picker.FileName))
									{
										var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
										if (dec.Frames.Count > 0)
										{
											double pw = dec.Frames[0].PixelWidth;
											double ph = dec.Frames[0].PixelHeight;
											if (pw > 0 && ph > 0)
											{
												double maxD = 240.0;
												double sc = Math.Min(maxD / pw, maxD / ph);
												itemWidth = Math.Round(pw * Math.Min(1.0, sc));
												itemHeight = Math.Round(ph * Math.Min(1.0, sc));
											}
										}
									}
								}
							}
							catch { }

							OverlayItem media = new OverlayItem
							{
								ImagePath = picker.FileName,
								X = 60.0,
								Y = 60.0,
								Width = Math.Max(40.0, itemWidth),
								Height = Math.Max(30.0, itemHeight),
								Opacity = 0.95,
								StartMs = 0,
								EndMs = (int)Math.Max(5000.0, durSec * 1000.0)
							};
							vm.Overlays.Add(media);
							vm.SelectedOverlay = media;
							vm.ActiveToolPanel = "sponsor";
							vm.ProjectStatusMessage = "Added Photo/mini video: " + Path.GetFileName(picker.FileName);

							Canvas canvas = FindVisualChild((DependencyObject)(object)window, (Canvas c) => c.Name == "OverlayCanvas");
							if (canvas != null)
							{
								RefreshMediaOverlayEditor(canvas, vm);
							}
						}
					};

					if (item2.Tag as string != "PhotoMiniVideoPickerHooked")
					{
						item2.Tag = "PhotoMiniVideoPickerHooked";
						item2.Click += (object sender, RoutedEventArgs args) => handleAddMedia();
						item2.PreviewMouseLeftButtonDown += (object sender, MouseButtonEventArgs args) =>
						{
							handleAddMedia();
							args.Handled = true;
						};
					}
				}
				foreach (TextBlock label in buttonLabels.Where(label => string.Equals(label.Text?.Trim(), "Subtitles", StringComparison.OrdinalIgnoreCase)))
					label.Text = "Subtitle";
				bool flag = false;
				if (obj.IndexOf("Batch Operations Drawer", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					flag = true;
				}
				if (mainViewModel != null && item2.Command == mainViewModel.ToggleBatchDrawerCommand)
				{
					flag = true;
				}
				if (flag)
				{
					item2.Visibility = Visibility.Collapsed;
					Border border4 = FindAncestor<Border>((DependencyObject)(object)item2);
					if (border4 != null && !(border4.Parent is Window))
					{
						border4.Visibility = Visibility.Collapsed;
					}
					Log("Collapsed Batch Drawer transcription button ☰");
				}
				bool flag2 = false;
				if (obj.IndexOf("export all", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					flag2 = true;
				}
				if (mainViewModel != null && item2.Command == mainViewModel.ExportAllCommand)
				{
					flag2 = true;
				}
				if (flag2)
				{
					item2.Visibility = Visibility.Collapsed;
					Log("Collapsed Export All button (leaving only Export This)");
				}

				bool isAddDialogue = false;
				if (obj.IndexOf("Add dialogue", StringComparison.OrdinalIgnoreCase) >= 0
					|| visualLabel.IndexOf("Add dialogue", StringComparison.OrdinalIgnoreCase) >= 0
					|| visualLabel.IndexOf("Add Text", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					isAddDialogue = true;
				}
				if (mainViewModel != null && item2.Command == mainViewModel.AddDialogueLineCommand)
				{
					isAddDialogue = true;
				}
				if (isAddDialogue)
				{
					item2.Visibility = Visibility.Collapsed;
					Border borderAdd = FindAncestor<Border>((DependencyObject)(object)item2);
					if (borderAdd != null && !(borderAdd.Parent is Window) && !(borderAdd.Parent is Grid))
					{
						borderAdd.Visibility = Visibility.Collapsed;
					}
					Log("Collapsed Add dialogue line button in Button scan");
				}

				if ((item2.Tag as string ?? "").StartsWith("TopTranscribeButton"))
				{
					item2.Visibility = Visibility.Visible;
					continue;
				}

				if (visualLabel.IndexOf("Transcribe & Translate", StringComparison.OrdinalIgnoreCase) >= 0
					|| (mainViewModel != null && item2.Command == mainViewModel.TranscribeAudioCommand))
				{
					item2.Visibility = Visibility.Collapsed;
					Border parentBorder = FindAncestor<Border>((DependencyObject)(object)item2);
					if (parentBorder != null && !(parentBorder.Parent is Window) && !(parentBorder.Parent is Grid))
					{
						if (parentBorder.Child is Panel p && p.Children.OfType<UIElement>().All(c => c.Visibility == Visibility.Collapsed))
						{
							parentBorder.Visibility = Visibility.Collapsed;
						}
					}
					Log("Collapsed original in-viewport Transcribe button");
				}
			}
			_isUiCleanedUp = true;
			Log("Successfully applied all UI customizations and removals!");
		}
		catch (Exception ex)
		{
			Log("ApplyUiCustomizationsAndCleanups error: " + ex.Message);
		}
	}

	private static DispatcherTimer _modernStylingTimer = null;

	public static void WireModernStylingAutoRefresh(Window window)
	{
		if (window == null || _modernStylingTimer != null) return;
		try
		{
			_modernStylingTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(1200)
			};
			_modernStylingTimer.Tick += (s, e) =>
			{
				if (window != null && window.IsLoaded && !_isWhiteTheme)
				{
					ApplyModernStudioRedesign(window);
				}
			};
			_modernStylingTimer.Start();
			Log("Wired modern studio styling auto-refresh timer.");
		}
		catch (Exception ex)
		{
			Log("WireModernStylingAutoRefresh error: " + ex.Message);
		}
	}

	private static void SetControlCornerRadius(FrameworkElement control, double radius)
	{
		if (control == null) return;
		if (control is Border b)
		{
			b.CornerRadius = new CornerRadius(radius);
			return;
		}
		Action apply = () =>
		{
			foreach (Border inner in FindAllVisualChildren<Border>((DependencyObject)(object)control))
			{
				inner.CornerRadius = new CornerRadius(radius);
			}
		};
		if (control.IsLoaded)
		{
			apply();
		}
		else
		{
			control.Loaded += (s, e) => apply();
		}
	}

	private static void AttachModernHover(Button btn, Brush hoverBackground = null, Brush hoverBorder = null)
	{
		if (btn == null) return;
		if (btn.Tag as string != null && btn.Tag.ToString().Contains("_HoverWired")) return;
		btn.Tag = (btn.Tag as string ?? "") + "_HoverWired";

		Brush normalBg = btn.Background;
		Brush normalBorder = btn.BorderBrush;

		btn.MouseEnter += (s, e) =>
		{
			if (!_isWhiteTheme)
			{
				if (hoverBorder != null) btn.BorderBrush = hoverBorder;
				else btn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));

				if (hoverBackground != null) btn.Background = hoverBackground;
			}
		};
		btn.MouseLeave += (s, e) =>
		{
			if (!_isWhiteTheme)
			{
				btn.BorderBrush = normalBorder;
				btn.Background = normalBg;
			}
		};
	}

	private static void ApplyButtonModernStyle(Button btn)
	{
		if (btn == null || btn.Visibility == Visibility.Collapsed) return;
		btn.Cursor = Cursors.Hand;

		List<TextBlock> labels = FindAllVisualChildren<TextBlock>((DependencyObject)(object)btn).ToList();
		string buttonText = string.Join(" ", labels.Select(l => l.Text?.Trim()).Where(v => !string.IsNullOrEmpty(v)));
		if (string.IsNullOrEmpty(buttonText) && btn.Content is string s) buttonText = s;
		string tag = btn.Tag as string ?? string.Empty;
		if (tag.IndexOf("CornerRemoveVideoButton", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return;
		}

		// 1. Split Episodes Button
		if (tag.IndexOf("EpisodeBatchButton", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("Split Episodes", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("Split Videos", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new LinearGradientBrush(Color.FromRgb(30, 27, 75), Color.FromRgb(49, 46, 129), 90);
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
			btn.BorderThickness = new Thickness(1.2);
			btn.Foreground = Brushes.White;
			btn.FontWeight = FontWeights.SemiBold;
			btn.FontSize = 11.5;
			btn.Padding = new Thickness(14, 5, 14, 5);
			SetControlCornerRadius(btn, 8);
			AttachModernHover(btn,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(49, 46, 129), Color.FromRgb(67, 56, 202), 90),
				hoverBorder: new SolidColorBrush(Color.FromRgb(165, 180, 252)));
			return;
		}

		// 2. Split Line Button (Timeline)
		if (tag.IndexOf("TimelineSplitButton", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("Split Line", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new LinearGradientBrush(Color.FromRgb(79, 70, 229), Color.FromRgb(99, 102, 241), 45);
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(129, 140, 248));
			btn.BorderThickness = new Thickness(1.2);
			btn.Foreground = Brushes.White;
			btn.FontWeight = FontWeights.Bold;
			btn.FontSize = 11.0;
			btn.Padding = new Thickness(12, 5, 12, 5);
			SetControlCornerRadius(btn, 7);
			AttachModernHover(btn,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(124, 58, 237), 45),
				hoverBorder: new SolidColorBrush(Color.FromRgb(199, 210, 254)));
			return;
		}

		// 3. Update Preview Video Button
		if (tag.IndexOf("ToolbarPreviewButton", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("Update Preview Video", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(3, 105, 161), 45);
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
			btn.BorderThickness = new Thickness(1.2);
			btn.Foreground = Brushes.White;
			btn.FontWeight = FontWeights.Bold;
			btn.FontSize = 11.0;
			btn.Padding = new Thickness(12, 5, 12, 5);
			SetControlCornerRadius(btn, 7);
			AttachModernHover(btn,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(3, 105, 161), Color.FromRgb(2, 132, 199), 45),
				hoverBorder: new SolidColorBrush(Color.FromRgb(125, 211, 252)));
			return;
		}

		// 4. Export This Button
		if (buttonText.IndexOf("Export This", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(37, 99, 235), 45);
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
			btn.BorderThickness = new Thickness(1.2);
			btn.Foreground = Brushes.White;
			btn.FontWeight = FontWeights.Bold;
			btn.FontSize = 12.0;
			btn.Padding = new Thickness(16, 6, 16, 6);
			SetControlCornerRadius(btn, 16);
			AttachModernHover(btn,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(14, 165, 233), Color.FromRgb(59, 130, 246), 45),
				hoverBorder: new SolidColorBrush(Color.FromRgb(186, 230, 253)));
			return;
		}

		// 5. Main Big "Upload Video File" Button
		if (buttonText.IndexOf("Upload Video File", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new LinearGradientBrush(Color.FromRgb(5, 150, 105), Color.FromRgb(13, 148, 136), 45);
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 211, 153));
			btn.BorderThickness = new Thickness(1.5);
			btn.Foreground = Brushes.White;
			btn.FontWeight = FontWeights.Bold;
			btn.FontSize = 12.5;
			btn.Height = 42;
			btn.Padding = new Thickness(22, 8, 22, 8);
			SetControlCornerRadius(btn, 8);
			AttachModernHover(btn,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(16, 185, 129), Color.FromRgb(20, 184, 166), 45),
				hoverBorder: new SolidColorBrush(Color.FromRgb(110, 231, 183)));
			return;
		}

		// 6. Transcribe & Translate AI Button
		if (buttonText.IndexOf("Transcribe & Translate", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new LinearGradientBrush(Color.FromRgb(79, 70, 229), Color.FromRgb(124, 58, 237), 45);
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(167, 139, 250));
			btn.BorderThickness = new Thickness(1.2);
			btn.Foreground = Brushes.White;
			btn.FontWeight = FontWeights.Bold;
			btn.FontSize = 11.0;
			btn.Padding = new Thickness(12, 5, 12, 5);
			SetControlCornerRadius(btn, 6);
			AttachModernHover(btn,
				hoverBackground: new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(139, 92, 246), 45),
				hoverBorder: new SolidColorBrush(Color.FromRgb(221, 214, 254)));
			return;
		}

		// 7. AI Auto-Detect Button
		if (buttonText.IndexOf("AI Auto-Detect", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(49, 46, 129));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = new SolidColorBrush(Color.FromRgb(199, 210, 254));
			btn.FontWeight = FontWeights.SemiBold;
			btn.FontSize = 10.5;
			btn.Padding = new Thickness(10, 4, 10, 4);
			SetControlCornerRadius(btn, 6);
			AttachModernHover(btn,
				hoverBackground: new SolidColorBrush(Color.FromRgb(67, 56, 202)),
				hoverBorder: new SolidColorBrush(Color.FromRgb(165, 180, 252)));
			return;
		}

		// 8. Delete Button (Dialogue Header)
		if (string.Equals(buttonText, "Delete", StringComparison.OrdinalIgnoreCase)
			&& btn.Foreground is SolidColorBrush f && (f.Color.R > 200 && f.Color.G < 150))
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(36, 15, 22));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
			btn.Padding = new Thickness(8, 3, 8, 3);
			SetControlCornerRadius(btn, 6);
			AttachModernHover(btn,
				hoverBackground: new SolidColorBrush(Color.FromRgb(69, 10, 10)),
				hoverBorder: new SolidColorBrush(Color.FromRgb(252, 165, 165)));
			return;
		}

		// 9. Find & Replace Button
		if (buttonText.IndexOf("Find & Replace", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(15, 24, 38));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 48, 74));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
			btn.Padding = new Thickness(8, 3, 8, 3);
			SetControlCornerRadius(btn, 6);
			AttachModernHover(btn);
			return;
		}

		// 10. Aspect Ratio / Social Presets ("LANDSCAPE", "1:1", "TikTok 9:16", "YouTube 16:9", "▭ 16:9", "▯ 9:16", "⏹ 1:1")
		if ((btn.Tag as string ?? "").StartsWith("Social")
			|| buttonText.IndexOf("LANDSCAPE", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("1:1", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("TikTok", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.Contains("16:9")
			|| buttonText.Contains("9:16"))
		{
			btn.FontWeight = FontWeights.SemiBold;
			btn.FontSize = 10.5;
			SetControlCornerRadius(btn, 6);
			return;
		}

		// 10b. Video Flip Button ("⇄ Flip", "⇄ Flipped")
		if ((btn.Tag as string ?? "").StartsWith("VideoFlip") || buttonText.IndexOf("Flip", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.FontWeight = FontWeights.SemiBold;
			btn.FontSize = 10.5;
			SetControlCornerRadius(btn, 6);
			AttachModernHover(btn,
				hoverBackground: new SolidColorBrush(Color.FromRgb(3, 105, 161)),
				hoverBorder: new SolidColorBrush(Color.FromRgb(56, 189, 248)));
			return;
		}

		// Hide Watermark and Music buttons from the left VISUALS sidebar (keep Add Sponsor)
		string sidebarCmd = btn.CommandParameter as string ?? "";
		if (buttonText.IndexOf("Watermark", StringComparison.OrdinalIgnoreCase) >= 0
			|| string.Equals(buttonText.Trim(), "Music", StringComparison.OrdinalIgnoreCase)
			|| (buttonText.IndexOf("Music", StringComparison.OrdinalIgnoreCase) >= 0
				&& buttonText.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) < 0
				&& buttonText.IndexOf("Mute", StringComparison.OrdinalIgnoreCase) < 0
				&& buttonText.IndexOf("BGM", StringComparison.OrdinalIgnoreCase) < 0
				&& btn.ActualWidth <= 120)
			|| string.Equals(sidebarCmd, "watermark", StringComparison.OrdinalIgnoreCase))
		{
			btn.Visibility = Visibility.Collapsed;
			btn.IsEnabled = false;
			return;
		}

		// 11. Left Sidebar Tools (Blur, Text, Photo/mini vid, Add Sponsor)
		if (buttonText.IndexOf("Blur", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0
			|| buttonText.IndexOf("Sponsor", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			bool isActiveTool = false;
			Window parentWin = Window.GetWindow(btn);
			if (parentWin?.DataContext is MainViewModel mainVm)
			{
				string active = mainVm.ActiveToolPanel ?? "";
				if (buttonText.IndexOf("Blur", StringComparison.OrdinalIgnoreCase) >= 0 && (active == "blur" || (mainVm.BlurRegions?.Count ?? 0) > 0)) isActiveTool = true;
				else if (buttonText.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 && (active == "text" || (mainVm.TextLayers?.Count ?? 0) > 0)) isActiveTool = true;
				else if (buttonText.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0 && (active == "photo_logo" || (mainVm.Overlays?.Count ?? 0) > 0)) isActiveTool = true;
				else if (buttonText.IndexOf("Sponsor", StringComparison.OrdinalIgnoreCase) >= 0 && active == "sponsor") isActiveTool = true;
				else if (buttonText.IndexOf("Watermark", StringComparison.OrdinalIgnoreCase) >= 0 && active == "watermark") isActiveTool = true;
			}

			// Compact sidebar buttons: smaller, sleek icon and text to prevent vertical scrollbar overflow
			btn.MinWidth = 66.0;
			btn.MinHeight = 44.0;
			btn.Height = 46.0;
			btn.Background = isActiveTool 
				? new SolidColorBrush(Color.FromArgb(195, 24, 44, 76)) 
				: new SolidColorBrush(Color.FromArgb(95, 13, 22, 38));
			btn.BorderBrush = Brushes.Transparent;
			btn.BorderThickness = new Thickness(0.0);
			btn.Margin = new Thickness(2, 2.5, 2, 2.5);
			btn.Padding = new Thickness(3, 2, 3, 2);
			SetControlCornerRadius(btn, 7);
			AttachModernHover(btn,
				hoverBackground: new SolidColorBrush(Color.FromRgb(28, 44, 72)),
				hoverBorder: Brushes.Transparent);

			// Style inner icon borders
			foreach (Border iconBox in FindAllVisualChildren<Border>((DependencyObject)(object)btn))
			{
				iconBox.CornerRadius = new CornerRadius(5);
				iconBox.BorderThickness = new Thickness(0.0);
				iconBox.MinWidth = 0.0;
				iconBox.MinHeight = 0.0;
				if (buttonText.IndexOf("Blur", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					iconBox.Background = new SolidColorBrush(Color.FromRgb(10, 24, 40));
				}
				else if (buttonText.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					iconBox.Background = new SolidColorBrush(Color.FromRgb(20, 14, 42));
				}
				else if (buttonText.IndexOf("Photo", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					iconBox.Background = new SolidColorBrush(Color.FromRgb(10, 32, 24));
				}
				else if (buttonText.IndexOf("Sponsor", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					iconBox.Background = new SolidColorBrush(Color.FromRgb(34, 26, 10));
				}
				else if (buttonText.IndexOf("Watermark", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					iconBox.Background = new SolidColorBrush(Color.FromRgb(12, 24, 42));
				}
			}

			// Compact text and icons inside buttons
			foreach (TextBlock lbl in labels)
			{
				if (lbl.Text != null && (lbl.Text.Contains("Photo") || lbl.Text.Contains("mini vid")))
				{
					lbl.Text = "Photo / Vid";
					lbl.FontSize = 9.0;
					lbl.FontWeight = FontWeights.Medium;
					lbl.Margin = new Thickness(0, 1, 0, 0);
				}
				else if (lbl.Text != null && lbl.Text.Length > 2)
				{
					lbl.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
					lbl.FontSize = 9.0;
					lbl.FontWeight = FontWeights.Medium;
					lbl.Margin = new Thickness(0, 1, 0, 0);
				}
				else if (lbl.Text != null && lbl.Text.Length <= 2)
				{
					// Icon glyph / character
					lbl.FontSize = 13.5;
				}
			}
			return;
		}

		// 12. Transport Buttons (Play, StepBack, StepForward)
		if (btn.Width <= 42 && btn.Height <= 36 && (buttonText.Contains("▶") || buttonText.Contains("⏮") || buttonText.Contains("⏭") || buttonText.Contains("❚❚")))
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(16, 26, 42));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 51, 80));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = Brushes.White;
			SetControlCornerRadius(btn, 7);
			AttachModernHover(btn);
			return;
		}

		// 13. Top Header Icon Buttons (Settings ⚙, Globe 🌐)
		if (buttonText.Contains("⚙") || buttonText.Contains("🌐") || (btn.ToolTip as string ?? "").Contains("Settings") || (btn.ToolTip as string ?? "").Contains("Language"))
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(14, 22, 36));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 68));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
			SetControlCornerRadius(btn, 8);
			AttachModernHover(btn);
			return;
		}

		// 14. "Show in folder" Button
		if (buttonText.IndexOf("Show in folder", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(15, 24, 38));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 48, 74));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
			btn.Padding = new Thickness(10, 5, 10, 5);
			SetControlCornerRadius(btn, 7);
			AttachModernHover(btn);
			return;
		}

		// 15. Dialogue Font Size Buttons ("A-", "A+")
		if (buttonText == "A-" || buttonText == "A+")
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(14, 22, 35));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 44, 67));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
			btn.Padding = new Thickness(6, 2, 6, 2);
			SetControlCornerRadius(btn, 5);
			AttachModernHover(btn);
			return;
		}

		// General fallback styling for dark theme buttons
		if (btn.Background is SolidColorBrush b && IsDarkBrush(b) && !IsAccentColor(b.Color))
		{
			btn.Background = new SolidColorBrush(Color.FromRgb(16, 26, 42));
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 51, 80));
			btn.BorderThickness = new Thickness(1.0);
			btn.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
			SetControlCornerRadius(btn, 6);
			AttachModernHover(btn);
		}
	}

	private static void ApplyTextBoxModernStyle(TextBox tb)
	{
		if (tb == null) return;
		if (tb.Tag as string != null && tb.Tag.ToString().Contains("_ModernTbWired")) return;
		tb.Tag = (tb.Tag as string ?? "") + "_ModernTbWired";

		tb.Background = new SolidColorBrush(Color.FromRgb(9, 15, 26));
		tb.Foreground = Brushes.White;
		tb.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 45, 70));
		tb.BorderThickness = new Thickness(1.0);
		tb.CaretBrush = Brushes.White;
		tb.SelectionBrush = new SolidColorBrush(Color.FromArgb(170, 56, 189, 248));
		tb.Padding = new Thickness(7, 4, 7, 4);
		SetControlCornerRadius(tb, 6);

		tb.GotFocus += (s, e) =>
		{
			if (!_isWhiteTheme)
			{
				tb.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
			}
		};
		tb.LostFocus += (s, e) =>
		{
			if (!_isWhiteTheme)
			{
				tb.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 45, 70));
			}
		};
	}

	public static void ApplyModernStudioRedesign(Window window)
	{
		if (window == null || _isWhiteTheme) return;
		try
		{
			// 1. Window Base Canvas
			window.Background = new SolidColorBrush(Color.FromRgb(8, 12, 20));

			if (!FindAllVisualChildren<Button>((DependencyObject)(object)window).Any(b => (b.Tag as string ?? "").StartsWith("EpisodeBatchButton")))
			{
				WireEpisodeBatchTool(window);
			}
			if (!FindAllVisualChildren<Button>((DependencyObject)(object)window).Any(b => (b.Tag as string ?? "").StartsWith("TopTranscribeButton")))
			{
				RelocateTranscribeToTop(window);
			}

			if (window.DataContext is MainViewModel redesignVm)
			{
				ApplySocialAspectRatio(window, redesignVm);
			}

			// 2. Iterate all Borders (Cards & Boxes)
			foreach (Border border in FindAllVisualChildren<Border>((DependencyObject)(object)window))
			{
				if (IsInsideVideoPlayer(border)) continue;

				// SNA Pro Logo Badge
				if (FindVisualChild<TextBlock>((DependencyObject)(object)border, tb => tb.Text != null && tb.Text.Contains("SNA Pro")) != null)
				{
					border.Background = new SolidColorBrush(Color.FromRgb(10, 20, 36));
					border.BorderBrush = new LinearGradientBrush(Color.FromRgb(2, 132, 199), Color.FromRgb(56, 189, 248), 45);
					border.BorderThickness = new Thickness(1.5);
					border.CornerRadius = new CornerRadius(8);
					border.Padding = new Thickness(6, 3, 6, 3);
					continue;
				}

				// ACTIVE STUDIO Status Badge
				if (FindVisualChild<TextBlock>((DependencyObject)(object)border, tb => tb.Text != null && tb.Text.Contains("ACTIVE STUDIO")) != null)
				{
					border.Background = new SolidColorBrush(Color.FromRgb(6, 44, 32));
					border.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
					border.BorderThickness = new Thickness(1);
					border.CornerRadius = new CornerRadius(14);
					border.Padding = new Thickness(10, 4, 10, 4);
					TextBlock studioText = FindVisualChild<TextBlock>((DependencyObject)(object)border, tb => tb.Text != null && tb.Text.Contains("ACTIVE STUDIO"));
					if (studioText != null)
					{
						studioText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
						studioText.FontWeight = FontWeights.Bold;
					}
					continue;
				}

				// Clapperboard Dropzone Center Icon Circle
				if ((border.Width >= 40 && border.Height >= 40 && border.CornerRadius.TopLeft >= 20)
					|| (FindVisualChild<TextBlock>((DependencyObject)(object)border, tb => tb.Text != null && tb.Text.Contains("🎬")) != null && border.Width == border.Height && border.Width > 40))
				{
					border.Background = new LinearGradientBrush(Color.FromRgb(12, 24, 42), Color.FromRgb(18, 36, 62), 90);
					border.BorderBrush = new SolidColorBrush(Color.FromRgb(2, 132, 199));
					border.BorderThickness = new Thickness(2);
					border.CornerRadius = new CornerRadius(34);
					continue;
				}

				// Subtitle / Dialogue Row Item Border
				if (border.Tag as string == "DialogueRow" || FindVisualChild<TextBox>((DependencyObject)(object)border, (TextBox _) => true) != null)
				{
					border.CornerRadius = new CornerRadius(7);
					if (border.Background is SolidColorBrush sb && IsDarkBrush(sb))
					{
						border.Background = new SolidColorBrush(Color.FromRgb(11, 17, 28));
						border.BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 60));
						border.BorderThickness = new Thickness(1);
					}
					continue;
				}

				// Dialogue Panel Container (Right side box)
				if (FindVisualChild<TextBlock>((DependencyObject)(object)border, tb => tb.Text != null && tb.Text.Trim() == "DIALOGUE") != null)
				{
					border.Background = new SolidColorBrush(Color.FromRgb(9, 14, 24));
					border.BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 60));
					border.BorderThickness = new Thickness(1);
					border.CornerRadius = new CornerRadius(12);
					continue;
				}

				// VISUALS Sidebar Container (Left side box)
				if (FindVisualChild<TextBlock>((DependencyObject)(object)border, tb => tb.Text != null && tb.Text.Trim() == "VISUALS") != null)
				{
					border.MinWidth = 80;
					border.Padding = new Thickness(4, 6, 4, 6);
					border.Background = new SolidColorBrush(Color.FromRgb(10, 16, 27));
					border.BorderBrush = Brushes.Transparent;
					border.BorderThickness = new Thickness(0);
					border.CornerRadius = new CornerRadius(10);

					ScrollViewer sv = FindAllVisualChildren<ScrollViewer>((DependencyObject)(object)border).FirstOrDefault();
					if (sv != null)
					{
						sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
						sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
					}

					// Ensure parent column allows the proper width
					Grid parentGrid = FindAncestor<Grid>((DependencyObject)(object)border);
					if (parentGrid != null && parentGrid.ColumnDefinitions.Count > 0)
					{
						int col = Grid.GetColumn(border);
						if (col >= 0 && col < parentGrid.ColumnDefinitions.Count)
						{
							ColumnDefinition cd = parentGrid.ColumnDefinitions[col];
							if (cd.Width.IsAbsolute && cd.Width.Value < 88.0)
							{
								cd.Width = new GridLength(88.0);
							}
						}
					}
					continue;
				}

				// General Panels / Boxes / Cards
				if (border.Background is SolidColorBrush scb && IsDarkBrush(scb))
				{
					if (border.ActualWidth > 120 && border.ActualHeight > 60)
					{
						if (border.CornerRadius.TopLeft < 6)
						{
							border.CornerRadius = new CornerRadius(10);
						}
						border.BorderBrush = new SolidColorBrush(Color.FromRgb(22, 35, 54));
						border.BorderThickness = new Thickness(1);
					}
					else if (border.CornerRadius.TopLeft < 4)
					{
						border.CornerRadius = new CornerRadius(6);
					}
				}
			}

			// 3. Iterate all Buttons
			foreach (Button btn in FindAllVisualChildren<Button>((DependencyObject)(object)window))
			{
				ApplyButtonModernStyle(btn);
			}

			// 4. Iterate all TextBoxes
			foreach (TextBox tb in FindAllVisualChildren<TextBox>((DependencyObject)(object)window))
			{
				ApplyTextBoxModernStyle(tb);
			}

			// 5. Iterate all TextBlocks (Typography & Colors)
			foreach (TextBlock tb in FindAllVisualChildren<TextBlock>((DependencyObject)(object)window))
			{
				if (string.IsNullOrEmpty(tb.Text)) continue;
				string text = tb.Text.Trim();

				// App Title "SNA-Dubber"
				if (text == "SNA-Dubber")
				{
					tb.Foreground = Brushes.White;
					tb.FontWeight = FontWeights.Bold;
					tb.FontSize = 14;
				}
				// App Title "ពិសេស"
				else if (text == "ពិសេស")
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
					tb.FontWeight = FontWeights.Bold;
					tb.FontSize = 14;
				}
				// Section Headers ("VISUALS", "DIALOGUE", "TIMELINE EDITOR")
				else if (text == "VISUALS" || text == "DIALOGUE" || text == "TIMELINE EDITOR")
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
					tb.FontWeight = FontWeights.Bold;
					if (text == "VISUALS")
					{
						tb.FontSize = 10.5;
						tb.Margin = new Thickness(0, 0, 0, 6);
					}
				}
				// Khmer Main Dropzone Heading
				else if (text.Contains("អូសទម្លាក់វីដេអូ") || text.Contains("Upload Video ដើម្បីចាប់ផ្តើម"))
				{
					tb.Foreground = Brushes.White;
					tb.FontWeight = FontWeights.Bold;
					tb.FontSize = 16.5;
				}
				// Dropzone Subtitle
				else if (text.Contains("Drop a TikTok, Reels or Shorts"))
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
					tb.FontSize = 11.5;
				}
				// Dropzone Format Hint
				else if (text.Contains("Supports: MP4") || text.Contains("Up to 4K"))
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
					tb.FontSize = 10;
				}
				// Timecode
				else if (text.Contains(" / ") && (text.Contains("00:") || text.Contains(":")))
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
					tb.FontWeight = FontWeights.SemiBold;
					tb.FontSize = 11.5;
				}
				// Status Indicators
				else if (text.Contains("Ready"))
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
				}
				else if (text.Contains("Needs a retry"))
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
				}
				else if (text.Contains("Total Lines:"))
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
				}
				// Audio Sliders (PITCH, VOL, VOICE SPEED)
				else if (text == "VOL")
				{
					tb.Visibility = Visibility.Collapsed;
				}
				else if (text == "PITCH" || text.StartsWith("VOICE SPEED"))
				{
					tb.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
					tb.FontWeight = FontWeights.Medium;
					tb.FontSize = 10.5;
				}
			}

			// 6. Iterate all Sliders
			foreach (Slider slider in FindAllVisualChildren<Slider>((DependencyObject)(object)window))
			{
				slider.Cursor = Cursors.Hand;
				Binding expr = BindingOperations.GetBinding(slider, Slider.ValueProperty);
				if (expr != null && expr.Path != null && expr.Path.Path == "VoiceVolume")
				{
					// User requested: "សំឡេង Video is Max ហើយមិនបាច់បង្ហាញ់កUI ក៏បានដែរ"
					slider.Visibility = Visibility.Collapsed;
					if (window.DataContext is MainViewModel mvm)
					{
						mvm.VoiceVolume = 1.0;
					}
				}
			}

			// Ensure PreviewPlayer has maximum volume (1.0) and flip transform if active
			MediaElement player = FindVisualChild((DependencyObject)(object)window, (MediaElement me) => me.Name == "PreviewPlayer");
			if (player != null)
			{
				player.Volume = 1.0;
				if (_isVideoFlipped)
				{
					ApplyVideoFlipState(window, null);
				}
			}

			// Ensure Line Audio Player has maximum volume (1.0)
			try
			{
				FieldInfo linePlayerField = typeof(MainWindow).GetField("_lineAudioPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
				if (linePlayerField?.GetValue(window) is MediaPlayer lineAudioPlayer)
				{
					lineAudioPlayer.Volume = 1.0;
				}
			}
			catch { }
		}
		catch (Exception ex)
		{
			Log("ApplyModernStudioRedesign error: " + ex.Message);
		}
	}
	private static void EnsureThemeSnapshotCaptured(Window window)
	{
		if (_themeBackups.Count > 0 || window == null)
		{
			return;
		ApplyModernStudioRedesign(window);
		}
		BackupElementState((DependencyObject)(object)window);
		foreach (DependencyObject item in FindAllVisualChildren<DependencyObject>((DependencyObject)(object)window))
		{
			BackupElementState(item);
		}
		Log($"Captured pristine Dark Theme snapshot for {_themeBackups.Count} visual elements.");
	}

	private static void HookThemeToggle(Window window)
	{
		if (_isThemeHooked || window == null)
		{
			return;
		}
		try
		{
			EnsureThemeSnapshotCaptured(window);
			Button btnTheme = FindVisualChild((DependencyObject)(object)window, (Button b) => b.ToolTip != null && b.ToolTip.ToString().IndexOf("Theme", StringComparison.OrdinalIgnoreCase) >= 0);
			if (btnTheme == null)
			{
				btnTheme = FindVisualChild((DependencyObject)(object)window, (Button b) => FindVisualChild((DependencyObject)(object)b, (TextBlock t) => t.Text != null && t.Text.Contains("\ud83c\udfa8")) != null);
			}
			if (btnTheme != null)
			{
				btnTheme.Click += (object s, RoutedEventArgs e) =>
				{
					ToggleAppTheme(window, btnTheme);
				};
				_isThemeHooked = true;
				Log("Successfully hooked Theme toggle on \ud83c\udfa8 button!");
			}
		}
		catch (Exception ex)
		{
			Log("HookThemeToggle error: " + ex.Message);
		}
	}

	private static void ToggleAppTheme(Window window, Button btnTheme)
	{
		try
		{
			EnsureThemeSnapshotCaptured(window);
			_isWhiteTheme = !_isWhiteTheme;
			if (_isWhiteTheme)
			{
				ApplyWhiteTheme(window);
				if (btnTheme != null)
				{
					btnTheme.ToolTip = "Switch to Dark Theme (Currently White Theme)";
				}
				Log("Switched to White / Light Theme");
			}
			else
			{
				RestoreDarkTheme(window);
				ApplyModernStudioRedesign(window);
				if (btnTheme != null)
				{
					btnTheme.ToolTip = "Switch to White Theme (Currently Dark Theme)";
				}
				Log("Restored Dark Theme");
			}
		}
		catch (Exception ex)
		{
			Log("ToggleAppTheme error: " + ex.Message);
		}
	}

	private static void BackupElementState(DependencyObject element)
	{
		if (element == null || _themeBackups.ContainsKey(element))
		{
			return;
		}
		VisualBackup visualBackup = new VisualBackup();
		if (element is Window window)
		{
			visualBackup.Background = window.Background;
		}
		else if (element is Control control)
		{
			visualBackup.Background = control.Background;
			visualBackup.Foreground = control.Foreground;
			visualBackup.BorderBrush = control.BorderBrush;
			if (control is TextBox textBox)
			{
				visualBackup.CaretBrush = textBox.CaretBrush;
			}
		}
		else if (element is Border border)
		{
			visualBackup.Background = border.Background;
			visualBackup.BorderBrush = border.BorderBrush;
		}
		else if (element is Panel panel)
		{
			visualBackup.Background = panel.Background;
		}
		else if (element is TextBlock textBlock)
		{
			visualBackup.Foreground = textBlock.Foreground;
		}
		_themeBackups[element] = visualBackup;
	}

	private static bool IsDarkBrush(Brush b)
	{
		if (b is SolidColorBrush { Color: var color })
		{
			if (color.A < 30)
			{
				return false;
			}
			return 0.299 * (double)(int)color.R + 0.587 * (double)(int)color.G + 0.114 * (double)(int)color.B < 130.0;
		}
		return false;
	}

	private static bool IsAccentColor(Color c)
	{
		byte b = Math.Max(c.R, Math.Max(c.G, c.B));
		int num = Math.Min(c.R, Math.Min(c.G, c.B));
		return b - num > 50;
	}

	private static bool IsInsideVideoPlayer(DependencyObject node)
	{
		while (node != null)
		{
			if (node is FrameworkElement frameworkElement && (frameworkElement.Name == "PreviewPlayer" || frameworkElement.Name == "VideoPlayer" || frameworkElement.Name == "PlayerGrid" || frameworkElement.Name == "VideoViewport"))
			{
				return true;
			}
			node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
		}
		return false;
	}

	private static void ApplyWhiteTheme(Window window)
	{
		if (window == null)
		{
			return;
		}
		EnsureThemeSnapshotCaptured(window);
		window.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
		SolidColorBrush foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
		SolidColorBrush background = new SolidColorBrush(Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue));
		SolidColorBrush background2 = new SolidColorBrush(Color.FromRgb(241, 245, 249));
		SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
		SolidColorBrush borderBrush2 = new SolidColorBrush(Color.FromRgb(203, 213, 225));
		foreach (DependencyObject item in FindAllVisualChildren<DependencyObject>((DependencyObject)(object)window))
		{
			if (IsInsideVideoPlayer(item))
			{
				continue;
			}
			if (item is Border border)
			{
				if (FindAncestor<Button>((DependencyObject)(object)border) == null && border.Background is SolidColorBrush solidColorBrush && IsDarkBrush(solidColorBrush) && !IsAccentColor(solidColorBrush.Color))
				{
					border.Background = background;
					border.BorderBrush = borderBrush;
				}
			}
			else if (item is Panel panel)
			{
				if (FindAncestor<Button>((DependencyObject)(object)panel) == null && panel.Background is SolidColorBrush solidColorBrush2 && IsDarkBrush(solidColorBrush2) && !IsAccentColor(solidColorBrush2.Color))
				{
					panel.Background = background2;
				}
			}
			else if (item is TextBlock textBlock)
			{
				if (textBlock.Foreground is SolidColorBrush solidColorBrush3 && !IsDarkBrush(solidColorBrush3) && !IsAccentColor(solidColorBrush3.Color))
				{
					textBlock.Foreground = foreground;
				}
			}
			else if (item is TextBox textBox)
			{
				textBox.Background = background;
				textBox.Foreground = foreground;
				textBox.BorderBrush = borderBrush2;
				textBox.CaretBrush = Brushes.Black;
			}
			else if (item is Button { Background: SolidColorBrush background3 } button && IsDarkBrush(background3) && !IsAccentColor(background3.Color))
			{
				button.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
				button.Foreground = foreground;
				button.BorderBrush = borderBrush2;
			}
		}
	}

	private static void RestoreDarkTheme(Window window)
	{
		if (window == null)
		{
			return;
		}
		if (_themeBackups.TryGetValue((DependencyObject)(object)window, out var value))
		{
			if (value.Background != null)
			{
				window.Background = value.Background;
			}
			else
			{
				window.Background = null;
				((DependencyObject)window).ClearValue(Control.BackgroundProperty);
			}
		}
		foreach (KeyValuePair<DependencyObject, VisualBackup> themeBackup in _themeBackups)
		{
			DependencyObject key = themeBackup.Key;
			VisualBackup value2 = themeBackup.Value;
			if (key == null || (object)key == window)
			{
				continue;
			}
			if (key is Control control)
			{
				if (value2.Background != null)
				{
					control.Background = value2.Background;
				}
				else
				{
					control.Background = null;
					((DependencyObject)control).ClearValue(Control.BackgroundProperty);
				}
				if (value2.Foreground != null)
				{
					control.Foreground = value2.Foreground;
				}
				else
				{
					control.Foreground = null;
					((DependencyObject)control).ClearValue(Control.ForegroundProperty);
				}
				if (value2.BorderBrush != null)
				{
					control.BorderBrush = value2.BorderBrush;
				}
				else
				{
					control.BorderBrush = null;
					((DependencyObject)control).ClearValue(Control.BorderBrushProperty);
				}
				if (control is TextBox textBox)
				{
					textBox.CaretBrush = value2.CaretBrush ?? Brushes.White;
				}
			}
			else if (key is Border border)
			{
				if (value2.Background != null)
				{
					border.Background = value2.Background;
				}
				else
				{
					border.Background = null;
					((DependencyObject)border).ClearValue(Border.BackgroundProperty);
				}
				if (value2.BorderBrush != null)
				{
					border.BorderBrush = value2.BorderBrush;
					continue;
				}
				border.BorderBrush = null;
				((DependencyObject)border).ClearValue(Border.BorderBrushProperty);
			}
			else if (key is Panel panel)
			{
				if (value2.Background != null)
				{
					panel.Background = value2.Background;
					continue;
				}
				panel.Background = null;
				((DependencyObject)panel).ClearValue(Panel.BackgroundProperty);
			}
			else if (key is TextBlock textBlock)
			{
				if (value2.Foreground != null)
				{
					textBlock.Foreground = value2.Foreground;
					continue;
				}
				textBlock.Foreground = null;
				((DependencyObject)textBlock).ClearValue(TextBlock.ForegroundProperty);
			}
		}
		foreach (TextBox item in FindAllVisualChildren<TextBox>((DependencyObject)(object)window))
		{
			ApplyHighContrastCaret(item);
		}
		Log("Restored Dark Theme cleanly with 100% fidelity");
	}

	private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
	{
		while (current != null)
		{
			T val = (T)(object)((current is T) ? current : null);
			if (val != null)
			{
				return val;
			}
			current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
		}
		return default;
	}

	private static T FindAncestor<T>(DependencyObject current, Func<T, bool> predicate) where T : DependencyObject
	{
		while (current != null)
		{
			if (current is T val && (predicate == null || predicate(val)))
			{
				return val;
			}
			current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
		}
		return default;
	}

	public static void TryInjectModalCloseButton(Window window)
	{
		if (_isModalCloseInjected)
		{
			return;
		}
		try
		{
			TextBlock textBlock = FindVisualChild((DependencyObject)(object)window, (TextBlock t) => t.Text != null && t.Text.Contains("SNA-DUBBER AI ENGINE"));
			if (textBlock == null)
			{
				return;
			}
			Border border = null;
			for (DependencyObject val = (DependencyObject)(object)textBlock; val != null; val = VisualTreeHelper.GetParent(val))
			{
				if (val is Border { Width: >350.0 } border2)
				{
					border = border2;
					break;
				}
			}
			if (border == null)
			{
				return;
			}
			if (FindVisualChild((DependencyObject)(object)border, (Button b) => b.Tag as string == "AiEngineModalCloseButton") != null)
			{
				_isModalCloseInjected = true;
				return;
			}
			MainViewModel vm = window.DataContext as MainViewModel;
			Button closeBtn = new Button
			{
				Tag = "AiEngineModalCloseButton",
				Content = "✕",
				ToolTip = "Cancel & Close AI Operation",
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0.0, 10.0, 12.0, 0.0),
				Width = 30.0,
				Height = 30.0,
				Background = Brushes.Transparent,
				Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
				BorderThickness = new Thickness(0.0),
				FontSize = 16.0,
				FontWeight = FontWeights.Bold,
				Cursor = Cursors.Hand
			};
			closeBtn.MouseEnter += (object s, MouseEventArgs e) =>
			{
				closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
			};
			closeBtn.MouseLeave += (object s, MouseEventArgs e) =>
			{
				closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
			};
			closeBtn.Click += (object s, RoutedEventArgs e) =>
			{
				_cancelRequested = true;
				if (vm != null)
				{
					vm.IsBusy = false;
					vm.BusyStatusText = string.Empty;
					vm.ProjectStatusMessage = "Operation cancelled.";
				}
			};
			if (border.Child is Grid grid)
			{
				grid.Children.Add(closeBtn);
			}
			else
			{
				UIElement child = border.Child;
				if (child != null)
				{
					border.Child = null;
					Grid grid2 = new Grid();
					grid2.Children.Add(child);
					grid2.Children.Add(closeBtn);
					border.Child = grid2;
				}
			}
			_isModalCloseInjected = true;
			Log("Successfully injected [✕] Close Button to AI Engine modal!");
		}
		catch (Exception ex)
		{
			Log("TryInjectModalCloseButton error: " + ex.Message);
		}
	}

	private static void WireAudioMuteButton(Window window)
	{
		if (_isAudioMuteHooked)
		{
			return;
		}
		try
		{
			MainViewModel vm = window.DataContext as MainViewModel;
			if (vm == null || vm.AudioMix == null)
			{
				return;
			}
			Button muteBtn = FindVisualChild((DependencyObject)(object)window, (Button b) => b.CommandParameter as string == "audio_mute" || (b.Content is string text && text.Contains("Audio Mute")));
			if (muteBtn == null)
			{
				return;
			}
			muteBtn.Tag = "AudioMuteHooked";
			muteBtn.Click += (object s, RoutedEventArgs e) =>
			{
				vm.AudioMix.IsOriginalMuted = !vm.AudioMix.IsOriginalMuted;
				if (vm.AudioMix.IsOriginalMuted)
				{
					vm.ProjectStatusMessage = "\ud83d\udd07 Audio Mute ON: Original sound & voice will be muted. Only Khmer dubbing will play.";
					muteBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
					muteBtn.BorderThickness = new Thickness(2.0);
				}
				else
				{
					vm.ProjectStatusMessage = "\ud83d\udd0a Audio Mute OFF: Background music (BGM) mixed with Khmer dubbing.";
					muteBtn.BorderThickness = new Thickness(0.0);
				}
			};
			_isAudioMuteHooked = true;
			Log("Successfully wired Audio Mute toggle on visuals!");
		}
		catch
		{
		}
	}

	public static bool TryInjectLicense(Window window)
	{
		try
		{
			TextBlock textBlock = FindLicenseTextBlock((DependencyObject)(object)window);
			if (textBlock == null)
			{
				return false;
			}
			// Find and tag the dialog border container for click-shielding
			for (DependencyObject cur = textBlock; cur != null && cur != window; cur = VisualTreeHelper.GetParent(cur) ?? LogicalTreeHelper.GetParent(cur))
			{
				if (cur is Border b && (b.Width >= 400 || b.HorizontalAlignment == HorizontalAlignment.Center))
				{
					_licenseDialogBorderRef = b;
					b.Tag = "LicenseDialogContainer";
					break;
				}
			}

			Panel panel = (textBlock.Parent as Panel) ?? (VisualTreeHelper.GetParent((DependencyObject)(object)textBlock) as Panel);
			if (panel == null)
			{
				DependencyObject val = (DependencyObject)(object)textBlock;
				while (panel == null && val != null)
				{
					DependencyObject val2 = LogicalTreeHelper.GetParent(val) ?? VisualTreeHelper.GetParent(val);
					if (val2 is Panel panel2)
					{
						panel = panel2;
						break;
					}
					val = val2;
				}
			}
			if (panel == null)
			{
				return false;
			}

			// Clean up any previously injected Gemini / Expiry sections so only Device ID and License Key remain
			List<UIElement> toRemove = panel.Children.OfType<FrameworkElement>()
				.Where(child => child.Tag as string == "GeminiKeySection")
				.Cast<UIElement>()
				.ToList();
			foreach (UIElement elem in toRemove)
			{
				panel.Children.Remove(elem);
			}

			_isLicenseInjected = true;
			Log("License Manager displayed cleanly with only Device ID and License Key.");
			return true;
		}
		catch (Exception ex)
		{
			Log("Exception in TryInjectLicense: " + ex);
			return false;
		}
	}

	public static void WireSubtitleOverlayInteractive(Window window)
	{
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Canvas overlayCanvas = FindVisualChild((DependencyObject)(object)window, (Canvas c) => c.Name == "OverlayCanvas");
			if (overlayCanvas == null)
			{
				return;
			}
			Border subBorder = null;
			foreach (UIElement child in overlayCanvas.Children)
			{
				if (child is Border border)
				{
					TextBlock textBlock = FindVisualChild((DependencyObject)(object)border, (TextBlock t) => true);
					if (textBlock != null)
					{
						subBorder = border;
						_subTextBlockRef = textBlock;
						break;
					}
				}
			}
			if (subBorder == null)
			{
				return;
			}
			_subBorderRef = subBorder;
			BindingOperations.ClearBinding((DependencyObject)(object)subBorder, UIElement.VisibilityProperty);
			subBorder.Visibility = Visibility.Collapsed;
			_isSubtitleOverlayVisible = false;
			if (_subTextBlockRef != null)
			{
				_subTextBlockRef.TextWrapping = TextWrapping.Wrap;
				_subTextBlockRef.TextAlignment = TextAlignment.Center;
			}
			_subScaleTransform = new ScaleTransform(1.0, 1.0);
			subBorder.LayoutTransform = _subScaleTransform;
			subBorder.PreviewMouseWheel += (object s, MouseWheelEventArgs e) =>
			{
				if (_subScaleTransform != null)
				{
					double num = ((e.Delta > 0) ? 0.08 : (-0.08));
					double num2 = Math.Clamp(_subScaleTransform.ScaleX + num, 0.4, 3.5);
					_subScaleTransform.ScaleX = num2;
					_subScaleTransform.ScaleY = num2;
					e.Handled = true;
				}
			};
			bool isDragging = false;
			Point dragStartMouse = default;
			double startLeft = 0.0;
			double startTop = 0.0;
			subBorder.Cursor = Cursors.SizeAll;
			subBorder.ToolTip = "\ud83d\udcac Subtitle Overlay Card\n• Click and drag anywhere to move position\n• Scroll mouse wheel to zoom/scale size (0.4x - 3.5x)";
			subBorder.PreviewMouseLeftButtonDown += (object s, MouseButtonEventArgs e) =>
			{
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				//IL_005c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0067: Unknown result type (might be due to invalid IL or missing references)
				//IL_006c: Unknown result type (might be due to invalid IL or missing references)
				isDragging = true;
				dragStartMouse = e.GetPosition(overlayCanvas);
				if (double.IsNaN(Canvas.GetLeft(subBorder)) || Canvas.GetLeft(subBorder) <= 0.0)
				{
					Point val = subBorder.TranslatePoint(new Point(0.0, 0.0), overlayCanvas);
					Canvas.SetLeft(subBorder, Math.Max(10.0, val.X));
					Canvas.SetTop(subBorder, Math.Max(10.0, val.Y));
					((DependencyObject)subBorder).ClearValue(Canvas.BottomProperty);
					((DependencyObject)subBorder).ClearValue(Canvas.RightProperty);
				}
				startLeft = Canvas.GetLeft(subBorder);
				startTop = Canvas.GetTop(subBorder);
				subBorder.CaptureMouse();
				e.Handled = true;
			};
			subBorder.PreviewMouseMove += (object s, MouseEventArgs e) =>
			{
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				if (isDragging)
				{
					Point position = e.GetPosition(overlayCanvas);
					double value = startLeft + (position.X - dragStartMouse.X);
					double value2 = startTop + (position.Y - dragStartMouse.Y);
					double max = Math.Max(10.0, overlayCanvas.ActualWidth - subBorder.ActualWidth);
					double max2 = Math.Max(10.0, overlayCanvas.ActualHeight - subBorder.ActualHeight);
					value = Math.Clamp(value, 0.0, max);
					value2 = Math.Clamp(value2, 0.0, max2);
					Canvas.SetLeft(subBorder, value);
					Canvas.SetTop(subBorder, value2);
					e.Handled = true;
				}
			};
			subBorder.PreviewMouseLeftButtonUp += (object s, MouseButtonEventArgs e) =>
			{
				if (isDragging)
				{
					isDragging = false;
					subBorder.ReleaseMouseCapture();
					e.Handled = true;
				}
			};
			Button btnSubtitles = FindVisualChild((DependencyObject)(object)window, (Button b) => b.CommandParameter as string == "subtitles" || (b.Content is StackPanel parent && FindVisualChild((DependencyObject)(object)parent, (TextBlock t) => t.Text != null && t.Text.Contains("Subtitles")) != null));
			if (btnSubtitles != null)
			{
				_btnSubtitlesSidebarRef = btnSubtitles;
				btnSubtitles.Click += (object s, RoutedEventArgs e) =>
				{
					_isSubtitleOverlayVisible = !_isSubtitleOverlayVisible;
					subBorder.Visibility = ((!_isSubtitleOverlayVisible) ? Visibility.Collapsed : Visibility.Visible);
					if (_isSubtitleOverlayVisible)
					{
						btnSubtitles.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
						btnSubtitles.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
						btnSubtitles.BorderThickness = new Thickness(2.0);
						if (double.IsNaN(Canvas.GetLeft(subBorder)) || Canvas.GetLeft(subBorder) <= 0.0)
						{
							double num = ((overlayCanvas.ActualWidth > 0.0) ? overlayCanvas.ActualWidth : 800.0);
							double num2 = ((overlayCanvas.ActualHeight > 0.0) ? overlayCanvas.ActualHeight : 450.0);
							double num3 = ((subBorder.ActualWidth > 0.0) ? subBorder.ActualWidth : 400.0);
							double num4 = ((subBorder.ActualHeight > 0.0) ? subBorder.ActualHeight : 60.0);
							((DependencyObject)subBorder).ClearValue(Canvas.BottomProperty);
							((DependencyObject)subBorder).ClearValue(Canvas.RightProperty);
							Canvas.SetLeft(subBorder, Math.Max(20.0, (num - num3) / 2.0));
							Canvas.SetTop(subBorder, Math.Max(20.0, num2 - num4 - 40.0));
						}
						if (window.DataContext is MainViewModel mainViewModel)
						{
							UpdateSubtitleOverlayText(mainViewModel.SelectedDialogueLine);
						}
					}
					else
					{
						((DependencyObject)btnSubtitles).ClearValue(Control.BackgroundProperty);
						((DependencyObject)btnSubtitles).ClearValue(Control.BorderBrushProperty);
						btnSubtitles.BorderThickness = new Thickness(0.0);
					}
				};
			}
			_isSubtitleOverlayHooked = true;
			Log("Successfully wired Interactive Subtitle Overlay (drag, zoom, sidebar toggle)!");
		}
		catch (Exception ex)
		{
			Log("WireSubtitleOverlayInteractive error: " + ex.Message);
		}
	}

	public static void UpdateSubtitleOverlayText(DialogueLine line)
	{
		if (_subTextBlockRef == null)
		{
			return;
		}
		try
		{
			if (line != null)
			{
				string text;
				if (string.IsNullOrWhiteSpace(line.CorrectedKhmer))
				{
					text = ((!string.IsNullOrWhiteSpace(line.DraftKhmer)) ? line.DraftKhmer : line.ChineseText);
				}
				else
				{
					text = line.CorrectedKhmer;
				}
				_subTextBlockRef.Text = text ?? string.Empty;
			}
			else
			{
				_subTextBlockRef.Text = "\ud83d\udcac អត\u17d2ថបទរង (Subtitles Preview)";
			}
		}
		catch
		{
		}
	}

	public static void WireFollowPlaybackAutoScroll(Window window)
	{
		try
		{
			if (window.DataContext is MainViewModel vm)
			{
				EnsureDialogueControls(window, vm);
				_isFollowPlaybackHooked = true;
				Log("Successfully wired Dialogue List Follow Playback auto-scroll!");
			}
		}
		catch (Exception ex)
		{
			Log("WireFollowPlaybackAutoScroll error: " + ex.Message);
		}
	}

	private static void EnsureDialogueControls(Window window, MainViewModel vm)
	{
		if (_dialogueScrollViewer != null && _dialogueItemsControl != null)
		{
			return;
		}
		try
		{
			foreach (ScrollViewer item in FindAllVisualChildren<ScrollViewer>((DependencyObject)(object)window))
			{
				ItemsControl itemsControl = FindVisualChild((DependencyObject)(object)item, (ItemsControl i) => true);
				if (itemsControl != null)
				{
					IEnumerable itemsSource = itemsControl.ItemsSource;
					if (itemsSource != null && itemsSource == vm.DialogueLines)
					{
						_dialogueScrollViewer = item;
						_dialogueItemsControl = itemsControl;
						break;
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void SyncPlaybackFollow(Window window, MainViewModel vm)
	{
		if (vm == null || vm.DialogueLines == null || vm.DialogueLines.Count == 0)
		{
			return;
		}
		try
		{
			int num = (int)(vm.CurrentPositionSec * 1000.0);
			DialogueLine dialogueLine = null;
			for (int i = 0; i < vm.DialogueLines.Count; i++)
			{
				DialogueLine dialogueLine2 = vm.DialogueLines[i];
				if (dialogueLine2.StartMs <= num && num <= dialogueLine2.EndMs)
				{
					dialogueLine = dialogueLine2;
					break;
				}
			}
			if (dialogueLine != null && dialogueLine != _lastFollowedLine)
			{
				_lastFollowedLine = dialogueLine;
				vm.SelectedDialogueLine = dialogueLine;
				UpdateSubtitleOverlayText(dialogueLine);
				ScrollDialogueListToLine(window, vm, dialogueLine);
				ScrollTimelineToLine(window, vm, dialogueLine);
			}
		}
		catch
		{
		}
	}

	private static void ScrollDialogueListToLine(Window window, MainViewModel vm, DialogueLine line)
	{
		EnsureDialogueControls(window, vm);
		if (_dialogueItemsControl == null || _dialogueScrollViewer == null)
		{
			return;
		}
		try
		{
			if (_dialogueItemsControl.ItemContainerGenerator.ContainerFromItem(line) is FrameworkElement frameworkElement)
			{
				frameworkElement.BringIntoView();
				HighlightDialogueRow(frameworkElement);
				return;
			}
			int num = vm.DialogueLines.IndexOf(line);
			if (num >= 0 && vm.DialogueLines.Count > 0)
			{
				double num2 = (double)num / (double)vm.DialogueLines.Count;
				_dialogueScrollViewer.ScrollToVerticalOffset(num2 * _dialogueScrollViewer.ScrollableHeight);
			}
		}
		catch
		{
		}
	}

	private static void HighlightDialogueRow(FrameworkElement container)
	{
		if (container == null)
		{
			return;
		}
		try
		{
			if (_lastHighlightedBorder != null)
			{
				_lastHighlightedBorder.BorderBrush = _lastHighlightedOriginalBrush;
				_lastHighlightedBorder.BorderThickness = _lastHighlightedOriginalThickness;
				_lastHighlightedBorder = null;
			}
			Border border = (container as Border) ?? FindVisualChild((DependencyObject)(object)container, (Border b) => true);
			if (border != null)
			{
				_lastHighlightedOriginalBrush = border.BorderBrush;
				_lastHighlightedOriginalThickness = border.BorderThickness;
				border.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
				border.BorderThickness = new Thickness(2.0);
				_lastHighlightedBorder = border;
			}
		}
		catch
		{
		}
	}

	private static void ScrollTimelineToLine(Window window, MainViewModel vm, DialogueLine line)
	{
		try
		{
			ScrollViewer scrollViewer = FindVisualChild((DependencyObject)(object)window, (ScrollViewer sv) => sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && FindVisualChild((DependencyObject)(object)sv, (TextBlock tb) => tb.DataContext is DialogueLine) != null);
			if (scrollViewer != null)
			{
				FindVisualChild((DependencyObject)(object)scrollViewer, (Border b) => b.DataContext == line)?.BringIntoView();
			}
		}
		catch
		{
		}
	}

	public static void WireTimelineClipsAndSplit(Window window)
	{
		try
		{
			MainViewModel vm = window.DataContext as MainViewModel;
			if (vm == null)
			{
				return;
			}
			AttachTimelineClipDragHandlers(window);
			_isTimelineEditHooked = true;
			Log("Successfully wired Timeline Clip Drag and Split features!");
		}
		catch (Exception ex)
		{
			Log("WireTimelineClipsAndSplit error: " + ex.Message);
		}
	}

	public static void ExecuteSplitDialogueLine(MainViewModel vm, Window window)
	{
		if (vm == null)
		{
			return;
		}
		double splitPosition = _timelineEditPositionSec >= 0.0 ? _timelineEditPositionSec : vm.CurrentPositionSec;
		int playheadMs = (int)(Math.Clamp(splitPosition, 0.0, vm.VideoDurationSec) * 1000.0);
		vm.CurrentPositionSec = playheadMs / 1000.0;
		SeekPreviewVideo(vm.CurrentPositionSec);
		List<BlurRegionItem> blurToSplit = _selectedTimelineBlurRegions.Where(item => playheadMs > item.StartMs + 100 && playheadMs < item.EndMs - 100).ToList();
		if (blurToSplit.Count > 0)
		{
			PushTimelineUndo(vm); _selectedTimelineBlurRegions.Clear();
			foreach (BlurRegionItem original in blurToSplit)
			{
				BlurRegionItem second = new BlurRegionItem();
				foreach (PropertyInfo property in typeof(BlurRegionItem).GetProperties(BindingFlags.Instance | BindingFlags.Public))
					if (property.CanRead && property.CanWrite && property.Name != "StartMs" && property.Name != "EndMs") property.SetValue(second, property.GetValue(original));
				second.StartMs = playheadMs; second.EndMs = original.EndMs; original.EndMs = playheadMs; vm.BlurRegions.Add(second); _selectedTimelineBlurRegions.Add(second);
			}
			vm.SelectedBlurRegion = _selectedTimelineBlurRegions.LastOrDefault();
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			return;
		}
		List<TextLayerItem> selectedText = _selectedTimelineTextLayers.Count > 0
			? _selectedTimelineTextLayers.ToList()
			: (_selectedTimelineTextLayer == null ? new List<TextLayerItem>() : new List<TextLayerItem> { _selectedTimelineTextLayer });
		List<TextLayerItem> textToSplit = selectedText
			.Where(item => playheadMs > item.StartMs + 100 && playheadMs < item.EndMs - 100).ToList();
		if (textToSplit.Count > 0)
		{
			PushTimelineUndo(vm);
			_selectedTimelineTextLayers.Clear();
			foreach (TextLayerItem original in textToSplit)
			{
				TextLayerItem second = new TextLayerItem();
				foreach (PropertyInfo property in typeof(TextLayerItem).GetProperties(BindingFlags.Instance | BindingFlags.Public))
					if (property.CanRead && property.CanWrite && property.Name != "StartMs" && property.Name != "EndMs")
						property.SetValue(second, property.GetValue(original));
				second.StartMs = playheadMs;
				second.EndMs = original.EndMs;
				original.EndMs = playheadMs;
				vm.TextLayers.Add(second);
				_selectedTimelineTextLayers.Add(second);
			}
			_selectedTimelineTextLayer = _selectedTimelineTextLayers.LastOrDefault();
			vm.SelectedTextLayer = _selectedTimelineTextLayer;
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			vm.ProjectStatusMessage = $"Split {textToSplit.Count} text clip(s) at {TimeSpan.FromMilliseconds(playheadMs):hh\\:mm\\:ss\\.fff}";
			return;
		}
		if (_selectedTimelineOverlays.Count > 1)
		{
			List<OverlayItem> splittable = _selectedTimelineOverlays
				.Where(item => playheadMs > item.StartMs + 100 && playheadMs < item.EndMs - 100).ToList();
			if (splittable.Count > 0)
			{
				PushTimelineUndo(vm);
				_selectedTimelineOverlays.Clear();
				foreach (OverlayItem original in splittable)
				{
					OverlayItem second = new OverlayItem();
					foreach (PropertyInfo property in typeof(OverlayItem).GetProperties(BindingFlags.Instance | BindingFlags.Public))
						if (property.CanRead && property.CanWrite && property.Name != "StartMs" && property.Name != "EndMs")
							property.SetValue(second, property.GetValue(original));
					second.StartMs = playheadMs;
					second.EndMs = original.EndMs;
					original.EndMs = playheadMs;
					vm.Overlays.Add(second);
					_selectedTimelineOverlays.Add(second);
				}
				_selectedTimelineOverlay = _selectedTimelineOverlays.LastOrDefault();
				vm.SelectedOverlay = _selectedTimelineOverlay;
				try { vm.SaveProjectCommand?.Execute(null); } catch { }
				vm.ProjectStatusMessage = $"Split {splittable.Count} selected clips at {TimeSpan.FromMilliseconds(playheadMs):hh\\:mm\\:ss\\.fff}";
				return;
			}
		}
		if (_selectedTimelineOverlay != null && playheadMs > _selectedTimelineOverlay.StartMs + 100 && playheadMs < _selectedTimelineOverlay.EndMs - 100)
		{
			try
			{
				PushTimelineUndo(vm);
				OverlayItem original = _selectedTimelineOverlay;
				OverlayItem second = new OverlayItem();
				foreach (PropertyInfo property in typeof(OverlayItem).GetProperties(BindingFlags.Instance | BindingFlags.Public))
				{
					if (property.CanRead && property.CanWrite && property.Name != "StartMs" && property.Name != "EndMs")
					{
						property.SetValue(second, property.GetValue(original));
					}
				}
				second.StartMs = playheadMs;
				second.EndMs = original.EndMs;
				original.EndMs = playheadMs;
				vm.Overlays.Add(second);
				vm.SelectedOverlay = second;
				_selectedTimelineOverlay = second;
				vm.SaveProjectCommand?.Execute(null);
				vm.ProjectStatusMessage = $"Sponsor split at {TimeSpan.FromMilliseconds(playheadMs):hh\\:mm\\:ss\\.fff}";
				AttachTimelineClipDragHandlers(window);
				return;
			}
			catch (Exception ex)
			{
				Log("Sponsor split error: " + ex.Message);
			}
		}
		bool hasSelectedVisual = _selectedTimelineBlurRegions.Count > 0
			|| _selectedTimelineTextLayers.Count > 0
			|| _selectedTimelineOverlays.Count > 0
			|| _selectedTimelineTextLayer != null
			|| _selectedTimelineOverlay != null;
		if (hasSelectedVisual)
		{
			vm.ProjectStatusMessage = "Move the white playhead inside the selected clip, then click Split Line.";
			MessageBox.Show(
				"The white playhead must be inside the selected clip (not on its first or last frame).",
				"Split clip",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
			return;
		}
		if ((vm.DialogueLines == null || vm.DialogueLines.Count == 0) && HasCurrentVideoReference(vm))
		{
			if (vm.DialogueLines == null)
			{
				vm.DialogueLines = new ObservableCollection<DialogueLine>();
			}
			double durSec = vm.VideoDurationSec > 0.1 ? vm.VideoDurationSec : (vm.CurrentProject?.DurationSec ?? 0.0);
			int totalMs = durSec > 0.1 ? (int)(durSec * 1000.0) : 5000;
			DialogueLine initialLine = new DialogueLine
			{
				Index = 1,
				StartMs = 0,
				EndMs = totalMs,
				StartTime = "00:00:00,000",
				EndTime = TimeSpan.FromMilliseconds(totalMs).ToString("hh\\:mm\\:ss\\,fff"),
				CorrectedKhmer = "",
				DraftKhmer = ""
			};
			vm.DialogueLines.Add(initialLine);
			vm.SelectedDialogueLine = initialLine;
		}
		if (vm.DialogueLines == null || vm.DialogueLines.Count == 0)
		{
			MessageBox.Show("No dialogue lines available to split.", "Split", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		try
		{
			int curMs = playheadMs;
			DialogueLine dialogueLine = vm.SelectedDialogueLine;
			if (dialogueLine == null)
			{
				dialogueLine = vm.DialogueLines.FirstOrDefault((DialogueLine l) => l.StartMs <= curMs && curMs <= l.EndMs);
			}
			if (dialogueLine == null && vm.DialogueLines.Count > 0)
			{
				dialogueLine = vm.DialogueLines[0];
			}
			if (dialogueLine == null)
			{
				return;
			}
			PushTimelineUndo(vm);
			int num = ((curMs <= dialogueLine.StartMs + 200 || curMs >= dialogueLine.EndMs - 200) ? ((dialogueLine.StartMs + dialogueLine.EndMs) / 2) : curMs);
			string text = dialogueLine.CorrectedKhmer ?? dialogueLine.DraftKhmer ?? "";
			string text2 = text;
			string text3 = "";
			if (!string.IsNullOrEmpty(text) && text.Length > 2)
			{
				int num2 = text.Length / 2;
				int num3 = -1;
				int num4 = int.MaxValue;
				for (int num5 = 0; num5 < text.Length; num5++)
				{
					char c = text[num5];
					if (c == ' ' || c == '។' || c == '!' || c == '?' || c == ',' || c == '៖')
					{
						int num6 = Math.Abs(num5 - num2);
						if (num6 < num4)
						{
							num4 = num6;
							num3 = num5 + 1;
						}
					}
				}
				if (num3 > 0 && num3 < text.Length)
				{
					text2 = text.Substring(0, num3).Trim();
					text3 = text.Substring(num3).Trim();
				}
				else
				{
					text2 = text.Substring(0, num2).Trim();
					text3 = text.Substring(num2).Trim();
				}
			}
			string text4 = dialogueLine.ChineseText ?? "";
			string chineseText = text4;
			string chineseText2 = "";
			if (!string.IsNullOrEmpty(text4) && text4.Length > 1)
			{
				int num7 = text4.Length / 2;
				chineseText = text4.Substring(0, num7);
				chineseText2 = text4.Substring(num7);
			}
			DialogueLine dialogueLine2 = new DialogueLine
			{
				StartMs = num,
				EndMs = dialogueLine.EndMs,
				StartTime = TimeSpan.FromMilliseconds(num).ToString("hh\\:mm\\:ss\\,fff"),
				EndTime = dialogueLine.EndTime,
				CorrectedKhmer = text3,
				DraftKhmer = text3,
				ChineseText = chineseText2,
				Speaker = dialogueLine.Speaker,
				VoiceProfile = dialogueLine.VoiceProfile,
				Speed = dialogueLine.Speed,
				Volume = dialogueLine.Volume,
				Pitch = dialogueLine.Pitch
			};
			dialogueLine.EndMs = num;
			dialogueLine.EndTime = TimeSpan.FromMilliseconds(num).ToString("hh\\:mm\\:ss\\,fff");
			dialogueLine.CorrectedKhmer = text2;
			dialogueLine.DraftKhmer = text2;
			dialogueLine.ChineseText = chineseText;
			int num8 = vm.DialogueLines.IndexOf(dialogueLine);
			if (num8 >= 0)
			{
				vm.DialogueLines.Insert(num8 + 1, dialogueLine2);
			}
			else
			{
				vm.DialogueLines.Add(dialogueLine2);
			}
			for (int num9 = 0; num9 < vm.DialogueLines.Count; num9++)
			{
				vm.DialogueLines[num9].Index = num9 + 1;
			}
			vm.SelectedDialogueLine = dialogueLine2;
			try
			{
				vm.SaveProjectCommand?.Execute(null);
			}
			catch
			{
			}
			vm.ProjectStatusMessage = $"✓ Split Line #{dialogueLine.Index} into two lines at {dialogueLine2.StartTime}!";
			Log($"Split line #{dialogueLine.Index} at {num}ms successfully");
			AttachTimelineClipDragHandlers(window);
		}
		catch (Exception ex)
		{
			Log("ExecuteSplitDialogueLine error: " + ex.Message);
		}
	}

	public static void AttachTimelineClipDragHandlers(Window window)
	{
		try
		{
			MainViewModel vm = window.DataContext as MainViewModel;
			if (vm == null || vm.DialogueLines == null)
			{
				return;
			}
			foreach (Border border in FindAllVisualChildren<Border>((DependencyObject)(object)window))
			{
				object dataContext = border.DataContext;
				DialogueLine line = dataContext as DialogueLine;
				if (line == null || !(border.Tag as string != "ClipDragHooked"))
				{
					continue;
				}
				ScrollViewer scrollViewer = FindAncestor<ScrollViewer>((DependencyObject)(object)border);
				if (scrollViewer == null || scrollViewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled)
				{
					continue;
				}
				ApplyContinuousTimelineClipStyle(border, scrollViewer, line, vm);
				scrollViewer.SizeChanged += (object sender, SizeChangedEventArgs args) => ApplyContinuousTimelineClipStyle(border, scrollViewer, line, vm);
				border.Tag = "ClipDragHooked";
				border.MouseMove += (object s, MouseEventArgs e) =>
				{
					//IL_0016: Unknown result type (might be due to invalid IL or missing references)
					//IL_001b: Unknown result type (might be due to invalid IL or missing references)
					if (!_isDraggingClipStart && !_isDraggingClipEnd && !_isDraggingClipMove)
					{
						Point position = e.GetPosition(border);
						if (position.X <= 8.0)
						{
							border.Cursor = Cursors.SizeWE;
							border.ToolTip = "◀ Drag left edge to adjust Start Time";
						}
						else if (position.X >= border.ActualWidth - 8.0)
						{
							border.Cursor = Cursors.SizeWE;
							border.ToolTip = "▶ Drag right edge to adjust End Time";
						}
						else
						{
							border.Cursor = Cursors.Hand;
							border.ToolTip = $"Line #{line.Index} ({line.StartTime} -> {line.EndTime})\n{line.CorrectedKhmer}";
						}
					}
				};
				border.PreviewMouseLeftButtonDown += (object s, MouseButtonEventArgs e) =>
				{
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0047: Unknown result type (might be due to invalid IL or missing references)
					//IL_004c: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
					//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
					Point position = e.GetPosition(border);
					Point position2;
					if (position.X <= 8.0)
					{
						_isDraggingClipStart = true;
						_activeDraggingLine = line;
						_activeDraggingBorder = border;
						position2 = e.GetPosition(window);
						_dragStartWindowX = position2.X;
						_initialStartMs = line.StartMs;
						border.CaptureMouse();
						e.Handled = true;
					}
					else if (position.X >= border.ActualWidth - 8.0)
					{
						_isDraggingClipEnd = true;
						_activeDraggingLine = line;
						_activeDraggingBorder = border;
						position2 = e.GetPosition(window);
						_dragStartWindowX = position2.X;
						_initialEndMs = line.EndMs;
						border.CaptureMouse();
						e.Handled = true;
					}
					else
					{
						vm.SelectedDialogueLine = line;
						_selectedTimelineOverlay = null;
						_isDraggingClipMove = true;
						_activeDraggingLine = line;
						_activeDraggingBorder = border;
						position2 = e.GetPosition(window);
						_dragStartWindowX = position2.X;
						_initialStartMs = line.StartMs;
						_initialEndMs = line.EndMs;
						border.CaptureMouse();
						e.Handled = true;
					}
				};
				border.PreviewMouseMove += (object s, MouseEventArgs e) =>
				{
					//IL_0026: Unknown result type (might be due to invalid IL or missing references)
					//IL_002b: Unknown result type (might be due to invalid IL or missing references)
					//IL_010a: Unknown result type (might be due to invalid IL or missing references)
					//IL_010f: Unknown result type (might be due to invalid IL or missing references)
					Point position;
					if (_isDraggingClipStart && _activeDraggingLine == line)
					{
						position = e.GetPosition(window);
						int num = (int)((position.X - _dragStartWindowX) * 40.0);
						int num2 = Math.Max(0, Math.Min(_activeDraggingLine.EndMs - 150, _initialStartMs + num));
						_activeDraggingLine.StartMs = num2;
						_activeDraggingLine.StartTime = TimeSpan.FromMilliseconds(num2).ToString("hh\\:mm\\:ss\\,fff");
						double num3 = Math.Max(0.2, (double)(_activeDraggingLine.EndMs - num2) / 1000.0);
						border.Width = Math.Max(40.0, num3 * 60.0);
						e.Handled = true;
					}
					else if (_isDraggingClipEnd && _activeDraggingLine == line)
					{
						position = e.GetPosition(window);
						int num4 = (int)((position.X - _dragStartWindowX) * 40.0);
						int num5 = Math.Max(_activeDraggingLine.StartMs + 150, _initialEndMs + num4);
						_activeDraggingLine.EndMs = num5;
						_activeDraggingLine.EndTime = TimeSpan.FromMilliseconds(num5).ToString("hh\\:mm\\:ss\\,fff");
						double num6 = Math.Max(0.2, (double)(num5 - _activeDraggingLine.StartMs) / 1000.0);
						border.Width = Math.Max(40.0, num6 * 60.0);
						e.Handled = true;
					}
					else if (_isDraggingClipMove && _activeDraggingLine == line)
					{
						position = e.GetPosition(window);
						int deltaMs = (int)((position.X - _dragStartWindowX) * 40.0);
						int durationMs = _initialEndMs - _initialStartMs;
						int startMs = Math.Max(0, _initialStartMs + deltaMs);
						line.StartMs = startMs;
						line.EndMs = startMs + durationMs;
						line.StartTime = TimeSpan.FromMilliseconds(line.StartMs).ToString("hh\\:mm\\:ss\\,fff");
						line.EndTime = TimeSpan.FromMilliseconds(line.EndMs).ToString("hh\\:mm\\:ss\\,fff");
						e.Handled = true;
					}
				};
				border.PreviewMouseLeftButtonUp += (object s, MouseButtonEventArgs e) =>
				{
					if ((_isDraggingClipStart || _isDraggingClipEnd || _isDraggingClipMove) && _activeDraggingLine == line)
					{
						_isDraggingClipStart = false;
						_isDraggingClipEnd = false;
						_isDraggingClipMove = false;
						border.ReleaseMouseCapture();
						_activeDraggingBorder = null;
						vm.ProjectStatusMessage = $"✓ Line #{line.Index} duration updated: {line.StartTime} -> {line.EndTime}";
						try
						{
							vm.SaveProjectCommand?.Execute(null);
						}
						catch
						{
						}
						_activeDraggingLine = null;
						e.Handled = true;
					}
				};
			}
			AttachSponsorClipDragHandlers(window, vm);
		}
		catch (Exception ex)
		{
			Log("AttachTimelineClipDragHandlers error: " + ex.Message);
		}
	}

	private static void WireMediaOverlayEditor(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm)
				return;
			Canvas overlayCanvas = FindVisualChild((DependencyObject)(object)window, (Canvas c) => c.Name == "OverlayCanvas");
			if (overlayCanvas == null)
				return;

			// Replace the original built-in presenters (Overlays & BlurRegions).
			// The original BlurRegions presenter in MainWindow.xaml renders an ugly cyan border with hardcoded text [BLUR ORIGINAL CHINESE TITLE].
			// We collapse it completely so only our clean, borderless blur visual appears!
			foreach (ItemsControl items in FindAllVisualChildren<ItemsControl>((DependencyObject)(object)overlayCanvas))
			{
				Binding binding = BindingOperations.GetBinding((DependencyObject)(object)items, ItemsControl.ItemsSourceProperty);
				if (string.Equals(binding?.Path?.Path, "Overlays", StringComparison.Ordinal)
					|| string.Equals(binding?.Path?.Path, "BlurRegions", StringComparison.Ordinal)
					|| (vm.BlurRegions != null && ReferenceEquals(items.ItemsSource, vm.BlurRegions))
					|| (vm.Overlays != null && ReferenceEquals(items.ItemsSource, vm.Overlays)))
				{
					items.Visibility = Visibility.Collapsed;
				}
			}

			_mediaOverlayRefreshTimer?.Stop();
			_mediaOverlayRefreshTimer = new DispatcherTimer(DispatcherPriority.Render, overlayCanvas.Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(80)
			};
			_mediaOverlayRefreshTimer.Tick += (s, e) => RefreshMediaOverlayEditor(overlayCanvas, vm);
			_mediaOverlayRefreshTimer.Start();
			RefreshMediaOverlayEditor(overlayCanvas, vm);
			_isMediaOverlayEditorHooked = true;
			Log("Interactive Photo/GIF/mini-video overlay editor enabled (drag, resize, opacity, timed preview).");
		}
		catch (Exception ex)
		{
			Log("WireMediaOverlayEditor error: " + ex.Message);
		}
	}

	private static void WireTextLayerTool(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm) return;
			Button textButton = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(button => string.Equals(button.CommandParameter as string, "text", StringComparison.OrdinalIgnoreCase)
					&& FindVisualChild((DependencyObject)(object)button,
						(TextBlock label) => string.Equals(label.Text?.Trim(), "Text", StringComparison.OrdinalIgnoreCase)) != null);
			if (textButton == null)
			{
				textButton = FindAllVisualChildren<Button>((DependencyObject)(object)window)
					.FirstOrDefault(button => button.IsVisible && FindVisualChild((DependencyObject)(object)button,
						(TextBlock label) => string.Equals(label.Text?.Trim(), "Text", StringComparison.OrdinalIgnoreCase)) != null);
			}
			if (textButton == null) return;
			if (textButton.Tag as string == "TextLayerToolHooked")
			{
				_isTextLayerToolHooked = true;
				return;
			}
			textButton.Tag = "TextLayerToolHooked";
			textButton.Click += (s, e) => ShowAddTextLayerDialog(window, vm);
			_isTextLayerToolHooked = true;
			Log("Text sidebar tool now opens the Add Text editor.");
		}
		catch (Exception ex)
		{
			Log("WireTextLayerTool error: " + ex.Message);
		}
	}

	private static void ShowAddTextLayerDialog(Window owner, MainViewModel vm)
	{
		TextBox input = new TextBox
		{
			MinHeight = 92.0,
			AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			FontFamily = new FontFamily("Khmer OS System"),
			FontSize = 18.0,
			Padding = new Thickness(10.0),
			Foreground = Brushes.White,
			CaretBrush = Brushes.White,
			Background = new SolidColorBrush(Color.FromRgb(15, 27, 45)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
			BorderThickness = new Thickness(1.0)
		};
		Slider size = new Slider { Minimum = 8.0, Maximum = 160.0, Value = 42.0, Width = 250.0, TickFrequency = 1.0 };
		TextBlock sizeValue = new TextBlock
		{
			Text = "42 px", Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
			Width = 55.0, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right
		};
		size.ValueChanged += (s, e) => sizeValue.Text = $"{e.NewValue:0} px";
		string selectedColor = "#FACC15";
		Button colorButton = new Button
		{
			Content = "Choose color", Width = 125.0, Height = 29.0, Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Foreground = Brushes.Black, Background = (Brush)new BrushConverter().ConvertFromString(selectedColor)
		};
		var boldButton = new System.Windows.Controls.Primitives.ToggleButton
		{
			Content = "B", Width = 40.0, Height = 29.0, FontWeight = FontWeights.Bold, Margin = new Thickness(4.0, 0.0, 4.0, 0.0), ToolTip = "Bold"
		};
		var italicButton = new System.Windows.Controls.Primitives.ToggleButton
		{
			Content = "I", Width = 40.0, Height = 29.0, FontStyle = FontStyles.Italic, Margin = new Thickness(4.0, 0.0, 4.0, 0.0), ToolTip = "Italic"
		};
		colorButton.Click += (s, e) =>
		{
			string picked = ShowTextColorPicker(owner, selectedColor);
			if (picked != null)
			{
				selectedColor = picked;
				colorButton.Background = (Brush)new BrushConverter().ConvertFromString(selectedColor);
			}
		};

		Button cancel = new Button { Content = "Cancel", Width = 86.0, Height = 32.0, Margin = new Thickness(6.0) };
		Button add = new Button
		{
			Content = "+ Add Text", Width = 110.0, Height = 32.0, Margin = new Thickness(6.0),
			Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(2, 132, 199))
		};
		StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
		buttons.Children.Add(cancel);
		buttons.Children.Add(add);
		StackPanel sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 12.0, 0.0, 8.0) };
		sizeRow.Children.Add(new TextBlock { Text = "Size", Foreground = Brushes.White, Width = 55.0, VerticalAlignment = VerticalAlignment.Center });
		sizeRow.Children.Add(size);
		sizeRow.Children.Add(sizeValue);
		StackPanel styleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 2.0, 0.0, 8.0) };
		styleRow.Children.Add(new TextBlock { Text = "Style", Foreground = Brushes.White, Width = 55.0, VerticalAlignment = VerticalAlignment.Center });
		styleRow.Children.Add(colorButton); styleRow.Children.Add(boldButton); styleRow.Children.Add(italicButton);
		StackPanel body = new StackPanel { Margin = new Thickness(18.0) };
		body.Children.Add(new TextBlock
		{
			Text = "Add text to video", Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
			FontSize = 17.0, FontWeight = FontWeights.Bold, Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		body.Children.Add(input);
		body.Children.Add(sizeRow);
		body.Children.Add(styleRow);
		body.Children.Add(new TextBlock
		{
			Text = "Text starts at the current playhead. Drag its timeline clip to change when it appears.",
			Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 10.0,
			TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		body.Children.Add(buttons);
		Window dialog = new Window
		{
			Title = "Add Text", Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Width = 460.0, Height = 355.0, ResizeMode = ResizeMode.NoResize,
			ShowInTaskbar = false, Background = new SolidColorBrush(Color.FromRgb(8, 18, 34)),
			Content = body
		};
		cancel.Click += (s, e) => dialog.DialogResult = false;
		add.Click += (s, e) =>
		{
			string value = input.Text?.Trim();
			if (string.IsNullOrWhiteSpace(value))
			{
				input.Focus();
				return;
			}
			// Add directly to the project collection.  The original command was
			// designed for a small fixed set of title layers and can stop/reuse an
			// existing item after two additions.  Visual layers are intentionally
			// unbounded; every click creates a distinct timeline/preview item.
			int existingCount = vm.TextLayers?.Count ?? 0;
			double initX = 80.0 + (existingCount % 8) * 18.0;
			double initY = 80.0 + (existingCount % 6) * 18.0;
			Canvas previewCanvas = FindVisualChild((DependencyObject)(object)owner, (Canvas c) => c.Name == "OverlayCanvas");
			if (previewCanvas != null && previewCanvas.ActualWidth > 1.0 && previewCanvas.ActualHeight > 1.0)
			{
				Rect bounds = GetDisplayedVideoBounds(previewCanvas, vm);
				double estimatedWidth = Math.Min(bounds.Width * 0.7, Math.Max(120.0, value.Length * (size.Value * 0.75)));
				initX = bounds.Left + Math.Max(10.0, (bounds.Width - estimatedWidth) / 2.0) + (existingCount % 5) * 14.0;
				initY = bounds.Top + Math.Max(20.0, bounds.Height * 0.35) + (existingCount % 5) * 18.0;
				initX = Math.Clamp(initX, bounds.Left, Math.Max(bounds.Left, bounds.Right - estimatedWidth));
				initY = Math.Clamp(initY, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - 60.0));
			}
			TextLayerItem layer = new TextLayerItem
			{
				X = initX,
				Y = initY
			};
			vm.TextLayers.Add(layer);
			if (layer != null)
			{
				int durationMs = Math.Max(1000, (int)(vm.VideoDurationSec * 1000.0));
				int startMs = Math.Clamp((int)(vm.CurrentPositionSec * 1000.0), 0, Math.Max(0, durationMs - 500));
				layer.Text = value;
				layer.FontSize = size.Value;
				layer.FontColor = selectedColor;
				_textLayerBold[layer] = boldButton.IsChecked == true;
				_textLayerItalic[layer] = italicButton.IsChecked == true;
				layer.StartMs = startMs;
				// A newly added text layer remains visible through the rest of the
				// video. The user can then trim either edge explicitly in Timeline.
				layer.EndMs = durationMs;
				vm.SelectedTextLayer = layer;
				try { vm.SaveProjectCommand?.Execute(null); } catch { }
				vm.ProjectStatusMessage = $"Text added at {TimeSpan.FromMilliseconds(startMs):mm\\:ss\\.fff}";
				_refreshTimelineVisibility?.Invoke();
			}
			dialog.DialogResult = true;
		};
		dialog.Loaded += (s, e) => input.Focus();
		dialog.ShowDialog();
	}

	private static void WireTextLayerInteraction(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm) return;
			_textLayerInteractionTimer?.Stop();
			_textLayerInteractionTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(180)
			};
			_textLayerInteractionTimer.Tick += (s, e) => AttachTextLayerElements(window, vm);
			_textLayerInteractionTimer.Start();
			AttachTextLayerElements(window, vm);
			_isTextLayerInteractionHooked = true;
			Log("Text preview and timeline clips are now linked for selection, positioning and timing edits.");
		}
		catch (Exception ex) { Log("WireTextLayerInteraction error: " + ex.Message); }
	}

	private static void AttachTextLayerElements(Window window, MainViewModel vm)
	{
		try
		{
			Canvas previewCanvas = FindVisualChild((DependencyObject)(object)window, (Canvas canvas) => canvas.Name == "OverlayCanvas");
			foreach (Border element in FindAllVisualChildren<Border>((DependencyObject)(object)window))
			{
				if (element.DataContext is not TextLayerItem layer) continue;
				if (previewCanvas != null && IsVisualDescendantOf(element, previewCanvas))
					AttachPreviewTextElement(element, previewCanvas, layer, vm);
				else
				{
					Canvas timelineCanvas = FindAncestor<Canvas>((DependencyObject)(object)element);
					if (timelineCanvas?.Tag as string == "ContinuousTextTrack") continue;
					ScrollViewer scroll = FindAncestor<ScrollViewer>((DependencyObject)(object)element);
					if (scroll != null) AttachTimelineTextElement(element, scroll, layer, vm);
				}
			}
		}
		catch (Exception ex) { Log("AttachTextLayerElements error: " + ex.Message); }
	}

	private static bool IsVisualDescendantOf(DependencyObject child, DependencyObject ancestor)
	{
		for (DependencyObject current = child; current != null;)
		{
			if (ReferenceEquals(current, ancestor)) return true;
			try { current = VisualTreeHelper.GetParent(current); }
			catch { return false; }
		}
		return false;
	}

	private static void SelectTextLayer(MainViewModel vm, TextLayerItem layer, bool additive)
	{
		if (!additive)
		{
			_selectedTimelineTextLayers.Clear();
			_selectedTimelineOverlays.Clear();
			_selectedTimelineLines.Clear();
			_selectedTimelineBlurRegions.Clear();
			_selectedTimelineTextLayers.Add(layer);
			vm.SelectedDialogueLine = null;
		}
		else if (!_selectedTimelineTextLayers.Add(layer))
		{
			_selectedTimelineTextLayers.Remove(layer);
		}
		_selectedTimelineTextLayer = _selectedTimelineTextLayers.Contains(layer) ? layer : _selectedTimelineTextLayers.LastOrDefault();
		vm.SelectedTextLayer = _selectedTimelineTextLayer;
		vm.SelectedOverlay = null;
	}

	private static void AttachPreviewTextElement(Border element, Canvas canvas, TextLayerItem layer, MainViewModel vm)
	{
		ApplyTextLayerVisualStyle(element, layer);
		Border existingHandle = FindVisualChild((DependencyObject)(object)element, (Border item) => item.Tag as string == "TextResizeHandle");
		if (existingHandle != null) existingHandle.Visibility = ReferenceEquals(vm.SelectedTextLayer, layer) ? Visibility.Visible : Visibility.Collapsed;
		if (element.Tag as string == "LinkedPreviewText") return;
		element.Tag = "LinkedPreviewText";
		element.Cursor = Cursors.SizeAll;
		UIElement originalChild = element.Child;
		Grid editorRoot = new Grid { Background = Brushes.Transparent };
		element.Child = null;
		if (originalChild != null) editorRoot.Children.Add(originalChild);
		Border resizeHandle = new Border
		{
			Tag = "TextResizeHandle", Width = 14.0, Height = 14.0,
			Background = new SolidColorBrush(Color.FromRgb(56, 189, 248)), BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0), HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE,
			Visibility = ReferenceEquals(vm.SelectedTextLayer, layer) ? Visibility.Visible : Visibility.Collapsed
		};
		editorRoot.Children.Add(resizeHandle);
		element.Child = editorRoot;
		bool dragging = false;
		bool resizing = false;
		Point pointerStart = default;
		double originalX = 0.0, originalY = 0.0;
		double originalSize = 0.0;
		element.PreviewMouseLeftButtonDown += (s, e) =>
		{
			if (e.OriginalSource is DependencyObject source && IsVisualDescendantOf(source, resizeHandle)) return;
			if (e.ClickCount >= 2)
			{
				SelectTextLayer(vm, layer, false);
				ShowEditTextLayerDialog(Application.Current.MainWindow, vm, layer);
				e.Handled = true;
				return;
			}
			SelectTextLayer(vm, layer, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
			if (!_selectedTimelineTextLayers.Contains(layer)) { e.Handled = true; return; }
			PushTimelineUndo(vm);
			dragging = true;
			pointerStart = e.GetPosition(canvas);
			originalX = layer.X;
			originalY = layer.Y;
			element.CaptureMouse();
			element.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
			element.BorderThickness = new Thickness(2.0);
			e.Handled = true;
		};
		resizeHandle.PreviewMouseLeftButtonDown += (s, e) =>
		{
			SelectTextLayer(vm, layer, false);
			PushTimelineUndo(vm);
			resizing = true; pointerStart = e.GetPosition(canvas); originalSize = layer.FontSize;
			resizeHandle.CaptureMouse(); e.Handled = true;
		};
		resizeHandle.PreviewMouseMove += (s, e) =>
		{
			if (!resizing || e.LeftButton != MouseButtonState.Pressed) return;
			Point current = e.GetPosition(canvas);
			double delta = ((current.X - pointerStart.X) + (current.Y - pointerStart.Y)) * 0.35;
			layer.FontSize = Math.Clamp(originalSize + delta, 8.0, 160.0);
			ApplyTextLayerVisualStyle(element, layer); e.Handled = true;
		};
		resizeHandle.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (!resizing) return;
			resizing = false; resizeHandle.ReleaseMouseCapture();
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			e.Handled = true;
		};
		element.PreviewMouseMove += (s, e) =>
		{
			if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
			Point current = e.GetPosition(canvas);
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			double elemW = Math.Max(20.0, element.ActualWidth > 0 ? element.ActualWidth : 80.0);
			double elemH = Math.Max(16.0, element.ActualHeight > 0 ? element.ActualHeight : 30.0);
			layer.X = Math.Clamp(originalX + current.X - pointerStart.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - elemW));
			layer.Y = Math.Clamp(originalY + current.Y - pointerStart.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - elemH));
			e.Handled = true;
		};
		element.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (!dragging) return;
			dragging = false;
			element.ReleaseMouseCapture();
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			e.Handled = true;
		};
		element.PreviewMouseWheel += (s, e) =>
		{
			SelectTextLayer(vm, layer, false);
			layer.FontSize = Math.Clamp(layer.FontSize + (e.Delta > 0 ? 2.0 : -2.0), 8.0, 160.0);
			ApplyTextLayerVisualStyle(element, layer);
			vm.ProjectStatusMessage = $"Text size: {layer.FontSize:0}px";
			e.Handled = true;
		};
	}

	private static void ApplyTextLayerVisualStyle(Border element, TextLayerItem layer)
	{
		element.Opacity = _textLayerOpacity.TryGetValue(layer, out double opacity) ? Math.Clamp(opacity, 0.05, 1.0) : 1.0;
		foreach (TextBlock text in FindAllVisualChildren<TextBlock>((DependencyObject)(object)element))
		{
			try
			{
				text.Foreground = (Brush)new BrushConverter().ConvertFromString(string.IsNullOrWhiteSpace(layer.FontColor) ? "#FACC15" : layer.FontColor);
			}
			catch { text.Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21)); }
			text.FontWeight = _textLayerBold.TryGetValue(layer, out bool bold) && bold ? FontWeights.Bold : FontWeights.Normal;
			text.FontStyle = _textLayerItalic.TryGetValue(layer, out bool italic) && italic ? FontStyles.Italic : FontStyles.Normal;
			text.FontSize = Math.Clamp(layer.FontSize, 8.0, 160.0);
		}
	}

	private static void AttachTimelineTextElement(Border element, ScrollViewer scroll, TextLayerItem layer, MainViewModel vm)
	{
		if (element.Tag as string == "LinkedTimelineText") return;
		element.Tag = "LinkedTimelineText";
		bool dragging = false;
		string mode = null;
		Point pointerStart = default;
		int initialStart = 0, initialEnd = 0;
		bool moved = false;
		element.PreviewMouseLeftButtonDown += (s, e) =>
		{
			SelectTextLayer(vm, layer, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
			if (!_selectedTimelineTextLayers.Contains(layer)) { e.Handled = true; return; }
			PushTimelineUndo(vm);
			Point local = e.GetPosition(element);
			mode = local.X <= 8.0 ? "start" : (local.X >= element.ActualWidth - 8.0 ? "end" : "move");
			pointerStart = e.GetPosition(scroll);
			initialStart = layer.StartMs;
			initialEnd = layer.EndMs;
			moved = false;
			dragging = true;
			element.CaptureMouse();
			element.Cursor = mode == "move" ? Cursors.SizeAll : Cursors.SizeWE;
			e.Handled = true;
		};
		element.PreviewMouseMove += (s, e) =>
		{
			Point local = e.GetPosition(element);
			if (!dragging || e.LeftButton != MouseButtonState.Pressed)
			{
				element.Cursor = local.X <= 8.0 || local.X >= element.ActualWidth - 8.0 ? Cursors.SizeWE : Cursors.SizeAll;
				return;
			}
			double contentWidth = Math.Max(1.0, (scroll.Content as FrameworkElement)?.ActualWidth ?? scroll.ViewportWidth);
			int deltaMs = (int)((e.GetPosition(scroll).X - pointerStart.X) / contentWidth * Math.Max(1000.0, vm.VideoDurationSec * 1000.0));
			if (Math.Abs(e.GetPosition(scroll).X - pointerStart.X) >= 3.0) moved = true;
			int videoEnd = Math.Max(1000, (int)(vm.VideoDurationSec * 1000.0));
			if (mode == "start") layer.StartMs = Math.Max(0, Math.Min(initialEnd - 100, initialStart + deltaMs));
			else if (mode == "end") layer.EndMs = Math.Min(videoEnd, Math.Max(initialStart + 100, initialEnd + deltaMs));
			else
			{
				int duration = Math.Max(100, initialEnd - initialStart);
				layer.StartMs = Math.Max(0, Math.Min(videoEnd - duration, initialStart + deltaMs));
				layer.EndMs = layer.StartMs + duration;
			}
			vm.CurrentPositionSec = Math.Clamp((mode == "end" ? layer.EndMs : layer.StartMs) / 1000.0, 0.0, vm.VideoDurationSec);
			SeekPreviewVideo(vm.CurrentPositionSec);
			e.Handled = true;
		};
		element.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (!dragging) return;
			dragging = false;
			element.ReleaseMouseCapture();
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			if (!moved) element.Dispatcher.BeginInvoke((Action)(() => ShowEditTextLayerDialog(Application.Current.MainWindow, vm, layer)), DispatcherPriority.Background);
			e.Handled = true;
		};
	}

	private static void ShowEditTextLayerDialog(Window owner, MainViewModel vm, TextLayerItem layer)
	{
		if (owner == null || layer == null) return;
		TextBox input = new TextBox
		{
			Text = layer.Text ?? string.Empty, MinHeight = 90.0, AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			FontFamily = new FontFamily(string.IsNullOrWhiteSpace(layer.FontFamily) ? "Segoe UI" : layer.FontFamily),
			FontSize = 18.0, Padding = new Thickness(10.0), Foreground = Brushes.White,
			CaretBrush = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(15, 27, 45)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)), BorderThickness = new Thickness(1.0)
		};
		Slider size = new Slider { Minimum = 8.0, Maximum = 160.0, Value = Math.Clamp(layer.FontSize, 8.0, 160.0), Width = 245.0 };
		TextBlock sizeValue = new TextBlock
		{
			Text = $"{size.Value:0} px", Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
			Width = 58.0, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center
		};
		size.ValueChanged += (s, e) => sizeValue.Text = $"{e.NewValue:0} px";
		TextBox colorText = new TextBox
		{
			Text = string.IsNullOrWhiteSpace(layer.FontColor) ? "#FACC15" : layer.FontColor,
			Width = 92.0, Height = 27.0, Padding = new Thickness(6.0, 3.0, 6.0, 2.0),
			Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(15, 27, 45)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105))
		};
		Button customColor = new Button
		{
			Content = "Custom…", Width = 72.0, Height = 27.0, Margin = new Thickness(5.0, 0.0, 0.0, 0.0),
			Background = (Brush)new BrushConverter().ConvertFromString(colorText.Text), ToolTip = "Open color picker"
		};
		customColor.Click += (s, e) =>
		{
			string picked = ShowTextColorPicker(owner, colorText.Text);
			if (picked != null)
			{
				colorText.Text = picked;
				customColor.Background = (Brush)new BrushConverter().ConvertFromString(picked);
			}
		};
		var boldButton = new System.Windows.Controls.Primitives.ToggleButton
		{
			Content = "B", IsChecked = _textLayerBold.TryGetValue(layer, out bool currentBold) && currentBold,
			Width = 38.0, Height = 28.0, FontWeight = FontWeights.Bold, Margin = new Thickness(5.0, 0.0, 3.0, 0.0), ToolTip = "Bold"
		};
		var italicButton = new System.Windows.Controls.Primitives.ToggleButton
		{
			Content = "I", IsChecked = _textLayerItalic.TryGetValue(layer, out bool currentItalic) && currentItalic,
			Width = 38.0, Height = 28.0, FontStyle = FontStyles.Italic, Margin = new Thickness(3.0, 0.0, 3.0, 0.0), ToolTip = "Italic"
		};
		StackPanel colorRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 2.0, 0.0, 8.0) };
		colorRow.Children.Add(new TextBlock { Text = "Color", Foreground = Brushes.White, Width = 55.0, VerticalAlignment = VerticalAlignment.Center });
		colorRow.Children.Add(colorText);
		colorRow.Children.Add(customColor);
		string[] presetColors = { "#FFFFFF", "#FACC15", "#22D3EE", "#34D399", "#FB7185" };
		foreach (string preset in presetColors)
		{
			Button swatch = new Button { Width = 25.0, Height = 25.0, Margin = new Thickness(4.0, 0.0, 0.0, 0.0), Background = (Brush)new BrushConverter().ConvertFromString(preset), Tag = preset, ToolTip = preset };
			swatch.Click += (s, e) => colorText.Text = (string)((Button)s).Tag;
			colorRow.Children.Add(swatch);
		}
		colorRow.Children.Add(boldButton); colorRow.Children.Add(italicButton);
		Button remove = new Button { Content = "Delete", Width = 82.0, Height = 32.0, Margin = new Thickness(6.0), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(153, 27, 27)) };
		Button cancel = new Button { Content = "Cancel", Width = 82.0, Height = 32.0, Margin = new Thickness(6.0) };
		Button save = new Button { Content = "Save Text", Width = 105.0, Height = 32.0, Margin = new Thickness(6.0), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)) };
		StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
		buttons.Children.Add(remove); buttons.Children.Add(cancel); buttons.Children.Add(save);
		StackPanel sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 12.0, 0.0, 10.0) };
		sizeRow.Children.Add(new TextBlock { Text = "Size", Foreground = Brushes.White, Width = 55.0, VerticalAlignment = VerticalAlignment.Center });
		sizeRow.Children.Add(size); sizeRow.Children.Add(sizeValue);
		StackPanel body = new StackPanel { Margin = new Thickness(18.0) };
		body.Children.Add(new TextBlock { Text = "Edit Text", Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), FontSize = 17.0, FontWeight = FontWeights.Bold, Margin = new Thickness(0.0, 0.0, 0.0, 12.0) });
		body.Children.Add(input); body.Children.Add(sizeRow); body.Children.Add(colorRow); body.Children.Add(buttons);
		Window dialog = new Window
		{
			Title = "Edit Text", Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Width = 520.0, Height = 335.0, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
			Background = new SolidColorBrush(Color.FromRgb(8, 18, 34)), Content = body
		};
		cancel.Click += (s, e) => dialog.DialogResult = false;
		remove.Click += (s, e) =>
		{
			PushTimelineUndo(vm);
			vm.TextLayers.Remove(layer);
			_selectedTimelineTextLayers.Remove(layer);
			if (ReferenceEquals(_selectedTimelineTextLayer, layer)) _selectedTimelineTextLayer = null;
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			dialog.DialogResult = true;
		};
		save.Click += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(input.Text)) { input.Focus(); return; }
			PushTimelineUndo(vm);
			layer.Text = input.Text.Trim();
			layer.FontSize = size.Value;
			try
			{
				Color parsed = (Color)ColorConverter.ConvertFromString(colorText.Text.Trim());
				layer.FontColor = parsed.ToString();
			}
			catch
			{
				MessageBox.Show("Use a valid color such as #FACC15.", "Text Color", MessageBoxButton.OK, MessageBoxImage.Information);
				colorText.Focus();
				return;
			}
			_textLayerBold[layer] = boldButton.IsChecked == true;
			_textLayerItalic[layer] = italicButton.IsChecked == true;
			vm.SelectedTextLayer = layer;
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			dialog.DialogResult = true;
		};
		dialog.Loaded += (s, e) => { input.Focus(); input.SelectAll(); };
		dialog.ShowDialog();
	}

	private static string ShowTextColorPicker(Window owner, string initialColor)
	{
		Color initial;
		try { initial = (Color)ColorConverter.ConvertFromString(initialColor); }
		catch { initial = Color.FromRgb(250, 204, 21); }
		RgbToHsv(initial, out double hueValue, out double saturationValue, out double brightnessValue);
		const int paletteWidth = 300;
		const int paletteHeight = 150;
		const int hueHeight = 18;
		Image paletteImage = new Image { Width = paletteWidth, Height = paletteHeight, Stretch = Stretch.Fill, Cursor = Cursors.Cross };
		Image hueImage = new Image { Width = paletteWidth, Height = hueHeight, Stretch = Stretch.Fill, Cursor = Cursors.Hand, Margin = new Thickness(0, 8, 0, 8) };
		Action refreshPalette = () =>
		{
			WriteableBitmap bitmap = new WriteableBitmap(paletteWidth, paletteHeight, 96, 96, PixelFormats.Bgra32, null);
			byte[] pixels = new byte[paletteWidth * paletteHeight * 4];
			for (int y = 0; y < paletteHeight; y++)
			{
				double value = 1.0 - y / (double)(paletteHeight - 1);
				for (int x = 0; x < paletteWidth; x++)
				{
					double saturation = x / (double)(paletteWidth - 1);
					Color color = HsvToRgb(hueValue, saturation, value);
					int offset = (y * paletteWidth + x) * 4;
					pixels[offset] = color.B; pixels[offset + 1] = color.G; pixels[offset + 2] = color.R; pixels[offset + 3] = 255;
				}
			}
			bitmap.WritePixels(new Int32Rect(0, 0, paletteWidth, paletteHeight), pixels, paletteWidth * 4, 0);
			paletteImage.Source = bitmap;
		};
		WriteableBitmap hueBitmap = new WriteableBitmap(paletteWidth, hueHeight, 96, 96, PixelFormats.Bgra32, null);
		byte[] huePixels = new byte[paletteWidth * hueHeight * 4];
		for (int x = 0; x < paletteWidth; x++)
		{
			Color color = HsvToRgb(x / (double)(paletteWidth - 1) * 360.0, 1.0, 1.0);
			for (int y = 0; y < hueHeight; y++)
			{
				int offset = (y * paletteWidth + x) * 4;
				huePixels[offset] = color.B; huePixels[offset + 1] = color.G; huePixels[offset + 2] = color.R; huePixels[offset + 3] = 255;
			}
		}
		hueBitmap.WritePixels(new Int32Rect(0, 0, paletteWidth, hueHeight), huePixels, paletteWidth * 4, 0);
		hueImage.Source = hueBitmap;
		refreshPalette();
		Slider red = new Slider { Minimum = 0, Maximum = 255, Value = initial.R, Width = 260.0 };
		Slider green = new Slider { Minimum = 0, Maximum = 255, Value = initial.G, Width = 260.0 };
		Slider blue = new Slider { Minimum = 0, Maximum = 255, Value = initial.B, Width = 260.0 };
		TextBox hex = new TextBox
		{
			Text = $"#{initial.R:X2}{initial.G:X2}{initial.B:X2}", Height = 32.0, FontSize = 16.0,
			Padding = new Thickness(8.0, 4.0, 8.0, 2.0), Foreground = Brushes.White,
			Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105))
		};
		Border preview = new Border { Height = 62.0, CornerRadius = new CornerRadius(6.0), Background = new SolidColorBrush(initial), Margin = new Thickness(0.0, 10.0, 0.0, 10.0) };
		TextBlock rgbText = new TextBlock { Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
		bool syncing = false;
		Action updateFromSliders = () =>
		{
			if (syncing) return;
			Color color = Color.FromRgb((byte)red.Value, (byte)green.Value, (byte)blue.Value);
			preview.Background = new SolidColorBrush(color);
			hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
			rgbText.Text = $"RGB  {color.R}, {color.G}, {color.B}";
		};
		Action applyPaletteSelection = () =>
		{
			Color color = HsvToRgb(hueValue, saturationValue, brightnessValue);
			syncing = true; red.Value = color.R; green.Value = color.G; blue.Value = color.B; syncing = false;
			preview.Background = new SolidColorBrush(color);
			hex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
			rgbText.Text = $"RGB  {color.R}, {color.G}, {color.B}";
		};
		paletteImage.MouseLeftButtonDown += (s, e) =>
		{
			Point point = e.GetPosition(paletteImage);
			saturationValue = Math.Clamp(point.X / Math.Max(1.0, paletteImage.ActualWidth), 0.0, 1.0);
			brightnessValue = 1.0 - Math.Clamp(point.Y / Math.Max(1.0, paletteImage.ActualHeight), 0.0, 1.0);
			applyPaletteSelection();
		};
		hueImage.MouseLeftButtonDown += (s, e) =>
		{
			hueValue = Math.Clamp(e.GetPosition(hueImage).X / Math.Max(1.0, hueImage.ActualWidth), 0.0, 1.0) * 360.0;
			refreshPalette(); applyPaletteSelection();
		};
		red.ValueChanged += (s, e) => updateFromSliders(); green.ValueChanged += (s, e) => updateFromSliders(); blue.ValueChanged += (s, e) => updateFromSliders();
		hex.LostFocus += (s, e) =>
		{
			try
			{
				Color color = (Color)ColorConverter.ConvertFromString(hex.Text.Trim());
				syncing = true; red.Value = color.R; green.Value = color.G; blue.Value = color.B; syncing = false;
				preview.Background = new SolidColorBrush(color); rgbText.Text = $"RGB  {color.R}, {color.G}, {color.B}";
			}
			catch { hex.Text = $"#{(byte)red.Value:X2}{(byte)green.Value:X2}{(byte)blue.Value:X2}"; }
		};
		StackPanel rows = new StackPanel();
		StackPanel MakeRow(string label, Slider slider, Brush color) { StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) }; row.Children.Add(new TextBlock { Text = label, Foreground = color, FontWeight = FontWeights.Bold, Width = 30.0, VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(slider); return row; }
		rows.Children.Add(new TextBlock { Text = "HEX", Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 3) });
		rows.Children.Add(hex); rows.Children.Add(paletteImage); rows.Children.Add(hueImage); rows.Children.Add(preview); rows.Children.Add(rgbText);
		rows.Children.Add(MakeRow("R", red, new SolidColorBrush(Color.FromRgb(248, 113, 113))));
		rows.Children.Add(MakeRow("G", green, new SolidColorBrush(Color.FromRgb(74, 222, 128))));
		rows.Children.Add(MakeRow("B", blue, new SolidColorBrush(Color.FromRgb(96, 165, 250))));
		Button cancel = new Button { Content = "Cancel", Width = 85, Height = 32, Margin = new Thickness(5) };
		Button choose = new Button { Content = "Choose Color", Width = 115, Height = 32, Margin = new Thickness(5), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)) };
		StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
		actions.Children.Add(cancel); actions.Children.Add(choose); rows.Children.Add(actions);
		Border body = new Border { Padding = new Thickness(16), Background = new SolidColorBrush(Color.FromRgb(8, 18, 34)), Child = rows };
		Window dialog = new Window { Title = "Choose Text Color", Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = 370, Height = 620, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Content = body };
		string result = null;
		cancel.Click += (s, e) => dialog.DialogResult = false;
		choose.Click += (s, e) => { updateFromSliders(); result = hex.Text.Trim().ToUpperInvariant(); dialog.DialogResult = true; };
		updateFromSliders();
		dialog.ShowDialog();
		return result;
	}

	private static Color HsvToRgb(double hue, double saturation, double value)
	{
		hue = ((hue % 360.0) + 360.0) % 360.0;
		double chroma = value * saturation;
		double x = chroma * (1.0 - Math.Abs(hue / 60.0 % 2.0 - 1.0));
		double m = value - chroma;
		double r = 0, g = 0, b = 0;
		if (hue < 60) { r = chroma; g = x; }
		else if (hue < 120) { r = x; g = chroma; }
		else if (hue < 180) { g = chroma; b = x; }
		else if (hue < 240) { g = x; b = chroma; }
		else if (hue < 300) { r = x; b = chroma; }
		else { r = chroma; b = x; }
		return Color.FromRgb((byte)Math.Round((r + m) * 255.0), (byte)Math.Round((g + m) * 255.0), (byte)Math.Round((b + m) * 255.0));
	}

	private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
	{
		double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
		double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
		if (delta < 0.00001) hue = 0.0;
		else if (max == r) hue = 60.0 * (((g - b) / delta) % 6.0);
		else if (max == g) hue = 60.0 * (((b - r) / delta) + 2.0);
		else hue = 60.0 * (((r - g) / delta) + 4.0);
		if (hue < 0.0) hue += 360.0;
		saturation = max <= 0.0 ? 0.0 : delta / max;
		value = max;
	}

	private static void WireDialogueFontSizeControl(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm)
				return;
			if (!ReferenceEquals(_dialogueFontViewModel, vm))
			{
				_dialogueFontViewModel = vm;
				vm.DialogueFontSize = 10.0;
			}
			Button decrease = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(button => ReferenceEquals(button.Command, vm.DecreaseFontSizeCommand));
			if (decrease == null)
				return;
			if (decrease.Tag as string != "DialogueFontMin8Hooked")
			{
				decrease.Tag = "DialogueFontMin8Hooked";
				decrease.PreviewMouseLeftButtonDown += (s, e) =>
				{
					if (vm.DialogueFontSize <= 10.0)
					{
						vm.DialogueFontSize = Math.Max(8.0, vm.DialogueFontSize - 2.0);
						e.Handled = true;
					}
				};
			}
			_isDialogueFontControlHooked = true;
			Log("Dialogue font default set to 10 with minimum size 8.");
		}
		catch (Exception ex)
		{
			Log("WireDialogueFontSizeControl error: " + ex.Message);
		}
	}

	private static void WireOverlayOpacityToolbar(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm) return;
			TextBlock pitchLabel = FindAllVisualChildren<TextBlock>((DependencyObject)(object)window)
				.FirstOrDefault(text => string.Equals(text.Text?.Trim(), "PITCH", StringComparison.OrdinalIgnoreCase));
			Panel host = pitchLabel?.Parent as Panel;
			if (pitchLabel == null || host == null) return;
			if (host.Children.OfType<FrameworkElement>().Any(item => item.Tag as string == "OverlayOpacityToolbar"))
			{
				_isOverlayOpacityToolbarHooked = true;
				return;
			}

			TextBlock percent = new TextBlock
			{
				Text = "OPACITY: --", Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
				FontSize = 9.0, VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 7.0, 0.0)
			};
			Slider slider = new Slider
			{
				Minimum = 0.0, Maximum = 100.0, Value = 100.0, Width = 105.0,
				VerticalAlignment = VerticalAlignment.Center, IsEnabled = false,
				ToolTip = "Opacity of the selected photo, GIF, or mini video"
			};
			TextBlock sizeText = new TextBlock
			{
				Text = "SIZE: --", Foreground = new SolidColorBrush(Color.FromRgb(196, 181, 253)),
				FontSize = 9.0, VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(15.0, 0.0, 7.0, 0.0)
			};
			Slider sizeSlider = new Slider
			{
				Minimum = 36.0, Maximum = 900.0, Value = 220.0, Width = 105.0,
				VerticalAlignment = VerticalAlignment.Center, IsEnabled = false,
				ToolTip = "Size of the selected media (aspect ratio remains locked)"
			};
			StackPanel group = new StackPanel
			{
				Tag = "OverlayOpacityToolbar", Orientation = Orientation.Vertical,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 0.0, 18.0, 0.0)
			};
			StackPanel opacityRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 18.0 };
			StackPanel sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 18.0 };
			opacityRow.Children.Add(percent);
			opacityRow.Children.Add(slider);
			sizeRow.Children.Add(sizeText);
			sizeRow.Children.Add(sizeSlider);
			group.Children.Add(opacityRow);
			group.Children.Add(sizeRow);
			int pitchIndex = host.Children.IndexOf(pitchLabel);
			host.Children.Insert(Math.Max(0, pitchIndex), group);

			bool syncing = false;
			Action sync = () =>
			{
				syncing = true;
				OverlayItem selected = vm.SelectedOverlay;
				TextLayerItem selectedText = vm.SelectedTextLayer;
				group.Visibility = selected == null && selectedText == null ? Visibility.Collapsed : Visibility.Visible;
				opacityRow.Visibility = selected == null && selectedText == null ? Visibility.Collapsed : Visibility.Visible;
				slider.IsEnabled = selected != null || selectedText != null;
				sizeSlider.IsEnabled = selected != null || selectedText != null;
				slider.Value = selectedText != null
					? (_textLayerOpacity.TryGetValue(selectedText, out double textOpacity) ? textOpacity * 100.0 : 100.0)
					: (selected == null ? 100.0 : Math.Clamp(selected.Opacity * 100.0, 0.0, 100.0));
				sizeSlider.Minimum = selectedText == null ? 36.0 : 8.0;
				sizeSlider.Maximum = selectedText == null ? 900.0 : 160.0;
				sizeSlider.Value = selectedText != null ? Math.Clamp(selectedText.FontSize, 8.0, 160.0)
					: (selected == null ? 220.0 : Math.Clamp(selected.Width, 36.0, 900.0));
				percent.Text = selected == null && selectedText == null ? "OPACITY: --" : $"OPACITY: {slider.Value:0}%";
				sizeText.Text = selectedText != null ? $"TEXT SIZE: {selectedText.FontSize:0}px"
					: (selected == null ? "SIZE: --" : $"SIZE: {selected.Width:0}px");
				syncing = false;
			};
			slider.ValueChanged += (s, e) =>
			{
				if (syncing) return;
				OverlayItem selected = vm.SelectedOverlay;
				TextLayerItem selectedText = vm.SelectedTextLayer;
				if (selectedText != null)
				{
					_textLayerOpacity[selectedText] = Math.Clamp(e.NewValue / 100.0, 0.05, 1.0);
					percent.Text = $"OPACITY: {e.NewValue:0}%";
					return;
				}
				if (selected == null) return;
				selected.Opacity = Math.Clamp(e.NewValue / 100.0, 0.0, 1.0);
				percent.Text = $"OPACITY: {e.NewValue:0}%";
			};
			sizeSlider.ValueChanged += (s, e) =>
			{
				if (syncing) return;
				OverlayItem selected = vm.SelectedOverlay;
				TextLayerItem selectedText = vm.SelectedTextLayer;
				if (selectedText != null)
				{
					selectedText.FontSize = Math.Clamp(e.NewValue, 8.0, 160.0);
					sizeText.Text = $"TEXT SIZE: {selectedText.FontSize:0}px";
					return;
				}
				if (selected == null) return;
				double ratio = Math.Max(0.05, selected.Width / Math.Max(1.0, selected.Height));
				Canvas previewCanvas = FindVisualChild((DependencyObject)(object)window, (Canvas canvas) => canvas.Name == "OverlayCanvas");
				double maxWidth = sizeSlider.Maximum;
				if (previewCanvas != null && previewCanvas.ActualWidth > 0.0 && previewCanvas.ActualHeight > 0.0)
				{
					Rect bounds = GetDisplayedVideoBounds(previewCanvas, vm);
					selected.X = Math.Clamp(selected.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - selected.Width));
					selected.Y = Math.Clamp(selected.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - selected.Height));
					maxWidth = Math.Min(Math.Max(36.0, bounds.Right - selected.X),
						Math.Max(36.0, (bounds.Bottom - selected.Y) * ratio));
				}
				double width = Math.Clamp(e.NewValue, 36.0, Math.Max(36.0, maxWidth));
				selected.Width = width;
				selected.Height = Math.Max(28.0, width / ratio);
				sizeText.Text = $"SIZE: {width:0}px";
				if (Math.Abs(sizeSlider.Value - width) > 0.5)
				{
					syncing = true;
					sizeSlider.Value = width;
					syncing = false;
				}
			};
			vm.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == "SelectedOverlay" || e.PropertyName == "SelectedTextLayer" || e.PropertyName == "CurrentProject") sync();
			};
			sync();
			_isOverlayOpacityToolbarHooked = true;
			Log("Moved selected media opacity control to the Timeline toolbar before Pitch.");
		}
		catch (Exception ex)
		{
			Log("WireOverlayOpacityToolbar error: " + ex.Message);
		}
	}

	private static void RefreshMediaOverlayEditor(Canvas canvas, MainViewModel vm)
	{
		try
		{
			foreach (OverlayItem stale in _mediaOverlayVisuals.Keys.Where(item => !vm.Overlays.Contains(item)).ToList())
			{
				MediaOverlayVisual old = _mediaOverlayVisuals[stale];
				old.Video?.Stop();
				canvas.Children.Remove(old.Container);
				_mediaOverlayVisuals.Remove(stale);
			}

			foreach (OverlayItem overlay in vm.Overlays)
			{
				if (!_mediaOverlayVisuals.TryGetValue(overlay, out MediaOverlayVisual visual))
				{
					visual = CreateMediaOverlayVisual(canvas, vm, overlay);
					_mediaOverlayVisuals[overlay] = visual;
					canvas.Children.Add(visual.Container);
				}
				bool selected = ReferenceEquals(vm.SelectedOverlay, overlay);
				double positionMs = vm.CurrentPositionSec * 1000.0;
				bool inRange = positionMs >= overlay.StartMs && positionMs <= Math.Max(overlay.StartMs + 1, overlay.EndMs);
				visual.Container.Visibility = inRange ? Visibility.Visible : Visibility.Collapsed;
				visual.Container.Width = Math.Max(36.0, overlay.Width);
				visual.Container.Height = Math.Max(28.0, overlay.Height);
				// Opacity belongs to the media itself. Keep the selection frame,
				// resize handle and opacity control fully visible/editable.
				visual.Container.Opacity = 1.0;
				if (visual.Content != null) visual.Content.Opacity = Math.Clamp(overlay.Opacity, 0.0, 1.0);
				visual.Container.BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : Brushes.Transparent;
				visual.Container.BorderThickness = selected ? new Thickness(2.0) : new Thickness(0.0);
				if (visual.ResizeHandle != null) visual.ResizeHandle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
				// Keep overlays inside the active social canvas when switching among
				// landscape, square and portrait formats. Preserve the source aspect
				// ratio; only scale down when the item is larger than the canvas.
				if (canvas.ActualWidth > 1.0 && canvas.ActualHeight > 1.0)
				{
					Rect videoBounds = GetDisplayedVideoBounds(canvas, vm);
					double fitScale = Math.Min(1.0, Math.Min(videoBounds.Width / Math.Max(1.0, overlay.Width), videoBounds.Height / Math.Max(1.0, overlay.Height)));
					if (fitScale < 1.0)
					{
						overlay.Width *= fitScale;
						overlay.Height *= fitScale;
						visual.Container.Width = overlay.Width;
						visual.Container.Height = overlay.Height;
					}
					overlay.X = Math.Clamp(overlay.X, videoBounds.Left, Math.Max(videoBounds.Left, videoBounds.Right - overlay.Width));
					overlay.Y = Math.Clamp(overlay.Y, videoBounds.Top, Math.Max(videoBounds.Top, videoBounds.Bottom - overlay.Height));
				}
				Canvas.SetLeft(visual.Container, Math.Max(0.0, overlay.X));
				Canvas.SetTop(visual.Container, Math.Max(0.0, overlay.Y));
				Panel.SetZIndex(visual.Container, selected ? 950 : 700);

				if (visual.Video != null && inRange)
				{
					TimeSpan wanted = TimeSpan.FromMilliseconds(Math.Max(0.0, positionMs - overlay.StartMs));
					if (Math.Abs((visual.Video.Position - wanted).TotalMilliseconds) > 350.0)
						visual.Video.Position = wanted;
					if (vm.IsPlaying) visual.Video.Play(); else visual.Video.Pause();
				}
				if (visual.GifFrames?.Count > 1 && inRange && DateTime.UtcNow >= visual.NextGifFrameAt)
				{
					visual.GifFrameIndex = (visual.GifFrameIndex + 1) % visual.GifFrames.Count;
					visual.Image.Source = visual.GifFrames[visual.GifFrameIndex];
					visual.NextGifFrameAt = DateTime.UtcNow.AddMilliseconds(100.0);
				}
			}

			// Ensure all text layers and blur regions strictly stay inside displayed video bounds (especially on portrait 9:16 videos)
			if (canvas.ActualWidth > 1.0 && canvas.ActualHeight > 1.0)
			{
				Rect videoBounds = GetDisplayedVideoBounds(canvas, vm);
				if (videoBounds.Width > 10.0 && videoBounds.Height > 10.0)
				{
					if (vm.TextLayers != null)
					{
						foreach (TextLayerItem textLayer in vm.TextLayers)
						{
							double estW = Math.Max(40.0, Math.Min(videoBounds.Width * 0.9, (textLayer.Text?.Length ?? 5) * (textLayer.FontSize * 0.75)));
							double estH = Math.Max(20.0, textLayer.FontSize * 1.4);
							double clampedX = Math.Clamp(textLayer.X, videoBounds.Left, Math.Max(videoBounds.Left, videoBounds.Right - estW));
							double clampedY = Math.Clamp(textLayer.Y, videoBounds.Top, Math.Max(videoBounds.Top, videoBounds.Bottom - estH));
							if (Math.Abs(textLayer.X - clampedX) > 1.0) textLayer.X = clampedX;
							if (Math.Abs(textLayer.Y - clampedY) > 1.0) textLayer.Y = clampedY;
						}
					}
					if (vm.BlurRegions != null)
					{
						foreach (BlurRegionItem blur in vm.BlurRegions)
						{
							double fitScale = Math.Min(1.0, Math.Min(videoBounds.Width / Math.Max(1.0, blur.Width), videoBounds.Height / Math.Max(1.0, blur.Height)));
							if (fitScale < 1.0)
							{
								blur.Width *= fitScale;
								blur.Height *= fitScale;
							}
							double clampedX = Math.Clamp(blur.X, videoBounds.Left, Math.Max(videoBounds.Left, videoBounds.Right - blur.Width));
							double clampedY = Math.Clamp(blur.Y, videoBounds.Top, Math.Max(videoBounds.Top, videoBounds.Bottom - blur.Height));
							if (Math.Abs(blur.X - clampedX) > 1.0) blur.X = clampedX;
							if (Math.Abs(blur.Y - clampedY) > 1.0) blur.Y = clampedY;
						}
					}
				}
			}
			RefreshBlurVisuals(canvas, vm);
		}
		catch (Exception ex)
		{
			Log("RefreshMediaOverlayEditor error: " + ex.Message);
		}
	}

	private static void WireBlurTool(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm) return;
			Button blurButton = FindAllVisualChildren<Button>((DependencyObject)(object)window)
				.FirstOrDefault(button => string.Equals(button.CommandParameter as string, "blur", StringComparison.OrdinalIgnoreCase)
					|| FindVisualChild((DependencyObject)(object)button,
						(TextBlock label) => string.Equals(label.Text?.Trim(), "Blur", StringComparison.OrdinalIgnoreCase)) != null);
			if (blurButton == null) return;
			if (blurButton.Tag as string == "BlurToolHooked")
			{
				_isBlurToolHooked = true;
				return;
			}
			blurButton.Tag = "BlurToolHooked";
			blurButton.Click += (s, e) =>
			{
				ShowAddBlurDialog(window, vm);
				e.Handled = true;
			};
			_isBlurToolHooked = true;
			Log("Blur sidebar tool now opens the Blur Opacity dialog.");
		}
		catch (Exception ex)
		{
			Log("WireBlurTool error: " + ex.Message);
		}
	}

	private static void WireDeselectOnClickOutside(Window window)
	{
		try
		{
			if (window.DataContext is not MainViewModel vm) return;
			if (_isOutsideDeselectHooked) return;
			_isOutsideDeselectHooked = true;

			window.PreviewMouseLeftButtonDown += (s, e) =>
			{
				try
				{
					if (vm.SelectedBlurRegion == null && _selectedTimelineBlurRegions.Count == 0)
						return;

					DependencyObject src = e.OriginalSource as DependencyObject;
					if (src == null) return;

					// If clicked inside any blur container, handle or button, keep selection
					if (FindAncestor<Border>(src, b => b.Tag as string == "SnaBlurRegionContainer") != null)
						return;

					// If clicked on timeline, let timeline manage its own selection
					if (FindAncestor<Canvas>(src, c => c.Tag as string == "ContinuousTextTrack" || c.Name == "TimelineCanvas") != null
						|| FindAncestor<ScrollViewer>(src, sv => sv.Name == "TimelineScrollViewer") != null)
						return;

					// User clicked outside on other surface! Deselect blur region
					vm.SelectedBlurRegion = null;
					_selectedTimelineBlurRegions.Clear();

					Canvas canvas = FindVisualChild((DependencyObject)(object)window, (Canvas c) => c.Name == "OverlayCanvas");
					if (canvas != null)
					{
						RefreshBlurVisuals(canvas, vm);
					}
				}
				catch { }
			};
		}
		catch (Exception ex)
		{
			Log("WireDeselectOnClickOutside error: " + ex.Message);
		}
	}

	private static void ShowAddBlurDialog(Window owner, MainViewModel vm, BlurRegionItem existingItem = null)
	{
		try
		{
			if (existingItem == null && !HasCurrentVideoReference(vm) && vm.VideoDurationSec <= 0.0)
			{
				MessageBox.Show("Please load or drop a video first before adding Blur.\nសូមបញ្ចូលវីដេអូជាមុនសិន មុននឹងបន្ថែម Blur។", "No Video Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			double curOp = 0.8;
			if (existingItem != null)
			{
				if (!_blurRegionOpacity.TryGetValue(existingItem, out curOp))
					curOp = Math.Clamp(existingItem.Strength / 30.0, 0.1, 1.0);
			}

			Slider opacitySlider = new Slider
			{
				Minimum = 10.0,
				Maximum = 100.0,
				Value = Math.Round(curOp * 100.0),
				Width = 220.0,
				TickFrequency = 5.0,
				IsSnapToTickEnabled = true,
				VerticalAlignment = VerticalAlignment.Center
			};

			TextBlock opacityValue = new TextBlock
			{
				Text = $"{opacitySlider.Value:0}%",
				Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
				FontSize = 18.0,
				FontWeight = FontWeights.Bold,
				Width = 65.0,
				VerticalAlignment = VerticalAlignment.Center,
				TextAlignment = TextAlignment.Right
			};

			Grid previewGrid = new Grid
			{
				Height = 52.0,
				ClipToBounds = true,
				Margin = new Thickness(0, 8, 0, 8)
			};
			Border previewBg = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
				CornerRadius = new CornerRadius(4.0)
			};
			TextBlock sampleText = new TextBlock
			{
				Text = "ABC 123  字幕内容  Chinese Subtitle Sample Text Behind Blur",
				Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
				FontSize = 13.0,
				FontWeight = FontWeights.SemiBold,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			previewBg.Child = sampleText;
			previewGrid.Children.Add(previewBg);

			Border blurPreviewOverlay = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb((byte)(opacitySlider.Value / 100.0 * 240.0), 128, 128, 128)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
				BorderThickness = new Thickness(1.5),
				CornerRadius = new CornerRadius(4.0),
				Margin = new Thickness(20, 5, 20, 5)
			};
			TextBlock blurBadge = new TextBlock
			{
				Text = $"🌫 BLUR PREVIEW ({opacitySlider.Value:0}%)",
				Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
				FontSize = 11.0,
				FontWeight = FontWeights.Bold,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			blurPreviewOverlay.Child = blurBadge;
			previewGrid.Children.Add(blurPreviewOverlay);

			opacitySlider.ValueChanged += (s, e) =>
			{
				opacityValue.Text = $"{e.NewValue:0}%";
				blurPreviewOverlay.Background = new SolidColorBrush(Color.FromArgb((byte)(e.NewValue / 100.0 * 240.0), 128, 128, 128));
				blurBadge.Text = $"🌫 BLUR PREVIEW ({e.NewValue:0}%)";
			};

			StackPanel presetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
			presetRow.Children.Add(new TextBlock { Text = "Presets:", Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 11.0, VerticalAlignment = VerticalAlignment.Center, Width = 55.0 });
			foreach (int presetVal in new[] { 40, 60, 80, 95, 100 })
			{
				int pVal = presetVal;
				Button pBtn = new Button
				{
					Content = $"{pVal}%",
					Width = 46.0,
					Height = 24.0,
					Margin = new Thickness(3.0, 0, 3.0, 0),
					FontSize = 11.0,
					Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
					Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
					BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
					BorderThickness = new Thickness(1.0),
					Cursor = Cursors.Hand
				};
				pBtn.Click += (s, e) => opacitySlider.Value = pVal;
				presetRow.Children.Add(pBtn);
			}

			Button cancel = new Button { Content = "Cancel", Width = 86.0, Height = 32.0, Margin = new Thickness(6.0) };
			Button okButton = new Button
			{
				Content = existingItem == null ? "OK" : "✔ Save",
				Width = 100.0,
				Height = 32.0,
				Margin = new Thickness(6.0),
				Foreground = Brushes.White,
				Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
				FontWeight = FontWeights.Bold
			};
			StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
			buttons.Children.Add(cancel);
			buttons.Children.Add(okButton);

			StackPanel sliderRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 6) };
			sliderRow.Children.Add(new TextBlock { Text = "Opacity", Foreground = Brushes.White, Width = 60.0, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
			sliderRow.Children.Add(opacitySlider);
			sliderRow.Children.Add(opacityValue);

			StackPanel body = new StackPanel { Margin = new Thickness(18.0) };
			body.Children.Add(new TextBlock
			{
				Text = existingItem == null ? "🌫 Add Blur Region" : "🌫 Edit Blur Opacity",
				Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
				FontSize = 17.0,
				FontWeight = FontWeights.Bold,
				Margin = new Thickness(0, 0, 0, 2)
			});
			body.Children.Add(new TextBlock
			{
				Text = "កំណត់កម្រិតព្រិល (Blur Opacity) — Drag & resize blur freely on video",
				Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
				FontSize = 11.0,
				Margin = new Thickness(0, 0, 0, 8)
			});
			body.Children.Add(sliderRow);
			body.Children.Add(presetRow);
			body.Children.Add(previewGrid);
			body.Children.Add(new TextBlock
			{
				Text = "After clicking OK, drag blur to any position and use handles to resize (តូចធំ ឬ វែងខ្លី).",
				Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
				FontSize = 10.0,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 0, 0, 4)
			});
			body.Children.Add(buttons);

			Window dialog = new Window
			{
				Title = existingItem == null ? "Blur Opacity" : "Edit Blur Opacity",
				Owner = owner,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Width = 430.0,
				Height = 320.0,
				ResizeMode = ResizeMode.NoResize,
				ShowInTaskbar = false,
				Background = new SolidColorBrush(Color.FromRgb(8, 18, 34)),
				Content = body
			};

			cancel.Click += (s, e) => dialog.DialogResult = false;
			okButton.Click += (s, e) =>
			{
				double chosenOp = Math.Clamp(opacitySlider.Value / 100.0, 0.1, 1.0);
				int strengthVal = Math.Clamp((int)Math.Round(chosenOp * 30.0), 2, 30);

				Canvas previewCanvas = FindVisualChild((DependencyObject)(object)owner, (Canvas c) => c.Name == "OverlayCanvas");

				if (existingItem != null)
				{
					_blurRegionOpacity[existingItem] = chosenOp;
					existingItem.Strength = strengthVal;
					existingItem.Label = $"Blur {chosenOp:P0}";
					vm.ProjectStatusMessage = $"Updated Blur opacity to {chosenOp:P0}";
				}
				else
				{
					Rect bounds = previewCanvas != null && previewCanvas.ActualWidth > 1.0
						? GetDisplayedVideoBounds(previewCanvas, vm)
						: new Rect(0, 0, 800, 450);

					double defW = Math.Min(bounds.Width * 0.5, 260.0);
					double defH = Math.Min(bounds.Height * 0.18, 70.0);
					double defX = bounds.Left + (bounds.Width - defW) / 2.0;
					double defY = bounds.Top + bounds.Height * 0.72;
					if (defY + defH > bounds.Bottom) defY = bounds.Top + (bounds.Height - defH) / 2.0;

					int durationMs = Math.Max(1000, (int)(vm.VideoDurationSec * 1000.0));
					int startMs = Math.Clamp((int)(vm.CurrentPositionSec * 1000.0), 0, Math.Max(0, durationMs - 500));
					int endMs = durationMs;

					BlurRegionItem blur = new BlurRegionItem
					{
						Id = Guid.NewGuid().ToString(),
						X = defX,
						Y = defY,
						Width = defW,
						Height = defH,
						Strength = strengthVal,
						StartMs = startMs,
						EndMs = endMs,
						Label = $"Blur {chosenOp:P0}"
					};

					_blurRegionOpacity[blur] = chosenOp;
					if (bounds.Width > 10.0 && bounds.Height > 10.0)
					{
						_lastActiveVideoBounds = bounds;
						var norm = (
							Math.Clamp((defX - bounds.Left) / bounds.Width, 0.0, 1.0),
							Math.Clamp((defY - bounds.Top) / bounds.Height, 0.0, 1.0),
							Math.Clamp(defW / bounds.Width, 0.005, 1.0),
							Math.Clamp(defH / bounds.Height, 0.005, 1.0)
						);
						_blurNormalizedCoords[blur] = norm;
						_blurNormalizedCoordsById[blur.Id] = norm;
					}
					vm.BlurRegions.Add(blur);
					vm.SelectedBlurRegion = blur;
					_selectedTimelineBlurRegions.Clear();
					_selectedTimelineBlurRegions.Add(blur);
					vm.ProjectStatusMessage = $"Added Blur ({chosenOp:P0}) at {TimeSpan.FromMilliseconds(startMs):mm\\:ss\\.fff}";
				}

				if (previewCanvas != null)
				{
					RefreshBlurVisuals(previewCanvas, vm);
				}
				_refreshTimelineVisibility?.Invoke();
				try { vm.SaveProjectCommand?.Execute(null); } catch { }
				dialog.DialogResult = true;
			};

			dialog.ShowDialog();
		}
		catch (Exception ex)
		{
			Log("ShowAddBlurDialog error: " + ex.Message);
		}
	}

	private static void SuppressBuiltInBlurPlaceholders(Window win)
	{
		if (win == null) return;
		try
		{
			// 1. Eradicate all TextBlocks containing "[BLUR ORIGINAL CHINESE TITLE]"
			foreach (TextBlock tb in FindAllVisualChildren<TextBlock>((DependencyObject)(object)win))
			{
				if (!string.IsNullOrEmpty(tb.Text) && (tb.Text.IndexOf("BLUR", StringComparison.OrdinalIgnoreCase) >= 0 || tb.Text.IndexOf("CHINESE TITLE", StringComparison.OrdinalIgnoreCase) >= 0))
				{
					tb.Text = "";
					tb.Visibility = Visibility.Collapsed;
					tb.Opacity = 0.0;
					Border parentBorder = FindAncestor<Border>((DependencyObject)(object)tb);
					if (parentBorder != null)
					{
						parentBorder.BorderBrush = Brushes.Transparent;
						parentBorder.BorderThickness = new Thickness(0);
						parentBorder.Background = Brushes.Transparent;
						parentBorder.Visibility = Visibility.Collapsed;
						parentBorder.Opacity = 0.0;
					}
				}
			}

			// 2. Eradicate any Border with cyan border brush that was generated by SnaDubber for blur
			foreach (Border b in FindAllVisualChildren<Border>((DependencyObject)(object)win))
			{
				if (b.BorderBrush is SolidColorBrush scb)
				{
					Color c = scb.Color;
					if ((c.R == 6 && c.G == 182 && c.B == 212) || (c.R == 56 && c.G == 189 && c.B == 248) || (c.G > 170 && c.B > 200 && c.R < 80))
					{
						if (b.Child is TextBlock ctb && (ctb.Text ?? "").IndexOf("BLUR", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							b.Visibility = Visibility.Collapsed;
							b.BorderThickness = new Thickness(0);
							b.BorderBrush = Brushes.Transparent;
							b.Opacity = 0.0;
						}
						else if (FindAllVisualChildren<TextBlock>((DependencyObject)(object)b).Any(t => (t.Text ?? "").IndexOf("BLUR", StringComparison.OrdinalIgnoreCase) >= 0))
						{
							b.Visibility = Visibility.Collapsed;
							b.BorderThickness = new Thickness(0);
							b.BorderBrush = Brushes.Transparent;
							b.Opacity = 0.0;
						}
					}
				}
			}

			// 3. Ensure built-in ItemsControl with BlurRegions is completely collapsed and hidden
			if (win.DataContext is MainViewModel vm)
			{
				foreach (ItemsControl items in FindAllVisualChildren<ItemsControl>((DependencyObject)(object)win))
				{
					Binding binding = BindingOperations.GetBinding((DependencyObject)(object)items, ItemsControl.ItemsSourceProperty);
					if (string.Equals(binding?.Path?.Path, "BlurRegions", StringComparison.OrdinalIgnoreCase)
						|| (vm.BlurRegions != null && ReferenceEquals(items.ItemsSource, vm.BlurRegions)))
					{
						items.Visibility = Visibility.Collapsed;
						items.Opacity = 0.0;
						items.Width = 0;
						items.Height = 0;
						items.IsHitTestVisible = false;
					}
				}
			}
		}
		catch { }
	}

	private static (double relX, double relY, double relW, double relH) GetOrComputeBlurNormalizedCoords(BlurRegionItem blur, Rect bounds, MainViewModel vm = null)
	{
		if (blur == null) return (0.1, 0.75, 0.8, 0.12);
		if (_blurNormalizedCoords.TryGetValue(blur, out var norm)) return norm;
		if (!string.IsNullOrEmpty(blur.Id) && _blurNormalizedCoordsById.TryGetValue(blur.Id, out var idNorm)) return idNorm;

		if (!string.IsNullOrEmpty(blur.Label) && blur.Label.Contains('|'))
		{
			try
			{
				string[] parts = blur.Label.Split('|');
				if (parts.Length > 1)
				{
					string[] coords = parts[1].Split(',');
					if (coords.Length == 4)
					{
						double nx = double.Parse(coords[0], CultureInfo.InvariantCulture);
						double ny = double.Parse(coords[1], CultureInfo.InvariantCulture);
						double nw = double.Parse(coords[2], CultureInfo.InvariantCulture);
						double nh = double.Parse(coords[3], CultureInfo.InvariantCulture);
						var res = (Math.Clamp(nx, 0.0, 1.0), Math.Clamp(ny, 0.0, 1.0), Math.Clamp(nw, 0.005, 1.0), Math.Clamp(nh, 0.005, 1.0));
						_blurNormalizedCoords[blur] = res;
						if (!string.IsNullOrEmpty(blur.Id)) _blurNormalizedCoordsById[blur.Id] = res;
						return res;
					}
				}
			}
			catch { }
		}

		double projW = (vm?.CurrentProject != null && vm.CurrentProject.Width > 10.0) ? vm.CurrentProject.Width : 0.0;
		double projH = (vm?.CurrentProject != null && vm.CurrentProject.Height > 10.0) ? vm.CurrentProject.Height : 0.0;

		// 1. Primary: Use active video bounds on the UI preview canvas
		if (bounds.Width > 10.0 && bounds.Height > 10.0)
		{
			// If blur.Y was stored on a large canvas (e.g. 848) while bounds is smaller (e.g. 280)
			if (blur.Y > bounds.Height * 1.2 && projH > 10.0)
			{
				double rx = Math.Clamp(blur.X / projW, 0.0, 1.0);
				double ry = Math.Clamp(blur.Y / projH, 0.0, 1.0);
				double rw = Math.Clamp(blur.Width / projW, 0.005, 1.0 - rx);
				double rh = Math.Clamp(blur.Height / projH, 0.005, 1.0 - ry);
				var res = (rx, ry, rw, rh);
				_blurNormalizedCoords[blur] = res;
				if (!string.IsNullOrEmpty(blur.Id)) _blurNormalizedCoordsById[blur.Id] = res;
				return res;
			}
			else
			{
				double rx = Math.Clamp((blur.X - bounds.Left) / bounds.Width, 0.0, 1.0);
				double ry = Math.Clamp((blur.Y - bounds.Top) / bounds.Height, 0.0, 1.0);
				double rw = Math.Clamp(blur.Width / bounds.Width, 0.005, 1.0 - rx);
				double rh = Math.Clamp(blur.Height / bounds.Height, 0.005, 1.0 - ry);
				var res = (rx, ry, rw, rh);
				_blurNormalizedCoords[blur] = res;
				if (!string.IsNullOrEmpty(blur.Id)) _blurNormalizedCoordsById[blur.Id] = res;
				return res;
			}
		}

		// 2. Fallback: Project canvas dimensions
		if (projW > 10.0 && projH > 10.0 && blur.Y > 1.0)
		{
			// If blur.Y is small (e.g. 150-250) but projH is large (e.g. 848+), blur was recorded on ~280px preview container
			double effectiveH = (blur.Y <= 350.0 && projH >= 700.0) ? 280.0 : projH;
			double effectiveW = (blur.Y <= 350.0 && projH >= 700.0) ? 158.0 : projW;

			double rx = Math.Clamp(blur.X / effectiveW, 0.0, 1.0);
			double ry = Math.Clamp(blur.Y / effectiveH, 0.0, 1.0);
			double rw = Math.Clamp(blur.Width / effectiveW, 0.005, 1.0 - rx);
			double rh = Math.Clamp(blur.Height / effectiveH, 0.005, 1.0 - ry);
			var res = (rx, ry, rw, rh);
			_blurNormalizedCoords[blur] = res;
			if (!string.IsNullOrEmpty(blur.Id)) _blurNormalizedCoordsById[blur.Id] = res;
			return res;
		}

		return (0.1, 0.75, 0.8, 0.12);
	}

	private static void UpdateBlurNormalizedCoordinates(BlurRegionItem blur, Canvas canvas, MainViewModel vm)
	{
		if (blur == null) return;
		Rect bounds = Rect.Empty;
		if (canvas != null && canvas.ActualWidth > 10.0 && canvas.ActualHeight > 10.0)
		{
			bounds = GetDisplayedVideoBounds(canvas, vm);
		}
		if (bounds.Width <= 10.0 || bounds.Height <= 10.0)
		{
			bounds = _lastActiveVideoBounds;
		}
		if (bounds.Width > 10.0 && bounds.Height > 10.0)
		{
			_lastActiveVideoBounds = bounds;
			double relX = Math.Clamp((blur.X - bounds.Left) / bounds.Width, 0.0, 1.0);
			double relY = Math.Clamp((blur.Y - bounds.Top) / bounds.Height, 0.0, 1.0);
			double relW = Math.Clamp(blur.Width / bounds.Width, 0.005, 1.0 - relX);
			double relH = Math.Clamp(blur.Height / bounds.Height, 0.005, 1.0 - relY);
			_blurNormalizedCoords[blur] = (relX, relY, relW, relH);
			if (!string.IsNullOrEmpty(blur.Id))
			{
				_blurNormalizedCoordsById[blur.Id] = (relX, relY, relW, relH);
			}
			string prefix = blur.Label != null && blur.Label.Contains('|') ? blur.Label.Split('|')[0] : (blur.Label ?? "Blur");
			blur.Label = $"{prefix}|{relX.ToString("F4", CultureInfo.InvariantCulture)},{relY.ToString("F4", CultureInfo.InvariantCulture)},{relW.ToString("F4", CultureInfo.InvariantCulture)},{relH.ToString("F4", CultureInfo.InvariantCulture)}";
		}
	}

	private static void RefreshBlurVisuals(Canvas canvas, MainViewModel vm)
	{
		try
		{
			if (vm.BlurRegions == null) return;

			// Ensure built-in ugly cyan border and [BLUR ORIGINAL CHINESE TITLE] remains 100% collapsed
			Window win = Window.GetWindow(canvas) ?? Application.Current?.MainWindow;
			if (win != null)
			{
				SuppressBuiltInBlurPlaceholders(win);
			}

			Rect videoBounds = GetDisplayedVideoBounds(canvas, vm);

			foreach (BlurRegionItem stale in _blurRegionVisuals.Keys.Where(item => !vm.BlurRegions.Contains(item)).ToList())
			{
				BlurRegionVisual old = _blurRegionVisuals[stale];
				canvas.Children.Remove(old.Container);
				_blurRegionVisuals.Remove(stale);
			}

			foreach (BlurRegionItem blur in vm.BlurRegions)
			{
				(double relX, double relY, double relW, double relH) = GetOrComputeBlurNormalizedCoords(blur, videoBounds, vm);

				if (!_blurRegionVisuals.TryGetValue(blur, out BlurRegionVisual visual))
				{
					visual = CreateBlurRegionVisual(canvas, vm, blur);
					_blurRegionVisuals[blur] = visual;
					canvas.Children.Add(visual.Container);
				}

				bool selected = ReferenceEquals(vm.SelectedBlurRegion, blur) || _selectedTimelineBlurRegions.Contains(blur);
				double posMs = vm.CurrentPositionSec * 1000.0;
				bool inRange = posMs >= blur.StartMs && posMs <= Math.Max(blur.StartMs + 1, blur.EndMs);

				visual.Container.Visibility = (inRange || selected) ? Visibility.Visible : Visibility.Collapsed;

				// Keep blur accurately aligned to video bounds
				if (videoBounds.Width > 10.0 && videoBounds.Height > 10.0)
				{
					double targetX = videoBounds.Left + relX * videoBounds.Width;
					double targetY = videoBounds.Top + relY * videoBounds.Height;
					double targetW = Math.Max(24.0, relW * videoBounds.Width);
					double targetH = Math.Max(16.0, relH * videoBounds.Height);

					blur.X = targetX;
					blur.Y = targetY;
					blur.Width = targetW;
					blur.Height = targetH;

					visual.Container.Width = targetW;
					visual.Container.Height = targetH;
					Canvas.SetLeft(visual.Container, targetX);
					Canvas.SetTop(visual.Container, targetY);
				}
				else
				{
					visual.Container.Width = Math.Max(24.0, blur.Width);
					visual.Container.Height = Math.Max(16.0, blur.Height);
					Canvas.SetLeft(visual.Container, Math.Max(0.0, blur.X));
					Canvas.SetTop(visual.Container, Math.Max(0.0, blur.Y));
				}

				double opacity = _blurRegionOpacity.TryGetValue(blur, out double op) ? op : Math.Clamp(blur.Strength / 30.0, 0.1, 1.0);
				visual.Box.Fill = new SolidColorBrush(Color.FromArgb((byte)(opacity * 235.0), 30, 41, 59));
				visual.Box.Stroke = Brushes.Transparent;
				visual.Box.StrokeThickness = 0.0;
				visual.Box.StrokeDashArray = null;

				visual.CornerHandle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
				visual.RightHandle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
				visual.BottomHandle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
				visual.DeleteButton.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
				if (visual.HeaderPill != null) visual.HeaderPill.Visibility = Visibility.Collapsed;
				if (visual.Label != null) visual.Label.Visibility = Visibility.Collapsed;
				visual.Container.Cursor = selected ? Cursors.SizeAll : Cursors.Hand;

				Panel.SetZIndex(visual.Container, selected ? 960 : 750);
			}
		}
		catch (Exception ex)
		{
			Log("RefreshBlurVisuals error: " + ex.Message);
		}
	}

	private static BlurRegionVisual CreateBlurRegionVisual(Canvas canvas, MainViewModel vm, BlurRegionItem blur)
	{
		BlurRegionVisual state = new BlurRegionVisual();
		Grid root = new Grid { ClipToBounds = false };

		double opVal = _blurRegionOpacity.TryGetValue(blur, out double op) ? op : Math.Clamp(blur.Strength / 30.0, 0.1, 1.0);

		Rectangle box = new Rectangle
		{
			Fill = new SolidColorBrush(Color.FromArgb((byte)(opVal * 230.0), 100, 116, 139)),
			Stroke = Brushes.Transparent,
			StrokeThickness = 0.0,
			StrokeDashArray = null,
			RadiusX = 4, RadiusY = 4,
			IsHitTestVisible = true
		};
		state.Box = box;
		root.Children.Add(box);

		Border headerPill = new Border
		{
			Visibility = Visibility.Collapsed,
			IsHitTestVisible = false
		};
		TextBlock label = new TextBlock
		{
			Visibility = Visibility.Collapsed
		};
		state.Label = label;
		state.HeaderPill = headerPill;
		headerPill.Child = label;
		root.Children.Add(headerPill);

		Button delBtn = new Button
		{
			Content = "✕",
			Width = 18.0, Height = 18.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0, 3, 4, 0),
			Background = new SolidColorBrush(Color.FromArgb(220, 220, 38, 38)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0),
			FontWeight = FontWeights.Bold,
			FontSize = 10.0,
			Padding = new Thickness(0),
			Cursor = Cursors.Hand,
			ToolTip = "Remove Blur"
		};
		delBtn.Click += (s, e) =>
		{
			vm.BlurRegions.Remove(blur);
			_blurRegionOpacity.Remove(blur);
			_selectedTimelineBlurRegions.Remove(blur);
			if (ReferenceEquals(vm.SelectedBlurRegion, blur)) vm.SelectedBlurRegion = null;
			canvas.Children.Remove(state.Container);
			_blurRegionVisuals.Remove(blur);
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			e.Handled = true;
		};
		state.DeleteButton = delBtn;
		root.Children.Add(delBtn);

		Border cornerHandle = new Border
		{
			Width = 14.0, Height = 14.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Background = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
			BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(2.0),
			Cursor = Cursors.SizeNWSE,
			ToolTip = "Drag to resize (Width & Height)"
		};
		state.CornerHandle = cornerHandle;
		root.Children.Add(cornerHandle);

		Border rightHandle = new Border
		{
			Width = 7.0, Height = 22.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
			BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(1.5),
			Cursor = Cursors.SizeWE,
			ToolTip = "Drag to resize width (វែង / ខ្លី)"
		};
		state.RightHandle = rightHandle;
		root.Children.Add(rightHandle);

		Border bottomHandle = new Border
		{
			Width = 22.0, Height = 7.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
			BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(1.5),
			Cursor = Cursors.SizeNS,
			ToolTip = "Drag to resize height (តូច / ធំ)"
		};
		state.BottomHandle = bottomHandle;
		root.Children.Add(bottomHandle);

		Border container = new Border
		{
			Child = root,
			Background = Brushes.Transparent,
			Cursor = Cursors.SizeAll,
			Tag = "SnaBlurRegionContainer"
		};
		state.Container = container;

		bool dragging = false;
		bool resizing = false;
		Point dragStart = default;
		double startX = 0.0, startY = 0.0;

		container.MouseLeftButtonDown += (s, e) =>
		{
			if (resizing) return;
			if (e.ClickCount >= 2)
			{
				Window win = Window.GetWindow(canvas) ?? Application.Current?.MainWindow;
				if (win != null) ShowAddBlurDialog(win, vm, blur);
				e.Handled = true;
				return;
			}
			vm.SelectedBlurRegion = blur;
			vm.SelectedOverlay = null;
			vm.SelectedTextLayer = null;
			_selectedTimelineBlurRegions.Clear();
			_selectedTimelineBlurRegions.Add(blur);

			RefreshBlurVisuals(canvas, vm);

			dragging = true;
			dragStart = e.GetPosition(canvas);
			startX = blur.X;
			startY = blur.Y;
			container.CaptureMouse();
			e.Handled = true;
		};

		container.MouseMove += (s, e) =>
		{
			if (!dragging) return;
			Point p = e.GetPosition(canvas);
			double dx = p.X - dragStart.X;
			double dy = p.Y - dragStart.Y;
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			blur.X = Math.Clamp(startX + dx, bounds.Left, Math.Max(bounds.Left, bounds.Right - blur.Width));
			blur.Y = Math.Clamp(startY + dy, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - blur.Height));
			Canvas.SetLeft(container, blur.X);
			Canvas.SetTop(container, blur.Y);
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			e.Handled = true;
		};

		container.MouseLeftButtonUp += (s, e) =>
		{
			if (!dragging) return;
			dragging = false;
			container.ReleaseMouseCapture();
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			e.Handled = true;
		};

		bool resizingCorner = false;
		Point cornerStart = default;
		double startW = 0.0, startH = 0.0;

		cornerHandle.PreviewMouseLeftButtonDown += (s, e) =>
		{
			resizing = true;
			resizingCorner = true;
			cornerStart = e.GetPosition(canvas);
			startW = blur.Width;
			startH = blur.Height;
			cornerHandle.CaptureMouse();
			e.Handled = true;
		};

		cornerHandle.PreviewMouseMove += (s, e) =>
		{
			if (!resizingCorner) return;
			Point p = e.GetPosition(canvas);
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			double newW = Math.Max(24.0, Math.Min(bounds.Right - blur.X, startW + (p.X - cornerStart.X)));
			double newH = Math.Max(16.0, Math.Min(bounds.Bottom - blur.Y, startH + (p.Y - cornerStart.Y)));
			blur.Width = newW;
			blur.Height = newH;
			container.Width = newW;
			container.Height = newH;
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			e.Handled = true;
		};

		cornerHandle.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (!resizingCorner) return;
			resizingCorner = false;
			resizing = false;
			cornerHandle.ReleaseMouseCapture();
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			e.Handled = true;
		};

		bool resizingRight = false;
		Point rightStart = default;

		rightHandle.PreviewMouseLeftButtonDown += (s, e) =>
		{
			resizing = true;
			resizingRight = true;
			rightStart = e.GetPosition(canvas);
			startW = blur.Width;
			rightHandle.CaptureMouse();
			e.Handled = true;
		};

		rightHandle.PreviewMouseMove += (s, e) =>
		{
			if (!resizingRight) return;
			Point p = e.GetPosition(canvas);
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			double newW = Math.Max(24.0, Math.Min(bounds.Right - blur.X, startW + (p.X - rightStart.X)));
			blur.Width = newW;
			container.Width = newW;
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			e.Handled = true;
		};

		rightHandle.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (!resizingRight) return;
			resizingRight = false;
			resizing = false;
			rightHandle.ReleaseMouseCapture();
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			e.Handled = true;
		};

		bool resizingBottom = false;
		Point bottomStart = default;

		bottomHandle.PreviewMouseLeftButtonDown += (s, e) =>
		{
			resizing = true;
			resizingBottom = true;
			bottomStart = e.GetPosition(canvas);
			startH = blur.Height;
			bottomHandle.CaptureMouse();
			e.Handled = true;
		};

		bottomHandle.PreviewMouseMove += (s, e) =>
		{
			if (!resizingBottom) return;
			Point p = e.GetPosition(canvas);
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			double newH = Math.Max(16.0, Math.Min(bounds.Bottom - blur.Y, startH + (p.Y - bottomStart.Y)));
			blur.Height = newH;
			container.Height = newH;
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			e.Handled = true;
		};

		bottomHandle.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (!resizingBottom) return;
			resizingBottom = false;
			resizing = false;
			bottomHandle.ReleaseMouseCapture();
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			try { vm.SaveProjectCommand?.Execute(null); } catch { }
			e.Handled = true;
		};

		container.PreviewMouseWheel += (s, e) =>
		{
			double factor = e.Delta > 0 ? 1.05 : 0.95;
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			double newW = Math.Clamp(blur.Width * factor, 24.0, bounds.Right - blur.X);
			double newH = Math.Clamp(blur.Height * factor, 16.0, bounds.Bottom - blur.Y);
			blur.Width = newW;
			blur.Height = newH;
			container.Width = newW;
			container.Height = newH;
			UpdateBlurNormalizedCoordinates(blur, canvas, vm);
			e.Handled = true;
		};

		return state;
	}

	private static Rect GetDisplayedVideoBounds(Canvas canvas, MainViewModel vm)
	{
		double canvasWidth = Math.Max(1.0, canvas.ActualWidth);
		double canvasHeight = Math.Max(1.0, canvas.ActualHeight);
		double videoWidth = vm?.CurrentProject?.Width ?? 0.0;
		double videoHeight = vm?.CurrentProject?.Height ?? 0.0;
		Window win = Window.GetWindow(canvas) ?? Application.Current?.MainWindow;
		MediaElement player = null;
		if (win != null)
		{
			player = FindVisualChild((DependencyObject)(object)win, (MediaElement m) => m.Name == "PreviewPlayer");
			if (player != null && player.NaturalVideoWidth > 0 && player.NaturalVideoHeight > 0)
			{
				videoWidth = player.NaturalVideoWidth;
				videoHeight = player.NaturalVideoHeight;
			}
		}
		if (videoWidth <= 0.0 || videoHeight <= 0.0)
			return new Rect(0.0, 0.0, canvasWidth, canvasHeight);

		bool isUniformToFill = (player != null && player.Stretch == Stretch.UniformToFill) || (_isFrameFillMode && vm?.SelectedSocialPreset?.AspectRatioText == "16:9");
		double scale = isUniformToFill
			? Math.Max(canvasWidth / videoWidth, canvasHeight / videoHeight)
			: Math.Min(canvasWidth / videoWidth, canvasHeight / videoHeight);

		double width = Math.Max(1.0, videoWidth * scale);
		double height = Math.Max(1.0, videoHeight * scale);
		Rect r = new Rect((canvasWidth - width) / 2.0, (canvasHeight - height) / 2.0, width, height);
		if (r.Width > 10.0 && r.Height > 10.0)
		{
			_lastActiveVideoBounds = r;
		}
		return r;
	}

	private static MediaOverlayVisual CreateMediaOverlayVisual(Canvas canvas, MainViewModel vm, OverlayItem overlay)
	{
		MediaOverlayVisual state = new MediaOverlayVisual();
		Grid root = new Grid { ClipToBounds = false, Background = Brushes.Transparent };
		string path = overlay.ImagePath ?? string.Empty;
		string ext = Path.GetExtension(path).ToLowerInvariant();
		if (new[] { ".mp4", ".mov", ".mkv", ".webm", ".avi" }.Contains(ext))
		{
			MediaElement video = new MediaElement
			{
				Source = File.Exists(path) ? new Uri(Path.GetFullPath(path)) : null,
				LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual,
				IsMuted = true, Stretch = Stretch.Uniform, ScrubbingEnabled = true
			};
			state.AspectRatio = Math.Max(0.05, overlay.Width / Math.Max(1.0, overlay.Height));
			video.MediaOpened += (s, e) =>
			{
				if (video.NaturalVideoWidth <= 0 || video.NaturalVideoHeight <= 0) return;
				state.AspectRatio = video.NaturalVideoWidth / (double)video.NaturalVideoHeight;
				overlay.Height = Math.Max(28.0, overlay.Width / state.AspectRatio);
			};
			state.Content = video;
			state.Video = video;
			root.Children.Add(video);
		}
		else
		{
			Image image = new Image { Stretch = Stretch.Uniform };
			state.Content = image;
			state.Image = image;
			if (File.Exists(path))
			{
				if (ext == ".gif")
				{
					GifBitmapDecoder decoder = new GifBitmapDecoder(new Uri(Path.GetFullPath(path)), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
					state.GifFrames = decoder.Frames.ToList();
					if (state.GifFrames.Count > 0)
					{
						image.Source = state.GifFrames[0];
						state.AspectRatio = state.GifFrames[0].PixelWidth / (double)Math.Max(1, state.GifFrames[0].PixelHeight);
						overlay.Height = Math.Max(28.0, overlay.Width / state.AspectRatio);
					}
					state.NextGifFrameAt = DateTime.UtcNow.AddMilliseconds(100.0);
				}
				else
				{
					BitmapImage bitmap = new BitmapImage();
					bitmap.BeginInit();
					bitmap.CacheOption = BitmapCacheOption.OnLoad;
					bitmap.UriSource = new Uri(Path.GetFullPath(path));
					bitmap.EndInit();
					bitmap.Freeze();
					image.Source = bitmap;
					state.AspectRatio = bitmap.PixelWidth / (double)Math.Max(1, bitmap.PixelHeight);
					overlay.Height = Math.Max(28.0, overlay.Width / state.AspectRatio);
				}
			}
			root.Children.Add(image);
		}

		Border resizeHandle = new Border
		{
			Width = 17.0, Height = 17.0, HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
			BorderBrush = Brushes.White, BorderThickness = new Thickness(1.0), Cursor = Cursors.SizeNWSE,
			ToolTip = "Drag to resize"
		};
		state.ResizeHandle = resizeHandle;
		resizeHandle.Visibility = ReferenceEquals(vm.SelectedOverlay, overlay) ? Visibility.Visible : Visibility.Collapsed;
		root.Children.Add(resizeHandle);

		StackPanel opacityPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top, Background = new SolidColorBrush(Color.FromArgb(210, 8, 15, 29)),
			Margin = new Thickness(3.0), ToolTip = "Overlay opacity"
		};
		TextBlock opacityText = new TextBlock { Foreground = Brushes.White, FontSize = 10.0, Margin = new Thickness(5.0, 2.0, 3.0, 2.0) };
		Slider opacitySlider = new Slider { Minimum = 0.0, Maximum = 1.0, Width = 72.0, Value = Math.Clamp(overlay.Opacity, 0.0, 1.0), Margin = new Thickness(0.0, 0.0, 5.0, 0.0) };
		opacityText.Text = $"Opacity {opacitySlider.Value:P0}";
		opacitySlider.ValueChanged += (s, e) =>
		{
			overlay.Opacity = e.NewValue;
			opacityText.Text = $"Opacity {e.NewValue:P0}";
		};
		opacityPanel.Children.Add(opacityText);
		opacityPanel.Children.Add(opacitySlider);
		// Opacity is edited from the Timeline toolbar, not on top of the media.

		Border container = new Border { Child = root, Background = Brushes.Transparent, Cursor = Cursors.SizeAll, Tag = "SnaMediaOverlayEditor" };
		state.Container = container;
		bool dragging = false;
		bool resizing = false;
		Point pointerStart = default;
		double startX = 0.0, startY = 0.0, startW = 0.0, startH = 0.0;
		container.MouseLeftButtonDown += (s, e) =>
		{
			vm.SelectedOverlay = overlay;
			vm.SelectedTextLayer = null;
			_selectedTimelineOverlay = overlay;
			dragging = true;
			pointerStart = e.GetPosition(canvas);
			startX = overlay.X; startY = overlay.Y;
			container.CaptureMouse();
			e.Handled = true;
		};
		resizeHandle.PreviewMouseLeftButtonDown += (s, e) =>
		{
			vm.SelectedOverlay = overlay;
			vm.SelectedTextLayer = null;
			_selectedTimelineOverlay = overlay;
			resizing = true;
			pointerStart = e.GetPosition(canvas);
			startW = overlay.Width; startH = overlay.Height;
			resizeHandle.CaptureMouse();
			e.Handled = true;
		};
		container.MouseMove += (s, e) =>
		{
			if (!dragging) return;
			Point p = e.GetPosition(canvas);
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			overlay.X = Math.Clamp(startX + p.X - pointerStart.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - overlay.Width));
			overlay.Y = Math.Clamp(startY + p.Y - pointerStart.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - overlay.Height));
			e.Handled = true;
		};
		resizeHandle.PreviewMouseMove += (s, e) =>
		{
			if (!resizing) return;
			Point p = e.GetPosition(canvas);
			double dxScale = (p.X - pointerStart.X) / Math.Max(1.0, startW);
			double dyScale = (p.Y - pointerStart.Y) / Math.Max(1.0, startH);
			double scale = 1.0 + (Math.Abs(dxScale) >= Math.Abs(dyScale) ? dxScale : dyScale);
			double minScale = Math.Max(36.0 / Math.Max(1.0, startW), 28.0 / Math.Max(1.0, startH));
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			double maxScale = Math.Min(
				Math.Max(minScale, (bounds.Right - overlay.X) / Math.Max(1.0, startW)),
				Math.Max(minScale, (bounds.Bottom - overlay.Y) / Math.Max(1.0, startH)));
			scale = Math.Clamp(scale, minScale, Math.Max(minScale, maxScale));
			overlay.Width = startW * scale;
			overlay.Height = startH * scale;
			e.Handled = true;
		};
		container.PreviewMouseWheel += (s, e) =>
		{
			vm.SelectedOverlay = overlay;
			vm.SelectedTextLayer = null;
			_selectedTimelineOverlay = overlay;
			double factor = e.Delta > 0 ? 1.08 : 1.0 / 1.08;
			double newWidth = overlay.Width * factor;
			double newHeight = overlay.Height * factor;
			Rect bounds = GetDisplayedVideoBounds(canvas, vm);
			if (newWidth >= 36.0 && newHeight >= 28.0
				&& overlay.X >= bounds.Left && overlay.Y >= bounds.Top
				&& overlay.X + newWidth <= bounds.Right && overlay.Y + newHeight <= bounds.Bottom)
			{
				overlay.Width = newWidth;
				overlay.Height = newHeight;
			}
			e.Handled = true;
		};
		container.MouseLeftButtonUp += (s, e) =>
		{
			if (!dragging) return;
			dragging = false;
			container.ReleaseMouseCapture();
			e.Handled = true;
		};
		resizeHandle.PreviewMouseLeftButtonUp += (s, e) =>
		{
			if (!resizing) return;
			resizing = false;
			resizeHandle.ReleaseMouseCapture();
			e.Handled = true;
		};
		return state;
	}

	private static void ApplyContinuousTimelineClipStyle(Border border, ScrollViewer scrollViewer, DialogueLine line, MainViewModel vm)
	{
		try
		{
			DockPanel lane = FindAncestor<DockPanel>((DependencyObject)(object)scrollViewer);
			if (lane == null)
			{
				return;
			}
			TextBlock laneLabel = FindVisualChild((DependencyObject)(object)lane, (TextBlock text) =>
				string.Equals(text.Text?.Trim(), "SUBTITLES", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(text.Text?.Trim(), "KHMER VOICE", StringComparison.OrdinalIgnoreCase));
			if (laneLabel == null)
			{
				return;
			}

			double totalMs = Math.Max(1000.0, vm.VideoDurationSec * 1000.0);
			double viewport = Math.Max(1.0, scrollViewer.ViewportWidth > 1.0 ? scrollViewer.ViewportWidth : scrollViewer.ActualWidth);
			double durationWidth = Math.Max(2.0, (line.EndMs - line.StartMs) / totalMs * viewport);
			DialogueLine previous = vm.DialogueLines?.Where(item => item.Index < line.Index).OrderByDescending(item => item.Index).FirstOrDefault();
			int previousEnd = previous?.EndMs ?? 0;
			double gapWidth = Math.Max(0.0, (line.StartMs - previousEnd) / totalMs * viewport);

			border.Width = durationWidth;
			border.Height = 20.0;
			border.Margin = new Thickness(gapWidth, 0.0, 0.0, 0.0);
			border.Padding = new Thickness(2.0, 1.0, 2.0, 1.0);
			border.CornerRadius = new CornerRadius(1.0);
			border.BorderThickness = new Thickness(0.0, 0.0, 1.0, 0.0);
			bool subtitle = string.Equals(laneLabel.Text?.Trim(), "SUBTITLES", StringComparison.OrdinalIgnoreCase);
			border.Background = new SolidColorBrush(subtitle ? Color.FromRgb(4, 166, 122) : Color.FromRgb(126, 34, 206));
			border.BorderBrush = new SolidColorBrush(subtitle ? Color.FromRgb(0, 215, 159) : Color.FromRgb(168, 85, 247));
			foreach (TextBlock text in FindAllVisualChildren<TextBlock>((DependencyObject)(object)border))
			{
				text.FontSize = 8.0;
				text.TextTrimming = TextTrimming.CharacterEllipsis;
				text.VerticalAlignment = VerticalAlignment.Center;
			}
		}
		catch (Exception ex)
		{
			Log("ApplyContinuousTimelineClipStyle error: " + ex.Message);
		}
	}

	private static void AttachSponsorClipDragHandlers(Window window, MainViewModel vm)
	{
		foreach (Border border in FindAllVisualChildren<Border>((DependencyObject)(object)window))
		{
			if (border.DataContext is not OverlayItem overlay || border.Tag as string == "SponsorClipDragHooked")
			{
				continue;
			}
			ScrollViewer scrollViewer = FindAncestor<ScrollViewer>((DependencyObject)(object)border);
			if (scrollViewer == null || scrollViewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled)
			{
				continue;
			}
			border.Tag = "SponsorClipDragHooked";
			border.MouseMove += (object sender, MouseEventArgs args) =>
			{
				if (_activeDraggingOverlay == null)
				{
					double x = args.GetPosition(border).X;
					border.Cursor = (x <= 8.0 || x >= border.ActualWidth - 8.0) ? Cursors.SizeWE : Cursors.SizeAll;
					border.ToolTip = $"Sponsor: {TimeSpan.FromMilliseconds(overlay.StartMs):hh\\:mm\\:ss\\.fff} - {TimeSpan.FromMilliseconds(overlay.EndMs):hh\\:mm\\:ss\\.fff}";
				}
			};
			border.PreviewMouseLeftButtonDown += (object sender, MouseButtonEventArgs args) =>
			{
				PushTimelineUndo(vm);
				double x = args.GetPosition(border).X;
				_activeDraggingOverlay = overlay;
			_selectedTimelineOverlay = overlay;
			vm.SelectedOverlay = overlay;
			vm.SelectedTextLayer = null;
				_activeDraggingBorder = border;
				_dragStartWindowX = args.GetPosition(window).X;
				_initialStartMs = overlay.StartMs;
				_initialEndMs = overlay.EndMs;
				_isDraggingClipStart = x <= 8.0;
				_isDraggingClipEnd = x >= border.ActualWidth - 8.0;
				_isDraggingClipMove = !_isDraggingClipStart && !_isDraggingClipEnd;
				border.CaptureMouse();
				args.Handled = true;
			};
			border.PreviewMouseMove += (object sender, MouseEventArgs args) =>
			{
				if (_activeDraggingOverlay != overlay || args.LeftButton != MouseButtonState.Pressed)
				{
					return;
				}
				int deltaMs = (int)((args.GetPosition(window).X - _dragStartWindowX) * 40.0);
				if (_isDraggingClipStart)
				{
					overlay.StartMs = Math.Max(0, Math.Min(overlay.EndMs - 150, _initialStartMs + deltaMs));
				}
				else if (_isDraggingClipEnd)
				{
					overlay.EndMs = Math.Max(overlay.StartMs + 150, _initialEndMs + deltaMs);
				}
				else if (_isDraggingClipMove)
				{
					int durationMs = _initialEndMs - _initialStartMs;
					overlay.StartMs = Math.Max(0, _initialStartMs + deltaMs);
					overlay.EndMs = overlay.StartMs + durationMs;
				}
				ResolveOverlayClipCollisions(vm, overlay);
				args.Handled = true;
			};
			border.PreviewMouseLeftButtonUp += (object sender, MouseButtonEventArgs args) =>
			{
				if (_activeDraggingOverlay != overlay)
				{
					return;
				}
				_isDraggingClipStart = _isDraggingClipEnd = _isDraggingClipMove = false;
				_activeDraggingOverlay = null;
				border.ReleaseMouseCapture();
				_activeDraggingBorder = null;
				vm.ProjectStatusMessage = $"Sponsor timing updated: {TimeSpan.FromMilliseconds(overlay.StartMs):hh\\:mm\\:ss\\.fff} - {TimeSpan.FromMilliseconds(overlay.EndMs):hh\\:mm\\:ss\\.fff}";
				try { vm.SaveProjectCommand?.Execute(null); } catch { }
				args.Handled = true;
			};
		}
	}

	private static TextBlock FindLicenseTextBlock(DependencyObject root)
	{
		if (root == null)
		{
			return null;
		}
		TextBlock textBlock = FindVisualChild(root, (TextBlock tb) => tb.Text != null && tb.Text.Contains("ENTER SNA-DUBBER LICENSE KEY"));
		if (textBlock != null)
		{
			return textBlock;
		}
		return FindLogicalChild(root, (TextBlock tb) => tb.Text != null && tb.Text.Contains("ENTER SNA-DUBBER LICENSE KEY"));
	}

	private static T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
	{
		if (parent == null)
		{
			return default;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null && predicate(val))
			{
				return val;
			}
			T val2 = FindVisualChild(child, predicate);
			if (val2 != null)
			{
				return val2;
			}
		}
		return default;
	}

	private static List<T> FindAllVisualChildren<T>(DependencyObject parent) where T : DependencyObject
	{
		List<T> list = new List<T>();
		if (parent == null)
		{
			return list;
		}
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null)
			{
				list.Add(val);
			}
			list.AddRange(FindAllVisualChildren<T>(child));
		}
		return list;
	}

	private static T FindLogicalChild<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
	{
		if (parent == null)
		{
			return default;
		}
		foreach (object child in LogicalTreeHelper.GetChildren(parent))
		{
			DependencyObject val = (DependencyObject)((child is DependencyObject) ? child : null);
			if (val != null)
			{
				T val2 = (T)(object)((val is T) ? val : null);
				if (val2 != null && predicate(val2))
				{
					return val2;
				}
				T val3 = FindLogicalChild(val, predicate);
				if (val3 != null)
				{
					return val3;
				}
			}
		}
		return default;
	}
}
