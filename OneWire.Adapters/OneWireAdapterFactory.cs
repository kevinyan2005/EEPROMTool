using System;
using mmi.HcbProtocol;
using OneWire.Common;

namespace OneWire.Adapters
{
    public static class OneWireAdapterFactory
    {
        public static IOneWireAdapter Create(AdapterType type, string port)
        {
            switch (type)
            {
                case AdapterType.DS9490:
                    return new DS9490Adapter(port);
                case AdapterType.Mock:
                    return new MockAdapter();
                case AdapterType.HCB:
                    throw new NotImplementedException("HCB adapter is not yet implemented.");
                case AdapterType.DCT:
                    throw new NotImplementedException("DCT adapter is not yet implemented.");
                case AdapterType.MRPCB:
                    return new SerialPortAdapter(port, new MrpcbProtocolClient(port));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown adapter type.");
            }
        }
    }
}
