using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaControlPlayer.App.Data;
using MediaControlPlayer.App.Models;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;

namespace MediaControlPlayer.App.Services;

public sealed class WebApiHost
{
    private readonly PlayerService _playerService;
    private readonly PowerService _powerService;
    private readonly string _databasePath;
    private readonly string _mediaRoot;
    private IHost? _host;

    public WebApiHost(PlayerService playerService, PowerService powerService, string databasePath, string mediaRoot)
    {
        _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
        _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        _mediaRoot = mediaRoot ?? throw new ArgumentNullException(nameof(mediaRoot));
    }

    public Task StartAsync(string url, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton(_playerService);
        builder.Services.AddSingleton(_powerService);
        builder.Services.AddDbContext<MediaDbContext>(options =>
        {
            options.UseSqlite($"Data Source={_databasePath}");
        });

        // 让枚举在 JSON 中可以使用字符串表示，例如 "video" / "imageWithAudio"
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        var app = builder.Build();

        // 静态文件：测试 Web 界面（wwwroot/index.html）
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // 初始化数据库
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            db.Database.EnsureCreated();
            db.MigrateAddPlaylistColumns();
        }

        // 播放控制相关接口
        app.MapPost("/api/player/play", async (HttpContext context, PlayerService player) =>
        {
            var request = await context.Request.ReadFromJsonAsync<PlayRequest>(cancellationToken: context.RequestAborted);
            if (request == null)
            {
                return Results.BadRequest("Invalid request");
            }

            if (request.Type == MediaType.Video)
            {
                if (!string.IsNullOrWhiteSpace(request.VideoPath))
                {
                    player.PlayVideo(request.VideoPath);
                }
            }
            else if (request.Type == MediaType.ImageWithAudio)
            {
                player.PlayImageWithAudio(request.ImagePath ?? string.Empty, request.AudioPath ?? string.Empty);
            }

            return Results.Ok();
        });

        app.MapPost("/api/player/play/{id:int}", async (int id, MediaDbContext db, PlayerService player) =>
        {
            var content = await db.MediaContents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (content == null)
            {
                return Results.NotFound();
            }

            if (content.Type == MediaType.Video)
            {
                if (!string.IsNullOrWhiteSpace(content.VideoPath))
                {
                    player.PlayVideo(content.VideoPath);
                }
            }
            else if (content.Type == MediaType.ImageWithAudio)
            {
                player.PlayImageWithAudio(content.ImagePath ?? string.Empty, content.AudioPath ?? string.Empty);
            }

            return Results.Ok();
        });

        app.MapPost("/api/player/pause", (PlayerService player) =>
        {
            player.Pause();
            return Results.Ok();
        });

        app.MapPost("/api/player/resume", (PlayerService player) =>
        {
            player.Resume();
            return Results.Ok();
        });

        app.MapPost("/api/player/restart", (PlayerService player) =>
        {
            player.Restart();
            return Results.Ok();
        });

        app.MapPost("/api/player/stop", (PlayerService player) =>
        {
            player.Stop();
            return Results.Ok();
        });

