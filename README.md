# sbgECom for .NET 10

A dependency-free C# implementation of the SBG Systems binary protocol and binary output messages.
It is pinned and parity-tested against upstream sbgECom 5.8.935-stable commit
`15c6a492c1f1463ab2f2f73ab60ac935fa223e8d`.

The port intentionally excludes serial/UDP interfaces, CAN messages, commands, REST configuration,
file transfer, and examples. Supply bytes from any `Stream`, serial library, socket, or other transport.

## Use

```csharp
using SbgECom.BinaryMessages;
using SbgECom.Protocol;

var decoder = new SbgStreamDecoder();
foreach (var message in decoder.Append(bytesReadFromYourTransport))
{
    object log = BinaryLogParser.Parse(message.MessageClass, message.MessageId, message.Payload.Span);
    if (log is EkfNavigationLog nav)
        Console.WriteLine($"{nav.Position[0]}, {nav.Position[1]}");
}

byte[] request = SbgFrameCodec.Encode(messageClass: 0x10, messageId: 0x01, payload);
```

Every typed message provides `ToUserReadableString()` and overrides `ToString()` with the same
culture-independent representation. Incoming messages can also be exported in long-form CSV:

```csharp
string csv = CsvMessageSerializer.Serialize(messages);
// Timestamp,Message ID,Value,Unit
// 42,Depth (47),101325.5,Pa
```

`SbgStreamDecoder` accepts arbitrary chunks, retains incomplete frames, skips noise, reports corrupt
frames through `DecodeFailed`, and reassembles extended multi-page payloads. `BinaryMessageReader`
is public for binary log layouts not represented by a typed record; unknown message IDs remain available
as immutable `byte[]` payloads.

## Build and test

```bash
dotnet build tests/SbgECom.Tests/SbgECom.Tests.csproj
./tests/run-tests.sh
```

The test script compiles an independent C oracle from the pinned upstream sources. It verifies C/C#
CRC parity, standard frame bytes, and IMU parsing, plus incremental input, corrupt-frame recovery, and
extended payload behavior in C#—without xUnit, NuGet test packages, P/Invoke, or runtime dependencies.

The C# project itself has no `PackageReference` entries. Upstream C source is included only as a test
reference and remains under its MIT license.

## Native AOT

The library enables the .NET trimming and native-AOT compatibility analyzers through
`IsAotCompatible`. Message formatting uses exhaustive type patterns and does not use reflection or
runtime code generation. `tests/run-tests.sh` publishes and runs a native-AOT smoke executable that
exercises framing, parsing, human-readable output, and CSV serialization.

## Formatting

CSharpier 1.3.0 is pinned as a local .NET tool. After restoring tools, format or validate sources with:

```bash
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
```
