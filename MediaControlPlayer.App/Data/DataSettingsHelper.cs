using System.IO;
using System.Text.Json;
using MediaControlPlayer.App.Models;

namespace MediaControlPlayer.App.Data;

/// <summary>Data 目录下 settings.json 的读写</summary>
public static class DataSettingsHelper
{
    private static string GetSettingsPath(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        return string.IsNullOrEmpty(dir) ? "settings.json" : Path.Combine(dir, "settings.json");
    }

    public static DataSettings Load(string databasePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(databasePath);
            var path = GetSettingsPath(databasePath);

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<DataSettings>(json);
                return settings ?? new DataSettings();
            }

            // 迁移旧的 autoplay.json
            if (!string.IsNullOrEmpty(dir))
            {
                var oldPath = Path.Combine(dir, "autoplay.json");
                if (File.Exists(oldPath))
                {
                    var oldJson = File.ReadAllText(oldPath);
                    var obj = JsonSerializer.Deserialize<JsonElement>(oldJson);
                    var isAutoPlay = true;
                    if (obj.TryGetProperty("isAutoPlay", out var prop) && prop.ValueKind == JsonValueKind.False)
                        isAutoPlay = false;
                    var settings = new DataSettings { IsAutoPlay = isAutoPlay };
                    Save(databasePath, settings);
                    return settings;
                }
            }

            return new DataSettings();
        }
        catch
        {
            return new DataSettings();
        }
    }

    public static void Save(string databasePath, DataSettings settings)
    {
        try
        {
            var path = GetSettingsPath(databasePath);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // 忽略写入失败
        }
    }

    public static async Task SaveAsync(string databasePath, DataSettings settings, CancellationToken ct = default)
    {
        try
        {
            var path = GetSettingsPath(databasePath);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json, ct);
        }
        catch
        {
            // 忽略写入失败
        }
    }
}
