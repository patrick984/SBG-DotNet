using System.Buffers.Binary;
using System.Diagnostics;
using SbgECom.BinaryMessages;
using SbgECom.Protocol;

var oracle = args.SingleOrDefault() ?? throw new ArgumentException("Path to C oracle is required.");
var tests = new (string Name, Action Test)[]
{
    ("C CRC parity", CrcParity),
    ("C frame parity", FrameParity),
    ("C IMU parser parity", ImuParity),
    ("fragmented stream", FragmentedStream),
    ("noise and CRC recovery", Recovery),
    ("extended payload", ExtendedPayload),
    ("human-readable messages", HumanReadable),
    ("CSV serialization", CsvSerialization),
};
var failed = 0;
foreach (var (name, test) in tests)
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception e)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {e.Message}");
    }
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed");
return failed == 0 ? 0 : 1;

void CrcParity()
{
    var random = new Random(0x5b6e);
    foreach (var size in new[] { 0, 1, 2, 7, 31, 255, 4086 })
    {
        var bytes = new byte[size];
        random.NextBytes(bytes);
        Equal(C(oracle, "crc", Hex(bytes)), Crc16.Compute(bytes).ToString("x4"));
    }
}
void FrameParity()
{
    foreach (
        var payload in new[]
        {
            Array.Empty<byte>(),
            [1, 2, 3, 4, 5],
            Enumerable.Range(0, 255).Select(i => (byte)i).ToArray(),
        }
    )
        Equal(
            C(oracle, "frame", Hex(payload), "16", "42"),
            Hex(SbgFrameCodec.Encode(16, 42, payload))
        );
}
void ImuParity()
{
    var p = new byte[58];
    BinaryPrimitives.WriteUInt32LittleEndian(p, 123456);
    BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(4), 0xa55a);
    for (var i = 0; i < 13; i++)
        BinaryPrimitives.WriteInt32LittleEndian(
            p.AsSpan(6 + i * 4),
            BitConverter.SingleToInt32Bits(i + 0.25f)
        );
    var log = (ImuLog)BinaryLogParser.Parse(0, (byte)BinaryLog.ImuData, p);
    var actual =
        $"{log.TimeStamp},{log.Status},{log.Accelerometers[0]:G9},{log.Temperature:G9},{log.DeltaAngle[2]:G9}";
    Equal(C(oracle, "imu", Hex(p)), actual);
}
void FragmentedStream()
{
    var frame = SbgFrameCodec.Encode(0, 6, [1, 2, 3]);
    var d = new SbgStreamDecoder();
    var all = new List<SbgMessage>();
    foreach (var b in frame)
        all.AddRange(d.Append([b]));
    Equal(1, all.Count);
    Equal("010203", Hex(all[0].Payload.Span));
}
void Recovery()
{
    var bad = SbgFrameCodec.Encode(0, 1, [9]);
    bad[^2] ^= 1;
    var good = SbgFrameCodec.Encode(0, 2, [8, 7]);
    var input = new byte[] { 3, 4, 5 }
        .Concat(bad)
        .Concat(good)
        .ToArray();
    var d = new SbgStreamDecoder();
    var errors = 0;
    d.DecodeFailed += (_, _) => errors++;
    var messages = d.Append(input);
    Equal(1, messages.Count);
    Equal(2, messages[0].MessageId);
    Equal(1, errors);
}
void ExtendedPayload()
{
    var payload = Enumerable.Range(0, 10000).Select(i => (byte)(i * 17)).ToArray();
    var frames = SbgFrameCodec.EncodeMessage(1, 55, payload, 7);
    Equal(3, frames.Count);
    var d = new SbgStreamDecoder();
    var messages = new List<SbgMessage>();
    foreach (var f in frames)
        messages.AddRange(d.Append(f));
    Equal(1, messages.Count);
    Equal(Hex(payload), Hex(messages[0].Payload.Span));
}
void HumanReadable()
{
    var log = new DepthLog(42, 3, 101325.5f, 12.25f);
    Equal(
        "DepthLog { TimeStamp=42, Status=0x3, Pressure=101325.5, Depth=12.25 }",
        log.ToUserReadableString()
    );
    Equal(log.ToUserReadableString(), log.ToString());
}
void CsvSerialization()
{
    var payload = new byte[14];
    BinaryPrimitives.WriteUInt32LittleEndian(payload, 42);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), 3);
    BinaryPrimitives.WriteInt32LittleEndian(
        payload.AsSpan(6),
        BitConverter.SingleToInt32Bits(101325.5f)
    );
    BinaryPrimitives.WriteInt32LittleEndian(
        payload.AsSpan(10),
        BitConverter.SingleToInt32Bits(12.25f)
    );
    var csv = CsvMessageSerializer.Serialize([new SbgMessage(0, (byte)BinaryLog.Depth, payload)]);
    var expected = string.Join(
        Environment.NewLine,
        "Timestamp,Message ID,Value,Unit",
        "42,Depth (47),3,",
        "42,Depth (47),101325.5,Pa",
        "42,Depth (47),12.25,m",
        ""
    );
    Equal(expected, csv);
}
static string C(string exe, params string[] arguments)
{
    var info = new ProcessStartInfo(exe)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var argument in arguments)
        info.ArgumentList.Add(argument);
    var p = Process.Start(info)!;
    var output = p.StandardOutput.ReadToEnd().Trim();
    var error = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0)
        throw new Exception($"C oracle exited {p.ExitCode}: {error}");
    return output;
}
static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();
static void Equal<T>(T expected, T actual)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"expected {expected}, got {actual}");
}
