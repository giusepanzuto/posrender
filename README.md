# Posrender

A .NET library that parses an ESC/POS byte stream and renders it as a PNG image, reproducing what an Epson label printer would physically print. Useful for generating label previews in applications that drive POS printers.

## Installation

```
dotnet add package Posrender
```

## Usage

```csharp
using Posrender;

byte[] escPosData = /* raw ESC/POS command stream */;

// Render with default options (80 mm Epson printer, 203 DPI)
Stream png = PosRenderer.Render(escPosData);

// Render with custom paper width
var options = PosRenderOptions.FromMillimeters(58);
Stream png = PosRenderer.Render(escPosData, options);

// Write to file
using var file = File.Create("label.png");
png.CopyTo(file);
```

## Options

| Property | Default | Description |
|---|---|---|
| `PaperWidthDots` | 576 | Paper width in dots (≈ 72 mm printable area at 203 DPI) |
| `Dpi` | 203 | Printer resolution |

Use `PosRenderOptions.FromMillimeters(widthMm, dpi)` to compute the dot width from physical dimensions.

## Supported ESC/POS commands

| Command | Description |
|---|---|
| `ESC @` | Initialize printer |
| `ESC a n` | Text alignment (left / center / right) |
| `ESC E n` | Bold on/off |
| `ESC - n` | Underline (none / single / double) |
| `ESC ! n` | Print mode (bold, double-height, double-width, underline) |
| `ESC M n` | Font select (A / B) |
| `ESC d n` | Feed n lines |
| `GS ! n` | Character size (width × height multiplier) |
| `GS v 0` | Print raster image |
| `GS ( k` | QR code (rendered as a placeholder rectangle) |
| `GS V` | Paper cut (ignored) |
| Printable bytes | Text output |

## License

MIT
