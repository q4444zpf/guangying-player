using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using LibVLCSharp.Shared;
using MediaControlPlayer.App.Models;

namespace MediaControlPlayer.App.Services;

public sealed class PlayerService
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;

    private MainWindow? _mainWindow;

    private bool _hasEnded;
    private List<MediaContent>? _playlist;
    private int _playlistIndex;
    private bool _isLooping;

    public bool HasEnded => _hasEnded;

    public bool IsLooping
    {
        get => _isLooping;
        set => _isLooping = value;
    }

    public PlayerService()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var architectureFolder = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        var vlcDirectory = Path.Combine(baseDirectory, "libvlc", architectureFolder);

        Core.Initialize(vlcDirectory);
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.EndReached += MediaPlayer_EndReached;
    }

    private void MediaPlayer_EndReached(object? sender, EventArgs e)
    {
        if (_playlist != null)
        {
            if (_playlistIndex + 1 < _playlist.Count)
            {
                _playlistIndex++;
                _mainWindow?.Dispatcher.BeginInvoke(() => PlayItemAtIndex(_playlistIndex));
                return;
            }
            if (_isLooping && _playlist.Count > 0)
            {
                _playlistIndex = 0;
                _mainWindow?.Dispatcher.BeginInvoke(() => PlayItemAtIndex(0));
                return;
            }
        }

        _playlist = null;
        _hasEnded = true;
        _mainWindow?.Dispatcher.BeginInvoke(() =>
        {
            _mainWindow.UpdatePlayButtonVisibility();
        });
    }

    public MediaPlayer MediaPlayer => _mediaPlayer;

    public bool IsPlaying => _mediaPlayer.IsPlaying;

    public long Time => _mediaPlayer.Time;

    public long Length => _mediaPlayer.Length;

    public float Position => _mediaPlayer.Position;

    public int Volume
    {
        get => _mediaPlayer.Volume;
        set
        {
            var v = Math.Clamp(value, 0, 100);
            _mediaPlayer.Volume = v;
            if (v > 0)
            {
                _isMuted = false;
                _volumeBeforeMute = v;
            }
        }
    }

    private int _volumeBeforeMute = 100;
    private bool _isMuted;

    public bool IsMuted => _isMuted;

    public void ToggleMute()
    {
        if (_isMuted)
        {
            _mediaPlayer.Volume = _volumeBeforeMute;
            _isMuted = false;
        }
        else
        {
            _volumeBeforeMute = _mediaPlayer.Volume > 0 ? _mediaPlayer.Volume : 100;
            _mediaPlayer.Volume = 0;
            _isMuted = true;
        }
    }

    public void Seek(float position)
    {
        if (position is < 0 or > 1)
        {
            return;
        }

        var wasPaused = !_mediaPlayer.IsPlaying;
        if (wasPaused)
        {
            SeekWhenPaused(() => _mediaPlayer.Position = position);
        }
        else
        {
            _mediaPlayer.Position = position;
        }
    }

    public void SeekToTime(long timeMs)
    {
        var wasPaused = !_mediaPlayer.IsPlaying;
        if (wasPaused)
        {
            SeekWhenPaused(() => _mediaPlayer.Time = timeMs);
        }
        else
        {
            _mediaPlayer.Time = timeMs;
        }
    }

    /// <summary>
    /// 暂停时 seek：Play 与 Seek/Pause 需异步间隔，否则后续无法恢复播放
    /// </summary>
    private void SeekWhenPaused(Action doSeek)
    {
        // 若当前已为 0（上次 seek 刚静音），用 100 避免多次快速 seek 后误恢复为 0
        var vol = _mediaPlayer.Volume == 0 ? 100 : _mediaPlayer.Volume;
        _mediaPlayer.Volume = 0;
        _mediaPlayer.Play();

        var dispatcher = _mainWindow?.Dispatcher ?? System.Windows.Application.Current.Dispatcher;
        var timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Normal, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            doSeek();
            _mediaPlayer.Pause();
            _mediaPlayer.Volume = _isMuted ? 0 : vol;
        };
        timer.Start();
    }

    public void AttachMainWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _mainWindow.Dispatcher.Invoke(() =>
        {
            if (_mainWindow.VideoViewControl != null)
            {
                _mainWindow.VideoViewControl.MediaPlayer = _mediaPlayer;
            }
        });
    }

    /// <summary>按播放列表顺序连续播放（仅播放 IsEnabled 且已排序的项）</summary>
    public void PlayPlaylist(IReadOnlyList<MediaContent> items)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        _playlist = new List<MediaContent>(items);
        _playlistIndex = 0;
        PlayItemAtIndex(0);
    }

    private void PlayItemAtIndex(int index)
    {
        if (_playlist == null || index < 0 || index >= _playlist.Count)
        {
            return;
        }

        var item = _playlist[index];
        if (item.Type == MediaControlPlayer.App.Models.MediaType.Video && !string.IsNullOrWhiteSpace(item.VideoPath))
        {
            _hasEnded = false;
            using var media = new Media(_libVlc, new Uri(item.VideoPath));
            _mediaPlayer.Play(media);
            _mainWindow?.Dispatcher.BeginInvoke(() => _mainWindow.ShowVideoMode());
        }
        else if (item.Type == MediaControlPlayer.App.Models.MediaType.ImageWithAudio)
        {
            PlayImageWithAudio(item.ImagePath ?? string.Empty, item.AudioPath ?? string.Empty);
        }
    }

    public void PlayVideo(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _playlist = null;
        _hasEnded = false;
        using var media = new Media(_libVlc, new Uri(path));
        _mediaPlayer.Play(media);

        if (_mainWindow != null)
        {
            _mainWindow.Dispatcher.BeginInvoke(() =>
            {
                _mainWindow.ShowVideoMode();
            });
        }
    }

    public void PlayImageWithAudio(string imagePath, string audioPath)
    {
        if (_mainWindow == null)
        {
            return;
        }

        _playlist = null;
        _mainWindow.Dispatcher.BeginInvoke(() =>
        {
            _mainWindow.ShowImageMode(imagePath);
        });

        _hasEnded = false;
        if (!string.IsNullOrWhiteSpace(audioPath))
        {
            using var media = new Media(_libVlc, new Uri(audioPath));
            _mediaPlayer.Play(media);
        }
    }

    public void Pause() => _mediaPlayer.Pause();

    public void Resume() => _mediaPlayer.Play();

    public void TogglePlayPause()
    {
        if (_mediaPlayer.IsPlaying)
        {
            Pause();
        }
        else if (_hasEnded)
        {
            Restart();
            _hasEnded = false;
        }
        else
        {
            Resume();
        }
    }

    public void Stop()
    {
        _playlist = null;
        _mediaPlayer.Stop();
    }

    public void Restart()
    {
        var media = _mediaPlayer.Media;
        if (media == null)
        {
            return;
        }

        _hasEnded = false;
        _mediaPlayer.Stop();
        _mediaPlayer.Play(media);
    }
}

