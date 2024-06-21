using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MapExporter.Server
{
    public class LocalServer : IDisposable
    {
        public const string URL = "http://localhost:8000/";
        private HttpListener listener;

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
                Plugin.Logger.LogError(ex);
                Dispose();
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            Stream output = null;
            try
            {
                bool print = !(req.Url.AbsolutePath.StartsWith("/slugcats/") && req.Url.AbsolutePath.EndsWith(".png")); // don't spam the print thingy with tile requests
                string message = req.RemoteEndPoint + " requested " + req.RawUrl + " - ";
                if (req.Url.AbsolutePath == "/")
                {
                    message += "route to index.html - ";
                }

                byte[] buffer = [];

                if (Resources.TryGetJsonResource(req.Url.AbsolutePath, out buffer))
                {
                    res.ContentType = "application/json";
                    message += "json resource. (200)";
                }
                else if (Resources.TryGetTile(req.Url.AbsolutePath, out buffer))
                {
                    res.ContentType = "image/png";
                    message += "tile. (200)";
                }
                else if (Resources.TryGetActualPath(req.Url.AbsolutePath, out string path))
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
                output = null;

                // Message
                if (print) // tile requests spam the console, we don't care about that
                    Message(message);
            }
            catch (IOException ex)
            {
                Message("IO error while handling request " + req.RawUrl + " (requested by " + req.RemoteEndPoint + "); likely connection interrupted. Ignored.");
                Plugin.Logger.LogError(ex);
            }
            catch (Exception ex)
            {
                Message("Fatal error while handling request " + req?.ToString() + "");
                Plugin.Logger.LogError(ex);
                Dispose();
            }
            finally
            {
                res.Close();
                output?.Close();
            }
        }

        private string GetMimeType(string file)
        {
            string type = null;
            if (file == "/")
            {
                return "text/html";
            }
            else if (file.Substring(file.LastIndexOf('/')).LastIndexOf('.') > -1)
            {
                type = file.Substring(file.LastIndexOf('.'));
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
                listener = null;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
            }
        }

        private void Message(object message) => Message(message.ToString());
        private void Message(string message)
        {
            OnMessage?.Invoke(DateTime.Now.ToString() + ": " + message);
        }

        public event Action<string> OnMessage;
    }
}
