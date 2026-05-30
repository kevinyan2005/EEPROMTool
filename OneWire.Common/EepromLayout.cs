namespace OneWire.Common
{
    /// <summary>
    /// Single source of truth for the 128-byte DS2431 EEPROM memory map.
    ///
    /// Absolute offsets are from byte 0 of the full EEPROM image.
    /// Field offsets within each block are relative to that block's start.
    ///
    /// Byte map:
    ///   0–37   Identification block  (36 data + 2 CRC)
    ///   38–79  Calibration block     (38 data + 2 CRC + 2 unused)
    ///   80–127 User block            (26 data + 2 CRC + 8 usage date + 12 unused)
    /// </summary>
    public static class EepromLayout
    {
        public const int TotalSize = 128;
        public const int CrcSize   = 2;

        // ── Identification block (absolute start: 0) ─────────────────────────
        //  Offset  Size  Content
        //       0     2  Version        (ushort, big-endian)
        //       2     2  ID             (ASCII)
        //       4    16  Model          (ASCII)
        //      20    16  Serial Number  (ASCII)
        //      36     2  CRC-16         (big-endian)
        public const int IdBlockStart    = 0;
        public const int IdVersionOffset = 0;
        public const int IdDataIdOffset  = 2;
        public const int IdModelOffset   = 4;
        public const int IdSerialOffset  = 20;
        public const int IdCrcOffset     = 36;
        public const int IdVersionSize   = 2;
        public const int IdDataIdSize    = 2;
        public const int IdModelSize     = 16;
        public const int IdSerialSize    = 16;
        public const int IdBlockLength   = 36;   // bytes before CRC

        // ── Calibration block (absolute start: 38) ───────────────────────────
        //  Offset  Size  Content
        //       0     4  GaugeFactor0      (float, word-swapped)
        //       4     4  GaugeFactor1      (float, word-swapped)
        //       8     4  GaugeFactor2      (float, word-swapped)
        //      12     4  GaugeFactor3      (float, word-swapped)
        //      16     4  Reference Value   (float, word-swapped)
        //      20     8  Manufacturing Date (vendor DateTime)
        //      28     8  Expiration Date   (vendor DateTime)
        //      36     2  Gauge Type        (ASCII)
        //      38     2  CRC-16            (big-endian)
        //      40     2  (not used)
        public const int CalBlockStart            = 38;
        public const int CalGaugeFactor0Offset    = 0;
        public const int CalGaugeFactor1Offset    = 4;
        public const int CalGaugeFactor2Offset    = 8;
        public const int CalGaugeFactor3Offset    = 12;
        public const int CalReferenceValueOffset  = 16;
        public const int CalManufactureDateOffset = 20;
        public const int CalExpiryDateOffset      = 28;
        public const int CalGaugeTypeOffset       = 36;
        public const int CalCrcOffset             = 38;
        public const int CalGaugeFactorSize       = 4;
        public const int CalGaugeFactorCount      = 4;
        public const int CalReferenceValueSize    = 4;
        public const int CalManufactureDateSize   = 8;
        public const int CalExpiryDateSize        = 8;
        public const int CalGaugeTypeSize         = 2;
        public const int CalBlockLength           = 38;  // bytes before CRC

        // ── User block (absolute start: 80) ──────────────────────────────────
        //  Offset  Size  Content
        //       0     2  Schema              (ushort, big-endian)
        //       2    16  Probe Serial Number (ASCII)
        //      18     8  Probe Expiry Date   (vendor DateTime)
        //      26     2  CRC-16              (big-endian)
        //      28     8  Probe Usage Date    (vendor DateTime) — stored after CRC
        //      36    12  (not used)
        public const int UserBlockStart           = 80;
        public const int UserSchemaOffset         = 0;
        public const int UserProbeSerialOffset    = 2;
        public const int UserProbeExpiryOffset    = 18;
        public const int UserCrcOffset            = 26;
        public const int UserProbeUsageDateOffset = 28;
        public const int UserSchemaSize           = 2;
        public const int UserProbeSerialSize      = 16;
        public const int UserProbeExpirySize      = 8;
        public const int UserProbeUsageDateSize   = 8;
        public const int UserBlockLength          = 26;  // bytes before CRC
    }
}
