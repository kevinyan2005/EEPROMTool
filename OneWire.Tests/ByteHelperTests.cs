using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWire.Common;

namespace OneWire.Tests
{
    [TestFixture]
    public class ByteHelperTests
    {
        [Test]
        public void Verify_VendorExample_Read()
        {
            // Arrange: The specific example from your vendor
            byte[] input = { 0xE7, 0xD9, 0x00, 0x01 };
            uint expected = 124889;

            // Act
            uint actual = ByteHelper.ReadUInt32FromBytesWithWordSwap(input, 0);

            // Assert
            Assert.AreEqual(expected, actual, "The 32-bit conversion failed for the vendor example.");
        }

        [Test]
        public void ReadUInt32FromBytesWithShuffle_VendorByteOrder_ReturnsExpectedValue()
        {
            // Arrange: The specific example from your vendor
            byte[] input = { 0xE7, 0xD9, 0x00, 0x01 };
            uint expected = 124889;

            // Act
            uint actual = ByteHelper.ReadUInt32FromBytesWithShuffle(input);

            // Assert
            Assert.AreEqual(expected, actual, "The 32-bit conversion failed for the vendor example.");
        }

        [Test]
        public void Verify_VendorExample_Write()
        {
            // Arrange
            uint input = 124889;
            byte[] expected = { 0xE7, 0xD9, 0x00, 0x01 };

            // Act
            byte[] actual = ByteHelper.ConvertUInt32ToBytesWithWordSwap(input);

            // Assert
            Assert.AreEqual(expected, actual, "The write conversion did not produce the correct byte sequence.");
        }

        [Test]
        [TestCase((uint)124889, new byte[] { 0xE7, 0xD9, 0x00, 0x01 }, Description = "Original Vendor Example")]
        [TestCase((uint)6695, new byte[] { 0x1A, 0x27, 0x00, 0x00 }, Description = "Input 6695 Example")]
        [TestCase((uint)3253, new byte[] { 0x0C, 0xB5, 0x00, 0x00 }, Description = "Input 3253 Example")]
        [TestCase((uint)0, new byte[] { 0x00, 0x00, 0x00, 0x00 }, Description = "Zero value")]
        public void Verify_ConvertUInt32ToBytesWithWordSwap(uint input, byte[] expected)
        {
            // Act
            byte[] actual = ByteHelper.ConvertUInt32ToBytesWithWordSwap(input);

            // Assert
            // Using NUnit's Is.EqualTo for proper array content comparison
            Assert.That(actual, Is.EqualTo(expected),
                $"Conversion failed for input {input}. Expected {BitConverter.ToString(expected)}, but got {BitConverter.ToString(actual)}");
        }

        [Test]
        public void ReadVendorDateTime8OrNull_ValidDate_ParsesCorrectly()
        {
            // 20 24 11 20 01 05 03 00 => 2024-11-20 01:05:03
            byte[] data = { 0x20, 0x24, 0x11, 0x20, 0x01, 0x05, 0x03, 0x00 };

            DateTime? dt = ByteHelper.ReadVendorDateTimeOrNull(data, 0, DateTimeKind.Utc);

            Assert.That(dt.HasValue, Is.True);
            Assert.That(dt.Value, Is.EqualTo(new DateTime(2024, 11, 20, 1, 5, 3, DateTimeKind.Utc)));
        }

        [TestCase(
            new byte[] { 0xDD, 0xD0, 0xD0, 0xD0, 0xD0, 0xD0, 0xD0, 0x00 },
            TestName = "Unset_DD_D0_Pattern_ReturnsNull")]
        [TestCase(
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
            TestName = "All_FF_ReturnsNull")]
        public void ReadVendorDateTime8OrNull_InvalidInputs_ReturnNull(byte[] data)
        {
            DateTime? result = ByteHelper.ReadVendorDateTimeOrNull(
                data,
                offset: 0,
                kind: DateTimeKind.Utc);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Verify_Null_DateTime_Pattern()
        {
            // Act
            byte[] encoded = ByteHelper.ConvertDateTimeToVendorBytes(null);

            // Assert
            Assert.AreEqual(0xD0, encoded[1], "Unset date should use 0xD0 padding.");
            Assert.AreEqual(0xD0, encoded[6], "Unset date should use 0xD0 padding.");
        }

        [Test, TestCaseSource(nameof(DateTimeTestCases))]
        public void Verify_ConvertDateTimeToVendorBytes(DateTime? input, byte[] expected)
        {
            // Act
            byte[] actual = ByteHelper.ConvertDateTimeToVendorBytes(input);

            // Assert
            Assert.That(actual, Is.EqualTo(expected),
                $"Failed for {(input.HasValue ? input.Value.ToString() : "null")}. " +
                $"Expected: {BitConverter.ToString(expected)}, Got: {BitConverter.ToString(actual)}");
        }

        [TestCase()]
        public void Verify_DateTime_RoundTrip()
        {
            // Arrange
            DateTime original = new DateTime(2024, 10, 15, 14, 30, 05, DateTimeKind.Utc);

            // Act
            byte[] encoded = ByteHelper.ConvertDateTimeToVendorBytes(original);
            DateTime? decoded = ByteHelper.ReadVendorDateTimeOrNull(encoded, 0);

            // Assert
            Assert.IsNotNull(decoded);
            Assert.AreEqual(original, decoded.Value, "The date should match after encoding/decoding.");
            Assert.AreEqual(0x24, encoded[1], "Year Low should be 0x24 in BCD.");
        }

        private static IEnumerable<TestCaseData> DateTimeTestCases
        {
            get
            {
                // Case 1: Standard Date (Oct 15, 2024, 14:30:05)
                yield return new TestCaseData(
                    new DateTime(2024, 10, 15, 14, 30, 05, DateTimeKind.Utc),
                    new byte[] { 0x20, 0x24, 0x10, 0x15, 0x14, 0x30, 0x05, 0x00 }
                ).SetDescription("Standard BCD Date Conversion");

                // Case 2: New Year's Eve (Dec 31, 2025, 23:59:59)
                yield return new TestCaseData(
                    new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                    new byte[] { 0x20, 0x25, 0x12, 0x31, 0x23, 0x59, 0x59, 0x00 }
                ).SetDescription("End of Year Boundary Check");

                // Case 3: Single digit BCD (Jan 02, 2026, 03:04:09)
                yield return new TestCaseData(
                    new DateTime(2026, 1, 2, 3, 4, 9, DateTimeKind.Utc),
                    new byte[] { 0x20, 0x26, 0x01, 0x02, 0x03, 0x04, 0x09, 0x00 }
                ).SetDescription("Single Digit Padding (BCD)");

                // Case 4: Null / Unset (The 0xD0 Vendor Pattern)
                yield return new TestCaseData(
                    (DateTime?)null,
                    new byte[] { 0x00, 0xD0, 0xD0, 0xD0, 0xD0, 0xD0, 0xD0, 0x00 }
                ).SetDescription("Null Input - Vendor Unset Pattern");
            }
        }

    }
}
