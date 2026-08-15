using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyLibraryAccess.Services;

public sealed class KidsAccessHostedService : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly KidsAccessService _service;
    private readonly ILogger<KidsAccessHostedService> _logger;
    private CancellationTokenSource? _cts;

    public KidsAccessHostedService(ILibraryManager libraryManager, KidsAccessService service, ILogger<KidsAccessHostedService> logger)
    {
        _libraryManager = libraryManager;
        _service = service;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        _ = DelayedInitialSync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item.GetBaseItemKind() != Jellyfin.Data.Enums.BaseItemKind.Movie || _cts is null) return;
        _ = SafeSync(_cts.Token);
    }

    private async Task DelayedInitialSync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), token).ConfigureAwait(false);
            await SafeSync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private async Task SafeSync(CancellationToken token)
    {
        try { await _service.SyncAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "JellyLibraryAccess sync failed"); }
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
