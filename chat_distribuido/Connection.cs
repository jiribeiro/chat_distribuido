using System.Net;
using System.Net.Sockets;

namespace chat_distribuido;

public static class Connection
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    public static async Task ListenLoopAsync(int port, Func<Socket, Task> onAccepted, CancellationToken ct)
    {
        using var listener = new Socket(
            addressFamily: AddressFamily.InterNetwork,
            socketType: SocketType.Stream,
            protocolType: ProtocolType.Tcp);

        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.Listen(32);

        Console.WriteLine($"Listening on {listener.LocalEndPoint}...");

        while (!ct.IsCancellationRequested)
        {
            Socket peer = await listener.AcceptAsync(ct);
            peer.NoDelay = true;
            _ = onAccepted(peer);
        }
    }

    public static async Task<Socket> ConnectAsync(string host, int port, CancellationToken ct)
    {
        var socket = new Socket(
            addressFamily: AddressFamily.InterNetwork,
            socketType: SocketType.Stream,
            protocolType: ProtocolType.Tcp)
        { NoDelay = true };

        Console.WriteLine($"Connecting to {host}:{port}...");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ConnectTimeout);
            await socket.ConnectAsync(host, port, timeout.Token);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
