using System.Text.Json;

namespace chat_distribuido;

class PeerAddress
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
}

class Config
{
    public int Port { get; set; }
    public string Nickname { get; set; } = "";
    public List<PeerAddress> Peers { get; set; } = new();

    public static Config Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<Config>(json, options)!;
    }
}
