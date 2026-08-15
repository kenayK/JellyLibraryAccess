# JellyLibraryAccess for Jellyfin

Purpose-built Jellyfin plugin for approving individual movies for a restricted profile without moving files between libraries.

## How it works

Jellyfin's `Allowed Tags` user restriction is a whitelist. Normally that can make an existing restricted library disappear unless every item is tagged. This plugin fixes that workflow:

1. Select an existing library (for example **Kids Movies**) as a baseline library.
2. The plugin automatically applies the configurable approval tag to every movie in that library. The default is `jellylibraryaccess-approved`.
3. Give the restricted Jellyfin user access to both the baseline library and any other libraries from which individual titles may be approved.
4. Set that user's **Allowed Tags** to the same approval tag.
5. Use the plugin page to search any movie and approve or remove access.

All unapproved movies remain hidden. No media files are moved or duplicated.

The approval tag is configurable so the plugin is not tied specifically to children or to a particular server naming convention.

## Radarr-safe approvals

Approved movies are persisted by TMDb ID first, IMDb ID second, and Jellyfin Item ID only as a fallback/cache. A sync re-applies the tag after Radarr upgrades or Jellyfin rescans.

## Target

The included project targets Jellyfin Server **10.11.9** / .NET 9. Jellyfin requires plugin package references to match the installed server version. This package is pinned to Jellyfin Server **10.11.9**.

## Build

```bash
dotnet publish Jellyfin.Plugin.JellyLibraryAccess/Jellyfin.Plugin.JellyLibraryAccess.csproj -c Release
```

## License

GPL-3.0-or-later, required for compatibility with Jellyfin plugin distribution.
