using System;
using System.Text;

namespace OneWire.Common
{
    public static class HexFormatter
    {
        public static string FormatHexAscii(byte[] data)
        {
            const int bytesPerLine = 16;
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                sb.Append($"{i:X8}  ");
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length) sb.Append($"{data[i + j]:X2} ");
                    else sb.Append("   ");
                    if (j == 7) sb.Append(" ");
                }
                sb.Append(" ");
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                    {
                        byte b = data[i + j];
                        sb.Append((b >= 32 && b <= 126) ? (char)b : '.');
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string FormatHexRaw(byte[] data)
        {
            const int bytesPerLine = 16;
            if (data == null || data.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                int count = Math.Min(bytesPerLine, data.Length - i);
                for (int j = 0; j < count; j++)
                {
                    if (j > 0) sb.Append(' ');
                    sb.Append(data[i + j].ToString("X2"));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
