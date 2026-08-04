using System.Collections.Generic;

namespace OneWire.Core
{
    public enum ProbeTypeEnum
    {
        SideFire33,
        FullFire33,
        FullFire16
    }

    public static class ProbeTypeExtensions
    {
        public static readonly IReadOnlyDictionary<ProbeTypeEnum, string> PartNumbers =
            new Dictionary<ProbeTypeEnum, string>
            {
                { ProbeTypeEnum.FullFire33, "20893" },
                { ProbeTypeEnum.FullFire16, "22002" },
                { ProbeTypeEnum.SideFire33, "20891" },
            };

        public static string ToPartNumber(this ProbeTypeEnum probe) =>
            PartNumbers.TryGetValue(probe, out var pn) ? pn : string.Empty;

        public static ProbeTypeEnum? FromPartNumber(string partNumber)
        {
            foreach (var kvp in PartNumbers)
                if (kvp.Value == partNumber) return kvp.Key;
            return null;
        }

        /// <summary>
        /// FullFire33 and SideFire33 include a probe-length segment in the serial number;
        /// FullFire16 does not.
        /// </summary>
        public static bool HasProbeLength(this ProbeTypeEnum probe) =>
            probe == ProbeTypeEnum.FullFire33 || probe == ProbeTypeEnum.SideFire33;
    }
}
