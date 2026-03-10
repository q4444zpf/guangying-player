namespace MediaControlPlayer.App.Models;

public sealed class PlayRequest
{
    public MediaType Type { get; set; } = MediaType.Video;

    public string? VideoPath { get; set; }

    public string? ImagePath { get; set; }

    public string? AudioPath { get; set; }
}

