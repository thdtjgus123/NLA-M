using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NLAM.App.Views;

public partial class TimelineView : UserControl
{
    private double _currentScrollOffset = 0;
    private bool _isDuplicating = false;
    private ViewModels.ClipViewModel? _dragClip = null;
    private ViewModels.TrackViewModel? _originalTrack = null;
    private double _dragStartY = 0;
    
    public TimelineView()
    {
        InitializeComponent();
        DataContextChanged += TimelineView_DataContextChanged;
        SizeChanged += TimelineView_SizeChanged;
        Loaded += TimelineView_Loaded;
    }

    private bool _initialFitDone = false;

    private void TimelineView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateViewport();
        // Only auto-fit on initial load
        if (!_initialFitDone && DataContext is ViewModels.TimelineViewModel timeline)
        {
            timeline.FitToView();
            _initialFitDone = true;
        }
    }

    private void TimelineView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewport();
    }

    private void UpdateViewport()
    {
        if (DataContext is ViewModels.TimelineViewModel timeline)
        {
            // Calculate available width (minus track header width of 150)
            double availableWidth = Math.Max(100, ActualWidth - 150);
            timeline.ViewportWidth = availableWidth;
            DrawTimeRuler();
        }
    }

    private void TimelineView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ViewModels.TimelineViewModel oldTimeline)
        {
            oldTimeline.PropertyChanged -= Timeline_PropertyChanged;
        }
        if (e.NewValue is ViewModels.TimelineViewModel newTimeline)
        {
            newTimeline.PropertyChanged += Timeline_PropertyChanged;
            UpdateViewport();
        }
    }

    private void Timeline_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.TimelineViewModel.ZoomLevel) ||
            e.PropertyName == nameof(ViewModels.TimelineViewModel.ViewportWidth))
        {
            DrawTimeRuler();
        }
        else if (e.PropertyName == nameof(ViewModels.TimelineViewModel.TotalLength))
        {
            // Auto-fit when timeline length changes
            if (DataContext is ViewModels.TimelineViewModel timeline)
            {
                timeline.FitToView();
            }
            DrawTimeRuler();
        }
    }

    private void TimeRuler_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        DrawTimeRuler();
    }

    private void TracksScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Sync ruler with horizontal scroll
        _currentScrollOffset = e.HorizontalOffset;
        DrawTimeRuler();
    }

    // Cache brushes to avoid creating new ones every time
    private static readonly SolidColorBrush RulerBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));
    private static readonly SolidColorBrush TickBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
    private static readonly SolidColorBrush EndMarkerBrush = new SolidColorBrush(Colors.Red);

    static TimelineView()
    {
        RulerBackgroundBrush.Freeze();
        TickBrush.Freeze();
        EndMarkerBrush.Freeze();
    }

    private void DrawTimeRuler()
    {
        if (TimeRuler == null) return;
        
        TimeRuler.Children.Clear();

        if (DataContext is not ViewModels.TimelineViewModel timeline) return;

        double pixelsPerSecond = timeline.GetPixelsPerSecond();
        double totalLength = timeline.TotalLength;
        double viewportWidth = timeline.ViewportWidth;
        double scrollOffset = _currentScrollOffset;
        
        // Calculate visible time range based on scroll position
        double visibleStartTime = scrollOffset / pixelsPerSecond;
        double visibleEndTime = (scrollOffset + viewportWidth + 100) / pixelsPerSecond;
        visibleEndTime = Math.Min(visibleEndTime, totalLength);
        
        // Calculate total content width
        double totalContentWidth = totalLength * pixelsPerSecond;

        // Determine tick interval - ensure we don't draw too many ticks
        double majorTickInterval = GetMajorTickInterval(pixelsPerSecond);
        
        // Limit max ticks to prevent lag (max ~50 major ticks visible)
        int estimatedMajorTicks = (int)((visibleEndTime - visibleStartTime) / majorTickInterval);
        if (estimatedMajorTicks > 50)
        {
            majorTickInterval = (visibleEndTime - visibleStartTime) / 50;
            majorTickInterval = RoundToNiceInterval(majorTickInterval);
        }

        // Draw background for the visible area
        var background = new Rectangle
        {
            Width = totalContentWidth,
            Height = 30,
            Fill = RulerBackgroundBrush
        };
        Canvas.SetLeft(background, -scrollOffset);
        TimeRuler.Children.Add(background);

        // Draw only major ticks in visible range (with buffer)
        double startTick = Math.Floor((visibleStartTime - majorTickInterval) / majorTickInterval) * majorTickInterval;
        startTick = Math.Max(0, startTick);
        
        for (double time = startTick; time <= visibleEndTime + majorTickInterval; time += majorTickInterval)
        {
            if (time < 0) continue;
            if (time > totalLength) break;
            
            double x = (time * pixelsPerSecond) - scrollOffset;
            
            // Skip if outside visible area
            if (x < -50 || x > viewportWidth + 50) continue;
            
            var tick = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = 30,
                Stroke = TickBrush,
                StrokeThickness = 1
            };
            TimeRuler.Children.Add(tick);

            var label = new TextBlock
            {
                Text = FormatRulerTime(time),
                Foreground = TickBrush,
                FontSize = 10
            };
            Canvas.SetLeft(label, x + 3);
            Canvas.SetTop(label, 2);
            TimeRuler.Children.Add(label);
        }

        // Draw end marker if visible
        double endX = (totalLength * pixelsPerSecond) - scrollOffset;
        if (endX >= -50 && endX <= viewportWidth + 50)
        {
            var endLine = new Line
            {
                X1 = endX,
                Y1 = 0,
                X2 = endX,
                Y2 = 30,
                Stroke = EndMarkerBrush,
                StrokeThickness = 2
            };
            TimeRuler.Children.Add(endLine);

            var endLabel = new TextBlock
            {
                Text = "END",
                Foreground = EndMarkerBrush,
                FontSize = 9,
                FontWeight = System.Windows.FontWeights.Bold
            };
            Canvas.SetLeft(endLabel, endX - 25);
            Canvas.SetTop(endLabel, 2);
            TimeRuler.Children.Add(endLabel);
        }
    }

    private double GetMajorTickInterval(double pixelsPerSecond)
    {
        // Determine tick interval based on how many pixels per second we have
        // We want roughly 50-150 pixels between major ticks
        double targetPixelsBetweenTicks = 100; // Increased for fewer ticks
        
        // Calculate the ideal interval in seconds
        double idealInterval = targetPixelsBetweenTicks / pixelsPerSecond;
        
        return RoundToNiceInterval(idealInterval);
    }

    private static readonly double[] NiceIntervals = { 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1800, 3600, 7200, 21600, 43200, 86400, 172800, 604800 };

    private static double RoundToNiceInterval(double idealInterval)
    {
        // Find the closest nice interval
        foreach (var interval in NiceIntervals)
        {
            if (interval >= idealInterval)
            {
                return interval;
            }
        }
        return NiceIntervals[^1]; // Return largest if none found
    }

    private static string FormatRulerTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours:D2}h";
        else if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        else if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        else
            return $"{ts.Seconds:F1}s";
    }

    private void Timeline_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        // Ctrl+Wheel for zoom
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            if (DataContext is ViewModels.TimelineViewModel timeline)
            {
                // Use multiplicative zoom for smoother scaling
                double factor = e.Delta > 0 ? 1.15 : 0.87;
                double newZoom = timeline.ZoomLevel * factor;
                
                // Clamp zoom level between 1% and 100%
                newZoom = Math.Clamp(newZoom, 0.01, 1.0);
                timeline.ZoomLevel = newZoom;
            }
            e.Handled = true;
        }
    }


    private void Clip_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Thumb thumb &&
            thumb.Tag is ViewModels.ClipViewModel clip)
        {
            // Find the MainViewModel and set selected clip
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
            {
                mainViewModel.Properties.SetSelectedClip(clip);
            }

            // Store drag start Y position for cross-track dragging
            _dragStartY = e.GetPosition(this).Y;
            
            // Check for Alt key for duplication
            _isDuplicating = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt);
            
            // Don't mark as handled - allow drag to work
        }
    }

    private void Clip_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Thumb thumb &&
            thumb.Tag is ViewModels.ClipViewModel clip &&
            DataContext is ViewModels.TimelineViewModel timeline)
        {
            _dragClip = clip;
            _originalTrack = FindTrackForClip(timeline, clip);

            // If Alt is pressed, duplicate the clip
            if (_isDuplicating && _originalTrack != null)
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                {
                    var newClip = mainViewModel.DuplicateClip(clip, _originalTrack);
                    if (newClip != null)
                    {
                        // The duplicated clip stays, original is being dragged
                        // Actually, we want to drag the NEW clip, so swap
                        _dragClip = newClip;
                        mainViewModel.Properties.SetSelectedClip(newClip);
                    }
                }
                _isDuplicating = false; // Reset flag
            }
        }
    }

    private void Clip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Thumb thumb && 
            thumb.Tag is ViewModels.ClipViewModel clip && 
            DataContext is ViewModels.TimelineViewModel timeline)
        {
            double pixelsPerSecond = timeline.GetPixelsPerSecond();
            double deltaTime = e.HorizontalChange / pixelsPerSecond;
            
            double newTime = clip.StartTime + deltaTime;
            if (newTime < 0) newTime = 0;
            
            // Ensure clip doesn't extend beyond timeline length
            if (newTime + clip.Duration > timeline.TotalLength)
                newTime = timeline.TotalLength - clip.Duration;
            
            // Handle cross-track dragging (vertical movement)
            var currentTrack = FindTrackForClip(timeline, clip);
            if (currentTrack != null && Math.Abs(e.VerticalChange) > 5)
            {
                // Calculate which track we're dragging to
                double trackHeight = currentTrack.Height;
                int trackDelta = (int)(e.VerticalChange / trackHeight);
                
                if (trackDelta != 0)
                {
                    int currentIndex = timeline.Tracks.IndexOf(currentTrack);
                    int newIndex = currentIndex + trackDelta;
                    
                    if (newIndex >= 0 && newIndex < timeline.Tracks.Count)
                    {
                        var targetTrack = timeline.Tracks[newIndex];
                        
                        // Move clip to new track
                        currentTrack.Clips.Remove(clip);
                        targetTrack.Clips.Add(clip);
                        
                        Log($"Moved clip '{clip.Name}' from '{currentTrack.Name}' to '{targetTrack.Name}'");
                        
                        // Update current track reference
                        currentTrack = targetTrack;
                    }
                }
            }
            
            // Find the track containing this clip and check for collisions
            var track = FindTrackForClip(timeline, clip);
            if (track != null && newTime >= 0)
            {
                // Check if new position would cause collision
                if (!track.WouldCollide(clip, newTime))
                {
                    clip.StartTime = newTime;
                }
                else
                {
                    // Find nearest valid position
                    double validPos = track.FindNearestValidPosition(clip, newTime, timeline.TotalLength);
                    if (validPos != clip.StartTime)
                    {
                        clip.StartTime = validPos;
                    }
                }
            }
            else if (newTime >= 0)
            {
                clip.StartTime = newTime;
            }
        }
    }

    private void ClipResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Thumb thumb && 
            thumb.Tag is ViewModels.ClipViewModel clip && 
            DataContext is ViewModels.TimelineViewModel timeline)
        {
            double pixelsPerSecond = timeline.GetPixelsPerSecond();
            double deltaDuration = e.HorizontalChange / pixelsPerSecond;
            
            double newDuration = clip.Duration + deltaDuration;
            if (newDuration < 0.1) newDuration = 0.1; // Minimum duration
            
            // Ensure clip doesn't extend beyond timeline length
            if (clip.StartTime + newDuration > timeline.TotalLength)
                newDuration = timeline.TotalLength - clip.StartTime;
            
            // Find the track and check for collisions
            var track = FindTrackForClip(timeline, clip);
            if (track != null)
            {
                // Limit duration to prevent collision with next clip
                double maxDuration = track.FindMaxDuration(clip, timeline.TotalLength);
                if (newDuration > maxDuration)
                    newDuration = maxDuration;
            }
            
            clip.Duration = newDuration;
        }
    }

    private ViewModels.TrackViewModel? FindTrackForClip(ViewModels.TimelineViewModel timeline, ViewModels.ClipViewModel clip)
    {
        foreach (var track in timeline.Tracks)
        {
            if (track.Clips.Contains(clip))
                return track;
        }
        return null;
    }

    // Static AI service instance to persist model across script editor sessions
    private static Services.AIScriptService? _sharedAIService;

    private void EditScript_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var clip = GetClipFromMenuItem(sender);
        if (clip == null) return;

        ShowScriptEditor(clip, null);
    }

    /// <summary>
    /// Opens the unified script editor window for the given clip.
    /// Can be called from MainWindow or context menu.
    /// </summary>
    public static void ShowScriptEditor(ViewModels.ClipViewModel clip, Action? onSave)
    {

        // Use shared service to keep model loaded
        _sharedAIService ??= new Services.AIScriptService();
        var aiService = _sharedAIService;

        // Create a script editor window
        var scriptWindow = new System.Windows.Window
        {
            Title = $"Edit AHK Script - {clip.Name}",
            Width = 850,
            Height = 650,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526"))
        };

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) }); // AI Panel
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) }); // Status Panel
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = System.Windows.GridLength.Auto });

        // Header with help text
        var headerBorder = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
            Padding = new System.Windows.Thickness(10)
        };
        var headerPanel = new StackPanel();
        var headerText = new TextBlock
        {
            Text = "AutoHotkey Script Editor",
            Foreground = Brushes.White,
            FontWeight = System.Windows.FontWeights.Bold,
            FontSize = 14
        };
        var helpText = new TextBlock
        {
            Text = "Write your custom AHK script or use AI to generate one. Connect to Ollama for AI generation.",
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
            FontSize = 11,
            Margin = new System.Windows.Thickness(0, 5, 0, 0)
        };
        headerPanel.Children.Add(headerText);
        headerPanel.Children.Add(helpText);
        headerBorder.Child = headerPanel;
        Grid.SetRow(headerBorder, 0);
        mainGrid.Children.Add(headerBorder);

        // AI Generation Panel
        var aiBorder = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")),
            Padding = new System.Windows.Thickness(10),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
            BorderThickness = new System.Windows.Thickness(0, 1, 0, 0)
        };
        var aiOuterPanel = new StackPanel();
        
        // First row: Ollama connection and model selection
        var modelPanel = new Grid { Margin = new System.Windows.Thickness(0, 0, 0, 8) };
        modelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
        modelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        modelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });

        var modelLabel = new TextBlock
        {
            Text = "🧠 Ollama:",
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CE93D8")),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new System.Windows.Thickness(0, 0, 10, 0),
            FontWeight = System.Windows.FontWeights.SemiBold
        };
        Grid.SetColumn(modelLabel, 0);
        modelPanel.Children.Add(modelLabel);

        // Model dropdown (ComboBox) with dark theme
        var modelComboBox = new ComboBox
        {
            MinWidth = 200,
            Padding = new System.Windows.Thickness(8, 5, 8, 5),
            IsEnabled = false
        };
        // Apply dark theme style
        modelComboBox.Resources.Add(SystemColors.WindowBrushKey, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")));
        modelComboBox.Resources.Add(SystemColors.HighlightBrushKey, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC")));
        modelComboBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30"));
        modelComboBox.Foreground = Brushes.White;
        modelComboBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));
        modelComboBox.Items.Add("Click 'Connect' to load models...");
        modelComboBox.SelectedIndex = 0;
        Grid.SetColumn(modelComboBox, 1);
        modelPanel.Children.Add(modelComboBox);

        var connectButton = new Button
        {
            Content = aiService.IsModelLoaded ? "✓ Connected" : "🔗 Connect",
            Padding = new System.Windows.Thickness(12, 5, 12, 5),
            Background = aiService.IsModelLoaded 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#388E3C"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B1FA2")),
            Foreground = Brushes.White,
            BorderThickness = new System.Windows.Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new System.Windows.Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(connectButton, 2);
        modelPanel.Children.Add(connectButton);

        aiOuterPanel.Children.Add(modelPanel);

        // Model status text
        var modelAvailable = aiService.IsModelAvailable();
        var modelStatusText = new TextBlock
        {
            Text = aiService.IsModelLoaded ? $"✅ Connected - {aiService.SelectedModel}" : 
                   "❌ Not connected to Ollama",
            Foreground = aiService.IsModelLoaded 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#81C784"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373")),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            FontSize = 11,
            Margin = new System.Windows.Thickness(0, 5, 0, 0)
        };
        aiOuterPanel.Children.Add(modelStatusText);

        // Second row: Prompt input
        var aiPanel = new Grid();
        aiPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
        aiPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        aiPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });

        var aiLabel = new TextBlock
        {
            Text = "✨ Prompt:",
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FC3F7")),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new System.Windows.Thickness(0, 0, 10, 0),
            FontWeight = System.Windows.FontWeights.SemiBold
        };
        Grid.SetColumn(aiLabel, 0);
        aiPanel.Children.Add(aiLabel);

        var aiPromptBox = new TextBox
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
            Padding = new System.Windows.Thickness(8, 5, 8, 5),
            FontSize = 12,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Text = "Describe what you want the script to do..."
        };
        aiPromptBox.GotFocus += (s, args) =>
        {
            if (aiPromptBox.Text == "Describe what you want the script to do...")
            {
                aiPromptBox.Text = "";
                aiPromptBox.Foreground = Brushes.White;
            }
        };
        aiPromptBox.LostFocus += (s, args) =>
        {
            if (string.IsNullOrWhiteSpace(aiPromptBox.Text))
            {
                aiPromptBox.Text = "Describe what you want the script to do...";
                aiPromptBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
            }
        };
        Grid.SetColumn(aiPromptBox, 1);
        aiPanel.Children.Add(aiPromptBox);

        var aiButtonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new System.Windows.Thickness(10, 0, 0, 0)
        };

        TextBox? scriptTextBox = null;
        TextBlock? statusText = null;

        var generateButton = new Button
        {
            Content = "🚀 Generate",
            Padding = new System.Windows.Thickness(12, 5, 12, 5),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
            Foreground = Brushes.White,
            BorderThickness = new System.Windows.Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new System.Windows.Thickness(0, 0, 5, 0)
        };

        var suggestionsButton = new Button
        {
            Content = "💡",
            Padding = new System.Windows.Thickness(8, 5, 8, 5),
            ToolTip = "Show example prompts",
            Cursor = System.Windows.Input.Cursors.Hand
        };

        aiButtonPanel.Children.Add(generateButton);
        aiButtonPanel.Children.Add(suggestionsButton);
        Grid.SetColumn(aiButtonPanel, 2);
        aiPanel.Children.Add(aiButtonPanel);

        aiOuterPanel.Children.Add(aiPanel);
        aiBorder.Child = aiOuterPanel;
        Grid.SetRow(aiBorder, 1);
        mainGrid.Children.Add(aiBorder);

        // Status bar
        var statusBorder = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
            Padding = new System.Windows.Thickness(10, 5, 10, 5),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
            BorderThickness = new System.Windows.Thickness(0, 0, 0, 1)
        };
        statusText = new TextBlock
        {
            Text = aiService.IsModelLoaded ? $"Ready - Connected to Ollama ({aiService.SelectedModel})" : 
                   "Click 'Connect' to connect to Ollama and select a model",
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
            FontSize = 11
        };
        statusBorder.Child = statusText;
        Grid.SetRow(statusBorder, 2);
        mainGrid.Children.Add(statusBorder);

        // Script text box
        scriptTextBox = new TextBox
        {
            Text = clip.CustomScript,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            AcceptsReturn = true,
            AcceptsTab = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4D4")),
            CaretBrush = Brushes.White,
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(10)
        };
        Grid.SetRow(scriptTextBox, 3);
        mainGrid.Children.Add(scriptTextBox);

        // Wire up status updates from AI service
        aiService.StatusChanged += (s, status) =>
        {
            scriptWindow.Dispatcher.Invoke(() =>
            {
                statusText.Text = status;
            });
        };

        // Wire up models loaded event
        aiService.ModelsLoaded += (s, models) =>
        {
            scriptWindow.Dispatcher.Invoke(() =>
            {
                modelComboBox.Items.Clear();
                if (models.Count > 0)
                {
                    foreach (var model in models)
                    {
                        modelComboBox.Items.Add(model);
                    }
                    modelComboBox.SelectedIndex = 0;
                    modelComboBox.IsEnabled = true;
                    aiService.SelectModel(models[0]);
                }
                else
                {
                    modelComboBox.Items.Add("No models found. Run: ollama pull llama3.2");
                    modelComboBox.SelectedIndex = 0;
                }
            });
        };

        // Wire up model selection change
        modelComboBox.SelectionChanged += (s, args) =>
        {
            if (modelComboBox.SelectedItem is string selectedModel && !selectedModel.StartsWith("Click") && !selectedModel.StartsWith("No models"))
            {
                aiService.SelectModel(selectedModel);
                modelStatusText.Text = $"✅ Selected: {selectedModel}";
                modelStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#81C784"));
            }
        };

        // Wire up connect button
        connectButton.Click += async (s, args) =>
        {
            connectButton.IsEnabled = false;
            connectButton.Content = "⏳ Connecting...";
            statusText.Text = "Connecting to Ollama...";

            try
            {
                var success = await aiService.ConnectAsync();
                
                if (success)
                {
                    connectButton.Content = "✓ Connected";
                    connectButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#388E3C"));
                    
                    if (aiService.AvailableModels.Count > 0)
                    {
                        statusText.Text = $"Connected! {aiService.AvailableModels.Count} models available. Select a model to start.";
                    }
                    else
                    {
                        statusText.Text = "Connected but no models found. Run 'ollama pull <model>' to download.";
                        modelStatusText.Text = "⚠️ No models - run: ollama pull llama3.2";
                        modelStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500"));
                    }
                }
                else
                {
                    connectButton.Content = "🔗 Connect";
                    connectButton.IsEnabled = true;
                    modelStatusText.Text = "❌ Failed to connect";
                    modelStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373"));
                    statusText.Text = "Cannot connect to Ollama. Make sure it's running (ollama serve)";
                }
            }
            catch (Exception ex)
            {
                connectButton.Content = "🔗 Connect";
                connectButton.IsEnabled = true;
                statusText.Text = $"Error: {ex.Message}";
            }
        };

        // Wire up generate button
        generateButton.Click += async (s, args) =>
        {
            var prompt = aiPromptBox.Text;
            if (string.IsNullOrWhiteSpace(prompt) || prompt == "Describe what you want the script to do...")
            {
                System.Windows.MessageBox.Show("Please enter a description of what you want the script to do.", 
                    "Enter Description", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            generateButton.IsEnabled = false;
            generateButton.Content = "⏳ Generating...";
            statusText.Text = aiService.IsModelLoaded ? $"Generating with {aiService.SelectedModel}..." : "Generating with templates...";

            try
            {
                var generatedScript = await aiService.GenerateScriptAsync(prompt);
                scriptTextBox.Text = generatedScript;
                statusText.Text = aiService.IsModelLoaded ? $"Generated with {aiService.SelectedModel}" : "Generated with template matching";
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Error generating script: {ex.Message}", 
                    "Generation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                generateButton.IsEnabled = true;
                generateButton.Content = "🚀 Generate";
            }
        };

        // Allow Enter key to generate
        aiPromptBox.KeyDown += (s, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter && generateButton.IsEnabled)
            {
                generateButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            }
        };

        // Suggestions popup - styled like context menu
        suggestionsButton.Click += (s, args) =>
        {
            var suggestions = aiService.GetSuggestions();
            var menu = new ContextMenu 
            { 
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
                BorderThickness = new System.Windows.Thickness(1)
            };
            foreach (var suggestion in suggestions)
            {
                var item = new MenuItem 
                { 
                    Header = suggestion, 
                    Foreground = Brushes.White,
                    Background = Brushes.Transparent
                };
                item.Click += (ms, ma) =>
                {
                    aiPromptBox.Text = suggestion;
                    aiPromptBox.Foreground = Brushes.White;
                };
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
            menu.PlacementTarget = suggestionsButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        };

        // Bottom button panel
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(10),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526"))
        };
        Grid.SetRow(buttonPanel, 4);

        var insertTemplateButton = new Button
        {
            Content = "Insert Template",
            Padding = new System.Windows.Thickness(10, 5, 10, 5),
            Margin = new System.Windows.Thickness(0, 0, 5, 0)
        };
        insertTemplateButton.Click += (s, args) =>
        {
            var template = "; Hello World AHK v2 Script\n#Requires AutoHotkey v2.0\nMsgBox \"Hello World!\"\n";
            scriptTextBox.Text = template + scriptTextBox.Text;
        };
        buttonPanel.Children.Add(insertTemplateButton);

        var saveButton = new Button
        {
            Content = "Save",
            Padding = new System.Windows.Thickness(15, 5, 15, 5),
            Margin = new System.Windows.Thickness(0, 0, 5, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")),
            Foreground = Brushes.White,
            BorderThickness = new System.Windows.Thickness(0)
        };
        saveButton.Click += (s, args) =>
        {
            clip.CustomScript = scriptTextBox.Text;
            onSave?.Invoke();
            scriptWindow.Close();
        };
        buttonPanel.Children.Add(saveButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new System.Windows.Thickness(15, 5, 15, 5)
        };
        cancelButton.Click += (s, args) => scriptWindow.Close();
        buttonPanel.Children.Add(cancelButton);

        mainGrid.Children.Add(buttonPanel);
        scriptWindow.Content = mainGrid;
        scriptWindow.ShowDialog();
    }

    private void PickColor_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var clip = GetClipFromMenuItem(sender);
        if (clip == null) return;

        var colorWindow = new System.Windows.Window
        {
            Title = $"Pick Color - {clip.Name}",
            Width = 350,
            Height = 300,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")),
            ResizeMode = System.Windows.ResizeMode.NoResize
        };

        var mainStack = new StackPanel { Margin = new System.Windows.Thickness(15) };

        // Title
        var titleText = new TextBlock
        {
            Text = "Select a color for the clip:",
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new System.Windows.Thickness(0, 0, 0, 10)
        };
        mainStack.Children.Add(titleText);

        // Predefined colors grid
        var colorsGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 6, Margin = new System.Windows.Thickness(0, 0, 0, 15) };
        var predefinedColors = new[]
        {
            "#007ACC", "#0078D4", "#00BFFF", "#00CED1", "#20B2AA", "#3CB371",
            "#107C10", "#32CD32", "#7FFF00", "#FFD700", "#FFA500", "#FF8C00",
            "#D13438", "#FF4500", "#FF1493", "#FF69B4", "#9B4F96", "#8A2BE2",
            "#6A5ACD", "#4169E1", "#1E90FF", "#87CEEB", "#708090", "#2F4F4F"
        };

        foreach (var colorHex in predefinedColors)
        {
            var btn = new Button
            {
                Width = 40,
                Height = 30,
                Margin = new System.Windows.Thickness(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
                BorderThickness = new System.Windows.Thickness(1),
                Tag = colorHex
            };
            btn.Click += (s, args) =>
            {
                clip.Color = (s as Button)?.Tag as string ?? "#007ACC";
                colorWindow.Close();
            };
            colorsGrid.Children.Add(btn);
        }
        mainStack.Children.Add(colorsGrid);

        // Custom hex input
        var customPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new System.Windows.Thickness(0, 5, 0, 0) };
        var hexLabel = new TextBlock
        {
            Text = "Custom Hex:",
            Foreground = Brushes.White,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new System.Windows.Thickness(0, 0, 10, 0)
        };
        customPanel.Children.Add(hexLabel);

        var hexTextBox = new TextBox
        {
            Text = clip.Color,
            Width = 100,
            Margin = new System.Windows.Thickness(0, 0, 10, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
            Padding = new System.Windows.Thickness(5, 3, 5, 3)
        };
        customPanel.Children.Add(hexTextBox);

        var previewRect = new Border
        {
            Width = 40,
            Height = 25,
            CornerRadius = new System.Windows.CornerRadius(3),
            BorderBrush = Brushes.White,
            BorderThickness = new System.Windows.Thickness(1)
        };
        try
        {
            previewRect.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(clip.Color));
        }
        catch
        {
            previewRect.Background = new SolidColorBrush(Colors.Blue);
        }
        customPanel.Children.Add(previewRect);

        hexTextBox.TextChanged += (s, args) =>
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexTextBox.Text);
                previewRect.Background = new SolidColorBrush(color);
            }
            catch { }
        };

        mainStack.Children.Add(customPanel);

        // Apply custom button
        var applyButton = new Button
        {
            Content = "Apply Custom Color",
            Margin = new System.Windows.Thickness(0, 15, 0, 0),
            Padding = new System.Windows.Thickness(10, 5, 10, 5),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };
        applyButton.Click += (s, args) =>
        {
            try
            {
                // Validate the color
                ColorConverter.ConvertFromString(hexTextBox.Text);
                clip.Color = hexTextBox.Text;
                colorWindow.Close();
            }
            catch
            {
                System.Windows.MessageBox.Show("Invalid color format. Please use hex format like #RRGGBB", "Invalid Color", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        };
        mainStack.Children.Add(applyButton);

        colorWindow.Content = mainStack;
        colorWindow.ShowDialog();
    }

    private void DuplicateClip_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var clip = GetClipFromMenuItem(sender);
        if (clip == null) return;

        // Find the track containing this clip
        if (DataContext is ViewModels.TimelineViewModel timeline)
        {
            foreach (var track in timeline.Tracks)
            {
                if (track.Clips.Contains(clip))
                {
                    var newClip = new ViewModels.ClipViewModel(
                        clip.Name + " (Copy)",
                        clip.StartTime + clip.Duration + 0.1,
                        clip.Duration,
                        clip.Color,
                        clip.ActionType
                    )
                    {
                        CustomScript = clip.CustomScript
                    };
                    track.Clips.Add(newClip);
                    Log($"Duplicated clip '{clip.Name}' to '{newClip.Name}' at {newClip.StartTime:F2}s");
                    break;
                }
            }
        }
    }

    private void DeleteClip_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var clip = GetClipFromMenuItem(sender);
        if (clip == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete '{clip.Name}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            if (DataContext is ViewModels.TimelineViewModel timeline)
            {
                foreach (var track in timeline.Tracks)
                {
                    if (track.Clips.Contains(clip))
                    {
                        track.Clips.Remove(clip);
                        Log($"Deleted clip '{clip.Name}' from track '{track.Name}'");
                        
                        // Clear selection if this was selected
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                        {
                            if (mainViewModel.Properties.SelectedClip == clip)
                            {
                                mainViewModel.Properties.SetSelectedClip(null);
                            }
                            mainViewModel.GenerateScript();
                        }
                        break;
                    }
                }
            }
        }
    }

    private ViewModels.ClipViewModel? GetClipFromMenuItem(object sender)
    {
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is System.Windows.Controls.Primitives.Thumb thumb &&
            thumb.Tag is ViewModels.ClipViewModel clip)
        {
            return clip;
        }
        return null;
    }

    private void SetLength_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.TimelineViewModel timeline)
        {
            ShowTimelineLengthDialog(timeline);
        }
    }

    private void AddTrack_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.TimelineViewModel timeline)
        {
            var trackNumber = timeline.Tracks.Count + 1;
            var newTrack = new ViewModels.TrackViewModel($"Track {trackNumber}");
            timeline.Tracks.Add(newTrack);
            Log($"Added track '{newTrack.Name}'");
        }
    }

    private void AddMacro_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.TimelineViewModel timeline)
        {
            if (timeline.Tracks.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Please add a track first.",
                    "No Track Available",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            // Add clip to the first track
            var targetTrack = timeline.Tracks[0];
            
            // Calculate start time (end of last clip or 0)
            double startTime = 0;
            if (targetTrack.Clips.Count > 0)
            {
                var lastClip = targetTrack.Clips.OrderByDescending(c => c.StartTime + c.Duration).First();
                startTime = lastClip.StartTime + lastClip.Duration + 0.1;
            }

            // Ensure it fits within timeline
            if (startTime + 1.0 > timeline.TotalLength)
            {
                startTime = Math.Max(0, timeline.TotalLength - 1.0);
            }

            var newClip = new ViewModels.ClipViewModel($"Macro {targetTrack.Clips.Count + 1}", startTime, 1.0);
            targetTrack.Clips.Add(newClip);
            Log($"Added macro '{newClip.Name}' to track '{targetTrack.Name}' at {startTime:F2}s");

            // Select the new clip
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
            {
                mainViewModel.Properties.SetSelectedClip(newClip);
                mainViewModel.GenerateScript();
            }
        }
    }

    private void ShowTimelineLengthDialog(ViewModels.TimelineViewModel timeline)
    {
        var lengthWindow = new System.Windows.Window
        {
            Title = "Set Timeline Length",
            Width = 350,
            Height = 280,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")),
            ResizeMode = System.Windows.ResizeMode.NoResize
        };

        var mainStack = new StackPanel { Margin = new System.Windows.Thickness(15) };

        // Title
        var titleText = new TextBlock
        {
            Text = "Set the total timeline length:",
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = System.Windows.FontWeights.Bold,
            Margin = new System.Windows.Thickness(0, 0, 0, 15)
        };
        mainStack.Children.Add(titleText);

        // Current length
        var currentText = new TextBlock
        {
            Text = $"Current: {ViewModels.TimelineViewModel.FormatTimeSpan(timeline.TotalLength)}",
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
            FontSize = 11,
            Margin = new System.Windows.Thickness(0, 0, 0, 10)
        };
        mainStack.Children.Add(currentText);

        // Time input grid
        var inputGrid = new Grid { Margin = new System.Windows.Thickness(0, 0, 0, 10) };
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        inputGrid.RowDefinitions.Add(new RowDefinition { Height = System.Windows.GridLength.Auto });
        inputGrid.RowDefinitions.Add(new RowDefinition { Height = System.Windows.GridLength.Auto });

        var ts = TimeSpan.FromSeconds(timeline.TotalLength);
        
        // Labels
        var labels = new[] { "Days", "Hours", "Minutes", "Seconds" };
        for (int i = 0; i < labels.Length; i++)
        {
            var label = new TextBlock { Text = labels[i], Foreground = Brushes.White, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new System.Windows.Thickness(2) };
            Grid.SetColumn(label, i);
            inputGrid.Children.Add(label);
        }

        // Input boxes
        var daysBox = CreateInputBox(((int)ts.TotalDays).ToString());
        var hoursBox = CreateInputBox(ts.Hours.ToString());
        var minsBox = CreateInputBox(ts.Minutes.ToString());
        var secsBox = CreateInputBox(ts.Seconds.ToString());
        
        Grid.SetColumn(daysBox, 0); Grid.SetRow(daysBox, 1);
        Grid.SetColumn(hoursBox, 1); Grid.SetRow(hoursBox, 1);
        Grid.SetColumn(minsBox, 2); Grid.SetRow(minsBox, 1);
        Grid.SetColumn(secsBox, 3); Grid.SetRow(secsBox, 1);
        inputGrid.Children.Add(daysBox);
        inputGrid.Children.Add(hoursBox);
        inputGrid.Children.Add(minsBox);
        inputGrid.Children.Add(secsBox);

        mainStack.Children.Add(inputGrid);

        // Quick presets
        var presetsLabel = new TextBlock { Text = "Quick Presets:", Foreground = Brushes.White, FontSize = 11, Margin = new System.Windows.Thickness(0, 10, 0, 5) };
        mainStack.Children.Add(presetsLabel);

        var presetsPanel = new WrapPanel { Margin = new System.Windows.Thickness(0, 0, 0, 15) };
        var presets = new[] { ("1 min", 60.0), ("5 min", 300.0), ("30 min", 1800.0), ("1 hour", 3600.0), ("1 day", 86400.0), ("1 week", 604800.0) };
        foreach (var (label, seconds) in presets)
        {
            var btn = new Button { Content = label, Margin = new System.Windows.Thickness(0, 0, 5, 5), Padding = new System.Windows.Thickness(8, 3, 8, 3), Tag = seconds };
            btn.Click += (s, args) =>
            {
                var secs = (double)((Button)s).Tag;
                var preset = TimeSpan.FromSeconds(secs);
                daysBox.Text = ((int)preset.TotalDays).ToString();
                hoursBox.Text = preset.Hours.ToString();
                minsBox.Text = preset.Minutes.ToString();
                secsBox.Text = preset.Seconds.ToString();
            };
            presetsPanel.Children.Add(btn);
        }
        mainStack.Children.Add(presetsPanel);

        // Buttons
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var applyButton = new Button { Content = "Apply", Width = 75, Margin = new System.Windows.Thickness(0, 0, 5, 0), Padding = new System.Windows.Thickness(5) };
        applyButton.Click += (s, args) =>
        {
            int days = int.TryParse(daysBox.Text, out var d) ? d : 0;
            int hours = int.TryParse(hoursBox.Text, out var h) ? h : 0;
            int mins = int.TryParse(minsBox.Text, out var m) ? m : 0;
            int secs = int.TryParse(secsBox.Text, out var sec) ? sec : 0;

            double totalSeconds = days * 86400 + hours * 3600 + mins * 60 + secs;

            if (totalSeconds < ViewModels.TimelineViewModel.MinLength)
            {
                System.Windows.MessageBox.Show("Minimum timeline length is 1 second.", "Invalid Length", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            if (totalSeconds > ViewModels.TimelineViewModel.MaxLength)
            {
                System.Windows.MessageBox.Show("Maximum timeline length is 1 week (7 days).", "Invalid Length", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            timeline.TotalLength = totalSeconds;
            lengthWindow.Close();
        };
        buttonPanel.Children.Add(applyButton);

        var cancelButton = new Button { Content = "Cancel", Width = 75, Padding = new System.Windows.Thickness(5) };
        cancelButton.Click += (s, args) => lengthWindow.Close();
        buttonPanel.Children.Add(cancelButton);

        mainStack.Children.Add(buttonPanel);
        lengthWindow.Content = mainStack;
        lengthWindow.ShowDialog();
    }

    private static TextBox CreateInputBox(string text)
    {
        return new TextBox
        {
            Text = text,
            Margin = new System.Windows.Thickness(2),
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"))
        };
    }

    private void Log(string message)
    {
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
        {
            mainViewModel.Log(message);
        }
    }

    #region Track Context Menu Handlers

    private ViewModels.TrackViewModel? GetTrackFromContextMenu(object sender)
    {
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.Tag is ViewModels.TrackViewModel track)
        {
            return track;
        }
        return null;
    }

    private void RenameTrack_Click(object sender, RoutedEventArgs e)
    {
        var track = GetTrackFromContextMenu(sender);
        if (track == null) return;

        var renameWindow = new Window
        {
            Title = "Rename Track",
            Width = 300,
            Height = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")),
            ResizeMode = ResizeMode.NoResize
        };

        var stack = new StackPanel { Margin = new Thickness(15) };
        var label = new TextBlock { Text = "Track Name:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) };
        stack.Children.Add(label);

        var textBox = new TextBox
        {
            Text = track.Name,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
            Padding = new Thickness(5)
        };
        stack.Children.Add(textBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0), Padding = new Thickness(5) };
        okButton.Click += (s, args) =>
        {
            var oldName = track.Name;
            track.Name = textBox.Text;
            Log($"Track renamed: '{oldName}' -> '{track.Name}'");
            renameWindow.Close();
        };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button { Content = "Cancel", Width = 75, Padding = new Thickness(5) };
        cancelButton.Click += (s, args) => renameWindow.Close();
        buttonPanel.Children.Add(cancelButton);

        stack.Children.Add(buttonPanel);
        renameWindow.Content = stack;
        renameWindow.ShowDialog();
    }

    private void MoveTrackUp_Click(object sender, RoutedEventArgs e)
    {
        var track = GetTrackFromContextMenu(sender);
        if (track == null || DataContext is not ViewModels.TimelineViewModel timeline) return;

        var index = timeline.Tracks.IndexOf(track);
        if (index > 0)
        {
            timeline.Tracks.Move(index, index - 1);
            Log($"Track '{track.Name}' moved up to position {index}");
        }
    }

    private void MoveTrackDown_Click(object sender, RoutedEventArgs e)
    {
        var track = GetTrackFromContextMenu(sender);
        if (track == null || DataContext is not ViewModels.TimelineViewModel timeline) return;

        var index = timeline.Tracks.IndexOf(track);
        if (index < timeline.Tracks.Count - 1)
        {
            timeline.Tracks.Move(index, index + 1);
            Log($"Track '{track.Name}' moved down to position {index + 2}");
        }
    }

    private void DeleteTrack_Click(object sender, RoutedEventArgs e)
    {
        var track = GetTrackFromContextMenu(sender);
        if (track == null || DataContext is not ViewModels.TimelineViewModel timeline) return;

        var clipCount = track.Clips.Count;
        var message = clipCount > 0
            ? $"Are you sure you want to delete track '{track.Name}'?\nThis will also delete {clipCount} clip(s)."
            : $"Are you sure you want to delete track '{track.Name}'?";

        var result = MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Clear selection if any clip in this track is selected
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
            {
                if (mainViewModel.Properties.SelectedClip != null && track.Clips.Contains(mainViewModel.Properties.SelectedClip))
                {
                    mainViewModel.Properties.SetSelectedClip(null);
                }
            }

            timeline.Tracks.Remove(track);
            Log($"Track '{track.Name}' deleted (had {clipCount} clips)");
        }
    }

    #endregion

    #region Move Clip Between Tracks

    private void ClipContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is Thumb thumb &&
            thumb.Tag is ViewModels.ClipViewModel clip &&
            DataContext is ViewModels.TimelineViewModel timeline)
        {
            // Find the "Move to Track" menu item
            MenuItem? moveToTrackMenu = null;
            foreach (var item in contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header?.ToString() == "Move to Track")
                {
                    moveToTrackMenu = menuItem;
                    break;
                }
            }

            if (moveToTrackMenu != null)
            {
                moveToTrackMenu.Items.Clear();

                // Find current track
                ViewModels.TrackViewModel? currentTrack = null;
                foreach (var track in timeline.Tracks)
                {
                    if (track.Clips.Contains(clip))
                    {
                        currentTrack = track;
                        break;
                    }
                }

                // Add menu items for each track
                foreach (var track in timeline.Tracks)
                {
                    var menuItem = new MenuItem
                    {
                        Header = track.Name,
                        Tag = track,
                        IsEnabled = track != currentTrack,
                        IsChecked = track == currentTrack
                    };
                    menuItem.Click += (s, args) =>
                    {
                        if (s is MenuItem mi && mi.Tag is ViewModels.TrackViewModel targetTrack)
                        {
                            MoveClipToTrack(clip, currentTrack!, targetTrack);
                        }
                    };
                    moveToTrackMenu.Items.Add(menuItem);
                }

                if (timeline.Tracks.Count == 0)
                {
                    moveToTrackMenu.Items.Add(new MenuItem { Header = "(No tracks)", IsEnabled = false });
                }
            }
        }
    }

    private void MoveClipToTrack(ViewModels.ClipViewModel clip, ViewModels.TrackViewModel sourceTrack, ViewModels.TrackViewModel targetTrack)
    {
        sourceTrack.Clips.Remove(clip);
        targetTrack.Clips.Add(clip);
        Log($"Clip '{clip.Name}' moved from '{sourceTrack.Name}' to '{targetTrack.Name}'");

        // Refresh script
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
        {
            mainViewModel.GenerateScript();
        }
    }

    #endregion
}
