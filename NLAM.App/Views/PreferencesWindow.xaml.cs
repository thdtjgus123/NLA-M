using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NLAM.App.Services;

namespace NLAM.App.Views;

public partial class PreferencesWindow : Window
{
    private readonly AIScriptService _aiService;

    public PreferencesWindow(AIScriptService aiService)
    {
        InitializeComponent();
        _aiService = aiService;

        // Initialize with current values
        OllamaUrlTextBox.Text = _aiService.OllamaUrl;
        
        if (_aiService.IsModelLoaded)
        {
            ConnectionStatusText.Text = $"Connected - {_aiService.SelectedModel}";
            ConnectionStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#81C784"));
            
            // Populate models
            foreach (var model in _aiService.AvailableModels)
            {
                ModelsListBox.Items.Add(model);
                DefaultModelComboBox.Items.Add(model);
            }
            
            if (!string.IsNullOrEmpty(_aiService.SelectedModel))
            {
                DefaultModelComboBox.SelectedItem = _aiService.SelectedModel;
            }
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        TestConnectionButton.IsEnabled = false;
        TestConnectionButton.Content = "Testing...";
        ConnectionStatusText.Text = "Connecting...";
        ConnectionStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA500"));

        try
        {
            _aiService.OllamaUrl = OllamaUrlTextBox.Text;
            var success = await _aiService.ConnectAsync();

            if (success)
            {
                ConnectionStatusText.Text = $"Connected! {_aiService.AvailableModels.Count} models available";
                ConnectionStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#81C784"));

                // Update models list
                ModelsListBox.Items.Clear();
                DefaultModelComboBox.Items.Clear();
                
                foreach (var model in _aiService.AvailableModels)
                {
                    ModelsListBox.Items.Add(model);
                    DefaultModelComboBox.Items.Add(model);
                }

                if (_aiService.AvailableModels.Count > 0)
                {
                    DefaultModelComboBox.SelectedIndex = 0;
                }
            }
            else
            {
                ConnectionStatusText.Text = "Connection failed. Is Ollama running?";
                ConnectionStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373"));
            }
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = $"Error: {ex.Message}";
            ConnectionStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E57373"));
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
            TestConnectionButton.Content = "Test Connection";
        }
    }

    private void ThemeColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(textBox.Text);
                var brush = new SolidColorBrush(color);

                if (textBox == WindowBgTextBox && WindowBgPreview != null)
                    WindowBgPreview.Background = brush;
                else if (textBox == PanelBgTextBox && PanelBgPreview != null)
                    PanelBgPreview.Background = brush;
                else if (textBox == BorderTextBox && BorderPreview != null)
                    BorderPreview.Background = brush;
                else if (textBox == AccentTextBox && AccentPreview != null)
                    AccentPreview.Background = brush;
                else if (textBox == SelectionTextBox && SelectionPreview != null)
                    SelectionPreview.Background = brush;
            }
            catch
            {
                // Invalid color format
            }
        }
    }

    private void ResetTheme_Click(object sender, RoutedEventArgs e)
    {
        WindowBgTextBox.Text = "#1E1E1E";
        PanelBgTextBox.Text = "#252526";
        BorderTextBox.Text = "#3F3F46";
        AccentTextBox.Text = "#007ACC";
        SelectionTextBox.Text = "#FFFFFF";
    }

    private void ApplyTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Update application resources
            var app = Application.Current;
            
            app.Resources["WindowBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(WindowBgTextBox.Text));
            app.Resources["PanelBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(PanelBgTextBox.Text));
            app.Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BorderTextBox.Text));
            app.Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AccentTextBox.Text));

            MessageBox.Show("Theme applied! Some changes may require restart.", "Theme Applied", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error applying theme: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        // Save settings
        _aiService.OllamaUrl = OllamaUrlTextBox.Text;
        
        if (DefaultModelComboBox.SelectedItem is string selectedModel)
        {
            _aiService.SelectModel(selectedModel);
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
