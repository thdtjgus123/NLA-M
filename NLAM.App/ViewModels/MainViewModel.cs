using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace NLAM.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public TimelineViewModel Timeline { get; }
    public PropertiesViewModel Properties { get; }
    private readonly Services.FileService _fileService;
    private readonly Services.AIScriptService _aiService;

    // Clipboard for copy/paste
    private ClipViewModel? _clipboardClip;
    
    // Track under mouse for paste target
    [ObservableProperty]
    private TrackViewModel? _hoverTrack;

    [ObservableProperty]
    private string _scriptText = "; AutoHotkey Script\n; Timeline will generate script here\n";

    public ObservableCollection<string> DebugLogs { get; } = new();

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] {message}";
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            DebugLogs.Add(logEntry);
            // Keep only last 500 entries
            while (DebugLogs.Count > 500)
                DebugLogs.RemoveAt(0);
        });
    }

    public MainViewModel(TimelineViewModel timeline, PropertiesViewModel properties, Services.FileService fileService, Services.AIScriptService aiService)
    {
        Timeline = timeline;
        Properties = properties;
        _fileService = fileService;
        _aiService = aiService;
        
        // Subscribe to timeline changes to regenerate script
        Timeline.Tracks.CollectionChanged += (s, e) => 
        {
            GenerateScript();
            Log($"Tracks changed: {e.Action}");
        };
        
        Log("NLAM initialized");
    }

    public void GenerateScript()
    {
        var script = new System.Text.StringBuilder();
        
        // Collect all clips from all tracks and sort by start time
        var allClips = Timeline.Tracks
            .SelectMany(t => t.Clips)
            .OrderBy(c => c.StartTime)
            .ToList();
        
        if (allClips.Count == 0)
        {
            // Simple Hello World script (AHK v2)
            script.AppendLine("#Requires AutoHotkey v2.0");
            script.AppendLine("MsgBox \"Hello World!\"");
            ScriptText = script.ToString();
            return;
        }
        
        // AHK v2 script
        script.AppendLine("#Requires AutoHotkey v2.0");
        script.AppendLine("#SingleInstance Force");
        script.AppendLine();
        
        double lastEndTime = 0;
        
        foreach (var clip in allClips)
        {
            // Calculate delay from end of last action to start of this one
            double delay = clip.StartTime - lastEndTime;
            if (delay > 0)
            {
                script.AppendLine($"Sleep {(int)(delay * 1000)}");
            }
            
            script.AppendLine($"; {clip.Name}");
            
            // Use custom script if available
            if (!string.IsNullOrWhiteSpace(clip.CustomScript))
            {
                script.AppendLine(clip.CustomScript);
            }
            else
            {
                // Default: show Hello World message (AHK v2 syntax)
                script.AppendLine("MsgBox \"Hello World!\"");
            }
            
            lastEndTime = clip.StartTime + clip.Duration;
        }
        
        script.AppendLine();
        script.AppendLine("ExitApp");
        ScriptText = script.ToString();
    }

    [RelayCommand]
    private void RunScript()
    {
        try
        {
            // Check if AutoHotkey is installed
            var ahkPaths = new[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AutoHotkey", "v2", "AutoHotkey.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AutoHotkey", "AutoHotkey.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AutoHotkey", "v2", "AutoHotkey.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AutoHotkey", "AutoHotkey.exe"),
            };

            string? ahkExe = ahkPaths.FirstOrDefault(System.IO.File.Exists);

            if (ahkExe == null)
            {
                var result = System.Windows.MessageBox.Show(
                    "AutoHotkey is not installed.\n\nWould you like to download it from the official website?",
                    "AutoHotkey Not Found",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://www.autohotkey.com/download/",
                        UseShellExecute = true
                    });
                }
                return;
            }

            // Save script to temp file
            var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nlam_script.ahk");
            System.IO.File.WriteAllText(tempFile, ScriptText);

            // Run the script
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ahkExe,
                Arguments = $"\"{tempFile}\"",
                UseShellExecute = false
            });

            Log($"Script started: {tempFile}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to run script: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Play() => RunScript();

    [RelayCommand]
    private void Pause() => Timeline.Pause();

    [RelayCommand]
    private void Stop() => Timeline.Stop();

    [RelayCommand]
    private void New()
    {
        var result = System.Windows.MessageBox.Show(
            "Create a new timeline? Any unsaved changes will be lost.",
            "New Timeline",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Timeline.Tracks.Clear();
            Timeline.Tracks.Add(new TrackViewModel("Track 1"));
            Timeline.CurrentTime = 0;
            Properties.SetSelectedClip(null);
            GenerateScript();
        }
    }

    [RelayCommand]
    private void SaveAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "NLAM Timeline (*.nlam)|*.nlam|JSON Files (*.json)|*.json",
            DefaultExt = ".nlam"
        };

        if (dialog.ShowDialog() == true)
        {
            _fileService.SaveTimeline(Timeline, dialog.FileName);
        }
    }

    [RelayCommand]
    private void ExportScript()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "AutoHotkey Script (*.ahk)|*.ahk|Text Files (*.txt)|*.txt",
            DefaultExt = ".ahk",
            FileName = "macro_script.ahk"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(dialog.FileName, ScriptText);
                System.Windows.MessageBox.Show(
                    $"Script exported successfully to:\n{dialog.FileName}",
                    "Export Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to export script: {ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void Undo()
    {
        // TODO: Implement undo functionality
    }

    [RelayCommand]
    private void Redo()
    {
        // TODO: Implement redo functionality
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Properties.SelectedClip != null)
        {
            var clip = Properties.SelectedClip;
            foreach (var track in Timeline.Tracks)
            {
                if (track.Clips.Contains(clip))
                {
                    track.Clips.Remove(clip);
                    Properties.SetSelectedClip(null);
                    GenerateScript();
                    break;
                }
            }
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        // TODO: Implement select all functionality
    }

    [RelayCommand]
    private void CopyClip()
    {
        if (Properties.SelectedClip != null)
        {
            _clipboardClip = Properties.SelectedClip;
            Log($"Copied clip '{_clipboardClip.Name}' to clipboard");
        }
    }

    [RelayCommand]
    private void PasteClip()
    {
        if (_clipboardClip == null)
        {
            Log("Nothing to paste - clipboard is empty");
            return;
        }

        // Find target track - use hover track or first track
        var targetTrack = HoverTrack ?? Timeline.Tracks.FirstOrDefault();
        if (targetTrack == null)
        {
            System.Windows.MessageBox.Show(
                "Please add a track first.",
                "No Track Available",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        // Calculate start time (end of last clip or 0)
        double startTime = 0;
        if (targetTrack.Clips.Count > 0)
        {
            var lastClip = targetTrack.Clips.OrderByDescending(c => c.StartTime + c.Duration).First();
            startTime = lastClip.StartTime + lastClip.Duration + 0.1;
        }

        // Ensure it fits within timeline
        if (startTime + _clipboardClip.Duration > Timeline.TotalLength)
        {
            startTime = Math.Max(0, Timeline.TotalLength - _clipboardClip.Duration);
        }

        var newClip = new ClipViewModel(
            _clipboardClip.Name + " (Copy)",
            startTime,
            _clipboardClip.Duration,
            _clipboardClip.Color,
            _clipboardClip.ActionType
        )
        {
            CustomScript = _clipboardClip.CustomScript
        };

        targetTrack.Clips.Add(newClip);
        Properties.SetSelectedClip(newClip);
        GenerateScript();
        Log($"Pasted clip '{newClip.Name}' to track '{targetTrack.Name}' at {startTime:F2}s");
    }

    public ClipViewModel? DuplicateClip(ClipViewModel clip, TrackViewModel? targetTrack = null)
    {
        // Find source track if target not specified
        TrackViewModel? sourceTrack = null;
        foreach (var track in Timeline.Tracks)
        {
            if (track.Clips.Contains(clip))
            {
                sourceTrack = track;
                break;
            }
        }

        targetTrack ??= sourceTrack;
        if (targetTrack == null) return null;

        // Calculate start time (end of last clip or after current clip)
        double startTime = clip.StartTime + clip.Duration + 0.1;
        
        // Ensure it fits within timeline
        if (startTime + clip.Duration > Timeline.TotalLength)
        {
            startTime = Math.Max(0, Timeline.TotalLength - clip.Duration);
        }

        var newClip = new ClipViewModel(
            clip.Name + " (Copy)",
            startTime,
            clip.Duration,
            clip.Color,
            clip.ActionType
        )
        {
            CustomScript = clip.CustomScript
        };

        targetTrack.Clips.Add(newClip);
        GenerateScript();
        Log($"Duplicated clip '{clip.Name}' to '{newClip.Name}' at {startTime:F2}s");
        return newClip;
    }

    [RelayCommand]
    private void AddTrack()
    {
        var trackNumber = Timeline.Tracks.Count + 1;
        Timeline.Tracks.Add(new TrackViewModel($"Track {trackNumber}"));
    }

    [RelayCommand]
    private void AddClip()
    {
        if (Timeline.Tracks.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Please add a track first.",
                "No Track Available",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        // Add clip to the first track or find a selected track
        var targetTrack = Timeline.Tracks[0];
        
        // Calculate start time (end of last clip or 0)
        double startTime = 0;
        if (targetTrack.Clips.Count > 0)
        {
            var lastClip = targetTrack.Clips.OrderByDescending(c => c.StartTime + c.Duration).First();
            startTime = lastClip.StartTime + lastClip.Duration + 0.1;
        }

        var newClip = new ClipViewModel($"Clip {targetTrack.Clips.Count + 1}", startTime, 1.0);
        targetTrack.Clips.Add(newClip);
        Properties.SetSelectedClip(newClip);
        GenerateScript();
    }

    [RelayCommand]
    private void ResetZoom()
    {
        Timeline.ZoomLevel = 1.0;
    }

    [RelayCommand]
    private void RefreshScript()
    {
        GenerateScript();
    }

    [RelayCommand]
    private async Task CodeFixerAsync()
    {
        if (string.IsNullOrWhiteSpace(ScriptText))
        {
            System.Windows.MessageBox.Show(
                "No script to review. Add some clips first.",
                "No Script",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var mainWindow = System.Windows.Application.Current.MainWindow;
        var progressDialog = new Views.AIProgressDialog(mainWindow, "Code Fixer (AI)");
        
        // Set model info
        progressDialog.SetModel(_aiService.IsModelLoaded ? _aiService.SelectedModel : "Connecting...");
        
        // Disable main window
        mainWindow.IsEnabled = false;

        Services.CodeFixResult? result = null;

        // Start the task
        var task = Task.Run(async () =>
        {
            try
            {
                progressDialog.AppendLog("═══════════════════════════════════════");
                progressDialog.AppendLog("  NLAM Code Fixer (AI)");
                progressDialog.AppendLog("═══════════════════════════════════════");
                progressDialog.AppendLog("");
                progressDialog.AppendLog($"📄 Script size: {ScriptText.Length} characters");
                progressDialog.AppendLog("");

                // Check if AI is connected
                if (!_aiService.IsModelLoaded)
                {
                    progressDialog.AppendLog("🔌 AI not connected. Attempting to connect...");
                    progressDialog.UpdateStatus("Connecting to Ollama...");
                    
                    var connected = await _aiService.ConnectAsync(progressDialog.CancellationToken);
                    if (connected && _aiService.AvailableModels.Count > 0)
                    {
                        _aiService.SelectModel(_aiService.AvailableModels[0]);
                        progressDialog.SetModel(_aiService.SelectedModel);
                        progressDialog.AppendLog($"✅ Connected! Using model: {_aiService.SelectedModel}");
                    }
                    else
                    {
                        progressDialog.AppendLog("❌ Could not connect to Ollama.");
                        progressDialog.AppendLog("");
                        progressDialog.AppendLog("💡 Make sure Ollama is running:");
                        progressDialog.AppendLog("   1. Install Ollama from https://ollama.ai");
                        progressDialog.AppendLog("   2. Run: ollama serve");
                        progressDialog.AppendLog("   3. Pull a model: ollama pull llama3.2");
                        progressDialog.Complete(false, "Failed to connect to Ollama");
                        return;
                    }
                    progressDialog.AppendLog("");
                }

                progressDialog.AppendLog("🔍 Analyzing script for errors...");
                progressDialog.UpdateStatus("Analyzing code with AI...");
                
                result = await _aiService.FixCodeAsync(
                    ScriptText, 
                    msg => progressDialog.AppendLog(msg),
                    progressDialog.CancellationToken);

                if (result.Issues.Count == 0)
                {
                    progressDialog.Complete(true, "No issues found!");
                }
                else
                {
                    progressDialog.Complete(true, $"Found {result.Issues.Count} issue(s)");
                }
            }
            catch (OperationCanceledException)
            {
                progressDialog.Complete(false);
            }
            catch (Exception ex)
            {
                progressDialog.AppendLog($"❌ Error: {ex.Message}");
                progressDialog.Complete(false, ex.Message);
            }
        });

        // Show dialog (blocks until closed)
        progressDialog.ShowDialog();
        
        // Re-enable main window
        mainWindow.IsEnabled = true;

        // Wait for task to complete
        await task;

        // Show results if not cancelled and we have results
        if (!progressDialog.WasCancelled && result != null)
        {
            Log($"Code Fixer completed: {result.Issues.Count} issues found");
            ShowCodeFixerResults(result);
        }
    }

    private void ShowCodeFixerResults(Services.CodeFixResult result)
    {
        var window = new System.Windows.Window
        {
            Title = "Code Fixer Results",
            Width = 800,
            Height = 600,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow,
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252526"))
        };

        var mainGrid = new System.Windows.Controls.Grid();
        mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

        // Header
        var headerBorder = new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
            Padding = new System.Windows.Thickness(15)
        };
        var headerPanel = new System.Windows.Controls.StackPanel();
        
        var titleText = new System.Windows.Controls.TextBlock
        {
            Text = result.Issues.Count == 0 ? "✅ No Issues Found!" : $"🔧 Found {result.Issues.Count} Issue(s)",
            Foreground = result.Issues.Count == 0 
                ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#81C784"))
                : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726")),
            FontSize = 16,
            FontWeight = System.Windows.FontWeights.Bold
        };
        headerPanel.Children.Add(titleText);

        var modelText = new System.Windows.Controls.TextBlock
        {
            Text = $"Analyzed with: {_aiService.SelectedModel}",
            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#888888")),
            FontSize = 11,
            Margin = new System.Windows.Thickness(0, 5, 0, 0)
        };
        headerPanel.Children.Add(modelText);

        headerBorder.Child = headerPanel;
        System.Windows.Controls.Grid.SetRow(headerBorder, 0);
        mainGrid.Children.Add(headerBorder);

        // Content area with tabs
        var tabControl = new System.Windows.Controls.TabControl
        {
            Margin = new System.Windows.Thickness(10),
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"))
        };

        // Issues Tab
        var issuesTab = new System.Windows.Controls.TabItem { Header = $"Issues ({result.Issues.Count})" };
        var issuesScroll = new System.Windows.Controls.ScrollViewer { VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
        var issuesStack = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(10) };

        if (result.Issues.Count == 0)
        {
            var noIssuesText = new System.Windows.Controls.TextBlock
            {
                Text = "🎉 Your script looks good! No errors or warnings detected.",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#81C784")),
                FontSize = 14,
                Margin = new System.Windows.Thickness(0, 10, 0, 0)
            };
            issuesStack.Children.Add(noIssuesText);
        }
        else
        {
            foreach (var issue in result.Issues)
            {
                var issueBorder = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30")),
                    BorderBrush = GetSeverityBrush(issue.Severity),
                    BorderThickness = new System.Windows.Thickness(2, 0, 0, 0),
                    Padding = new System.Windows.Thickness(10),
                    Margin = new System.Windows.Thickness(0, 0, 0, 10),
                    CornerRadius = new System.Windows.CornerRadius(3)
                };

                var issuePanel = new System.Windows.Controls.StackPanel();

                var severityIcon = issue.Severity switch
                {
                    "Error" => "❌",
                    "Warning" => "⚠️",
                    _ => "ℹ️"
                };

                var issueHeader = new System.Windows.Controls.TextBlock
                {
                    Text = $"{severityIcon} {issue.Severity}" + (issue.Line > 0 ? $" (Line {issue.Line})" : ""),
                    Foreground = GetSeverityBrush(issue.Severity),
                    FontWeight = System.Windows.FontWeights.Bold,
                    FontSize = 12
                };
                issuePanel.Children.Add(issueHeader);

                var issueDesc = new System.Windows.Controls.TextBlock
                {
                    Text = issue.Description,
                    Foreground = System.Windows.Media.Brushes.White,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(0, 5, 0, 0)
                };
                issuePanel.Children.Add(issueDesc);

                if (!string.IsNullOrWhiteSpace(issue.Suggestion))
                {
                    var suggestionText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"💡 {issue.Suggestion}",
                        Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4FC3F7")),
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        Margin = new System.Windows.Thickness(0, 5, 0, 0),
                        FontStyle = System.Windows.FontStyles.Italic
                    };
                    issuePanel.Children.Add(suggestionText);
                }

                issueBorder.Child = issuePanel;
                issuesStack.Children.Add(issueBorder);
            }
        }

        issuesScroll.Content = issuesStack;
        issuesTab.Content = issuesScroll;
        tabControl.Items.Add(issuesTab);

        // Fixed Script Tab
        var fixedTab = new System.Windows.Controls.TabItem { Header = "Fixed Script" };
        var fixedTextBox = new System.Windows.Controls.TextBox
        {
            Text = result.FixedScript,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            AcceptsReturn = true,
            IsReadOnly = true,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D4D4D4")),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(10)
        };
        fixedTab.Content = fixedTextBox;
        tabControl.Items.Add(fixedTab);

        // Original Script Tab
        var originalTab = new System.Windows.Controls.TabItem { Header = "Original Script" };
        var originalTextBox = new System.Windows.Controls.TextBox
        {
            Text = result.OriginalScript,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            AcceptsReturn = true,
            IsReadOnly = true,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D4D4D4")),
            BorderThickness = new System.Windows.Thickness(0),
            Padding = new System.Windows.Thickness(10)
        };
        originalTab.Content = originalTextBox;
        tabControl.Items.Add(originalTab);

        System.Windows.Controls.Grid.SetRow(tabControl, 1);
        mainGrid.Children.Add(tabControl);

        // Bottom buttons
        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(10)
        };

        if (result.Issues.Count > 0 && result.FixedScript != result.OriginalScript)
        {
            var applyButton = new System.Windows.Controls.Button
            {
                Content = "Apply Fix",
                Padding = new System.Windows.Thickness(15, 8, 15, 8),
                Margin = new System.Windows.Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#388E3C")),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new System.Windows.Thickness(0)
            };
            applyButton.Click += (s, e) =>
            {
                // Update all clip scripts with the fixed version
                // For now, we'll just update the generated script text
                ScriptText = result.FixedScript;
                Log("Applied fixed script");
                window.Close();
            };
            buttonPanel.Children.Add(applyButton);
        }

        var closeButton = new System.Windows.Controls.Button
        {
            Content = "Close",
            Padding = new System.Windows.Thickness(15, 8, 15, 8)
        };
        closeButton.Click += (s, e) => window.Close();
        buttonPanel.Children.Add(closeButton);

        System.Windows.Controls.Grid.SetRow(buttonPanel, 2);
        mainGrid.Children.Add(buttonPanel);

        window.Content = mainGrid;
        window.ShowDialog();
    }

    private static System.Windows.Media.SolidColorBrush GetSeverityBrush(string severity)
    {
        return severity switch
        {
            "Error" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E57373")),
            "Warning" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726")),
            _ => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4FC3F7"))
        };
    }

    [RelayCommand]
    private void Save()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "NLAM Timeline (*.nlam)|*.nlam|JSON Files (*.json)|*.json",
            DefaultExt = ".nlam"
        };

        if (dialog.ShowDialog() == true)
        {
            _fileService.SaveTimeline(Timeline, dialog.FileName);
        }
    }

    [RelayCommand]
    private void Load()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "NLAM Timeline (*.nlam)|*.nlam|JSON Files (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            _fileService.LoadTimeline(Timeline, dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task AutoImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "AutoHotkey Script (*.ahk)|*.ahk|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Import AHK Script with AI Analysis"
        };

        if (dialog.ShowDialog() != true)
            return;

        var mainWindow = System.Windows.Application.Current.MainWindow;
        var progressDialog = new Views.AIProgressDialog(mainWindow, "Auto Import with AI");
        
        // Set model info
        progressDialog.SetModel(_aiService.IsModelLoaded ? _aiService.SelectedModel : "Connecting...");
        
        // Disable main window
        mainWindow.IsEnabled = false;

        List<Services.ParsedTrack>? parsedTracks = null;
        string scriptContent = "";

        // Start the task
        var task = Task.Run(async () =>
        {
            try
            {
                progressDialog.AppendLog("═══════════════════════════════════════");
                progressDialog.AppendLog("  NLAM Auto Import with AI");
                progressDialog.AppendLog("═══════════════════════════════════════");
                progressDialog.AppendLog("");
                progressDialog.AppendLog($"📂 Loading: {dialog.FileName}");
                progressDialog.UpdateStatus("Loading file...");

                scriptContent = await System.IO.File.ReadAllTextAsync(dialog.FileName, progressDialog.CancellationToken);
                progressDialog.AppendLog($"📄 File loaded: {scriptContent.Length} characters");
                progressDialog.AppendLog("");

                // Check if AI is connected
                if (!_aiService.IsModelLoaded)
                {
                    progressDialog.AppendLog("🔌 AI not connected. Attempting to connect...");
                    progressDialog.UpdateStatus("Connecting to Ollama...");
                    
                    var connected = await _aiService.ConnectAsync(progressDialog.CancellationToken);
                    if (connected && _aiService.AvailableModels.Count > 0)
                    {
                        _aiService.SelectModel(_aiService.AvailableModels[0]);
                        progressDialog.SetModel(_aiService.SelectedModel);
                        progressDialog.AppendLog($"✅ Connected! Using model: {_aiService.SelectedModel}");
                    }
                    else
                    {
                        progressDialog.AppendLog("⚠️ Could not connect to Ollama. Using simple parser...");
                    }
                    progressDialog.AppendLog("");
                }

                progressDialog.AppendLog("🔍 Analyzing script structure...");
                progressDialog.UpdateStatus("Analyzing script with AI...");
                
                parsedTracks = await _aiService.ParseScriptToTracksAsync(
                    scriptContent, 
                    msg => progressDialog.AppendLog(msg),
                    progressDialog.CancellationToken);

                if (parsedTracks != null && parsedTracks.Count > 0)
                {
                    progressDialog.Complete(true, $"Found {parsedTracks.Count} tracks with {parsedTracks.Sum(t => t.Clips.Count)} clips");
                }
                else
                {
                    progressDialog.Complete(false, "Failed to parse script");
                }
            }
            catch (OperationCanceledException)
            {
                progressDialog.Complete(false);
            }
            catch (Exception ex)
            {
                progressDialog.AppendLog($"❌ Error: {ex.Message}");
                progressDialog.Complete(false, ex.Message);
            }
        });

        // Show dialog (blocks until closed)
        progressDialog.ShowDialog();
        
        // Re-enable main window
        mainWindow.IsEnabled = true;

        // Wait for task to complete
        await task;

        // Process results if not cancelled
        if (!progressDialog.WasCancelled && parsedTracks != null && parsedTracks.Count > 0)
        {
            Log($"Auto-import completed: {parsedTracks.Count} tracks");
            
            // Ask user if they want to replace or append
            var replaceResult = System.Windows.MessageBoxResult.Yes;
            if (Timeline.Tracks.Count > 0 && Timeline.Tracks.Any(t => t.Clips.Count > 0))
            {
                replaceResult = System.Windows.MessageBox.Show(
                    $"Found {parsedTracks.Count} tracks with {parsedTracks.Sum(t => t.Clips.Count)} clips.\n\n" +
                    "Do you want to replace the current timeline?\n\n" +
                    "Yes = Replace current timeline\n" +
                    "No = Add to existing timeline",
                    "Import Options",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Question);

                if (replaceResult == System.Windows.MessageBoxResult.Cancel)
                    return;
            }

            if (replaceResult == System.Windows.MessageBoxResult.Yes)
            {
                Timeline.Tracks.Clear();
            }

            // Color palette for tracks
            var trackColors = new[] { "#007ACC", "#D13438", "#107C10", "#9B4F96", "#FF8C00", "#00CED1", "#6A5ACD", "#32CD32" };
            int colorIndex = Timeline.Tracks.Count;

            // Import tracks and clips
            foreach (var parsedTrack in parsedTracks)
            {
                var track = new TrackViewModel(parsedTrack.Name);
                var trackColor = trackColors[colorIndex % trackColors.Length];
                colorIndex++;

                double currentTime = 0;
                foreach (var parsedClip in parsedTrack.Clips)
                {
                    var clip = new ClipViewModel(
                        parsedClip.Name,
                        currentTime,
                        parsedClip.Duration,
                        trackColor)
                    {
                        CustomScript = parsedClip.Script
                    };
                    track.Clips.Add(clip);
                    currentTime += parsedClip.Duration + 0.1;
                }

                Timeline.Tracks.Add(track);
                Log($"Imported track: {parsedTrack.Name} ({parsedTrack.Clips.Count} clips)");
            }

            GenerateScript();

            System.Windows.MessageBox.Show(
                $"Successfully imported {parsedTracks.Count} tracks with {parsedTracks.Sum(t => t.Clips.Count)} clips.\n\n" +
                $"AI Model: {(_aiService.IsModelLoaded ? _aiService.SelectedModel : "Simple Parser")}",
                "Import Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        double newZoom = Timeline.ZoomLevel * 1.25;
        Timeline.ZoomLevel = Math.Min(newZoom, 1.0); // Max 100%
    }

    [RelayCommand]
    private void ZoomOut()
    {
        double newZoom = Timeline.ZoomLevel * 0.8;
        Timeline.ZoomLevel = Math.Max(newZoom, 0.01); // Min 1%
    }

    [RelayCommand]
    private void FitToView()
    {
        Timeline.FitToView();
    }
}
