using MediaControlPlayer.App.Models;

namespace MediaControlPlayer.App.Models;

public sealed class MediaContent
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public MediaType Type { get; set; } = MediaType.Video;

    public string? VideoPath { get; set; }

    public string? ImagePath { get; set; }

    public string? AudioPath { get; set; }

    public string? Description { get; set; }

    /// <summary>是否参与播放列表播放，默认 true</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>播放排序，数字越小越靠前，默认 0</summary>
    public int PlayOrder { get; set; }
}

