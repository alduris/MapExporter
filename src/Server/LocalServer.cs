using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                while (listener.IsListening)
                {
                    var ctx = await listener.GetContextAsync();
                    HandleRequest(ctx);
                }
            }
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
            try
            {
                Message(req.RemoteEndPoint + " requested " + req.RawUrl);
                string responseString = $"<HTML><BODY><P>{Resources.SafePath(req.Url.AbsolutePath)}</P><P>{File.Exists(Resources.SafePath(req.Url.AbsolutePath))}</P></BODY></HTML>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                // Get a response stream and write the response to it.
                res.ContentType = "text/html";
                res.ContentLength64 = buffer.Length;
                Stream output = res.OutputStream;
                output.Write(buffer, 0, buffer.Length);
            
                output.Close(); // You must close the output stream.
            }
            catch (Exception ex)
            {
                Message("Errored while handling request " + req?.ToString() + "");
                Plugin.Logger.LogError(ex);
                Dispose();
            }
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
