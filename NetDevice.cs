using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PiHome
{
    sealed class NetDevice : IDisposable
    {
        public delegate void CommandHandler(NetDevice source);
        
        public Dictionary<string, CommandHandler> Commands
        {
            get; set;
        } = new();

        public IManagedMqttClient Client
        {
            get; private set;
        }

        public ManagedMqttClientOptions Options
        {
            get; private set;
        }

        public NetDevice(string clientId, string username, string password, string address, int? port, int reconnectTime=5)
        {
            Options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(reconnectTime))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId(clientId)
                    .WithTcpServer(address, port)
                    .WithCredentials(username, password)
                    //.WithTls()
                    .Build())
                .Build();
            Client = new MqttFactory().CreateManagedMqttClient();
            Client.UseApplicationMessageReceivedHandler(OnMessageReceived);
            Client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(clientId + "/command").Build());
        }

        bool _disposed = false;

        ~NetDevice()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                Client.Dispose();
            }
            _disposed = true;
        }
        private void OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            string command = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
            if (Commands.ContainsKey(command))
            {
                Commands[command](this);
            }
        }

        public async void ConnectAsync()
        {
            await Client.StartAsync(Options);
        }
    }
}
