using HomeAssistant.Hub.Dsmr.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace HomeAssistant.Hub.Dsmr
{
    public class DsmrClient
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static DsmrClient _instance;
        private volatile bool isRunning;
        private static readonly Object _telegramLock = new Object();
        private static readonly Object _clientsLock = new Object();

        private IPAddress remoteHost;
        private int remotePort;
        private int localPort;
        private double intervalMinutes;
        private ObisParser parser;
        private Telegram lastReceivedTelegram;
        private Thread readerThread;
        private TcpClient readerClient;
        private TcpListener listener;
        private readonly IList<TcpClientHandle> clients = new List<TcpClientHandle>();

        public static DsmrClient Instance => _instance ?? (_instance = new DsmrClient());

        private DsmrClient()
        {
            InitializeDsmrClient();
        }

        private void InitializeDsmrClient()
        {
            remoteHost = IPAddress.Parse(ConfigurationManager.AppSettings["dsmr_remote_ip"]);
            remotePort = int.Parse(ConfigurationManager.AppSettings["dsmr_remote_port"]);
            localPort = int.Parse(ConfigurationManager.AppSettings["dsmr_local_port"]);
            intervalMinutes = double.Parse(ConfigurationManager.AppSettings["dsmr_interval"]);
            isRunning = false;
            parser = new ObisParser();
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
            listener = new TcpListener(IPAddress.Parse("127.0.0.1"), localPort);
            listener.Start();

            while (isRunning)
            {
                TcpClient newClient = await listener.AcceptTcpClientAsync();
                lock (_clientsLock)
                {
                    clients.Add(new TcpClientHandle(newClient));
                    logger.Info("Client connected to DSMR");
                }
            }

            lock (_clientsLock)
            {
                foreach (var client in clients)
                {
                    DisconnectClient(client);
                }
                clients.Clear();
            }
        }

        private async Task HandleConnections()
        {
            while (isRunning)
            {
                lock (_clientsLock)
                {
                    var disconnectClients = clients.Where(c => c.ShouldDisconnect).ToArray();
                    for (var i = disconnectClients.Length - 1; i >= 0; i--)
                    {
                        DisconnectClient(disconnectClients[i]);
                        clients.Remove(disconnectClients[i]);
                    }

                    var availableClients = clients.Where(c => !c.ShouldDisconnect);
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
                readerClient.Connect(remoteHost, remotePort);
                await parser.ParseFromStream(readerClient.GetStream(), (object sender, Telegram telegram) =>
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

                if (lastReceivedTelegram.Timestamp > client.LastSent.AddMinutes(intervalMinutes))
                {
                    try
                    {
                        var message = ObisParser.TelegramEncoding.GetBytes(lastReceivedTelegram.ToString());
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
