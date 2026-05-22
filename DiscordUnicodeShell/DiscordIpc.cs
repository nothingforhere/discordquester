using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace DiscordUnicodeShell;

public sealed class DiscordIpc : IDisposable
{
    private readonly string clientId;
    private NamedPipeClientStream? pipe;

    public DiscordIpc(string clientId)
    {
        this.clientId = clientId;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 10; i++)
        {
            var candidate = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await candidate.ConnectAsync(300, cancellationToken);
                pipe = candidate;
                await SendAsync(0, new { v = 1, client_id = clientId }, cancellationToken);
                var response = await ReceiveAsync(cancellationToken);
                if (response.Op == 2)
                {
                    throw new InvalidOperationException(response.Payload.RootElement.TryGetProperty("message", out var message)
                        ? message.GetString()
                        : "Handshake failed");
                }
                return;
            }
            catch
            {
                candidate.Dispose();
                if (i == 9)
                {
                    throw;
                }
            }
        }

        throw new InvalidOperationException("Discord is not running or IPC pipe could not be opened.");
    }

    public async Task SetActivityAsync(object activity, int pid, CancellationToken cancellationToken)
    {
        await SendAsync(
            1,
            new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid,
                    activity
                },
                nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            },
            cancellationToken);

        await ReceiveAsync(cancellationToken);
    }

    private async Task SendAsync(int op, object payload, CancellationToken cancellationToken)
    {
        if (pipe is null)
        {
            throw new InvalidOperationException("Not connected to Discord.");
        }

        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.GetBytes(op).CopyTo(header, 0);
        BitConverter.GetBytes(body.Length).CopyTo(header, 4);

        await pipe.WriteAsync(header, cancellationToken);
        await pipe.WriteAsync(body, cancellationToken);
        await pipe.FlushAsync(cancellationToken);
    }

    private async Task<(int Op, JsonDocument Payload)> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (pipe is null)
        {
            throw new InvalidOperationException("Not connected to Discord.");
        }

        var header = await ReadExactAsync(pipe, 8, cancellationToken);
        var op = BitConverter.ToInt32(header, 0);
        var length = BitConverter.ToInt32(header, 4);
        var payload = await ReadExactAsync(pipe, length, cancellationToken);
        return (op, JsonDocument.Parse(payload));
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed by Discord.");
            }
            offset += read;
        }

        return buffer;
    }

    public void Dispose()
    {
        pipe?.Dispose();
    }
}
