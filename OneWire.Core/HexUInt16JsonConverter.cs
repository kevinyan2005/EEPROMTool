using System;
using System.Globalization;
using Newtonsoft.Json;

namespace OneWire.Core
{
    /// <summary>Serializes a ushort as a "0xNNNN" hex string; accepts hex strings or plain numbers on read.</summary>
    internal class HexUInt16JsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(ushort);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue($"0x{(ushort)value:X4}");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var text = ((string)reader.Value).Trim();
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    text = text.Substring(2);
                return ushort.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return Convert.ToUInt16(reader.Value);
        }
    }
}
