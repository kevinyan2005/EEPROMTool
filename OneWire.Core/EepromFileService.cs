using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OneWire.Common;

namespace OneWire.Core
{
    internal class EepromFileService
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
            File.WriteAllText(path, HexFormatter.FormatHexRaw(data));
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
