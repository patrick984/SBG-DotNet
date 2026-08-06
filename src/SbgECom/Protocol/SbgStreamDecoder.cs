using System.Buffers.Binary;

namespace SbgECom.Protocol;

/// <summary>Incremental, noise-tolerant decoder with transparent extended-frame reassembly.</summary>
public sealed class SbgStreamDecoder
{
    private readonly List<byte> _buffer = [];
    private LargeTransfer? _transfer;

    public event EventHandler<DecodeErrorEventArgs>? DecodeFailed;

    public IReadOnlyList<SbgMessage> Append(ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty)
            _buffer.AddRange(data.ToArray());
        var messages = new List<SbgMessage>();
        while (TryTakeFrame(out var frame))
        {
            if (!frame.IsExtended)
                messages.Add(new(frame.MessageClass, frame.MessageId, frame.Payload));
            else if (ProcessExtended(frame) is { } message)
                messages.Add(message);
        }
        return messages;
    }

    public void Reset()
    {
        _buffer.Clear();
        _transfer = null;
    }

    private bool TryTakeFrame(out Frame frame)
    {
        frame = default;
        while (true)
        {
            var sync = FindSync();
            if (sync < 0)
            {
                if (_buffer.Count > 0 && _buffer[^1] == SbgFrameCodec.Sync1)
                    _buffer.RemoveRange(0, _buffer.Count - 1);
                else
                    _buffer.Clear();
                return false;
            }
            if (sync > 0)
                _buffer.RemoveRange(0, sync);
            if (_buffer.Count < 6)
                return false;
            var wirePayloadSize = _buffer[4] | (_buffer[5] << 8);
            if (wirePayloadSize > SbgFrameCodec.MaximumPayloadSize)
            {
                Fail(DecodeError.InvalidFrame, Math.Min(_buffer.Count, 6));
                _buffer.RemoveRange(0, 2);
                continue;
            }
            var frameSize = wirePayloadSize + 9;
            if (_buffer.Count < frameSize)
                return false;
            var bytes = _buffer.GetRange(0, frameSize).ToArray();
            if (bytes[^1] != SbgFrameCodec.Etx)
            {
                Fail(DecodeError.InvalidFrame, frameSize);
                _buffer.RemoveRange(0, 2);
                continue;
            }
            var expected = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(frameSize - 3));
            var actual = Crc16.Compute(bytes.AsSpan(2, wirePayloadSize + 4));
            if (expected != actual)
            {
                Fail(DecodeError.InvalidCrc, frameSize);
                _buffer.RemoveRange(0, 2);
                continue;
            }
            _buffer.RemoveRange(0, frameSize);
            var messageClass = bytes[3];
            var extended = (messageClass & 0x80) != 0;
            if (extended)
            {
                if (wirePayloadSize < 5)
                {
                    DecodeFailed?.Invoke(this, new(DecodeError.InvalidExtendedFrame, bytes));
                    continue;
                }
                var transferId = (byte)(bytes[6] & 0x0f);
                var page = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(7));
                var pages = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(9));
                if (pages == 0 || page >= pages)
                {
                    DecodeFailed?.Invoke(this, new(DecodeError.InvalidExtendedFrame, bytes));
                    continue;
                }
                frame = new(
                    (byte)(messageClass & 0x7f),
                    bytes[2],
                    bytes.AsMemory(11, wirePayloadSize - 5),
                    true,
                    transferId,
                    page,
                    pages
                );
            }
            else
                frame = new(
                    messageClass,
                    bytes[2],
                    bytes.AsMemory(6, wirePayloadSize),
                    false,
                    0,
                    0,
                    0
                );
            return true;
        }
    }

    private SbgMessage? ProcessExtended(Frame frame)
    {
        if (frame.PageIndex == 0)
            _transfer = new(frame.MessageClass, frame.MessageId, frame.TransferId, frame.PageCount);
        if (_transfer is null || !_transfer.Matches(frame) || frame.PageIndex != _transfer.NextPage)
        {
            DecodeFailed?.Invoke(this, new(DecodeError.ExtendedFrameOutOfSequence, frame.Payload));
            _transfer = null;
            return null;
        }
        _transfer.Payload.AddRange(frame.Payload.ToArray());
        _transfer.NextPage++;
        if (_transfer.NextPage != _transfer.PageCount)
            return null;
        var message = new SbgMessage(
            _transfer.MessageClass,
            _transfer.MessageId,
            _transfer.Payload.ToArray()
        );
        _transfer = null;
        return message;
    }

    private int FindSync()
    {
        for (var i = 0; i + 1 < _buffer.Count; i++)
            if (_buffer[i] == SbgFrameCodec.Sync1 && _buffer[i + 1] == SbgFrameCodec.Sync2)
                return i;
        return -1;
    }

    private void Fail(DecodeError error, int length) =>
        DecodeFailed?.Invoke(this, new(error, _buffer.GetRange(0, length).ToArray()));

    private readonly record struct Frame(
        byte MessageClass,
        byte MessageId,
        ReadOnlyMemory<byte> Payload,
        bool IsExtended,
        byte TransferId,
        ushort PageIndex,
        ushort PageCount
    );

    private sealed class LargeTransfer(
        byte messageClass,
        byte messageId,
        byte transferId,
        ushort pageCount
    )
    {
        public byte MessageClass { get; } = messageClass;
        public byte MessageId { get; } = messageId;
        public byte TransferId { get; } = transferId;
        public ushort PageCount { get; } = pageCount;
        public ushort NextPage { get; set; }
        public List<byte> Payload { get; } = [];

        public bool Matches(Frame frame) =>
            MessageClass == frame.MessageClass
            && MessageId == frame.MessageId
            && TransferId == frame.TransferId
            && PageCount == frame.PageCount;
    }
}
