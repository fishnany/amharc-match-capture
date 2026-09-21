# AMHARC Capture — CR-1 Executable Toolchain Manifest

Status: CR1-002 controlled recovery baseline  
Baseline commit: `84cca444b8e154a8e13c0ce3c869ff2f8557e62a`  
Supported production OS: Windows 11 x64

## Required toolchain

| Component | CR-1 requirement | Evidence / rationale |
|---|---|---|
| Windows | Windows 11 x64 | Root and Windows-agent documentation define Windows 11 as the production environment. |
| .NET SDK | .NET 8 SDK | All Capture projects target `net8.0` or `net8.0-windows`; repository documentation requires .NET 8. |
| Node.js | Node.js 24 or later for the current workspace recovery line | Root README requires Node.js 24+. The older Windows-agent README says Node.js 20 + pnpm; this conflict is recorded below rather than hidden. |
| pnpm | pnpm capable of lockfile version 9; exact version not yet historically evidenced | `pnpm-lock.yaml` declares `lockfileVersion: '9.0'`. Capture has no `packageManager` field or version file at FT-0. Do not invent an exact pin. |
| FFmpeg | Windows FFmpeg executable available either as configured/local `ffmpeg.exe` or on PATH | Windows-agent documentation and runtime architecture require FFmpeg for recording/streaming. No exact FFmpeg version is evidenced at FT-0. |
| Visual Studio | Visual Studio 2022 optional for development | Windows-agent documentation. CLI build remains supported through .NET SDK. |
| Inno Setup | Inno Setup 6 only when building installer | Installer build documentation. Not required for normal source build/test. |

## Repository commands to validate during CR-1

From repository root:

```powershell
pnpm install
pnpm run typecheck
pnpm run build
```

.NET solution validation:

```powershell
dotnet restore .\agent-windows\AmharcAgent.sln
dotnet build .\agent-windows\AmharcAgent.sln
```

The canonical repository-root test command is:

```powershell
.\scripts\tooling\test-dotnet.ps1
```

The script resolves `agent-windows\AmharcAgent.sln` from its own repository location, so invocation does not depend on the caller's current directory. CR1-004 also corrected test-only repository discovery to recognise both a normal `.git` directory and a linked-worktree `.git` file.

## Known version ambiguities

### Node.js

Two repository documents disagree:

- root `README.md`: Node.js 24+;
- `agent-windows/README-WINDOWS.md`: Node.js 20 + pnpm.

For CR-1 recovery, the root workspace requirement governs JavaScript workspace execution: Node.js 24+.

This does not assert that Node 24 was the exact version used for every historical Capture commit.

### pnpm

FT-0 Capture does not contain:
- a `packageManager` declaration;
- `.nvmrc`;
- `.node-version`;
- an exact pnpm version declaration.

Therefore CR1-002 does not manufacture an exact pnpm pin. Exact pinning may be added only after a successful supported Windows recovery run establishes the version used and the resulting lockfile/build behaviour is verified.

### .NET SDK patch version

No `global.json` exists at FT-0. The repository establishes the .NET 8 SDK family, not an exact SDK patch. CR1-002 therefore does not add an arbitrary `global.json`.

### FFmpeg version

The repository requires FFmpeg but does not establish an exact version. CR1-002 records the executable requirement without fabricating a version.

## Reproducibility policy for CR-1

1. Do not upgrade dependencies as part of toolchain recovery.
2. Use the committed lockfile.
3. Record actual versions used for every successful recovery run:
   - `dotnet --info`
   - `node --version`
   - `pnpm --version`
   - `ffmpeg -version`
4. If a tool version is later pinned, the pin must be justified by a successful recovery run and committed as a separate traceable change.
5. Toolchain changes must not alter AMHARC scoring, clock, media or domain semantics.

## Next gate

CR1-003 may repair the Windows-incompatible pnpm lifecycle only after this manifest is committed/recorded. CR1-004 separately owns .NET test discovery.
