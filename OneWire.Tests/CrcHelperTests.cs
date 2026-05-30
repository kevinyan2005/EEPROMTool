using System;
using System.Text;
using NUnit.Framework;
using OneWire.Common;

namespace OneWire.Tests
{
    [TestFixture]
    public class CrcHelperTests
    {
        [Test]
        public void ComputeCrc8_EmptyArray_ReturnsZero()
        {
            // Arrange
            byte[] data = Array.Empty<byte>();

            // Act
            byte actual = CrcHelper.ComputeCrc8(data);

            // Assert
            Assert.AreEqual(0x00, actual);
        }

        [Test]
        public void ComputeCrc8_SingleByte_ReturnsExpected()
        {
            // Arrange
            byte[] data = { 0xA5 };
            byte expected = 0x90; // CRC8 of 0xA5 should be 0x90

            // Act
            byte actual = CrcHelper.ComputeCrc8(data);

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ComputeCrc8_SingleByteFF_ReturnsExpected()
        {
            // Arrange
            byte[] data = { 0xFF };
            byte expected = 0x35; // Verified using Dallas/Maxim CRC8 calculator

            // Act
            byte actual = CrcHelper.ComputeCrc8(data);

            // Assert
            Assert.AreEqual(expected, actual, $"CRC8 mismatch for data: expected 0x{expected:X2} but got 0x{actual:X2}");
        }
        
        [TestCase(new byte[] { 0x81, 0x6A, 0x88, 0x3F, 0x00, 0x00, 0x00 }, 0x81)]
        [TestCase(new byte[] { 0x2D, 0xDE, 0x1A, 0x2E, 0x3C, 0x00, 0x00 }, 0xC1)]
        public void ComputeCrc8_ReturnsExpected(byte[] data, byte expected)
        {
            // Act
            byte actual = CrcHelper.ComputeCrc8(data);

            // Assert
            Assert.AreEqual(expected, actual, $"CRC8 mismatch for data: expected 0x{expected:X2} but got 0x{actual:X2}");
        }


        [Test]
        public void ComputeCrc16DallasMaxim_EmptyArray_ReturnsZero()
        {
            // Arrange
            byte[] data = new byte[0];

            // Act
            ushort crc = CrcHelper.ComputeCrc16DallasMaxim(data, data.Length);

            // Assert
            Assert.AreEqual(0x0000, crc);
        }

        [Test]
        public void ComputeCrc16DallasMaxim_SingleByte_ReturnsExpectedValue()
        {
            // Arrange
            byte[] data = { 0xA5 };
            ushort expected = 0x7BC0; // verified expected CRC16 for [0xA5]

            // Act
            ushort actual = CrcHelper.ComputeCrc16DallasMaxim(data, data.Length);

            // Assert
            Assert.AreEqual(expected, actual, $"CRC16 mismatch for data, expected 0x{expected:X4} but got 0x{actual:X4}");
        }

        [TestCase(new byte[] { 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x55,
                                    0xAA, 0xFF, 0x11, 0x22, 0x33, 0x44, 0x99, 0x77 }, (ushort)0x96B9)]
        [TestCase(new byte[] { 0x00, 0x01, 0x4F, 0x58, 0x4F, 0x57, 0x2D, 0x30,
                                    0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                    0x00, 0x00, 0x00, 0x00, 0x4C, 0x32, 0x32, 0x32,
                                    0x2D, 0x32, 0x31, 0x39, 0x34, 0x2D, 0x30, 0x32,
                                    0x00, 0x00, 0x00, 0x00}, (ushort)0x05C3)]

        [TestCase(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0E, 0x03, 0x00, 0x00, 0x1A, 0xB2,
            0x00, 0x00, 0x1E, 0x43, 0x00, 0x00, 0xFF, 0xFF,
            0xFF, 0xFF, 0x20, 0x24, 0x11, 0x19, 0x12, 0x00,
            0x00, 0x00, 0xDD, 0xD0, 0xD0, 0xD0, 0xD0, 0xD0, 
            0xD0, 0x00, 0x50, 0x6E}, (ushort)0x036C)]
        [TestCase(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0E, 0x03, 0x00, 0x00, 0x1A, 0xB2,
            0x00, 0x00, 0x1E, 0x43, 0x00, 0x00, 0xFF, 0xFF,
            0xFF, 0xFF, 0x20, 0x24, 0x11, 0x19, 0x12, 0x00,
            0x00, 0x00, 0xDD, 0xD0, 0xD0, 0xD0, 0xD0, 0xD0, 
            0xD0, 0x00, 0x54, 0x31}, (ushort)0xFB2E)]

