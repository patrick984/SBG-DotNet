namespace SbgECom.Protocol;

/// <summary>A completely received sbgECom binary message.</summary>
public sealed record SbgMessage(byte MessageClass, byte MessageId, ReadOnlyMemory<byte> Payload);

public enum DecodeError
{
    InvalidFrame,
    InvalidCrc,
    InvalidExtendedFrame,
    ExtendedFrameOutOfSequence,
}

public sealed class DecodeErrorEventArgs(DecodeError error, ReadOnlyMemory<byte> candidate)
    : EventArgs
{
    public DecodeError Error { get; } = error;
    public ReadOnlyMemory<byte> Candidate { get; } = candidate;
}
