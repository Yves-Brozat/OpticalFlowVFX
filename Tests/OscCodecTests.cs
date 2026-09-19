// Standalone test executable: compile with OscMessageCodec.cs (no Unity required).
using System;
using System.Net;
using System.Net.Sockets;
using OpticalFlowTest;

static class OscCodecTests
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }

    static void Main()
    {
        // Independent OSC wire fixture: /test ,fff 1 -2 0.5, network byte order.
        byte[] fixture = { 47,116,101,115,116,0,0,0, 44,102,102,102,0,0,0,0,
            63,128,0,0, 192,0,0,0, 63,0,0,0 };
        Check(OscMessageCodec.TryDecode(fixture, out var address, out var values), "fixture decode");
        Check(address == "/test" && values.Length == 3 && values[0] == 1 && values[1] == -2 && values[2] == .5f, "fixture values");
        var encoded = OscMessageCodec.Encode("/test", new[] { 1f, -2f, .5f });
        Check(Convert.ToBase64String(encoded) == Convert.ToBase64String(fixture), "wire encoding");
        for (int length = 1; length <= 12; length++)
        {
            string path = "/" + new string('a', length);
            for (int count = 0; count <= 9; count++)
            {
                var input = new float[count];
                for (int i = 0; i < count; i++) input[i] = i * -.125f;
                Check(OscMessageCodec.TryDecode(OscMessageCodec.Encode(path, input), out address, out values)
                    && address == path && values.Length == count, "padding round trip");
                for (int i = 0; i < count; i++) Check(values[i] == input[i], "argument round trip");
            }
        }
        for (int length = 0; length < fixture.Length; length++)
        {
            var truncated = new byte[length];
            Array.Copy(fixture, truncated, length);
            Check(!OscMessageCodec.TryDecode(truncated, out address, out values), "truncation rejection");
        }
        byte[] mixed = { 47,120,0,0, 44,105,84,70,0,0,0,0, 255,255,255,254 };
        Check(OscMessageCodec.TryDecode(mixed, out address, out values) && values[0] == -2 && values[1] == 1 && values[2] == 0, "int and bool types");
        var invalid = (byte[])fixture.Clone();
        invalid[16] = 127; invalid[17] = 128;
        Check(!OscMessageCodec.TryDecode(invalid, out address, out values), "infinity rejection");
        invalid = (byte[])fixture.Clone(); invalid[9] = (byte)'s';
        Check(!OscMessageCodec.TryDecode(invalid, out address, out values), "unsupported type");
        invalid = (byte[])fixture.Clone(); invalid[7] = 1;
        Check(!OscMessageCodec.TryDecode(invalid, out address, out values), "invalid padding");
        Check(!OscMessageCodec.TryDecode(null, out address, out values), "null packet");
        var random = new Random(42);
        for (int i = 0; i < 10000; i++)
        {
            var packet = new byte[random.Next(256)];
            random.NextBytes(packet);
            OscMessageCodec.TryDecode(packet, out address, out values); // Must never throw.
        }
        checks++;
        using (var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        using (var sender = new UdpClient(AddressFamily.InterNetwork))
        {
            receiver.Client.ReceiveTimeout = 2000;
            var destination = (IPEndPoint)receiver.Client.LocalEndPoint;
            sender.Send(fixture, fixture.Length, destination);
            IPEndPoint source = null;
            Check(OscMessageCodec.TryDecode(receiver.Receive(ref source), out address, out values)
                && address == "/test" && values[2] == .5f, "UDP loopback");
        }
        Console.WriteLine("PASS: " + checks + " checks, including 10000 malformed-packet probes and UDP loopback.");
    }
}
