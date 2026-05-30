using System;

namespace OneWire.Common
{
    public static class DateTimeHelper
    {
        // Vendor datetime (BCD): YY_hi YY_lo MM DD HH mm ss reserved
        // Example: 20 24 11 20 01 05 03 00 => 2024-11-20 01:05:03
        // Unset pattern: DD D0 D0 D0 D0 D0 D0 00 => null
        public static DateTime? ReadVendorDateTimeOrNull(byte[] data, int offset, DateTimeKind kind = DateTimeKind.Utc)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset + 8 > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            byte b1 = data[offset + 1];
            byte b2 = data[offset + 2];
            byte b3 = data[offset + 3];
            byte b4 = data[offset + 4];
            byte b5 = data[offset + 5];
            byte b6 = data[offset + 6];

            bool looksUnset = b1 == 0xD0 && b2 == 0xD0 && b3 == 0xD0
                           && b4 == 0xD0 && b5 == 0xD0 && b6 == 0xD0;
            if (looksUnset) return null;

            int yyHi   = BcdToInt(data[offset]);
            int yyLo   = BcdToInt(b1);
            int month  = BcdToInt(b2);
            int day    = BcdToInt(b3);
            int hour   = BcdToInt(b4);
            int minute = BcdToInt(b5);
            int second = BcdToInt(b6);

            if (yyHi < 0 || yyLo < 0 || month < 0 || day < 0 || hour < 0 || minute < 0 || second < 0)
                return null;

            int year = yyHi * 100 + yyLo;
            if (year < 1 || year > 9999) return null;
            if (month < 1 || month > 12) return null;
            if (day < 1 || day > 31)     return null;
            if (hour > 23)               return null;
            if (minute > 59)             return null;
            if (second > 59)             return null;

            try { return new DateTime(year, month, day, hour, minute, second, kind); }
            catch { return null; }
        }

        public static byte[] ConvertDateTimeToVendorBytes(DateTime? dateTime)
        {
            byte[] data = new byte[8];

            if (!dateTime.HasValue)
            {
                data[0] = 0x00;
                for (int i = 1; i < 7; i++) data[i] = 0xD0;
                data[7] = 0x00;
                return data;
            }

            DateTime dt = dateTime.Value;
            data[0] = IntToBcd(dt.Year / 100);
            data[1] = IntToBcd(dt.Year % 100);
            data[2] = IntToBcd(dt.Month);
            data[3] = IntToBcd(dt.Day);
            data[4] = IntToBcd(dt.Hour);
            data[5] = IntToBcd(dt.Minute);
            data[6] = IntToBcd(dt.Second);
            data[7] = 0x00;
            return data;
        }

        private static int BcdToInt(byte b)
        {
            int hi = (b >> 4) & 0x0F;
            int lo = b & 0x0F;
            if (hi > 9 || lo > 9) return -1;
            return hi * 10 + lo;
        }

        private static byte IntToBcd(int value)
        {
            return (byte)(((value / 10) << 4) | (value % 10));
        }
    }
}
