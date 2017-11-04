using DsmrParser.Dsmr;
using DsmrParser.Models;
using NLog;
using SimpleDI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HomeAssistant.Hub.Dsmr
{
    public sealed class DsmrService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private volatile bool isRunning;
        private static readonly Object _telegramLock = new Object();
        private static readonly Object _clientsLock = new Object();

        //private IPAddress remoteHost;
        //private int remotePort;
        //private int localPort;
        //private double intervalMinutes;
        //private Parser parser;
        private Telegram lastReceivedTelegram;
        private Thread readerThread;
        private TcpClient readerClient;
        private TcpListener listener;

        private readonly Parser _parser;
        private readonly DsmrConfig _settings;
        private readonly IList<TcpClientHandle> _clients = new List<TcpClientHandle>();

        public DsmrService(IOptions<DsmrConfig> settings)
        {
            //remoteHost = IPAddress.Parse(ConfigurationManager.AppSettings["dsmr_remote_ip"]);
            //remotePort = int.Parse(ConfigurationManager.AppSettings["dsmr_remote_port"]);
            //localPort = int.Parse(ConfigurationManager.AppSettings["dsmr_local_port"]);
            //intervalMinutes = double.Parse(ConfigurationManager.AppSettings["dsmr_interval"]);
            _parser = new Parser();
            _settings = settings.Value;
            
            isRunning = false;
        }

        public void Start()
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;

            readerClient = new TcpClient();
            readerThread = new Thread(async () => { await ReadDsmrData(); });
            readerThread.Start();

            var listenerThread = new Thread(async () => { await ListenToConnections(); });
            listenerThread.Start();

            var handlerThread = new Thread(async () => { await HandleConnections(); });
            handlerThread.Start();
        }

        public void Stop()
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;

            readerClient.Close();
            readerClient.Dispose();
            readerThread = null;

            listener.Stop();
        }

        private async Task ListenToConnections()
        {
            listener = new TcpListener(IPAddress.Parse("127.0.0.1"), _settings.LocalPort);
            listener.Start();

            while (isRunning)
            {
                TcpClient newClient = await listener.AcceptTcpClientAsync();
                lock (_clientsLock)
                {
                    _clients.Add(new TcpClientHandle(newClient));
                    logger.Info("Client connected to DSMR");
                }
            }

            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    DisconnectClient(client);
                }
                _clients.Clear();
            }
        }

        private async Task HandleConnections()
        {
            while (isRunning)
            {
                lock (_clientsLock)
                {
                    var disconnectClients = _clients.Where(c => c.ShouldDisconnect).ToArray();
                    for (var i = disconnectClients.Length - 1; i >= 0; i--)
                    {
                        DisconnectClient(disconnectClients[i]);
                        _clients.Remove(disconnectClients[i]);
                    }

                    var availableClients = _clients.Where(c => !c.ShouldDisconnect);
                    foreach (var client in availableClients)
                    {
                        WriteDataToClient(client);
                    }
                }

                await Task.Delay(1000);
            }
        }

        private async Task ReadDsmrData()
        {
            try
            {
                readerClient.Connect(_settings.RemoteHost, _settings.RemotePort);
                await _parser.ParseFromStream(readerClient.GetStream(), (object sender, Telegram telegram) =>
                {
                    lock (_telegramLock)
                    {
                        lastReceivedTelegram = telegram;
                    }
                });
            }
            catch (ObjectDisposedException e)
            {
                logger.Error(e);
            }
            catch (SocketException e)
            {
                logger.Error(e);
            }
            catch (Exception e)
            {
                logger.Error(e);
                throw;
            }
        }

        private void WriteDataToClient(TcpClientHandle client)
        {
            lock (_telegramLock)
            {
                if (lastReceivedTelegram == null)
                {
                    return;
                }

                if (lastReceivedTelegram.Timestamp > client.LastSent.AddMinutes(_settings.IntervalMinutes))
                {
                    try
                    {
                        var message = Parser.TelegramEncoding.GetBytes(lastReceivedTelegram.ToString());
                        client.Connection.GetStream().Write(message, 0, message.Length);
                        client.LastSent = DateTime.Now;
                        logger.Info("Sent DSMR telegram to client");
                    }
                    catch (IOException ex)
                    {
                        if (ex.InnerException is SocketException)
                        {
                            client.ShouldDisconnect = true;
                        }
                    }
                }
            }
        }

        private void DisconnectClient(TcpClientHandle client)
        {
            client.Connection.Close();
            client.Connection.Dispose();
            client.Connection = null;
            logger.Info("client disconnected from DSMR");
        }

        private class TcpClientHandle
        {
            public TcpClient Connection { get; set; }
            public DateTime LastSent { get; set; }
            public bool ShouldDisconnect { get; set; }

            public TcpClientHandle(TcpClient connection)
            {
                Connection = connection;
                LastSent = DateTime.MinValue;
                ShouldDisconnect = false;
            }
        }
    }
}
