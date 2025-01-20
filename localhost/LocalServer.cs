using System.Net;
using System.Text;
using System.Text.RegularExpressions;

internal partial class LocalServer
{
    public const string URL = "http://localhost:8000/";
    private HttpListener? listener;
    public bool disposed = false;

    public async void Initialize()
    {
        listener = new HttpListener();
        listener.Prefixes.Add(URL);
        listener.Start();
        Message("Listening at " + URL);

        await Listen();
    }

    private async Task Listen()
    {
        try
        {
            while (listener != null && listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                HandleRequest(ctx);
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Error(ex);
            Dispose();
        }
    }

    private static bool TryGetActualPath(string req, out string path)
    {
        if (req.Length > 0)
            req = req[1..];

        if (req.Length == 0) req = "index.html";

        path = PathSafety().Replace(req, ".");
        if (!File.Exists(path))
        {
            path = null!;
            return false;
        }
        return true;
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        Stream output = null!;
        try
        {
            string message = req.RemoteEndPoint + " requested " + req.RawUrl + " - ";
            if (req.Url!.AbsolutePath == "/")
            {
                message += "route to index.html - ";
            }

            byte[] buffer = [];
            if (TryGetActualPath(req.Url.AbsolutePath, out string path))
            {
                // Get file contents
                buffer = File.ReadAllBytes(path);
                res.ContentType = GetMimeType(req.Url.AbsolutePath);

                message += "found. (200)";
            }
            else
            {
                // Not found
                buffer = Encoding.UTF8.GetBytes("404 Not Found!");
                res.ContentType = "text/plain";
                res.StatusCode = 404;
                message += "not found! (404)";
            }

            // Get a response stream and write the response to it.
            res.ContentLength64 = buffer.Length;
            output = res.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close(); // You must close the output stream.
            output = null!;
            Message(message);
        }
        catch (IOException ioex)
        {
            Message("IO error while handling request " + req.RawUrl + " (requested by " + req.RemoteEndPoint + "); likely connection interrupted. Ignored.");
            Error(ioex);
        }
        catch (Exception ex)
        {
            Message("Fatal error while handling request " + req?.ToString() + "");
            Error(ex);
            Dispose();
        }
        finally
        {
            res.Close();
            output?.Close();
        }
    }

    private static string GetMimeType(string file)
    {
        string type = null!;
        if (file == "/")
        {
            return "text/html";
        }
        else if (file[file.LastIndexOf('/')..].LastIndexOf('.') > -1)
        {
            type = file[file.LastIndexOf('.')..];
        }
        return type switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".json" => "application/json",
            ".txt" => "text/plain",
            ".jpeg" or ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".ico" => "image/vnd.microsoft.icon",
            ".otf" => "font/otf",
            ".ttf" => "font/ttf",
            _ => "text/plain"
        };
    }

    public void Dispose()
    {
        if (listener == null) return;
        try
        {
            Message("Shutting down");
            listener.Stop();
            listener.Close();
            listener = null!;
        }
        catch (Exception ex)
        {
            Error(ex);
        }
        disposed = true;
    }

    private static void Message(object message) => Message(message?.ToString() ?? "{NULL}");
    private static void Message(string message)
    {
        Console.WriteLine(DateTime.Now.ToString() + ": " + message);
    }

    private static void Error(object message) => Error(message?.ToString() ?? "{NULL}");
    private static void Error(string message)
    {
        Console.Error.WriteLine(DateTime.Now.ToString() + ": " + message);
    }

    static void Main()
    {
        var server = new LocalServer();
        server.Initialize();
        while (!server.disposed) { }
    }

    [GeneratedRegex(@"\.+")]
    private static partial Regex PathSafety();
}