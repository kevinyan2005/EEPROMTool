using slf4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OneWire.UI.Wpf
{
    public class SerialPortDetector
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(typeof(SerialPortDetector));

        public static string[] AutoDetectActiveSerialPorts()
        {
            string[] result;
            try
            {
                var discoveredPorts = new Dictionary<string, string>();
                using (var managementObjectSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
                {
                    try
                    {
                        var source = managementObjectSearcher.Get().Cast<ManagementBaseObject>().ToList();
                        foreach (var queryObj in source)
                        {
                            var caption = queryObj["Caption"] as string;
                            if (string.IsNullOrWhiteSpace(caption))
                            {
                                continue;
                            }

                            var portName = Regex.Match(caption, @"\((COM([^)]*))\)").Groups[1].Value;
                            var description = queryObj["Description"] as string;
                            if (string.IsNullOrWhiteSpace(description))
                            {
                                continue;
                            }

                            discoveredPorts[portName] = description;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error encountered during hardware enumeration ");
                    }
                }

                result = discoveredPorts.Keys
                    .Select(portName => $"{portName}: {discoveredPorts[portName]}")
                    .ToArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to get active serial ports");
                result = null;
            }

            return result;
        }
    }
}
