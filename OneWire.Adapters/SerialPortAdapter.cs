using mmi.HcbProtocol;
using OneWire.Common;
using slf4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneWire.Adapters
{
    /// <summary>
    /// IOneWireAdapter that communicates via an <see cref="IMrpcbProtocolClient"/>.
    /// Read: sends a heartbeat with a Read function code and awaits <see cref="IMrpcbProtocolClient.HeartbeatResponseReceived"/>.
    /// Write: sends a heartbeat with a Write function code and data, awaits acknowledgement.
    /// </summary>
    public class SerialPortAdapter : IOneWireAdapter
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(SerialPortAdapter));

        private readonly IMrpcbProtocolClient _client;
        private readonly string _portName;

        private const int EepromSize = 128;
        private const int HeartbeatTimeoutMs = 5000;
        private const int MrpcbFunctionCodeOffset = 18;
        private const int MrpcbHeartbeatResponsePayloadMinLength = 17;

        private MrpcbFunctionCode? _lastRequestedEepromReadFunction;
        private MrpcbFunctionCode? _lastRequestedEepromWriteFunction;

        private TaskCompletionSource<byte[]> _pendingReadTcs;
        private TaskCompletionSource<bool> _pendingWriteTcs;

        public SerialPortAdapter(string portName, IMrpcbProtocolClient client)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _client.Configure(portName, 460800);
            _client.HeartbeatResponseReceived += OnHeartbeatResponseReceived;
        }

        // --- Persistent heartbeat handler ---

        private void OnHeartbeatResponseReceived(byte[] payload)
        {
            if (!IsValidHeartbeatPayload(payload))
                return;

            ParseHeartbeatPayload(payload, out var eepromData, out var writeAcknowledged);

            if (eepromData != null)
                _pendingReadTcs?.TrySetResult(eepromData);

            if (writeAcknowledged)
                _pendingWriteTcs?.TrySetResult(true);
        }

        private void ParseHeartbeatPayload(byte[] payload, out byte[] eepromData, out bool writeAcknowledged)
        {
            TryExtractEepromData(payload, out eepromData, out writeAcknowledged);
        }

        

        private void TryExtractEepromData(byte[] payload, out byte[] eepromData, out bool writeAcknowledged)
        {
            eepromData = null;
            writeAcknowledged = false;

            if (payload.Length <= MrpcbFunctionCodeOffset)
                return;

            var functionCodeRaw = payload[MrpcbFunctionCodeOffset];
            var functionCode = Enum.IsDefined(typeof(MrpcbFunctionCode), (int)functionCodeRaw)
                ? (MrpcbFunctionCode)functionCodeRaw
                : MrpcbFunctionCode.No_Action;

            if (functionCode == MrpcbFunctionCode.No_Action)
                return;

            if (payload.Length <= MrpcbFunctionCodeOffset + 3)
            {
                Logger.Warn($"Heartbeat MRPCB function block is incomplete. RawFunctionCode={functionCodeRaw}, PayloadLength={payload.Length}.");
                return;
            }

            var declaredSize = payload[MrpcbFunctionCodeOffset + 1];
            var address = (ushort)(payload[MrpcbFunctionCodeOffset + 2] | (payload[MrpcbFunctionCodeOffset + 3] << 8));
            var dataOffset = MrpcbFunctionCodeOffset + 4;
            var availableDataLength = Math.Max(0, payload.Length - dataOffset);
            var dataLength = Math.Min(declaredSize, availableDataLength);
            var data = new byte[dataLength];

            if (dataLength > 0)
                Buffer.BlockCopy(payload, dataOffset, data, 0, dataLength);

            if (availableDataLength < declaredSize)
                Logger.Warn($"Heartbeat MRPCB function block data truncated. Function={functionCode}({functionCodeRaw}), DeclaredSize={declaredSize}, Available={availableDataLength}.");

            var dataHex = dataLength > 0 ? string.Join(", ", data.Select(b => $"0x{b:X2}")) : "<none>";

            switch (functionCode)
            {
                case MrpcbFunctionCode.Read_FOTS_EEPROM:
                case MrpcbFunctionCode.Read_RPD_EEPROM:
                case MrpcbFunctionCode.Read_Engine_EEPROM:
                    if (TryGetValidEepromData(functionCode, data, out var validEepromData))
                        eepromData = validEepromData;
                    Logger.Info($"MRPCB EEPROM read response. Function={functionCode}, Address=0x{address:X4}, Size={declaredSize}, Data={dataHex}");
                    break;

                case MrpcbFunctionCode.Write_FOTS_EEPROM:
                case MrpcbFunctionCode.Write_RPD_EEPROM:
                case MrpcbFunctionCode.Write_Engine_EEPROM:
                    if (_lastRequestedEepromWriteFunction.HasValue && _lastRequestedEepromWriteFunction.Value == functionCode)
                        writeAcknowledged = true;
                    else
                        Logger.Warn($"Ignoring stale or unexpected EEPROM write response. Requested={_lastRequestedEepromWriteFunction}, Received={functionCode}.");
                    Logger.Info($"MRPCB EEPROM write response. Function={functionCode}, Address=0x{address:X4}, Size={declaredSize}, Data={dataHex}");
                    break;

                case MrpcbFunctionCode.Reset_MRPCB:
                    Logger.Info($"MRPCB reset response. Address=0x{address:X4}, Size={declaredSize}, Data={dataHex}");
                    break;

                default:
                    Logger.Warn($"Heartbeat MRPCB function block has unknown function code. RawFunctionCode={functionCodeRaw}, Address=0x{address:X4}, Size={declaredSize}, Data={dataHex}");
                    break;
            }
        }

        private bool TryGetValidEepromData(MrpcbFunctionCode responseFunctionCode, byte[] data, out byte[] eepromData)
        {
            eepromData = null;

            if (!MrpcbFunctionPayloadBuilder.IsEepromReadFunction(responseFunctionCode))
                return false;

            if (data == null || data.Length != EepromSize)
            {
                Logger.Warn($"Ignoring EEPROM response due to invalid length. Function={responseFunctionCode}, Length={data?.Length ?? 0}, Expected={EepromSize}.");
                return false;
            }

            if (_lastRequestedEepromReadFunction.HasValue && _lastRequestedEepromReadFunction.Value != responseFunctionCode)
            {
                Logger.Warn($"Ignoring stale EEPROM response. Requested={_lastRequestedEepromReadFunction.Value}, Received={responseFunctionCode}.");
                return false;
            }

            eepromData = new byte[EepromSize];
            Buffer.BlockCopy(data, 0, eepromData, 0, EepromSize);
            return true;
        }

        private static bool IsValidHeartbeatPayload(byte[] payload) =>
            payload != null && payload.Length >= MrpcbHeartbeatResponsePayloadMinLength;

        // --- IOneWireAdapter ---

        public void Connect()
        {
            Logger.Info($"SerialPortAdapter: opening {_portName}");
            if (!_client.Open())
                throw new InvalidOperationException(
                    $"Failed to open serial port '{_portName}'. Check the port name and that no other process holds it.");
            Logger.Info("SerialPortAdapter: connected");
        }

        public void Disconnect()
        {
            Logger.Info("SerialPortAdapter: disconnecting");
            _client.Close();
            Logger.Info("SerialPortAdapter: disconnected");
        }

        public void Reset()
        {
            Logger.Debug("SerialPortAdapter: Reset (no-op — managed by protocol client)");
        }

        public bool OWReset()
        {
            Logger.Debug("SerialPortAdapter: OWReset (delegated to protocol client)");
            return true;
        }

        public void EnterOverdrive()
        {
            Logger.Warn("SerialPortAdapter: Overdrive not supported by MRPCB protocol; ignoring.");
        }

        public void EnterStandard()
        {
            Logger.Debug("SerialPortAdapter: EnterStandard (no-op)");
        }

        public async Task<byte[]> ReadEntireMemoryAsync(bool overdrive = false, IProgress<int>? progress = null)
        {
            Logger.Info("SerialPortAdapter: ReadEntireMemoryAsync");
            progress?.Report(0);

            MrpcbFunctionCode functionCode = MrpcbFunctionCode.Read_FOTS_EEPROM;
            _lastRequestedEepromReadFunction = functionCode;

            var relevantBytes = MrpcbFunctionPayloadBuilder.Build(
                functionCode,
                MrpcbFunctionPayloadBuilder.FixedEepromReadAddress,
                data: null,
                MrpcbFunctionPayloadBuilder.FixedEepromReadSize);

            _pendingReadTcs = new TaskCompletionSource<byte[]>();
            _client.SendHeartbeatCmd(BuildProgrammableOutput(functionCode, relevantBytes));

            if (await Task.WhenAny(_pendingReadTcs.Task, Task.Delay(HeartbeatTimeoutMs)) != _pendingReadTcs.Task)
                throw new TimeoutException($"No EEPROM read response from device within {HeartbeatTimeoutMs} ms.");

            progress?.Report(100);
            return _pendingReadTcs.Task.Result;
        }

        public async Task WriteMemoryAsync(ushort address, byte[] data, bool overdrive = false, IProgress<int>? progress = null)
        {
            Logger.Info($"SerialPortAdapter: WriteMemoryAsync address=0x{address:X4} length={data.Length}");
            progress?.Report(0);

            MrpcbFunctionCode functionCode = MrpcbFunctionCode.Write_FOTS_EEPROM;
            _lastRequestedEepromWriteFunction = functionCode;

            var relevantBytes = MrpcbFunctionPayloadBuilder.Build(functionCode, address, data, data.Length);

            _pendingWriteTcs = new TaskCompletionSource<bool>();
            _client.SendHeartbeatCmd(BuildProgrammableOutput(functionCode, relevantBytes));

            if (await Task.WhenAny(_pendingWriteTcs.Task, Task.Delay(HeartbeatTimeoutMs)) != _pendingWriteTcs.Task)
                throw new TimeoutException($"No EEPROM write acknowledgement from device within {HeartbeatTimeoutMs} ms.");

            progress?.Report(100);
        }

        // --- Frame builder ---

        private byte[] BuildProgrammableOutput(MrpcbFunctionCode mrpcbFunctionCode, byte[] mrpcbRelevantBytes)
        {
            var result = new List<byte>();

            byte mrpcbDigitalOutputByte = 0x02;
            result.Add(mrpcbDigitalOutputByte);

            byte[] mrpcbLedBytes =
            {
                0x10,
                0xFF, 0x00, 0x00,
                //0x00, 0xFF, 0x00,
                //0x00, 0x00, 0xFF,
                //0x00, 0x00, 0x00,
                //0xFF, 0xFF, 0xFF,
            };
            result.AddRange(mrpcbLedBytes);

            result.Add((byte)mrpcbFunctionCode);
            if (mrpcbRelevantBytes != null && mrpcbRelevantBytes.Length > 0)
                result.AddRange(mrpcbRelevantBytes);

            return result.ToArray();
        }
    }
}
