using HomeAssistant.Hub.Models;
using NLog;
using SimpleDI;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace HomeAssistant.Hub.Webhooks
{
    public sealed class WebhookService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly WebhookConfig _settings;
        private readonly HttpListener _httpListener;

        public delegate void MessageReceivedEventHandler(object sender, WebhookMessage message);
        public event MessageReceivedEventHandler MessageReceived;

        public WebhookService(IOptions<WebhookConfig> settings)
        {
            _settings = settings.Value;
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_settings.LocalPort}/");
        }

        public void Start()
        {
            if (_httpListener.IsListening)
            {
                return;
            }

            try
            {
                _httpListener.Start();
                var listenerThread = new Thread(() => { ListenToConnections(); });
                listenerThread.Start();
                logger.Info("Webhook listener started");
            }
            catch (Exception e)
            {
                logger.Error(e, "Could not start webhook listener");
            }
            
        }

        public void Stop()
        {
            if (!_httpListener.IsListening)
            {
                return;
            }

            _httpListener.Abort();
            _httpListener.Close();
            logger.Info("Webhook listener stopped");
        }

        private void ListenToConnections()
        {
            while (_httpListener.IsListening)
            {
                IAsyncResult result = _httpListener.BeginGetContext(new AsyncCallback(ListenerCallback), _httpListener);
                result.AsyncWaitHandle.WaitOne();
            }
        }

        private void ListenerCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            if (listener.IsListening)
            {
                logger.Info("Webhook listener received message");
                // Call EndGetContext to complete the asynchronous operation.
                HttpListenerContext context = listener.EndGetContext(result);
                ProcessRequest(context.Request);
                WriteResponse(context.Response);
            }
        }

        private void ProcessRequest(HttpListenerRequest request)
        {
            using (Stream stream = request.InputStream)
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                string body = Encoding.UTF8.GetString(ms.ToArray());

                var message = new WebhookMessage
                {
                    UrlSegments = request.Url.Segments,
                    Method = request.HttpMethod,
                    Data = body
                };

                MessageReceived?.Invoke(this, message);
            }
        }

        private void WriteResponse(HttpListenerResponse response)
        {
            using (Stream output = response.OutputStream)
            {
                byte[] buffer = Encoding.UTF8.GetBytes("ok");
                response.ContentLength64 = buffer.Length;
                output.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
