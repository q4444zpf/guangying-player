using System.Text.Json.Serialization;

namespace MediaControlPlayer.App.Models;

/// <summary>Data 目录下的配置文件（settings.json）</summary>
public sealed class DataSettings
{
    [JsonPropertyName("isAutoPlay")]
    public bool IsAutoPlay { get; set; } = true;

    [JsonPropertyName("isLooping")]
    public bool IsLooping { get; set; }
}
