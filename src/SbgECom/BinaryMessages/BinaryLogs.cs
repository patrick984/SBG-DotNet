namespace SbgECom.BinaryMessages;

public interface IBinaryLog { uint TimeStamp { get; } }
public sealed record StatusLog(uint TimeStamp, ushort GeneralStatus, ushort CommunicationStatus2, uint CommunicationStatus,
    uint AidingStatus, uint Reserved2, ushort Reserved3, uint Uptime, byte CpuUsage) : IBinaryLog;
public sealed record ImuLog(uint TimeStamp, ushort Status, float[] Accelerometers, float[] Gyroscopes,
    float Temperature, float[] DeltaVelocity, float[] DeltaAngle) : IBinaryLog;
public sealed record ImuShortLog(uint TimeStamp, ushort Status, float Temperature, int[] DeltaVelocity,
    int[] DeltaAngle) : IBinaryLog;
public sealed record EkfEulerLog(uint TimeStamp, float[] Euler, float[] EulerStdDev, uint Status,
    float MagneticDeclination, float MagneticInclination) : IBinaryLog;
public sealed record EkfQuaternionLog(uint TimeStamp, float[] Quaternion, float[] EulerStdDev, uint Status,
    float MagneticDeclination, float MagneticInclination) : IBinaryLog;
public sealed record EkfNavigationLog(uint TimeStamp, float[] Velocity, float[] VelocityStdDev, double[] Position,
    float Undulation, float[] PositionStdDev, uint Status) : IBinaryLog;
public sealed record VectorAccuracyLog(uint TimeStamp, uint Status, float[] Vector, float[] StandardDeviation) : IBinaryLog;
public sealed record AidingVectorLog(uint TimeStamp, ushort Status, float[] Vector, float[] StandardDeviation) : IBinaryLog;
public sealed record DepthLog(uint TimeStamp, ushort Status, float Pressure, float Depth) : IBinaryLog;
public sealed record DvlLog(uint TimeStamp, ushort Status, float[] Velocity, float[] VelocityQuality) : IBinaryLog;
public sealed record EventLog(uint TimeStamp, ushort Status, ushort TimeOffset0, ushort TimeOffset1,
    ushort TimeOffset2, ushort TimeOffset3) : IBinaryLog;
public sealed record MagLog(uint TimeStamp, ushort Status, short[] Magnetometers, short[] Accelerometers,
    ushort Rejection) : IBinaryLog;
public sealed record OdometerLog(uint TimeStamp, ushort Status, float Velocity) : IBinaryLog;
public sealed record RawDataLog(byte[] Data);
public sealed record DiagnosticLog(uint TimeStamp, byte Type, byte ErrorCode, string Message) : IBinaryLog;
public sealed record SessionInfoLog(ushort PageIndex, ushort PageCount, byte[] Data);

