# JellyLibraryAccess for Jellyfin

Purpose-built Jellyfin plugin for approving individual movies for a child account without moving files between libraries.

## How it works

Jellyfin's `Allowed Tags` user restriction is a whitelist. Normally that makes an existing Kids Movies library disappear unless every item is tagged. This plugin fixes that workflow:

1. Select your existing **Kids Movies** library as a baseline library.
2. The plugin automatically applies `kenay-kids-approved` to every movie in that library.
3. Give the child Jellyfin user access to both **Kids Movies** and **Movies**.
4. Set the child's **Allowed Tags** to `kenay-kids-approved`.
5. Use the plugin page to search any movie and press **Allow for Kids**.

All unapproved adult-library movies remain hidden. No media files are moved or duplicated.

## Radarr-safe approvals

Approved movies are persisted by TMDb ID first, IMDb ID second, and Jellyfin Item ID only as a fallback/cache. A sync re-applies the tag after Radarr upgrades or Jellyfin rescans.

## Target

The included project targets Jellyfin Server **10.11.9** / .NET 9. Jellyfin requires plugin package references to match the installed server version. This package is pinned to Jellyfin Server **10.11.9**, matching the target server.

## Build

```bash
dotnet publish Jellyfin.Plugin.JellyLibraryAccess/Jellyfin.Plugin.JellyLibraryAccess.csproj -c Release
```

Copy `Jellyfin.Plugin.JellyLibraryAccess.dll` from the publish output into a plugin folder under Jellyfin's `/config/plugins/`, then restart Jellyfin.

## License

GPL-3.0-or-later, required for compatibility with Jellyfin plugin distribution.
