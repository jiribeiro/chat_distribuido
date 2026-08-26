using System.Net;
using System.Net.Sockets;
using System.Text;
using chat_distribuido;

if (args.Length < 1)
{
    Console.WriteLine("Uso: dotnet run -- <caminho-do-config.json>");
    return 1;
}

Config config = Config.Load(args[0]);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var peers = new Dictionary<string, Peer>();

async Task HandlePeerAsync(Socket socket)
{
    var peer = new Peer(socket);
    _ = peer.WriteLoopAsync();
    peer.Send("HELLO", config.Nickname, "", config.Port.ToString());

    try
    {
        while (true)
        {
            byte[]? frame;
            try
            {
                frame = await Frames.ReadAsync(socket);
            }
            catch
            {
                break;
            }
            if (frame is null) break;

            var parts = Encoding.UTF8.GetString(frame).Split('|', 4);
            if (parts.Length < 4) continue;
            var (type, from, to, text) = (parts[0], parts[1], parts[2], parts[3]);

            try
            {
                switch (type)
                {
                    case "HELLO":
                        peer.Nickname = from;
                        peer.ListenPort = int.Parse(text);
                        lock (peers) peers[from] = peer;
                        Console.WriteLine($"* {from} entrou na conversa.");
                        break;

                    case "CHAT":
                        Console.WriteLine($"{from}: {text}");
                        break;

                    case "PRIVATE":
                        Console.WriteLine($"[privado de {from}]: {text}");
                        break;

                    case "LEAVE":
                        lock (peers) peers.Remove(from);
                        Console.WriteLine($"* {from} saiu da conversa.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mensagem inválida de {from}: {ex.Message}");
            }
        }
    }
    finally
    {
        peer.Stop();
        if (peer.Nickname is not null)
        {
            bool removed;
            lock (peers) removed = peers.Remove(peer.Nickname);
            if (removed)
                Console.WriteLine($"* {peer.Nickname} saiu da conversa (conexão perdida).");
        }
    }
}

var listenerTask = Connection.ListenLoopAsync(config.Port, socket =>
{
    _ = HandlePeerAsync(socket);
    return Task.CompletedTask;
}, cts.Token);

async Task TryConnectAsync(PeerAddress p)
{
    try
    {
        var socket = await Connection.ConnectAsync(p.Host, p.Port, cts.Token);
        _ = HandlePeerAsync(socket);
    }
    catch
    {
        Console.WriteLine($"Não foi possível conectar a {p.Host}:{p.Port}.");
    }
}

foreach (var p in config.Peers)
    _ = TryConnectAsync(p);

Console.WriteLine();
Console.WriteLine($"Você é '{config.Nickname}', porta {config.Port}.");
Console.WriteLine();

try
{
    while (!cts.IsCancellationRequested)
    {
        var line = await Task.Run(Console.ReadLine);
        if (line is null || line.Equals("/quit", StringComparison.OrdinalIgnoreCase))
            break;
        if (line.Length == 0)
            continue;

        if (line.Equals("/list", StringComparison.OrdinalIgnoreCase))
        {
            List<string> names;
            lock (peers) names = peers.Keys.ToList();
            Console.WriteLine(names.Count == 0 ? "(ninguém conectado)" : string.Join(", ", names));
            continue;
        }

        if (line.StartsWith("/msg ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = line["/msg ".Length..];
            var sep = rest.IndexOf(' ');
            if (sep <= 0)
            {
                Console.WriteLine("Uso: /msg <apelido> <texto>");
                continue;
            }

            var to = rest[..sep];
            var text = rest[(sep + 1)..];

            Peer? target;
            lock (peers) peers.TryGetValue(to, out target);
            if (target is null)
                Console.WriteLine($"Apelido desconhecido: {to}");
            else
                target.Send("PRIVATE", config.Nickname, to, text);
            continue;
        }

        if (line.StartsWith('/'))
        {
            Console.WriteLine($"Comando desconhecido: {line}");
            continue;
        }

        List<Peer> all;
        lock (peers) all = peers.Values.ToList();
        foreach (var p in all)
            p.Send("CHAT", config.Nickname, "", line);
    }
}
catch (OperationCanceledException)
{
}

List<Peer> allPeers;
lock (peers) allPeers = peers.Values.ToList();
foreach (var peer in allPeers)
    peer.Send("LEAVE", config.Nickname, "", "");

await Task.Delay(300);

cts.Cancel();
foreach (var peer in allPeers)
    peer.Stop();

try { await listenerTask; } catch (OperationCanceledException) { }

Console.WriteLine("Encerrado.");
return 0;
