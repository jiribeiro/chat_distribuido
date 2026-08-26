using System.Net.Sockets;
using System.Text;

namespace chat_distribuido;

class Peer
{
    public readonly Socket Socket;
    public string? Nickname;
    public int ListenPort;

    readonly Queue<string> _outbox = new();
    readonly SemaphoreSlim _signal = new(0);
    volatile bool _alive = true;

    public Peer(Socket socket) => Socket = socket;


    public void Send(string type, string from, string to, string text)
    {
        if (!_alive) return;
        lock (_outbox)
        {
            if (_outbox.Count >= 100)
                _outbox.Dequeue();
            _outbox.Enqueue($"{type}|{from}|{to}|{text}");
        }
        _signal.Release();
    }

    public async Task WriteLoopAsync()
    {
        while (_alive)
        {
            await _signal.WaitAsync();

            string? msg = null;
            lock (_outbox)
                if (_outbox.Count > 0) msg = _outbox.Dequeue();
            if (msg is null) continue;

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
        _signal.Release();
        try { Socket.Shutdown(SocketShutdown.Both); } catch { }
        try { Socket.Close(); } catch { }
    }
}
