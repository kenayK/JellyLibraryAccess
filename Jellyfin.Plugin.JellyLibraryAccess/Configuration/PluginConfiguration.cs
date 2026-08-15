using Jellyfin.Plugin.JellyLibraryAccess.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyLibraryAccess.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApprovalTag { get; set; } = "kenay-kids-approved";
    public List<Guid> BaselineLibraryIds { get; set; } = [];
    public List<MovieApproval> ApprovedMovies { get; set; } = [];
    public bool RemoveOrphanedApprovalTags { get; set; } = true;
}
