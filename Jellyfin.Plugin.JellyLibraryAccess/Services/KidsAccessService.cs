using Jellyfin.Data.Enums;
using Jellyfin.Plugin.JellyLibraryAccess.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyLibraryAccess.Services;

public class KidsAccessService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<KidsAccessService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public KidsAccessService(ILibraryManager libraryManager, ILogger<KidsAccessService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public async Task<bool> IsApprovedAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var item = _libraryManager.GetItemById(itemId);
        if (item is null) return false;
        var config = Plugin.Instance?.Configuration;
        if (config is null) return false;
        return HasTag(item, config.ApprovalTag) &&
               (IsBaseline(item, config.BaselineLibraryIds) || config.ApprovedMovies.Any(a => Matches(a, item)));
    }

    public async Task<MovieApproval> ApproveAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin is not initialized.");
            var config = plugin.Configuration;
            var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("Movie not found.");
            if (item.GetBaseItemKind() != BaseItemKind.Movie) throw new InvalidOperationException("Only movies can be approved.");

            await EnsureTagAsync(item, config.ApprovalTag, cancellationToken).ConfigureAwait(false);
            var approval = CreateApproval(item);
            config.ApprovedMovies.RemoveAll(a => Matches(a, item));
            config.ApprovedMovies.Add(approval);
            plugin.UpdateConfiguration(config);
            _logger.LogInformation("Approved {Movie} ({Year}) for kids access", item.Name, item.ProductionYear);
            return approval;
        }
        finally { _gate.Release(); }
    }

    public async Task RevokeAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin is not initialized.");
            var config = plugin.Configuration;
            var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("Movie not found.");
            config.ApprovedMovies.RemoveAll(a => Matches(a, item));
            plugin.UpdateConfiguration(config);
            if (!IsBaseline(item, config.BaselineLibraryIds))
                await RemoveTagAsync(item, config.ApprovalTag, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Revoked kids access for {Movie}", item.Name);
        }
        finally { _gate.Release(); }
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin is not initialized.");
            var config = plugin.Configuration;
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Movie],
                Recursive = true
            });

            var tagged = 0;
            var cleaned = 0;
            foreach (var movie in movies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var shouldHaveTag = IsBaseline(movie, config.BaselineLibraryIds) || config.ApprovedMovies.Any(a => Matches(a, movie));
                if (shouldHaveTag && !HasTag(movie, config.ApprovalTag))
                {
                    await EnsureTagAsync(movie, config.ApprovalTag, cancellationToken).ConfigureAwait(false);
                    tagged++;
                }
                else if (!shouldHaveTag && config.RemoveOrphanedApprovalTags && HasTag(movie, config.ApprovalTag))
                {
                    await RemoveTagAsync(movie, config.ApprovalTag, cancellationToken).ConfigureAwait(false);
                    cleaned++;
                }
            }

            foreach (var approval in config.ApprovedMovies)
            {
                var match = movies.FirstOrDefault(m => Matches(approval, m));
                if (match is not null) approval.LastKnownItemId = match.Id;
            }
            plugin.UpdateConfiguration(config);
            return new SyncResult(tagged, cleaned, movies.Count);
        }
        finally { _gate.Release(); }
    }

    private static MovieApproval CreateApproval(BaseItem item) => new()
    {
        TmdbId = item.ProviderIds.TryGetValue("Tmdb", out var tmdb) ? tmdb : null,
        ImdbId = item.ProviderIds.TryGetValue("Imdb", out var imdb) ? imdb : null,
        Title = item.Name,
        Year = item.ProductionYear,
        LastKnownItemId = item.Id
    };

    private static bool Matches(MovieApproval approval, BaseItem item)
    {
        if (!string.IsNullOrWhiteSpace(approval.TmdbId) && item.ProviderIds.TryGetValue("Tmdb", out var tmdb) && string.Equals(tmdb, approval.TmdbId, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(approval.ImdbId) && item.ProviderIds.TryGetValue("Imdb", out var imdb) && string.Equals(imdb, approval.ImdbId, StringComparison.OrdinalIgnoreCase)) return true;
        return approval.LastKnownItemId != Guid.Empty && approval.LastKnownItemId == item.Id;
    }

    private static bool IsBaseline(BaseItem item, IReadOnlyCollection<Guid> baselineLibraryIds)
    {
        if (baselineLibraryIds.Count == 0) return false;
        var topParent = item.GetTopParent();
        return topParent is not null && baselineLibraryIds.Contains(topParent.Id);
    }

    private static bool HasTag(BaseItem item, string tag) => item.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));

    private static async Task EnsureTagAsync(BaseItem item, string tag, CancellationToken cancellationToken)
    {
        if (HasTag(item, tag)) return;
        item.Tags = item.Tags.Append(tag).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RemoveTagAsync(BaseItem item, string tag, CancellationToken cancellationToken)
    {
        if (!HasTag(item, tag)) return;
        item.Tags = item.Tags.Where(t => !string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)).ToArray();
        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
    }
}

public record SyncResult(int Tagged, int Cleaned, int MoviesScanned);
