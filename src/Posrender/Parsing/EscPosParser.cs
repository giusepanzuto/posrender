using Posrender.Commands;
using System.Collections.Generic;
using System.Text;

namespace Posrender.Parsing;

/// <summary>Parses a raw ESC/POS byte array into a sequence of typed commands.</summary>
public static class EscPosParser
{
    private const byte ESC = 0x1B;
    private const byte GS  = 0x1D;
    private const byte LF  = 0x0A;
    private const byte CR  = 0x0D;

    public static IReadOnlyList<IEscPosCommand> Parse(byte[] data)
    {
        var commands = new List<IEscPosCommand>();
        var textBuffer = new StringBuilder();
        int i = 0;

        void FlushText()
        {
            if (textBuffer.Length > 0)
            {
                commands.Add(new PrintTextCommand(textBuffer.ToString()));
                textBuffer.Clear();
            }
        }

        while (i < data.Length)
        {
            byte b = data[i];

            if (b == ESC && i + 1 < data.Length)
            {
                byte next = data[i + 1];

                // ESC @ — Initialize
                if (next == 0x40)
                {
                    FlushText();
                    commands.Add(new InitializeCommand());
                    i += 2;
                    continue;
                }

                // ESC a n — Alignment
                if (next == 0x61 && i + 2 < data.Length)
                {
                    FlushText();
                    commands.Add(new SetAlignmentCommand((TextAlignment)(data[i + 2] & 0x03)));
                    i += 3;
                    continue;
                }

                // ESC E n — Bold
                if (next == 0x45 && i + 2 < data.Length)
                {
                    FlushText();
                    commands.Add(new SetBoldCommand(data[i + 2] != 0));
                    i += 3;
                    continue;
                }

                // ESC - n — Underline
                if (next == 0x2D && i + 2 < data.Length)
                {
                    FlushText();
                    var mode = (data[i + 2] & 0x03) switch
                    {
                        1 => UnderlineMode.Single,
                        2 => UnderlineMode.Double,
                        _ => UnderlineMode.None,
                    };
                    commands.Add(new SetUnderlineCommand(mode));
                    i += 3;
                    continue;
                }

                // ESC ! n — Print mode (bold bit 3, double-height bit 4, double-width bit 5, underline bit 7)
                if (next == 0x21 && i + 2 < data.Length)
                {
                    FlushText();
                    byte n = data[i + 2];
                    bool bold = (n & 0x08) != 0;
                    int wMul = (n & 0x20) != 0 ? 2 : 1;
                    int hMul = (n & 0x10) != 0 ? 2 : 1;
                    bool underline = (n & 0x80) != 0;
                    commands.Add(new SetBoldCommand(bold));
                    commands.Add(new SetFontSizeCommand(wMul, hMul));
                    commands.Add(new SetUnderlineCommand(underline ? UnderlineMode.Single : UnderlineMode.None));
                    i += 3;
                    continue;
                }

                // ESC M n — Font select
                if (next == 0x4D && i + 2 < data.Length)
                {
                    FlushText();
                    commands.Add(new SetFontCommand(data[i + 2] == 0 ? PrinterFont.A : PrinterFont.B));
                    i += 3;
                    continue;
                }

                // ESC d n — Feed n lines
                if (next == 0x64 && i + 2 < data.Length)
                {
                    FlushText();
                    commands.Add(new LineFeedCommand(data[i + 2]));
                    i += 3;
                    continue;
                }

                // Unknown ESC sequence — skip the two bytes
                i += 2;
                continue;
            }

            if (b == GS && i + 1 < data.Length)
            {
                byte next = data[i + 1];

                // GS ! n — Character size
                if (next == 0x21 && i + 2 < data.Length)
                {
                    FlushText();
                    byte n = data[i + 2];
                    int wMul = ((n >> 4) & 0x07) + 1;
                    int hMul = (n & 0x07) + 1;
                    commands.Add(new SetFontSizeCommand(wMul, hMul));
                    i += 3;
                    continue;
                }

                // GS v 0 m xL xH yL yH data... — Raster image
                if (next == 0x76 && i + 2 < data.Length && data[i + 2] == 0x30 && i + 7 < data.Length)
                {
                    FlushText();
                    // byte i+3: mode (ignored for rendering purposes)
                    int xL = data[i + 4];
                    int xH = data[i + 5];
                    int yL = data[i + 6];
                    int yH = data[i + 7];
                    int bytesPerRow = xL + xH * 256;
                    int rows = yL + yH * 256;
                    int pixelDataLen = bytesPerRow * rows;
                    int dataStart = i + 8;
                    int widthDots = bytesPerRow * 8;

                    if (dataStart + pixelDataLen <= data.Length)
                    {
                        var pixels = new byte[pixelDataLen];
                        System.Array.Copy(data, dataStart, pixels, 0, pixelDataLen);
                        commands.Add(new PrintRasterImageCommand(widthDots, rows, pixels));
                        i = dataStart + pixelDataLen;
                    }
                    else
                    {
                        // Incomplete data — skip
                        i = data.Length;
                    }
                    continue;
                }

                // GS ( fn pL pH data... — variable-length compound command (QR code, logo, etc.)
                // Format: GS ( fn pL pH [pL + pH*256 bytes]
                if (next == 0x28 && i + 4 < data.Length)
                {
                    int dataLen = data[i + 3] + data[i + 4] * 256;
                    i += 5 + dataLen; // skip GS + ( + fn + pL + pH + data
                    continue;
                }

                // GS V m [n] — paper cut (m=0x41/0x42/0x43 use 4 bytes, others 3 bytes)
                if (next == 0x56 && i + 2 < data.Length)
                {
                    byte m = data[i + 2];
                    // m=0x41(A), 0x42(B), 0x43(C) have an extra feed-distance byte n
                    bool hasN = m == 0x41 || m == 0x42 || m == 0x43;
                    i += hasN && i + 3 < data.Length ? 4 : 3;
                    continue;
                }

                // Unknown GS sequence — skip the two bytes
                i += 2;
                continue;
            }

            if (b == LF)
            {
                FlushText();
                commands.Add(new LineFeedCommand(1));
                i++;
                continue;
            }

            if (b == CR)
            {
                // CR is a no-op when followed by LF; otherwise treat as line feed
                if (i + 1 < data.Length && data[i + 1] == LF)
                {
                    i++;
                }
                else
                {
                    FlushText();
                    commands.Add(new LineFeedCommand(1));
                }
                i++;
                continue;
            }

            // Printable bytes (0x20–0xFF)
            if (b >= 0x20)
            {
                textBuffer.Append((char)b);
            }

            i++;
        }

        FlushText();
        return commands;
    }
}
