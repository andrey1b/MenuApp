using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MenuApp;

sealed class ShoppingListServer : IDisposable
{
    private readonly string _html;
    private TcpListener?   _listener;
    private volatile bool  _running;

    public int Port { get; }

    public ShoppingListServer(string html, int port)
    {
        _html = html;
        Port  = port;
    }

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        _running = true;
        var t = new Thread(Loop) { IsBackground = true, Name = "ShoppingHttp" };
        t.Start();
    }

    private void Loop()
    {
        while (_running)
        {
            TcpClient client;
            try { client = _listener!.AcceptTcpClient(); }
            catch { break; }
            ThreadPool.QueueUserWorkItem(_ => Respond(client));
        }
    }

    private void Respond(TcpClient client)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                // Вычитываем запрос (нам неважно, что именно)
                var buf = new byte[8192];
                stream.ReadTimeout = 1500;
                try { _ = stream.Read(buf, 0, buf.Length); } catch { }
                stream.ReadTimeout = Timeout.Infinite;

                var body   = Encoding.UTF8.GetBytes(_html);
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
                    $"Content-Length: {body.Length}\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
                stream.Write(header, 0, header.Length);
                stream.Write(body,   0, body.Length);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
    }
}
