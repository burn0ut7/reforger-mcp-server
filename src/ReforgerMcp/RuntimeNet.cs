using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace ReforgerMcp;

internal static class RuntimeNet
{
    // Verify the configured IPv4 listener belongs to the selected Workbench process.
    // No port discovery, remote host selection, or transport fallback.
    [DllImport("iphlpapi.dll")] private static extern uint GetExtendedTcpTable(nint table, ref int size, bool order, int family, int tableClass, uint reserved);
    internal static void VerifyListener(int port, int pid) => CheckListener(port, pid, false);
    internal static void VerifyPortAvailableOrOwned(int port, int pid) => CheckListener(port, pid, true);
    private static void CheckListener(int port, int pid, bool allowAbsent)
    {
        int size = 0;
        var error = GetExtendedTcpTable(0, ref size, false, 2, 3, 0); // OWNER_PID_LISTENER
        if (error != 122) throw new Win32Exception((int)error);
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            error = GetExtendedTcpTable(memory, ref size, false, 2, 3, 0);
            if (error != 0) throw new Win32Exception((int)error);
            var count = Marshal.ReadInt32(memory);
            if (count < 0 || count > (size - 4) / 24) throw new InvalidDataException("Invalid listener table.");
            var owners = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var row = memory + 4 + i * 24;
                var address = unchecked((uint)Marshal.ReadInt32(row, 4));
                var localPort = ((Marshal.ReadByte(row, 8) << 8) | Marshal.ReadByte(row, 9));
                if (localPort == port && (allowAbsent || address == 0 || address == 0x0100007f)) owners.Add(Marshal.ReadInt32(row, 20));
            }
            if (allowAbsent && owners.Count == 0) return;
            if (owners.Count != 1 || owners[0] != pid) throw new InvalidOperationException(
                $"Configured runtime port {port} is not a unique IPv4 listener owned by Workbench PID {pid}. Observed owners: [{string.Join(", ", owners)}].");
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    internal static JsonElement Call(int port, object request)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new TcpClient(AddressFamily.InterNetwork);
        bool dispatched = false;
        try
        {
            client.ConnectAsync(IPAddress.Loopback, port, deadline.Token).AsTask().GetAwaiter().GetResult();
            using var stream = client.GetStream();
            using var message = new MemoryStream();
            using (var writer = new BinaryWriter(message, Encoding.UTF8, true))
            {
                writer.Write(1);
                foreach (var value in new[] { "ReforgerMcp", "JsonRPC", JsonSerializer.Serialize(request) })
                { var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
            }
            dispatched = true;
            stream.WriteAsync(message.ToArray(), deadline.Token).AsTask().GetAwaiter().GetResult();
            string Read()
            {
                var sizeBytes = new byte[4];
                stream.ReadExactlyAsync(sizeBytes, deadline.Token).AsTask().GetAwaiter().GetResult();
                var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBytes);
                if (size is < 0 or > 65536) throw new InvalidDataException("Runtime response exceeds 64 KiB.");
                var bytes = new byte[size]; stream.ReadExactlyAsync(bytes, deadline.Token).AsTask().GetAwaiter().GetResult();
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            var code = Read(); var payload = Read();
            if (code != "Ok") throw new InvalidOperationException("Runtime adapter rejected request: " + code);
            using var parsed = JsonDocument.Parse(payload);
            return parsed.RootElement.Clone();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(dispatched
                ? "Runtime request outcome unknown or rejected after dispatch; inspect logs before any further call. No automatic retry. " + ex.Message
                : "Runtime unavailable before dispatch. " + ex.Message, ex);
        }
    }
}

