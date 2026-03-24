# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Posrender is a .NET NuGet library targeting `net6.0` that parses ESC/POS protocol byte arrays and renders them as PNG images, reproducing what an Epson label printer would physically print. The primary use case is generating label previews in projects that drive POS printers.

The core public API is expected to expose a method that accepts a `byte[]` (raw ESC/POS command stream) and returns the rendered image as a `Stream` (PNG).

## Structure

- `src/Posrender/` — Library source code (namespace `Posrender`)
  - `Commands/` — ESC/POS command model (`IEscPosCommand` and concrete classes)
  - `Parsing/` — `EscPosParser`: byte[] → `IReadOnlyList<IEscPosCommand>`
  - `Rendering/` — `EscPosImageRenderer` (internal) + `BitmapFont`
  - `PosRenderer.cs` — public entry point
  - `PosRenderOptions.cs` — paper width / DPI options
- `tests/Posrender.Tests/` — xUnit test project
- `posrender.slnx` — Solution file

## Build & Pack

```bash
# Build
dotnet build

# Pack NuGet package
dotnet pack src/Posrender/Posrender.csproj -c Release

# Run tests (once a test project exists)
dotnet test
```

## Development Approach

This project follows TDD. For every change, write the test first and only then write the implementation.

## Architecture

The pipeline is linear:

```
byte[] → EscPosParser.Parse() → IReadOnlyList<IEscPosCommand>
       → EscPosImageRenderer.Render() → Image<Rgb24>
       → PngEncoder → MemoryStream (PNG)
```

Text rendering uses an embedded 8×8 bitmap font (`BitmapFont`) — no TrueType font dependency. Width/height multipliers scale each pixel block. Alignment is computed per-line across all pending text segments before the next LF.

`EscPosImageRenderer` is `internal`; `InternalsVisibleTo` exposes it to the test project.

## NuGet Package Metadata

Defined in `src/Posrender/Posrender.csproj`:
- Package ID: `Posrender`
- Author: `giusepanzuto`
- License: MIT
- XML documentation generation is enabled (`GenerateDocumentationFile=true`) — public APIs should have `<summary>` XML doc comments.