        public void ComputeCrc16DallasMaxim_ReturnsExpected(byte[] data, ushort expected)
        {
            // Act
            ushort actual = CrcHelper.ComputeCrc16DallasMaxim(data, data.Length);

            // Assert
            Assert.AreEqual(expected, actual,
                $"CRC16 mismatch for data, expected 0x{expected:X4} but got 0x{actual:X4}");
        }

    }

    [TestFixture]
    public class Crc16CcittTests
    {
        // Standard check value: CRC-CCITT (init=0xFFFF) of ASCII "123456789" = 0x29B1
        private static readonly byte[] CheckSequence = Encoding.ASCII.GetBytes("123456789");

        [Test]
        public void ComputeCrc16CcittByTable_EmptyData_ReturnsInitialValue()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByTable(Array.Empty<byte>(), 0);
            Assert.AreEqual(0xFFFF, result);
        }

        [Test]
        public void ComputeCrc16CcittByTable_EmptyData_CustomInitial_ReturnsInitialValue()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByTable(Array.Empty<byte>(), 0, 0x0000);
            Assert.AreEqual(0x0000, result);
        }

        [Test]
        public void ComputeCrc16CcittByTable_CheckSequence_DefaultInit_Returns0x29B1()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByTable(CheckSequence, CheckSequence.Length);
            Assert.AreEqual(0x29B1, result, $"Expected 0x29B1 but got 0x{result:X4}");
        }

        [Test]
        public void ComputeCrc16CcittByTable_CheckSequence_XmodemInit_Returns0x31C3()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByTable(CheckSequence, CheckSequence.Length, 0x0000);
            Assert.AreEqual(0x31C3, result, $"Expected 0x31C3 but got 0x{result:X4}");
        }

        [TestCase(new byte[] { 0x00 }, (ushort)0xE1F0, Description = "Single zero byte, init=0xFFFF")]
        [TestCase(new byte[] { 0xFF }, (ushort)0xFF00, Description = "Single 0xFF byte, init=0xFFFF")]
        public void ComputeCrc16CcittByTable_KnownInputs_ReturnExpected(byte[] data, ushort expected)
        {
            ushort result = CrcHelper.ComputeCrc16CcittByTable(data, data.Length);
            Assert.AreEqual(expected, result, $"Expected 0x{expected:X4} but got 0x{result:X4}");
        }

        [Test]
        public void ComputeCrc16CcittByTable_LengthLessThanArray_OnlyProcessesSpecifiedBytes()
        {
            byte[] data = CheckSequence;
            ushort partial = CrcHelper.ComputeCrc16CcittByTable(data, 3);
            ushort full    = CrcHelper.ComputeCrc16CcittByTable(data, data.Length);
            Assert.AreNotEqual(partial, full);
        }
    }

    [TestFixture]
    public class Crc16CcittPregenTableTests
    {
        private static readonly byte[] CheckSequence = Encoding.ASCII.GetBytes("123456789");

        [Test]
        public void ComputeCrc16CcittByPregenTable_EmptyData_ReturnsInitialValue()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByPregenTable(Array.Empty<byte>(), 0);
            Assert.AreEqual(0xFFFF, result);
        }

        [Test]
        public void ComputeCrc16CcittByPregenTable_EmptyData_CustomInitial_ReturnsInitialValue()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByPregenTable(Array.Empty<byte>(), 0, 0x0000);
            Assert.AreEqual(0x0000, result);
        }

        [Test]
        public void ComputeCrc16CcittByPregenTable_CheckSequence_DefaultInit_Returns0x29B1()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByPregenTable(CheckSequence, CheckSequence.Length);
            Assert.AreEqual(0x29B1, result, $"Expected 0x29B1 but got 0x{result:X4}");
        }

        [Test]
        public void ComputeCrc16CcittByPregenTable_CheckSequence_XmodemInit_Returns0x31C3()
        {
            ushort result = CrcHelper.ComputeCrc16CcittByPregenTable(CheckSequence, CheckSequence.Length, 0x0000);
            Assert.AreEqual(0x31C3, result, $"Expected 0x31C3 but got 0x{result:X4}");
        }

        [TestCase(new byte[] { 0x00 }, (ushort)0xE1F0, Description = "Single zero byte, init=0xFFFF")]
        [TestCase(new byte[] { 0xFF }, (ushort)0xFF00, Description = "Single 0xFF byte, init=0xFFFF")]
        public void ComputeCrc16CcittByPregenTable_KnownInputs_ReturnExpected(byte[] data, ushort expected)
        {
            ushort result = CrcHelper.ComputeCrc16CcittByPregenTable(data, data.Length);
            Assert.AreEqual(expected, result, $"Expected 0x{expected:X4} but got 0x{result:X4}");
        }

        [Test]
        public void ComputeCrc16CcittByPregenTable_LengthLessThanArray_OnlyProcessesSpecifiedBytes()
        {
            byte[] data = CheckSequence;
            ushort partial = CrcHelper.ComputeCrc16CcittByPregenTable(data, 3);
            ushort full    = CrcHelper.ComputeCrc16CcittByPregenTable(data, data.Length);
            Assert.AreNotEqual(partial, full);
        }
    }
}
