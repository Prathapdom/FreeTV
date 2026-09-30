# Free TV

A Windows x64 desktop IPTV player using .NET 8, WinUI 3, and Windows App SDK 2.4.0, matching the Comic Reader stack. The original HTML design is preserved as index.html.

## Run

Open `publish/FreeTV.exe`. Keep the entire publish folder together; it includes the .NET and Windows App SDK runtimes. No separate .NET installation is required. Windows 10 version 1809 or later is required. This is an unpackaged, unsigned local build.

## Use

1. Open Settings (gear in the left rail).
2. Enter a public M3U catalogue URL. The supplied IPTV-org index is prefilled.
3. Click Save and load channels.
4. Search or choose a category, then click a channel to play.
5. Use Favorite to save channels and the heart in the left rail to filter favorites.

The player provides pause, volume, aspect ratio and full-window playback controls. Retry reopens the selected stream. Refresh playlist downloads the saved source again. A failed refresh retains the previous catalogue. Cached channels, the source and favorites are saved in `%LOCALAPPDATA%/FreeTV/settings.json`. Cached channel lists are available after restart; playing still requires a network connection. Set FREETV_DATA_DIR before launching for isolated test data.

HTTP/HTTPS stream URLs are supported. Windows codec and HLS/DASH compatibility apply; region-restricted or offline channels may fail. VLC-specific headers/options, DRM login, EPG schedules, recording and a VLC fallback are not implemented. The M3U catalogue itself is not a video stream manifest. There are no fabricated channels or program schedules in the app.

## Build on this machine

```powershell
$env:DOTNET_ROOT='F:\dev\dotnet'
$env:NUGET_PACKAGES='F:\dev\nuget\packages'
& 'F:\dev\dotnet\dotnet.exe' publish .\FreeTV.csproj -c Release -o .\publish
& 'F:\dev\dotnet\dotnet.exe' run --project .\Tests\ParserTests.csproj
```

Optional integration check: append `-- https://iptv-org.github.io/iptv/index.m3u` to the test command. Tests cover BOM/CRLF, quoted commas, relative stream URLs, duplicate entries, non-HTTP schemes and rejecting invalid input or HLS manifests used as catalogues.
