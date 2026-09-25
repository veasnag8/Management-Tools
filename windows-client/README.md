# Windows Client Desktop Application (C# / .NET 8 WPF)

SNA Pro — Streaming drama episode manager & batch downloader with licensing integration, DPAPI session security, and offline verification.

## Architecture

- **`Models/`**: `DramaModel.cs`, `EpisodeModel.cs`, `PlatformType.cs` with MVVM Observable properties.
- **`ViewModels/`**: `MainViewModel.cs` handling platform switching, multi-select, and concurrency-managed downloads.
- **`Services/`**: `DownloaderService.cs` running CLI tools (`N_m3u8DL-RE.exe` / `ffmpeg.exe`), and `DramaCrawlerService.cs`.
- **`License/`**: Hardware identification (`DeviceIdService.cs`), DPAPI storage (`SecureStorageService.cs`), and offline verification (`LicenseManager.cs`).

## Build Instructions

```bash
dotnet build Tool.sln -c Release
dotnet publish Tool/Tool.csproj -c Release -r win-x64 --self-contained false -o bin/Publish
dotnet test Tool.Tests/Tool.Tests.csproj
```
