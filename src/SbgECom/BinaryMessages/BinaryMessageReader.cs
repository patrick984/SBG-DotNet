using System.Buffers.Binary;
using System.Text;

namespace SbgECom.BinaryMessages;

/// <summary>Bounds-checked little-endian reader for sbgECom message payloads.</summary>
public ref struct BinaryMessageReader(ReadOnlySpan<byte> payload)
{
    private ReadOnlySpan<byte> _remaining = payload;
    public int Remaining => _remaining.Length;

    public byte UInt8() => Take(1)[0];

    public sbyte Int8() => unchecked((sbyte)UInt8());

    public ushort UInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

    public short Int16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));

    public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    public ulong UInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

    public long Int64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

    public float Float32() => BitConverter.Int32BitsToSingle(Int32());

    public double Float64() => BitConverter.Int64BitsToDouble(Int64());

    public byte[] Bytes(int count) => Take(count).ToArray();

    public string Utf8(int count) => Encoding.UTF8.GetString(Take(count)).TrimEnd('\0');

    public float[] Vector3() => [Float32(), Float32(), Float32()];

    public void EnsureFullyConsumed()
    {
        if (!_remaining.IsEmpty)
            throw new InvalidDataException(
                $"Unexpected {_remaining.Length} trailing payload bytes."
            );
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if ((uint)count > (uint)_remaining.Length)
            throw new EndOfStreamException("Truncated sbgECom binary message.");
        var value = _remaining[..count];
        _remaining = _remaining[count..];
        return value;
    }
}
