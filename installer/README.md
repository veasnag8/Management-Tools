# Windows Installer Build Guide (Inno Setup)

This directory contains the Inno Setup script (`setup.iss`) to compile a production-ready Windows installer.

## Prerequisites

1. Install **Inno Setup 6**.
2. Build and publish the Windows application:
   ```bash
   cd ../windows-client
   dotnet publish Tool/Tool.csproj -c Release -r win-x64 --self-contained false -o bin/Publish
   ```

## Compiling the Installer

Compile `setup.iss` with Inno Setup Compiler to produce `installer/Output/ToolSetup-1.0.0.exe`.
