namespace MediaControlPlayer.App.Models;

/// <summary>播放列表字段部分更新请求</summary>
public sealed class PlaylistUpdateRequest
{
    public bool? IsEnabled { get; set; }

    public int? PlayOrder { get; set; }
}
