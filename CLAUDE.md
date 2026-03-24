# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Posrender is a .NET NuGet library targeting `netstandard2.0` that parses ESC/POS protocol byte arrays and renders them as PNG images, reproducing what an Epson label printer would physically print. It is in early development — the library source is currently a placeholder.

## Structure

- `src/Posrender/` — Library source code (namespace `Posrender`)
- `docs/` — Documentation
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

## NuGet Package Metadata

Defined in `src/Posrender/Posrender.csproj`:
- Package ID: `Posrender`
- Author: `giusepanzuto`
- License: MIT
- XML documentation generation is enabled (`GenerateDocumentationFile=true`) — public APIs should have `<summary>` XML doc comments.
