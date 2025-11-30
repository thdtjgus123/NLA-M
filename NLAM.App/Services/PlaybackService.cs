using System.Diagnostics;
using System.Windows.Threading;

namespace NLAM.App.Services;

public class PlaybackService
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch;
    private double _playbackTime;
    private bool _isPlaying;

    public event Action<double>? TimeUpdated;
    public event Action? PlaybackStarted;
    public event Action? PlaybackStopped;

    public bool IsPlaying => _isPlaying;
    public double CurrentTime => _playbackTime;

    public PlaybackService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _timer.Tick += OnTimerTick;
        _stopwatch = new Stopwatch();
    }

    public void Play()
    {
        if (_isPlaying) return;

        _isPlaying = true;
        _stopwatch.Restart();
        _timer.Start();
        PlaybackStarted?.Invoke();
    }

    public void Pause()
    {
        if (!_isPlaying) return;

        _isPlaying = false;
        _stopwatch.Stop();
        _timer.Stop();
        PlaybackStopped?.Invoke();
    }

    public void Stop()
    {
        Pause();
        _playbackTime = 0;
        TimeUpdated?.Invoke(_playbackTime);
    }

    public void Seek(double time)
    {
        _playbackTime = time;
        if (_isPlaying)
        {
            _stopwatch.Restart();
        }
        TimeUpdated?.Invoke(_playbackTime);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPlaying)
        {
            _playbackTime += _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();
            TimeUpdated?.Invoke(_playbackTime);
        }
    }
}