        app.MapPost("/api/player/play/playlist", async (MediaDbContext db, PlayerService player) =>
        {
            var list = await db.MediaContents.AsNoTracking()
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.PlayOrder)
                .ThenBy(x => x.Id)
                .ToListAsync();
            if (list.Count == 0)
            {
                return Results.BadRequest("播放列表为空或没有启用的内容");
            }
            player.PlayPlaylist(list);
            return Results.Ok();
        });

        app.MapGet("/api/player/loop", (PlayerService player) =>
            Results.Ok(new { isLooping = player.IsLooping }));

        app.MapPost("/api/player/loop", async (HttpContext context, PlayerService player) =>
        {
            var body = await context.Request.ReadFromJsonAsync<LoopRequest>(context.RequestAborted);
            if (body != null)
            {
                player.IsLooping = body.IsLooping;
                var settings = DataSettingsHelper.Load(_databasePath);
                settings.IsLooping = body.IsLooping;
                await DataSettingsHelper.SaveAsync(_databasePath, settings, context.RequestAborted);
            }
            return Results.Ok(new { isLooping = player.IsLooping });
        });

        // 电源控制相关接口
        app.MapPost("/api/system/shutdown", (PowerService power) =>
        {
            power.Shutdown();
            return Results.Ok();
        });

        app.MapPost("/api/system/reboot", (PowerService power) =>
        {
            power.Reboot();
            return Results.Ok();
        });

        app.MapPost("/api/system/sleep", (PowerService power) =>
        {
            power.Sleep();
            return Results.Ok();
        });

        app.MapPost("/api/system/display-off", (PowerService power) =>
        {
            power.DisplayOff();
            return Results.Ok();
        });

        app.MapPost("/api/system/display-on", (PowerService power) =>
        {
            power.DisplayOn();
            return Results.Ok();
        });

        // 播放内容管理接口（基于 SQLite）
        app.MapGet("/api/contents", async (MediaDbContext db) =>
        {
            var list = await db.MediaContents.AsNoTracking()
                .OrderBy(x => x.PlayOrder)
                .ThenBy(x => x.Id)
                .ToListAsync();
            return Results.Ok(list);
        });

        app.MapGet("/api/contents/{id:int}", async (int id, MediaDbContext db) =>
        {
            var item = await db.MediaContents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        app.MapPost("/api/contents", async (MediaContent content, MediaDbContext db) =>
        {
            db.MediaContents.Add(content);
            await db.SaveChangesAsync();
            return Results.Created($"/api/contents/{content.Id}", content);
        });

        app.MapPut("/api/contents/{id:int}", async (int id, MediaContent update, MediaDbContext db) =>
        {
            var existing = await db.MediaContents.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null)
            {
                return Results.NotFound();
            }

            existing.Name = update.Name;
            existing.Type = update.Type;
            existing.VideoPath = update.VideoPath;
            existing.ImagePath = update.ImagePath;
            existing.AudioPath = update.AudioPath;
            existing.Description = update.Description;
            existing.IsEnabled = update.IsEnabled;
            existing.PlayOrder = update.PlayOrder;

            await db.SaveChangesAsync();
            return Results.Ok(existing);
        });

        app.MapPatch("/api/contents/{id:int}/playlist", async (int id, HttpContext context, MediaDbContext db) =>
        {
            var body = await context.Request.ReadFromJsonAsync<PlaylistUpdateRequest>(context.RequestAborted);
            if (body == null)
            {
                return Results.BadRequest("Invalid request");
            }
            var existing = await db.MediaContents.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null)
            {
                return Results.NotFound();
            }
            if (body.IsEnabled.HasValue)
            {
                existing.IsEnabled = body.IsEnabled.Value;
            }
            if (body.PlayOrder.HasValue)
            {
                existing.PlayOrder = body.PlayOrder.Value;
            }
            await db.SaveChangesAsync();
            return Results.Ok(existing);
        });

        app.MapDelete("/api/contents/{id:int}", async (int id, MediaDbContext db) =>
        {
            var existing = await db.MediaContents.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null)
            {
                return Results.NotFound();
            }

            db.MediaContents.Remove(existing);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // 媒体文件上传接口（将文件保存到安装目标的媒体目录）
        app.MapPost("/api/files/upload/video", async (HttpRequest request, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("Missing file");
            }

            var savedPath = await SaveUploadedFileAsync(file, "video", ct);
            return Results.Ok(new { path = savedPath });
        });

        app.MapPost("/api/files/upload/audio", async (HttpRequest request, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("Missing file");
            }

            var savedPath = await SaveUploadedFileAsync(file, "audio", ct);
            return Results.Ok(new { path = savedPath });
        });

        app.MapPost("/api/files/upload/image", async (HttpRequest request, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files["file"];
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("Missing file");
            }

            var savedPath = await SaveUploadedFileAsync(file, "image", ct);
            return Results.Ok(new { path = savedPath });
        });

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/api/settings/autoplay", () =>
        {
            var settings = DataSettingsHelper.Load(_databasePath);
            return Results.Ok(new { isAutoPlay = settings.IsAutoPlay });
        });

        app.MapPost("/api/settings/autoplay", async (HttpContext context, PlayerService player) =>
        {
            var body = await context.Request.ReadFromJsonAsync<AutoPlayRequest>(context.RequestAborted);
            if (body == null)
            {
                return Results.BadRequest("Invalid request");
            }
            var settings = DataSettingsHelper.Load(_databasePath);
            settings.IsAutoPlay = body.IsAutoPlay;
            await DataSettingsHelper.SaveAsync(_databasePath, settings, context.RequestAborted);
            return Results.Ok(new { isAutoPlay = settings.IsAutoPlay });
        });

        app.Urls.Add(url);

        _host = app;

        return app.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _host?.StopAsync(cancellationToken) ?? Task.CompletedTask;
    }

    private async Task<string> SaveUploadedFileAsync(IFormFile file, string category, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".dat" : extension;

        var folder = Path.Combine(_mediaRoot, category);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{safeExtension}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return fullPath;
    }
}

