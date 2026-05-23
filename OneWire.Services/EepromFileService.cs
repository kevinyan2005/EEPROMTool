using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OneWire.Common;

namespace OneWire.Services
{
    public class EepromFileService : IEepromFileService
    {
        public EepromData LoadFromJson(string path)
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<EepromData>(json);
        }

        public void SaveToJson(EepromData data, string path)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings
            {
                DateFormatString = "yyyy-MM-ddTHH:mm:ss"
            });
            File.WriteAllText(path, json);
        }

        public byte[] LoadFromRawTxt(string path)
        {
            var rawText = File.ReadAllText(path);
            return ParseRawHexText(rawText);
        }

        public void SaveToRawTxt(byte[] data, string path)
        {
            File.WriteAllText(path, FormatHexRaw(data));
        }

        public string FormatHexAscii(byte[] data)
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

        public string FormatHexRaw(byte[] data)
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

        private static byte[] ParseRawHexText(string rawText)
        {
            var matches = Regex.Matches(rawText ?? string.Empty, @"\b(?:0x)?([0-9A-Fa-f]{2})\b");
            if (matches.Count == 0)
                throw new InvalidDataException("No hex bytes found.");

            var parsed = new byte[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                parsed[i] = byte.Parse(matches[i].Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (parsed.Length < 128)
                throw new InvalidDataException("Not enough EEPROM bytes.");

            if (parsed.Length == 128) return parsed;

            var trimmed = new byte[128];
            Array.Copy(parsed, trimmed, 128);
            return trimmed;
        }
    }
}