/// <summary>Typed parser for the binary output messages. Unknown IDs are returned as immutable raw payloads.</summary>
public static class BinaryLogParser
{
    public static object Parse(byte messageClass, byte messageId, ReadOnlySpan<byte> payload)
    {
        if (messageClass != (byte)MessageClass.LogECom0 && messageClass != (byte)MessageClass.LogECom1)
            return payload.ToArray();
        var r = new BinaryMessageReader(payload);
        return (BinaryLog)messageId switch
        {
            BinaryLog.Status => Status(ref r),
            BinaryLog.ImuData => Imu(ref r),
            BinaryLog.ImuShort => ImuShort(ref r),
            BinaryLog.EkfEuler => Euler(ref r),
            BinaryLog.EkfQuaternion => Quaternion(ref r),
            BinaryLog.EkfNavigation => Navigation(ref r),
            BinaryLog.EkfVelocityBody or BinaryLog.EkfRotationAccelerationBody or BinaryLog.EkfRotationAccelerationNed => VectorAccuracy(ref r),
            BinaryLog.Velocity1 => AidingVector(ref r),
            BinaryLog.Depth => new DepthLog(r.UInt32(), r.UInt16(), r.Float32(), r.Float32()),
            BinaryLog.DvlBottomTrack or BinaryLog.DvlWaterTrack => new DvlLog(r.UInt32(), r.UInt16(), r.Vector3(), r.Vector3()),
            BinaryLog.EventA or BinaryLog.EventB or BinaryLog.EventC or BinaryLog.EventD or BinaryLog.EventE or BinaryLog.EventOutA or BinaryLog.EventOutB =>
                new EventLog(r.UInt32(), r.UInt16(), r.UInt16(), r.UInt16(), r.UInt16(), r.UInt16()),
            BinaryLog.Mag => new MagLog(r.UInt32(), r.UInt16(), [r.Int16(),r.Int16(),r.Int16()], [r.Int16(),r.Int16(),r.Int16()], r.UInt16()),
            BinaryLog.OdometerVelocity => new OdometerLog(r.UInt32(), r.UInt16(), r.Float32()),
            BinaryLog.Gnss1Raw or BinaryLog.Gnss2Raw or BinaryLog.RtcmRaw => new RawDataLog(payload.ToArray()),
            BinaryLog.Diagnostic => new DiagnosticLog(r.UInt32(), r.UInt8(), r.UInt8(), r.Utf8(r.Remaining)),
            BinaryLog.SessionInfo or BinaryLog.VibrationFft => Page(ref r),
            _ => payload.ToArray()
        };
    }

    private static StatusLog Status(ref BinaryMessageReader r) => new(r.UInt32(), r.UInt16(), r.UInt16(), r.UInt32(),
        r.UInt32(), r.UInt32(), r.UInt16(), r.Remaining >= 4 ? r.UInt32() : 0, r.Remaining >= 1 ? r.UInt8() : byte.MaxValue);
    private static ImuLog Imu(ref BinaryMessageReader r) => new(r.UInt32(), r.UInt16(), r.Vector3(), r.Vector3(),
        r.Float32(), r.Vector3(), r.Vector3());
    private static ImuShortLog ImuShort(ref BinaryMessageReader r) => new(r.UInt32(), r.UInt16(), r.Float32(),
        [r.Int32(),r.Int32(),r.Int32()], [r.Int32(),r.Int32(),r.Int32()]);
    private static EkfEulerLog Euler(ref BinaryMessageReader r)
    {
        var result = new EkfEulerLog(r.UInt32(), r.Vector3(), r.Vector3(), r.UInt32(),
            r.Remaining >= 4 ? r.Float32() : float.NaN, r.Remaining >= 4 ? r.Float32() : float.NaN);
        return result;
    }
    private static EkfQuaternionLog Quaternion(ref BinaryMessageReader r)
    {
        var time = r.UInt32(); var q = new[] {r.Float32(),r.Float32(),r.Float32(),r.Float32()}; var std = r.Vector3(); var status = r.UInt32();
        return new(time, q, std, status, r.Remaining >= 4 ? r.Float32() : float.NaN, r.Remaining >= 4 ? r.Float32() : float.NaN);
    }
    private static EkfNavigationLog Navigation(ref BinaryMessageReader r) => new(r.UInt32(), r.Vector3(), r.Vector3(),
        [r.Float64(),r.Float64(),r.Float64()], r.Float32(), r.Vector3(), r.UInt32());
    private static VectorAccuracyLog VectorAccuracy(ref BinaryMessageReader r) => new(r.UInt32(), r.UInt32(), r.Vector3(), r.Vector3());
    private static AidingVectorLog AidingVector(ref BinaryMessageReader r) => new(r.UInt32(), r.UInt16(), r.Vector3(), r.Vector3());
    private static SessionInfoLog Page(ref BinaryMessageReader r)
    {
        var index=r.UInt16(); var count=r.UInt16(); var size=r.UInt16();
        if (index >= count) throw new InvalidDataException("Invalid page index.");
        return new(index, count, r.Bytes(size));
    }
}
