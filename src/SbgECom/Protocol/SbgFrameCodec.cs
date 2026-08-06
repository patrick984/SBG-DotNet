using System.Buffers.Binary;

namespace SbgECom.Protocol;

/// <summary>Stateless encoder for standard and extended sbgECom binary frames.</summary>
public static class SbgFrameCodec
{
    public const byte Sync1 = 0xff;
    public const byte Sync2 = 0x5a;
    public const byte Etx = 0x33;
    public const int MaximumFrameSize = 4096;
    public const int MaximumPayloadSize = 4086;
    public const int MaximumExtendedPayloadSize = 4081;

    public static byte[] Encode(byte messageClass, byte messageId, ReadOnlySpan<byte> payload)
    {
        if (messageClass > 0x7f)
            throw new ArgumentOutOfRangeException(nameof(messageClass));
        if (payload.Length > MaximumPayloadSize)
            throw new ArgumentOutOfRangeException(nameof(payload));
        return EncodeFrame(messageClass, messageId, payload, default, false);
    }

    public static IReadOnlyList<byte[]> EncodeMessage(
        byte messageClass,
        byte messageId,
        ReadOnlySpan<byte> payload,
        byte transferId = 0
    )
    {
        if (messageClass > 0x7f)
            throw new ArgumentOutOfRangeException(nameof(messageClass));
        if (transferId > 0x0f)
            throw new ArgumentOutOfRangeException(nameof(transferId));
        if (payload.Length <= MaximumPayloadSize)
            return [Encode(messageClass, messageId, payload)];

        var pageCountLong =
            (payload.Length + (long)MaximumExtendedPayloadSize - 1) / MaximumExtendedPayloadSize;
        if (pageCountLong > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(payload));
        var pageCount = (ushort)pageCountLong;
        var frames = new byte[pageCount][];
        for (ushort page = 0; page < pageCount; page++)
        {
            var offset = page * MaximumExtendedPayloadSize;
            var count = Math.Min(MaximumExtendedPayloadSize, payload.Length - offset);
            frames[page] = EncodeFrame(
                (byte)(messageClass | 0x80),
                messageId,
                payload.Slice(offset, count),
                new ExtendedHeader(transferId, page, pageCount),
                true
            );
        }
        return frames;
    }

    private static byte[] EncodeFrame(
        byte messageClass,
        byte messageId,
        ReadOnlySpan<byte> payload,
        ExtendedHeader extended,
        bool isExtended
    )
    {
        var headerSize = isExtended ? 5 : 0;
        var wirePayloadSize = payload.Length + headerSize;
        var result = new byte[wirePayloadSize + 9];
        result[0] = Sync1;
        result[1] = Sync2;
        result[2] = messageId;
        result[3] = messageClass;
        BinaryPrimitives.WriteUInt16LittleEndian(
            result.AsSpan(4),
            checked((ushort)wirePayloadSize)
        );
        var cursor = 6;
        if (isExtended)
        {
            result[cursor++] = extended.TransferId;
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(cursor), extended.PageIndex);
            cursor += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(cursor), extended.PageCount);
            cursor += 2;
        }
        payload.CopyTo(result.AsSpan(cursor));
        var crcOffset = 6 + wirePayloadSize;
        BinaryPrimitives.WriteUInt16LittleEndian(
            result.AsSpan(crcOffset),
            Crc16.Compute(result.AsSpan(2, wirePayloadSize + 4))
        );
        result[crcOffset + 2] = Etx;
        return result;
    }

    private readonly record struct ExtendedHeader(
        byte TransferId,
        ushort PageIndex,
        ushort PageCount
    );
}
