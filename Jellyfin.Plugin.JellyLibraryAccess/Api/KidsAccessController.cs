using Jellyfin.Plugin.JellyLibraryAccess.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyLibraryAccess.Api;

[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("JellyLibraryAccess")]
public class KidsAccessController : ControllerBase
{
    private readonly KidsAccessService _service;
    private readonly ILibraryManager _libraryManager;

    public KidsAccessController(KidsAccessService service, ILibraryManager libraryManager)
    {
        _service = service;
        _libraryManager = libraryManager;
    }

    [HttpGet("settings")]
    public ActionResult<object> GetSettings()
    {
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin is not initialized.");

        // VirtualFolderInfo.ItemId is a string in Jellyfin's public API contract.
        // Preserve it as a string here; only parse it when persisting/comparing against BaseItem.Id.
        var libraries = _libraryManager.GetVirtualFolders()
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.ItemId))
            .Select(x => new
            {
                name = x.Name,
                itemId = x.ItemId
            })
            .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new
        {
            approvalTag = config.ApprovalTag,
            removeOrphanedApprovalTags = config.RemoveOrphanedApprovalTags,
            baselineLibraryIds = config.BaselineLibraryIds.Select(x => x.ToString("N")).ToArray(),
            libraries
        });
    }

    [HttpPost("settings")]
    public ActionResult<object> SaveSettings([FromBody] SettingsRequest request)
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin is not initialized.");
        var config = plugin.Configuration;

        config.ApprovalTag = string.IsNullOrWhiteSpace(request.ApprovalTag)
            ? "jellylibraryaccess-approved"
            : request.ApprovalTag.Trim();
        config.RemoveOrphanedApprovalTags = request.RemoveOrphanedApprovalTags;

        config.BaselineLibraryIds = (request.BaselineLibraryIds ?? [])
            .Select(ParseItemId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        plugin.UpdateConfiguration(config);
        return Ok(new { saved = true });
    }

    [HttpGet("libraries")]
    public ActionResult<object> Libraries()
    {
        var libraries = _libraryManager.GetVirtualFolders()
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.ItemId))
            .Select(x => new { itemId = x.ItemId, name = x.Name })
            .OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Ok(libraries);
    }

    [HttpGet("status/{itemId:guid}")]
    public async Task<ActionResult<object>> Status(Guid itemId, CancellationToken cancellationToken) =>
        Ok(new { approved = await _service.IsApprovedAsync(itemId, cancellationToken).ConfigureAwait(false) });

    [HttpPost("approve/{itemId:guid}")]
    public async Task<ActionResult<object>> Approve(Guid itemId, CancellationToken cancellationToken) =>
        Ok(await _service.ApproveAsync(itemId, cancellationToken).ConfigureAwait(false));

    [HttpDelete("approve/{itemId:guid}")]
    public async Task<IActionResult> Revoke(Guid itemId, CancellationToken cancellationToken)
    {
        await _service.RevokeAsync(itemId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncResult>> Sync(CancellationToken cancellationToken) =>
        Ok(await _service.SyncAsync(cancellationToken).ConfigureAwait(false));

    private static Guid? ParseItemId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

public sealed class SettingsRequest
{
    public string? ApprovalTag { get; set; }
    public bool RemoveOrphanedApprovalTags { get; set; } = true;
    public List<string>? BaselineLibraryIds { get; set; }
}
