using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using MediaControlPlayer.App.Services;

namespace MediaControlPlayer.App;

public partial class MainWindow : Window
{
    private const double TitleBarActivationHeight = 2.0;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private PlayerService? _playerService;
    private DispatcherTimer? _progressTimer;
    private bool _isUserSeeking;
    private bool _hasSyncedVolume;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetPlayerService(PlayerService playerService)
    {
        _playerService = playerService;
        StartProgressTimer();
    }

    private void StartProgressTimer()
    {
        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _progressTimer.Tick += ProgressTimer_Tick;
        _progressTimer.Start();
    }

    /// <summary>
    /// 使用 Win32 API 检测鼠标是否在窗口内（Mouse.GetPosition 在 VideoView 原生区域上不可靠）
    /// </summary>
    private bool IsMouseInsideWindow()
    {
        if (!GetCursorPos(out var screenPos))
        {
            return false;
        }
        var topLeft = PointToScreen(new System.Windows.Point(0, 0));
        var bottomRight = PointToScreen(new System.Windows.Point(ActualWidth, ActualHeight));
        return screenPos.X >= topLeft.X && screenPos.X <= bottomRight.X
            && screenPos.Y >= topLeft.Y && screenPos.Y <= bottomRight.Y;
    }

    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_playerService == null)
        {
            return;
        }

        var length = _playerService.Length;
        if (length > 0)
        {
            if (!_hasSyncedVolume)
            {
                _hasSyncedVolume = true;
                VolumeSlider.Value = _playerService.Volume;
                UpdateMuteButtonContent();
            }

            // 使用 Win32 GetCursorPos 检测鼠标是否移出窗口（Mouse.GetPosition 在 VideoView 上不可靠）
            if (ProgressBarRoot.Visibility == Visibility.Visible && !IsMouseInsideWindow())
            {
                ProgressBarRoot.Visibility = Visibility.Collapsed;
            }

            // 进度条可见性由 MouseEnter/MouseMove 控制，此处只更新数值
            if (!_isUserSeeking && ProgressBarRoot.Visibility == Visibility.Visible)
            {
                var pos = _playerService.Position;
                if (double.IsFinite(pos) && pos >= 0 && pos <= 1)
                {
                    ProgressSlider.Value = pos;
                }
                var time = _playerService.Time;
                CurrentTimeText.Text = FormatTime(time);
                TotalTimeText.Text = FormatTime(length);
            }
        }
        else
        {
            ProgressBarRoot.Visibility = Visibility.Collapsed;
            _hasSyncedVolume = false;
        }
    }

    private static string FormatTime(long ms)
    {
        if (ms < 0)
        {
            return "0:00";
        }
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isUserSeeking = true;
    }

    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isUserSeeking)
        {
            _playerService?.Seek((float)ProgressSlider.Value);
            _isUserSeeking = false;
        }
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUserSeeking && _playerService != null && e.NewValue >= 0 && e.NewValue <= 1)
        {
            _playerService.Seek((float)e.NewValue);
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_playerService == null)
        {
            return;
        }
        var vol = (int)Math.Round(e.NewValue);
        _playerService.Volume = vol;
        UpdateMuteButtonContent();
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        _playerService?.ToggleMute();
        UpdateMuteButtonContent();
    }

    private void UpdateMuteButtonContent()
    {
        MuteButton.Content = _playerService != null && _playerService.IsMuted ? "🔇" : "🔊";
    }

    public void ShowVideoMode()
    {
        ImageOverlay.Visibility = Visibility.Collapsed;
        VideoViewControl.Visibility = Visibility.Visible;
        DeferredUpdatePlayButtonVisibility();
    }

    public void ShowImageMode(string imagePath)
    {
        VideoViewControl.Visibility = Visibility.Collapsed;
        ImageOverlay.Visibility = Visibility.Visible;
        DeferredUpdatePlayButtonVisibility();

        if (System.IO.File.Exists(imagePath))
        {
            ImageOverlay.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath));
        }
    }

    private void TogglePlayPauseFromUi()
    {
        _playerService?.TogglePlayPause();
        DeferredUpdatePlayButtonVisibility();
    }

    private void DeferredUpdatePlayButtonVisibility()
    {
        // LibVLC 的 IsPlaying 会异步更新，延迟 80ms 再刷新，避免读到旧状态导致图标显示反了
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            UpdatePlayButtonVisibility();
        };
        timer.Start();
    }

    public void UpdatePlayButtonVisibility()
    {
        PlayButtonOverlay.Visibility = _playerService != null && !_playerService.IsPlaying
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_playerService?.Length > 0 && ProgressBarRoot.Visibility != Visibility.Visible)
        {
            ProgressBarRoot.Visibility = Visibility.Visible;
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (ProgressBarRoot.Visibility == Visibility.Visible)
        {
            ProgressBarRoot.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(this);

        if (_playerService?.Length > 0 && ProgressBarRoot.Visibility != Visibility.Visible)
        {
            ProgressBarRoot.Visibility = Visibility.Visible;
        }

        if (position.Y <= TitleBarActivationHeight)
        {
            if (TitleBarRoot.Visibility != Visibility.Visible)
            {
                TitleBarRoot.Visibility = Visibility.Visible;
            }
        }
        else if (TitleBarRoot.Visibility == Visibility.Visible && !TitleBarRoot.IsMouseOver)
        {
            TitleBarRoot.Visibility = Visibility.Collapsed;
        }
    }

    private void TitleBarRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _playerService?.Stop();
        PlayButtonOverlay.Visibility = Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void VideoOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        // 覆盖层在 VideoView 之上，可收到鼠标事件（解决 airspace 问题）
        var position = e.GetPosition(VideoOverlay);
        if (_playerService?.Length > 0 && ProgressBarRoot.Visibility != Visibility.Visible)
        {
            ProgressBarRoot.Visibility = Visibility.Visible;
        }
        if (position.Y <= TitleBarActivationHeight + 6)
        {
            if (TitleBarRoot.Visibility != Visibility.Visible)
            {
                TitleBarRoot.Visibility = Visibility.Visible;
            }
        }
        else if (TitleBarRoot.Visibility == Visibility.Visible && !TitleBarRoot.IsMouseOver)
        {
            TitleBarRoot.Visibility = Visibility.Collapsed;
        }
    }

    private void VideoOverlay_MouseLeave(object sender, MouseEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsMouseInsideWindow())
            {
                ProgressBarRoot.Visibility = Visibility.Collapsed;
            }
        }, DispatcherPriority.Background);
    }

    private void VideoOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        TogglePlayPauseFromUi();
    }

    private void PlayButtonOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        TogglePlayPauseFromUi();
    }

    private void ImageOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(ImageOverlay);
        if (_playerService?.Length > 0 && ProgressBarRoot.Visibility != Visibility.Visible)
        {
            ProgressBarRoot.Visibility = Visibility.Visible;
        }
        if (position.Y <= TitleBarActivationHeight + 6)
        {
            if (TitleBarRoot.Visibility != Visibility.Visible)
            {
                TitleBarRoot.Visibility = Visibility.Visible;
            }
        }
        else if (TitleBarRoot.Visibility == Visibility.Visible && !TitleBarRoot.IsMouseOver)
        {
            TitleBarRoot.Visibility = Visibility.Collapsed;
        }
    }

    private void ImageOverlay_MouseLeave(object sender, MouseEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsMouseInsideWindow())
            {
                ProgressBarRoot.Visibility = Visibility.Collapsed;
            }
        }, DispatcherPriority.Background);
    }

    private void ImageOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        TogglePlayPauseFromUi();
    }
}