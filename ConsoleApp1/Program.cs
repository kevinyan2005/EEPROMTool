using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using OneWire.Common;
using OneWireController;
using slf4net;


namespace ConsoleApp1
{
    public class Program
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(ConsoleApp1));

        private static DS2431Helper _helper;

        static void Main(string[] args)
        {
            Logger.Info("Starting app...");

            if (args.Length == 0)
            {
                Console.WriteLine("Please pass 0 or 1 as a command-line argument.");
                return;
            }

            int value;
            if (!int.TryParse(args[0], out value))
            {
                Console.WriteLine("Argument must be an integer (0 or 1).");
                return;
            }

            _helper = new DS2431Helper("USB1");
            //Start at standard speed
            _helper.Connect();
            _helper.OWReset();


            if (value == 0)
            {
                Logger.Info("Standard speed.");
                TestDriveStandardSpeed();
            }
            else if (value == 1)
            {
                Logger.Info("Override speed.");
                TestDriveOverdriveSpeed();
            }
            else
            {
                Console.WriteLine("Argument must be 0 or 1.");
            }
        }
        
        private static void TestDriveStandardSpeed()
        {
            Logger.Info("Read memory standard speed...");
            var stopwatch = Stopwatch.StartNew();
            var data = _helper.ReadMemory(0, 128);
            stopwatch.Stop();
            Logger.Info($"ReadMemory took {stopwatch.ElapsedMilliseconds:N0} ms.");
            Dump(data, 8);

            var eepromImage = CreateEepromData();

            Logger.Info("Write memory standard speed...");
            stopwatch = Stopwatch.StartNew();
            _helper.WriteMemory(0, eepromImage);
            stopwatch.Stop();
            Logger.Info($"WriteMemory took {stopwatch.ElapsedMilliseconds:N0} ms.");
            
            Thread.Sleep(1000);
            Logger.Info("Read memory...");
            data = _helper.ReadMemory(0, 128);
            Dump(data, 8);
        }

        private static void TestDriveOverdriveSpeed()
        {
            //Switch to Overdrive speed for read/write
            _helper.EnterOverdrive();
            Logger.Info("Read memory override speed...");
            var stopwatch = Stopwatch.StartNew();
            var data = _helper.ReadMemoryOverdrive(0, 128);
            stopwatch.Stop();
            Logger.Info($"ReadMemory took {stopwatch.ElapsedMilliseconds:N0} ms.");
            Dump(data, 8);

            var eepromImage = CreateEepromData();

            Logger.Info("Write memory override speed...");
            stopwatch = Stopwatch.StartNew();
            _helper.WriteMemoryOverdriveSpeed(0, eepromImage);

            stopwatch.Stop();
            Logger.Info($"WriteMemory took {stopwatch.ElapsedMilliseconds:N0} ms.");


            Thread.Sleep(1000);
            Logger.Info("Read memory...");
            data = _helper.ReadMemoryOverdrive(0, 128);
            Dump(data, 8);
        }

        private static byte[] CreateEepromData()
        {
            var identBlock = new OneWireIdentificationBlock
            {
                DataVersion = 1,
                DataId = "OX",
                Model = "DS2431-DEMO-xxxx",       //16 bytes 
                SerialNumber = "SN1234567890abcd" //16 bytes
            };

            byte[] identBytes = identBlock.ToBytes();

            Logger.Info("Identification Block with CRC:");
            Logger.Info(BitConverter.ToString(identBytes));
            Logger.Info($"CRC16 = 0x{identBlock.Crc16:X4}");

            var calibBlock = new SensorCalibrationBlock
            {
                GaugeFactors = new uint[] { 16000, 3253, 6695, 7753 },
                ReferenceValue = 50,
                ManufactureDate = new DateTime(2023, 1, 15),
                ExpiryDate = new DateTime(2026, 1, 15),
                //GaugeType = 2
                GaugeType = "Pn"
            };

            byte[] calBytes = calibBlock.ToBytes();
            Logger.Info("Calibration Block with CRC:");
            Logger.Info(BitConverter.ToString(calBytes));
            Logger.Info($"CRC16 = 0x{calibBlock.Crc16:X4}");

            var userBlock = new UserDefinedBlock
            {
                Schema = 1,
                ProbeSerialNumber = "20891-1-DV001-01",
                ProbeExpiryDate = new DateTime(2022, 6, 10),
                ProbeUsageDate = DateTime.MinValue
            };

            byte[] userBytes = userBlock.ToBytes();

            Logger.Info("User Defined Block with CRC:");
            Logger.Info(BitConverter.ToString(userBytes));
            Logger.Info($"CRC16 = 0x{userBlock.Crc16:X4}");

            byte[] eepromImage = ByteHelper.ConcatenateWithPadding(identBytes, calBytes, userBytes);

            Dump(eepromImage, 8);
            return eepromImage;
        }


        private static void Dump(byte[] data, int bytesPerLine = 16)
        {
            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                // Format address offset
                var line = $"{i:X4}: ";
                // Append up to bytesPerLine hex bytes
                for (int j = 0; j < bytesPerLine && i + j < data.Length; j++)
                {
                    line += $"{data[i + j]:X2} ";
                }
                // Log the line at Info level (adjust level if needed)
                Logger.Info(line);
            }
        }
        //private static void Dump(byte[] data, int bytesPerLine = 16)
        //{
        //    for (int i = 0; i < data.Length; i += bytesPerLine)
        //    {
        //        // Print address offset
        //        Console.Write($"{i:X4}: ");

        //        // Print up to N bytes
        //        for (int j = 0; j < bytesPerLine && i + j < data.Length; j++)
        //        {
        //            Console.Write($"{data[i + j]:X2} ");
        //        }

        //        Console.WriteLine();
        //    }
        //}

        private static void PrintArray(byte[] data)
        {
            for (int i = 0; i < data.Length; i += 8)
            {
                var line = data.Skip(i).Take(8)
                    .Select(b => b.ToString("X2"))
                    .ToArray();

                Console.WriteLine(string.Join(" ", line));

            }
        }
    }

}
