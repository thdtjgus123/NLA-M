using System.Text;
using System.Windows;
using System.Windows.Input;

namespace NLAM.App.Views;

public partial class AIProgressDialog : Window
{
    private readonly CancellationTokenSource _cts;
    private readonly StringBuilder _logBuilder = new();
    private bool _isCompleted;
    private bool _canClose;

    public CancellationToken CancellationToken => _cts.Token;
    public bool WasCancelled => _cts.IsCancellationRequested;

    public AIProgressDialog(Window owner, string title = "AI Task Running...")
    {
        InitializeComponent();
        Owner = owner;
        TitleText.Text = title;
        _cts = new CancellationTokenSource();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCompleted)
        {
            _canClose = true;
            DialogResult = !WasCancelled;
            Close();
        }
        else
        {
            _cts.Cancel();
            CancelButton.Content = "Cancelling...";
            CancelButton.IsEnabled = false;
            StatusText.Text = "Cancelling operation...";
            AppendLog("⚠️ Cancellation requested...");
        }
    }

    public void SetModel(string modelName)
    {
        Dispatcher.Invoke(() =>
        {
            ModelText.Text = $"Model: {modelName}";
        });
    }

    public void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logLine = $"[{timestamp}] {message}\n";
            _logBuilder.Append(logLine);
            LogTextBlock.Text = _logBuilder.ToString();
            LogScrollViewer.ScrollToEnd();
        });
    }

    public void UpdateStatus(string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
        });
    }

    public void SetProgress(double progress)
    {
        Dispatcher.Invoke(() =>
        {
            if (progress < 0)
            {
                ProgressBar.IsIndeterminate = true;
            }
            else
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = progress;
            }
        });
    }

    public void Complete(bool success, string? message = null)
    {
        Dispatcher.Invoke(() =>
        {
            _isCompleted = true;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 100;

            if (success)
            {
                StatusText.Text = message ?? "✅ Completed successfully!";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#81C784"));
                TitleText.Text = "✅ Task Completed";
                AppendLog("═══════════════════════════════════════");
                AppendLog("✅ Task completed successfully!");
            }
            else
            {
                StatusText.Text = message ?? (WasCancelled ? "⚠️ Operation cancelled" : "❌ Task failed");
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(WasCancelled ? "#FFA726" : "#E57373"));
                TitleText.Text = WasCancelled ? "⚠️ Cancelled" : "❌ Failed";
                AppendLog("═══════════════════════════════════════");
                AppendLog(WasCancelled ? "⚠️ Operation was cancelled by user" : "❌ Task failed");
            }

            CancelButton.Content = "Close";
            CancelButton.IsEnabled = true;
            CancelButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3E3E42"));
        });
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Prevent closing while task is running unless cancelled
        if (!_isCompleted && !_canClose)
        {
            e.Cancel = true;
            // Request cancellation instead
            if (!_cts.IsCancellationRequested)
            {
                CancelButton_Click(this, new RoutedEventArgs());
            }
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Dispose();
        base.OnClosed(e);
    }
}
