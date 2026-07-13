# Building the AMHARC Windows Installer

## Prerequisites

1. **Visual Studio 2022** or the .NET 8 SDK
2. **Inno Setup 6** — https://jrsoftware.org/isdl.php
3. **FFmpeg** — https://ffmpeg.org/download.html (download `ffmpeg-release-essentials.zip`, extract `ffmpeg.exe`)
4. **Node.js + pnpm** — to build the operator UI

## Step 1 — Build the operator UI

```powershell
# From the repo root
pnpm install
pnpm --filter @workspace/operator-ui run build

# Copy the built files to the API's wwwroot
xcopy /E /Y artifacts\operator-ui\dist\* src\AmharcAgent.Api\wwwroot\
```

## Step 2 — Publish the C# agent (self-contained, single EXE)

```powershell
cd agent-windows
dotnet publish src\AmharcAgent.Api\AmharcAgent.Api.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Step 3 — Place FFmpeg

```
agent-windows\installer\ffmpeg\ffmpeg.exe
```

## Step 4 — Build the installer

Open `agent-windows\installer\setup.iss` in Inno Setup Compiler and click **Build → Compile**.  
The output installer appears at `agent-windows\dist\amharc-match-capture-setup.exe`.
