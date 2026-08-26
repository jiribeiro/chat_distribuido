using System.Net.Sockets;
using System.Text;

namespace chat_distribuido;

class Peer
{
    public readonly Socket Socket;
    public string? Nickname;
    public int ListenPort;

    readonly Queue<string> _queue = new();
    volatile bool _alive = true;

    public Peer(Socket socket) => Socket = socket;

    public void Send(string type, string from, string to, string text)
    {
        if (!_alive) return;
        _queue.Enqueue($"{type}|{from}|{to}|{text}");
    }

    public async Task WriteLoopAsync()
    {
        while (_alive)
        {
            string? msg = null;
            if (_queue.Count > 0) msg = _queue.Dequeue();

            if (msg is null)
            {
                await Task.Delay(50);
                continue;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Frames.WriteAsync(Socket, Encoding.UTF8.GetBytes(msg), cts.Token);
            }
            catch
            {
                Stop();
            }
        }
    }

    public void Stop()
    {
        if (!_alive) return;
        _alive = false;
        try { Socket.Shutdown(SocketShutdown.Both); } catch { }
        try { Socket.Close(); } catch { }
    }
}
