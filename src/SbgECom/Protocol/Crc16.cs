namespace SbgECom.Protocol;

/// <summary>CRC-16/CCITT implementation used by the sbgECom binary protocol.</summary>
public static class Crc16
{
    public const ushort InitialValue = 0;
    public const ushort Polynomial = 0x8408;

    public static ushort Compute(ReadOnlySpan<byte> data, ushort initialValue = InitialValue)
    {
        var crc = initialValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1);
        }
        return crc;
    }
}
