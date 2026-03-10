using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

using MediaControlPlayer.App.Data;
using MediaControlPlayer.App.Services;
using Microsoft.EntityFrameworkCore;

namespace MediaControlPlayer.App;

public sealed class AppSettings
{
    public WebApiSettings WebApi { get; set; } = new();
    public MediaSettings Media { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
}

public sealed class WebApiSettings
{
    public string Url { get; set; } = "http://0.0.0.0:5000";
}

public sealed class MediaSettings
{
    public string RootDirectory { get; set; } = string.Empty;
}

public sealed class DatabaseSettings
{
    public string DbPath { get; set; } = string.Empty;
}

public partial class App : Application
{
    private WebApiHost? _webApiHost;
    private PlayerService? _playerService;
    private PowerService? _powerService;
    private AppSettings? _settings;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = LoadSettings();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        _playerService = new PlayerService();
        mainWindow.SetPlayerService(_playerService);
        _playerService.AttachMainWindow(mainWindow);

        _powerService = new PowerService();

        var databasePath = EnsureDatabasePath(_settings);
        var mediaRoot = EnsureMediaRoot(_settings);
        _webApiHost = new WebApiHost(_playerService, _powerService, databasePath, mediaRoot);

        mainWindow.Show();

        await _webApiHost.StartAsync(_settings.WebApi.Url);

        StartPlaylistIfAvailable(_playerService, databasePath);
    }

    private static void StartPlaylistIfAvailable(PlayerService player, string databasePath)
    {
        try
        {
            var options = new DbContextOptionsBuilder<MediaDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            using var db = new MediaDbContext(options);
            var list = db.MediaContents.AsNoTracking()
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.PlayOrder)
                .ThenBy(x => x.Id)
                .ToList();
            if (list.Count > 0)
            {
                player.PlayPlaylist(list);
            }
        }
        catch
        {
            // 数据库未就绪或为空，忽略
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_webApiHost != null)
        {
            await _webApiHost.StopAsync();
        }

        base.OnExit(e);
    }

    private static AppSettings LoadSettings()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var configPath = Path.Combine(baseDirectory, "Config", "AppSettings.json");

        if (!File.Exists(configPath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(configPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json);
        return settings ?? new AppSettings();
    }

    private static string EnsureDatabasePath(AppSettings settings)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        var configuredPath = settings.Database?.DbPath;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath!;
        }

        var dataDirectory = Path.Combine(baseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);

        var dbPath = Path.Combine(dataDirectory, "media.db");
        return dbPath;
    }

    private static string EnsureMediaRoot(AppSettings settings)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        var configuredRoot = settings.Media?.RootDirectory;
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            Directory.CreateDirectory(configuredRoot!);
            return configuredRoot!;
        }

        var mediaDirectory = Path.Combine(baseDirectory, "Media");
        Directory.CreateDirectory(mediaDirectory);
        return mediaDirectory;
    }
}

