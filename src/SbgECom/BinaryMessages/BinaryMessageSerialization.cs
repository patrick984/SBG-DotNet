using System.Globalization;
using System.Reflection;
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
        return $"{message.GetType().Name} {{ {string.Join(", ", values)} }}";
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
        foreach (
            var property in message
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(property => property.MetadataToken)
        )
        {
            var value = property.GetValue(message);
            if (value is byte[] bytes)
            {
                var text = Convert.ToHexString(bytes);
                yield return new(property.Name, text, text, "hex");
            }
            else if (value is Array array)
            {
                for (var index = 0; index < array.Length; index++)
                {
                    var name = $"{property.Name}[{index}]";
                    yield return new(
                        name,
                        Format(array.GetValue(index), property.Name, statusesAsHex: true),
                        Format(array.GetValue(index), property.Name, statusesAsHex: false),
                        UnitFor(message, name)
                    );
                }
            }
            else
            {
                yield return new(
                    property.Name,
                    Format(value, property.Name, statusesAsHex: true),
                    Format(value, property.Name, statusesAsHex: false),
                    UnitFor(message, property.Name)
                );
            }
        }
    }

    private static string Format(object? value, string propertyName, bool statusesAsHex)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (
            statusesAsHex
            && propertyName.Contains("Status", StringComparison.Ordinal)
            && value is IConvertible
        )
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

    private static string UnitFor(IBinaryMessage message, string name)
    {
        if (
            name == nameof(IBinaryLog.TimeStamp)
            || name.StartsWith("TimeOffset", StringComparison.Ordinal)
        )
            return "µs";
        if (name is "Uptime")
            return "s";
        if (name is "CpuUsage")
            return "%";
        if (name.Contains("Temperature", StringComparison.Ordinal))
            return "°C";
        if (name.Contains("Pressure", StringComparison.Ordinal))
            return "Pa";
        if (name is "Depth" or "Undulation")
            return "m";
        if (
            name.StartsWith("Euler", StringComparison.Ordinal)
            || name.StartsWith("DeltaAngle", StringComparison.Ordinal)
        )
            return "rad";
        if (name is "MagneticDeclination" or "MagneticInclination")
            return "rad";
        if (name.StartsWith("Gyroscopes", StringComparison.Ordinal))
            return "rad/s";
        if (name.StartsWith("Accelerometers", StringComparison.Ordinal))
            return message is MagLog ? "LSB" : "m/s²";
        if (name.StartsWith("Magnetometers", StringComparison.Ordinal))
            return "LSB";
        if (name.StartsWith("DeltaVelocity", StringComparison.Ordinal))
            return message is ImuShortLog ? "LSB" : "m/s";
        if (
            name.StartsWith("Velocity", StringComparison.Ordinal)
            || name.StartsWith("Vector", StringComparison.Ordinal)
        )
            return "m/s";
        if (name is "Position[0]" or "Position[1]")
            return "deg";
        if (name == "Position[2]")
            return "m";
        if (name is "Data")
            return "hex";
        return string.Empty;
    }
}
