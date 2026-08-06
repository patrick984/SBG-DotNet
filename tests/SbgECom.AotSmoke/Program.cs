using System.Buffers.Binary;
using SbgECom.BinaryMessages;
using SbgECom.Protocol;

var payload = new byte[14];
BinaryPrimitives.WriteUInt32LittleEndian(payload, 42);
BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), 3);
BinaryPrimitives.WriteInt32LittleEndian(
    payload.AsSpan(6),
    BitConverter.SingleToInt32Bits(101325.5f)
);
BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(10), BitConverter.SingleToInt32Bits(12.25f));

var wireFrame = SbgFrameCodec.Encode(0, (byte)BinaryLog.Depth, payload);
var decoded = new SbgStreamDecoder().Append(wireFrame).Single();
var log = (DepthLog)
    BinaryLogParser.Parse(decoded.MessageClass, decoded.MessageId, decoded.Payload.Span);
var csv = CsvMessageSerializer.Serialize([decoded]);

if (
    log.Depth != 12.25f
    || !log.ToUserReadableString().Contains("Depth=12.25", StringComparison.Ordinal)
    || !csv.Contains(",12.25,m", StringComparison.Ordinal)
)
{
    return 1;
}

Console.WriteLine("Native AOT smoke test passed.");
return 0;
