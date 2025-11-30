using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace NLAM.App.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ViewModels.MainViewModel mainViewModel)
        {
            mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
            UpdateScriptHighlighting(mainViewModel.ScriptText);
        }
    }

    private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.ScriptText) && 
            sender is ViewModels.MainViewModel vm)
        {
            UpdateScriptHighlighting(vm.ScriptText);
        }
    }

    private void UpdateScriptHighlighting(string scriptText)
    {
        if (ScriptRichTextBox == null) return;

        var flowDoc = new FlowDocument();
        flowDoc.PageWidth = 2000; // Prevent word wrap
        var paragraph = new Paragraph();
        paragraph.FontFamily = new FontFamily("Consolas");
        paragraph.FontSize = 12;

        var lines = scriptText.Split('\n');
        foreach (var line in lines)
        {
            HighlightLine(paragraph, line);
            paragraph.Inlines.Add(new LineBreak());
        }

        flowDoc.Blocks.Add(paragraph);
        ScriptRichTextBox.Document = flowDoc;
    }

    private static void HighlightLine(Paragraph paragraph, string line)
    {
        // AHK Keywords
        var keywords = new[] { "Send", "Click", "Sleep", "MouseMove", "MouseClick", "MsgBox", 
            "Run", "WinActivate", "WinWait", "WinClose", "IfWinExist", "IfWinActive",
            "Loop", "Return", "Exit", "Reload", "Suspend", "Pause", "SetTimer",
            "Hotkey", "Hotstring", "FileAppend", "FileRead", "FileDelete", "InputBox" };
        
        // Check if it's a comment
        var trimmedLine = line.TrimStart();
        if (trimmedLine.StartsWith(";"))
        {
            paragraph.Inlines.Add(new Run(line) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6A9955")) }); // Green for comments
            return;
        }

        // Check if it's a label/header (starts with ; but has special meaning like "; Track:")
        if (trimmedLine.StartsWith("; Track:") || trimmedLine.StartsWith("; Action:") || 
            trimmedLine.StartsWith("; AutoHotkey") || trimmedLine.StartsWith("; Timeline"))
        {
            paragraph.Inlines.Add(new Run(line) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#569CD6")) }); // Blue for headers
            return;
        }

        // Process the line for keywords
        var remaining = line;
        var currentIndex = 0;

        while (currentIndex < line.Length)
        {
            var foundKeyword = false;
            foreach (var keyword in keywords)
            {
                if (currentIndex + keyword.Length <= line.Length)
                {
                    var substr = line.Substring(currentIndex, keyword.Length);
                    if (substr.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it's a whole word
                        var isWordStart = currentIndex == 0 || !char.IsLetterOrDigit(line[currentIndex - 1]);
                        var isWordEnd = currentIndex + keyword.Length >= line.Length || !char.IsLetterOrDigit(line[currentIndex + keyword.Length]);

                        if (isWordStart && isWordEnd)
                        {
                            // Add keyword with color
                            paragraph.Inlines.Add(new Run(substr) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C586C0")) }); // Purple for keywords
                            currentIndex += keyword.Length;
                            foundKeyword = true;
                            break;
                        }
                    }
                }
            }

            if (!foundKeyword)
            {
                // Check for numbers
                if (char.IsDigit(line[currentIndex]))
                {
                    var numStart = currentIndex;
                    while (currentIndex < line.Length && (char.IsDigit(line[currentIndex]) || line[currentIndex] == '.'))
                    {
                        currentIndex++;
                    }
                    paragraph.Inlines.Add(new Run(line.Substring(numStart, currentIndex - numStart)) 
                        { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B5CEA8")) }); // Light green for numbers
                }
                // Check for strings in quotes
                else if (line[currentIndex] == '"')
                {
                    var strStart = currentIndex;
                    currentIndex++;
                    while (currentIndex < line.Length && line[currentIndex] != '"')
                    {
                        currentIndex++;
                    }
                    if (currentIndex < line.Length) currentIndex++; // Include closing quote
                    paragraph.Inlines.Add(new Run(line.Substring(strStart, currentIndex - strStart)) 
                        { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CE9178")) }); // Orange for strings
                }
                // Check for braces
                else if (line[currentIndex] == '{' || line[currentIndex] == '}')
                {
                    paragraph.Inlines.Add(new Run(line[currentIndex].ToString()) 
                        { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")) }); // Gold for braces
                    currentIndex++;
                }
                else
                {
                    // Regular text
                    paragraph.Inlines.Add(new Run(line[currentIndex].ToString()) 
                        { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")) }); // Default text color
                    currentIndex++;
                }
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        // Check for unsaved changes could be added here
        Application.Current.Shutdown();
    }

    private void TogglePropertiesPanel_Click(object sender, RoutedEventArgs e)
    {
        if (PropertiesPanelMenuItem.IsChecked)
        {
            PropertiesPanel.Visibility = Visibility.Visible;
        }
        else
        {
            PropertiesPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleScriptPreview_Click(object sender, RoutedEventArgs e)
    {
        if (ScriptPreviewMenuItem.IsChecked)
        {
            ScriptPreviewPanel.Visibility = Visibility.Visible;
            ScriptSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            ScriptPreviewPanel.Visibility = Visibility.Collapsed;
            ScriptSplitter.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleDebugLog_Click(object sender, RoutedEventArgs e)
    {
        if (DebugLogMenuItem.IsChecked)
        {
            DebugLogPanel.Visibility = Visibility.Visible;
            DebugLogSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            DebugLogPanel.Visibility = Visibility.Collapsed;
            DebugLogSplitter.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearDebugLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel mainViewModel)
        {
            mainViewModel.DebugLogs.Clear();
            mainViewModel.Log("Debug log cleared");
        }
    }

    private void SetTimelineLength_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel mainViewModel)
        {
            var timeline = mainViewModel.Timeline;
            
            var lengthWindow = new Window
            {
                Title = "Set Timeline Length",
                Width = 350,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")),
                ResizeMode = ResizeMode.NoResize
            };

            var mainStack = new StackPanel { Margin = new Thickness(15) };

            // Title
            var titleText = new TextBlock
            {
                Text = "Set the total timeline length:",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainStack.Children.Add(titleText);

            // Current length
            var currentText = new TextBlock
            {
                Text = $"Current: {ViewModels.TimelineViewModel.FormatTimeSpan(timeline.TotalLength)}",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainStack.Children.Add(currentText);

            // Time input grid
            var inputGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inputGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Calculate current values
            var ts = TimeSpan.FromSeconds(timeline.TotalLength);
            
            // Labels
            var daysLabel = new TextBlock { Text = "Days", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2) };
            var hoursLabel = new TextBlock { Text = "Hours", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2) };
            var minsLabel = new TextBlock { Text = "Minutes", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2) };
            var secsLabel = new TextBlock { Text = "Seconds", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2) };
            Grid.SetColumn(daysLabel, 0);
            Grid.SetColumn(hoursLabel, 1);
            Grid.SetColumn(minsLabel, 2);
            Grid.SetColumn(secsLabel, 3);
            inputGrid.Children.Add(daysLabel);
            inputGrid.Children.Add(hoursLabel);
            inputGrid.Children.Add(minsLabel);
            inputGrid.Children.Add(secsLabel);

            // Input boxes
            var textBoxStyle = new Style(typeof(TextBox));
            
            var daysBox = new TextBox
            {
                Text = ((int)ts.TotalDays).ToString(),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"))
            };
            var hoursBox = new TextBox
            {
                Text = (ts.Hours).ToString(),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"))
            };
            var minsBox = new TextBox
            {
                Text = (ts.Minutes).ToString(),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"))
            };
            var secsBox = new TextBox
            {
                Text = (ts.Seconds).ToString(),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"))
            };
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
            var presetsLabel = new TextBlock
            {
                Text = "Quick Presets:",
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 5)
            };
            mainStack.Children.Add(presetsLabel);

            var presetsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 15) };
            var presets = new[] { ("1 min", 60.0), ("5 min", 300.0), ("30 min", 1800.0), ("1 hour", 3600.0), ("1 day", 86400.0), ("1 week", 604800.0) };
            foreach (var (label, seconds) in presets)
            {
                var btn = new Button
                {
                    Content = label,
                    Margin = new Thickness(0, 0, 5, 5),
                    Padding = new Thickness(8, 3, 8, 3),
                    Tag = seconds
                };
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
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var applyButton = new Button
            {
                Content = "Apply",
                Width = 80,
                Height = 28,
                Margin = new Thickness(0, 0, 10, 0)
            };
            applyButton.Click += (s, args) =>
            {
                try
                {
                    int days = int.TryParse(daysBox.Text, out var d) ? d : 0;
                    int hours = int.TryParse(hoursBox.Text, out var h) ? h : 0;
                    int mins = int.TryParse(minsBox.Text, out var m) ? m : 0;
                    int secs = int.TryParse(secsBox.Text, out var sec) ? sec : 0;

                    double totalSeconds = days * 86400 + hours * 3600 + mins * 60 + secs;

                    if (totalSeconds < ViewModels.TimelineViewModel.MinLength)
                    {
                        MessageBox.Show("Minimum timeline length is 1 second.", "Invalid Length", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (totalSeconds > ViewModels.TimelineViewModel.MaxLength)
                    {
                        MessageBox.Show("Maximum timeline length is 1 week (7 days).", "Invalid Length", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    timeline.TotalLength = totalSeconds;
                    lengthWindow.Close();
                }
                catch
                {
                    MessageBox.Show("Please enter valid numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(applyButton);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 28
            };
            cancelButton.Click += (s, args) => lengthWindow.Close();
            buttonPanel.Children.Add(cancelButton);

            mainStack.Children.Add(buttonPanel);

            lengthWindow.Content = mainStack;
            lengthWindow.ShowDialog();
        }
    }

    private void ColorPicker_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel mainViewModel && 
            mainViewModel.Properties.SelectedClip != null)
        {
            var clip = mainViewModel.Properties.SelectedClip;

            var colorWindow = new Window
            {
                Title = $"Pick Color - {clip.Name}",
                Width = 350,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")),
                ResizeMode = ResizeMode.NoResize
            };

            var mainStack = new StackPanel { Margin = new Thickness(15) };

            // Title
            var titleText = new TextBlock
            {
                Text = "Select a color for the clip:",
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainStack.Children.Add(titleText);

            // Predefined colors grid
            var colorsGrid = new UniformGrid { Columns = 6, Margin = new Thickness(0, 0, 0, 15) };
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
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
                    BorderThickness = new Thickness(1),
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
            var customPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            var hexLabel = new TextBlock
            {
                Text = "Custom Hex:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            customPanel.Children.Add(hexLabel);

            var hexTextBox = new TextBox
            {
                Text = clip.Color,
                Width = 100,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
                Padding = new Thickness(5, 3, 5, 3)
            };
            customPanel.Children.Add(hexTextBox);

            var previewRect = new Border
            {
                Width = 40,
                Height = 25,
                CornerRadius = new CornerRadius(3),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1)
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
                Margin = new Thickness(0, 15, 0, 0),
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left
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
                    MessageBox.Show("Invalid color format. Please use hex format like #RRGGBB", "Invalid Color", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            mainStack.Children.Add(applyButton);

            colorWindow.Content = mainStack;
            colorWindow.ShowDialog();
        }
    }

    private void EditScript_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel mainViewModel && 
            mainViewModel.Properties.SelectedClip != null)
        {
            var clip = mainViewModel.Properties.SelectedClip;
            TimelineView.ShowScriptEditor(clip, () => mainViewModel.GenerateScript());
        }
    }

    private void ToolBar_Loaded(object sender, RoutedEventArgs e)
    {
        // Remove the overflow button from ToolBar to fix layout issues
        if (sender is ToolBar toolBar)
        {
            var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
            if (overflowGrid != null)
            {
                overflowGrid.Visibility = Visibility.Collapsed;
            }

            var mainPanelBorder = toolBar.Template.FindName("MainPanelBorder", toolBar) as FrameworkElement;
            if (mainPanelBorder != null)
            {
                mainPanelBorder.Margin = new Thickness(0);
            }
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel mainViewModel)
        {
            mainViewModel.PlayCommand.Execute(null);
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel mainViewModel)
        {
            mainViewModel.PauseCommand.Execute(null);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel mainViewModel)
        {
            mainViewModel.StopCommand.Execute(null);
        }
    }

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        var aiService = App.Current.Services.GetRequiredService<Services.AIScriptService>();
        var preferencesWindow = new PreferencesWindow(aiService)
        {
            Owner = this
        };
        preferencesWindow.ShowDialog();
    }
}