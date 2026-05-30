namespace OneWire.Core
{
    public interface IBlockWithCrc
    {
        ushort Crc16 { get; set; }
        bool ValidateCrc();
        void RecalculateCrc();
    }
}
