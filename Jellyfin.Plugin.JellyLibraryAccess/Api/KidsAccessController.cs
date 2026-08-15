using Jellyfin.Plugin.JellyLibraryAccess.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyLibraryAccess.Api;

[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("JellyLibraryAccess")]
public class KidsAccessController : ControllerBase
{
    private readonly KidsAccessService _service;
    public KidsAccessController(KidsAccessService service) => _service = service;

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
}
