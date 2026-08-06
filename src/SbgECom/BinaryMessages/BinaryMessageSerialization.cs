using System.Globalization;
using SbgECom.Protocol;

namespace SbgECom.BinaryMessages;

/// <summary>Creates compact, culture-independent text for parsed binary messages.</summary>
public static class BinaryMessageTextSerializer
{
    public static string Serialize(IBinaryMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var values = MessageValueExtractor
            .Extract(message)
            .Select(field => $"{field.Name}={field.DisplayValue}");
        return $"{MessageValueExtractor.NameOf(message)} {{ {string.Join(", ", values)} }}";
    }
}

/// <summary>Serializes incoming protocol messages as long-form, RFC 4180-compatible CSV.</summary>
public static class CsvMessageSerializer
{
    public const string Header = "Timestamp,Message ID,Value,Unit";

    public static string Serialize(IEnumerable<SbgMessage> messages, bool includeHeader = true)
    {
        ArgumentNullException.ThrowIfNull(messages);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Write(writer, messages, includeHeader);
        return writer.ToString();
    }

    public static void Write(
        TextWriter writer,
        IEnumerable<SbgMessage> messages,
        bool includeHeader = true
    )
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(messages);

        if (includeHeader)
        {
            writer.WriteLine(Header);
        }

        foreach (var message in messages)
        {
            WriteMessage(writer, message);
        }
    }

    private static void WriteMessage(TextWriter writer, SbgMessage message)
    {
        var parsed = BinaryLogParser.Parse(
            message.MessageClass,
            message.MessageId,
            message.Payload.Span
        );
        var timestamp = parsed is IBinaryLog log
            ? log.TimeStamp.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        var messageId = GetMessageId(message);

        if (parsed is IBinaryMessage binaryMessage)
        {
            foreach (var field in MessageValueExtractor.Extract(binaryMessage))
            {
                if (field.Name == nameof(IBinaryLog.TimeStamp))
                {
                    continue;
                }

                WriteRow(writer, timestamp, messageId, field.CsvValue, field.Unit);
            }
        }
        else
        {
            WriteRow(
                writer,
                timestamp,
                messageId,
                Convert.ToHexString(message.Payload.Span),
                "hex"
            );
        }
    }

    private static string GetMessageId(SbgMessage message)
    {
        if (
            message.MessageClass is (byte)MessageClass.LogECom0 or (byte)MessageClass.LogECom1
            && Enum.IsDefined(typeof(BinaryLog), message.MessageId)
        )
        {
            return $"{(BinaryLog)message.MessageId} ({message.MessageId})";
        }

        return $"0x{message.MessageClass:X2}:0x{message.MessageId:X2}";
    }

    private static void WriteRow(
        TextWriter writer,
        string timestamp,
        string messageId,
        string value,
        string unit
    )
    {
        writer.Write(Escape(timestamp));
        writer.Write(',');
        writer.Write(Escape(messageId));
        writer.Write(',');
        writer.Write(Escape(value));
        writer.Write(',');
        writer.WriteLine(Escape(unit));
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

internal static class MessageValueExtractor
{
    internal readonly record struct Field(
        string Name,
        string DisplayValue,
        string CsvValue,
        string Unit
    );

    public static IEnumerable<Field> Extract(IBinaryMessage message)
    {
        return message switch
        {
            StatusLog value =>
            [
                Scalar("TimeStamp", value.TimeStamp, "µs"),
                Status("GeneralStatus", value.GeneralStatus),
                Status("CommunicationStatus2", value.CommunicationStatus2),
                Status("CommunicationStatus", value.CommunicationStatus),
                Status("AidingStatus", value.AidingStatus),
                Scalar("Reserved2", value.Reserved2),
                Scalar("Reserved3", value.Reserved3),
                Scalar("Uptime", value.Uptime, "s"),
                Scalar("CpuUsage", value.CpuUsage, "%"),
            ],
            ImuLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs"), Status("Status", value.Status)],
                Vector("Accelerometers", value.Accelerometers, "m/s²"),
                Vector("Gyroscopes", value.Gyroscopes, "rad/s"),
                [Scalar("Temperature", value.Temperature, "°C")],
                Vector("DeltaVelocity", value.DeltaVelocity, "m/s"),
                Vector("DeltaAngle", value.DeltaAngle, "rad")
            ),
            ImuShortLog value => Concat(
                [
                    Scalar("TimeStamp", value.TimeStamp, "µs"),
                    Status("Status", value.Status),
                    Scalar("Temperature", value.Temperature, "°C"),
                ],
                Vector("DeltaVelocity", value.DeltaVelocity, "LSB"),
                Vector("DeltaAngle", value.DeltaAngle, "LSB")
            ),
            EkfEulerLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs")],
                Vector("Euler", value.Euler, "rad"),
                Vector("EulerStdDev", value.EulerStdDev, "rad"),
                [
                    Status("Status", value.Status),
                    Scalar("MagneticDeclination", value.MagneticDeclination, "rad"),
                    Scalar("MagneticInclination", value.MagneticInclination, "rad"),
                ]
            ),
            EkfQuaternionLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs")],
                Vector("Quaternion", value.Quaternion),
                Vector("EulerStdDev", value.EulerStdDev, "rad"),
                [
                    Status("Status", value.Status),
                    Scalar("MagneticDeclination", value.MagneticDeclination, "rad"),
                    Scalar("MagneticInclination", value.MagneticInclination, "rad"),
                ]
            ),
            EkfNavigationLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs")],
                Vector("Velocity", value.Velocity, "m/s"),
                Vector("VelocityStdDev", value.VelocityStdDev, "m/s"),
                Position(value.Position),
                [Scalar("Undulation", value.Undulation, "m")],
                Vector("PositionStdDev", value.PositionStdDev, "m"),
                [Status("Status", value.Status)]
            ),
            VectorAccuracyLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs"), Status("Status", value.Status)],
                Vector("Vector", value.Vector, "m/s"),
                Vector("StandardDeviation", value.StandardDeviation, "m/s")
            ),
            AidingVectorLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs"), Status("Status", value.Status)],
                Vector("Vector", value.Vector, "m/s"),
                Vector("StandardDeviation", value.StandardDeviation, "m/s")
            ),
            DepthLog value =>
            [
                Scalar("TimeStamp", value.TimeStamp, "µs"),
                Status("Status", value.Status),
                Scalar("Pressure", value.Pressure, "Pa"),
                Scalar("Depth", value.Depth, "m"),
            ],
            DvlLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs"), Status("Status", value.Status)],
                Vector("Velocity", value.Velocity, "m/s"),
                Vector("VelocityQuality", value.VelocityQuality, "m/s")
            ),
            EventLog value =>
            [
                Scalar("TimeStamp", value.TimeStamp, "µs"),
                Status("Status", value.Status),
                Scalar("TimeOffset0", value.TimeOffset0, "µs"),
                Scalar("TimeOffset1", value.TimeOffset1, "µs"),
                Scalar("TimeOffset2", value.TimeOffset2, "µs"),
                Scalar("TimeOffset3", value.TimeOffset3, "µs"),
            ],
            MagLog value => Concat(
                [Scalar("TimeStamp", value.TimeStamp, "µs"), Status("Status", value.Status)],
                Vector("Magnetometers", value.Magnetometers, "LSB"),
                Vector("Accelerometers", value.Accelerometers, "LSB"),
                [Scalar("Rejection", value.Rejection)]
            ),
            OdometerLog value =>
            [
                Scalar("TimeStamp", value.TimeStamp, "µs"),
                Status("Status", value.Status),
                Scalar("Velocity", value.Velocity, "m/s"),
            ],
            RawDataLog value => [Bytes("Data", value.Data)],
            DiagnosticLog value =>
            [
                Scalar("TimeStamp", value.TimeStamp, "µs"),
                Scalar("Type", value.Type),
                Scalar("ErrorCode", value.ErrorCode),
                Scalar("Message", value.Message),
            ],
            SessionInfoLog value =>
            [
                Scalar("PageIndex", value.PageIndex),
                Scalar("PageCount", value.PageCount),
                Bytes("Data", value.Data),
            ],
            _ => throw new ArgumentException("Unsupported binary message type.", nameof(message)),
        };
    }

    public static string NameOf(IBinaryMessage message) =>
        message switch
        {
            StatusLog => nameof(StatusLog),
            ImuLog => nameof(ImuLog),
            ImuShortLog => nameof(ImuShortLog),
            EkfEulerLog => nameof(EkfEulerLog),
            EkfQuaternionLog => nameof(EkfQuaternionLog),
            EkfNavigationLog => nameof(EkfNavigationLog),
            VectorAccuracyLog => nameof(VectorAccuracyLog),
            AidingVectorLog => nameof(AidingVectorLog),
            DepthLog => nameof(DepthLog),
            DvlLog => nameof(DvlLog),
            EventLog => nameof(EventLog),
            MagLog => nameof(MagLog),
            OdometerLog => nameof(OdometerLog),
            RawDataLog => nameof(RawDataLog),
            DiagnosticLog => nameof(DiagnosticLog),
            SessionInfoLog => nameof(SessionInfoLog),
            _ => throw new ArgumentException("Unsupported binary message type.", nameof(message)),
        };

    private static Field Scalar(string name, object? value, string unit = "") =>
        new(name, Format(value, statusAsHex: false), Format(value, statusAsHex: false), unit);

    private static Field Status(string name, object value) =>
        new(name, Format(value, statusAsHex: true), Format(value, statusAsHex: false), "");

    private static Field Bytes(string name, byte[] value)
    {
        var text = Convert.ToHexString(value);
        return new(name, text, text, "hex");
    }

    private static IEnumerable<Field> Vector<T>(string name, T[] values, string unit = "")
    {
        for (var index = 0; index < values.Length; index++)
            yield return Scalar($"{name}[{index}]", values[index], unit);
    }

    private static IEnumerable<Field> Position(double[] values)
    {
        for (var index = 0; index < values.Length; index++)
            yield return Scalar($"Position[{index}]", values[index], index < 2 ? "deg" : "m");
    }

    private static IEnumerable<Field> Concat(params IEnumerable<Field>[] groups) =>
        groups.SelectMany(group => group);

    private static string Format(object? value, bool statusAsHex)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (statusAsHex && value is IConvertible)
        {
            return $"0x{Convert.ToUInt64(value, CultureInfo.InvariantCulture):X}";
        }

        return value switch
        {
            float number => number.ToString("G9", CultureInfo.InvariantCulture),
            double number => number.ToString("G17", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
