namespace Jellyfin.Plugin.JellyLibraryAccess.Models;

public class MovieApproval
{
    public string? TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public string? Title { get; set; }
    public int? Year { get; set; }
    public Guid LastKnownItemId { get; set; }
}
