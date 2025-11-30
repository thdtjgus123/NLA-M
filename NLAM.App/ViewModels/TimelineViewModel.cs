using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace NLAM.App.ViewModels;

public partial class TimelineViewModel : ObservableObject
{
    // Constants for timeline length limits
    public const double MinLength = 1.0;           // 1 second
    public const double MaxLength = 604800.0;     // 1 week in seconds (7 * 24 * 60 * 60)

    [ObservableProperty]
    private double _zoomLevel = 0.01; // Start at 1% (fit to view)

    [ObservableProperty]
    private double _scrollOffset = 0.0;

    [ObservableProperty]
    private double _currentTime = 0.0;

    [ObservableProperty]
    private double _viewportWidth = 800.0; // Current visible width in pixels

    private double _totalLength = 60.0; // Default 1 minute
    public double TotalLength
    {
        get => _totalLength;
        set
        {
            var clampedValue = Math.Clamp(value, MinLength, MaxLength);
            if (SetProperty(ref _totalLength, clampedValue))
            {
                OnPropertyChanged(nameof(TotalLengthFormatted));
            }
        }
    }

    public string TotalLengthFormatted => FormatTimeSpan(TotalLength);

    public static string FormatTimeSpan(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        else if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        else
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    /// <summary>
    /// Calculate the optimal zoom level to fit the entire timeline in the viewport
    /// ZoomLevel 0.01 (1%) means the entire timeline fits exactly in the viewport
    /// ZoomLevel 1.0 (100%) means 100x zoom from fitted size
    /// </summary>
    public double CalculateFitZoom()
    {
        // 1% = fit to view
        return 0.01;
    }

    /// <summary>
    /// Get pixels per second based on viewport and timeline length
    /// 1% zoom = timeline fits viewport
    /// 100% zoom = consistent detail level regardless of timeline length
    /// </summary>
    public double GetPixelsPerSecond()
    {
        if (ViewportWidth <= 0 || TotalLength <= 0) return 100.0;
        
        // Base: at 1% zoom (0.01), entire timeline fits in viewport
        double basePixelsPerSecond = ViewportWidth / TotalLength;
        
        // At 100% zoom, we want a consistent detail level (e.g., 100 pixels per second)
        // regardless of timeline length
        // Interpolate between fit-to-view and fixed detail level
        double targetPixelsPerSecond = 100.0; // pixels per second at 100% zoom
        
        // Linear interpolation: at 0.01 use base, at 1.0 use max of base*100 or target
        double zoomFactor = (ZoomLevel - 0.01) / (1.0 - 0.01); // 0 to 1
        double maxZoomPps = Math.Max(basePixelsPerSecond * 100.0, targetPixelsPerSecond);
        
        return basePixelsPerSecond + zoomFactor * (maxZoomPps - basePixelsPerSecond);
    }

    /// <summary>
    /// Adjust zoom to fit the entire timeline in view (1%)
    /// </summary>
    public void FitToView()
    {
        ZoomLevel = CalculateFitZoom();
    }

    public ObservableCollection<TrackViewModel> Tracks { get; } = new();

    private readonly Services.PlaybackService _playbackService;

    public TimelineViewModel(Services.PlaybackService playbackService)
    {
        _playbackService = playbackService;
        _playbackService.TimeUpdated += OnPlaybackTimeUpdated;

        // Start with a single empty track
        Tracks.Add(new TrackViewModel("Track 1"));
    }

    private void OnPlaybackTimeUpdated(double time)
    {
        CurrentTime = time;
    }

    public void Play() => _playbackService.Play();
    public void Pause() => _playbackService.Pause();
    public void Stop() => _playbackService.Stop();
}
