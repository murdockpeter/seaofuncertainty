using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SeaOfUncertainty.Core;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    /// <summary>Unity Transport adapter used directly by MPS Sessions; no UI objects are synchronized.</summary>
    public sealed class AuthoritativeUtpTransport : INetworkHandler, IDisposable
    {
        private const int PayloadCapacity = 64 * 1024;
        private readonly List<NetworkConnection> clients = new List<NetworkConnection>();
        private NetworkDriver driver;
        private NetworkPipeline reliablePipeline;
        private NetworkConnection serverConnection;
        private NetworkRole role;
        private UtpUpdatePump pump;

        public event Action<string> MessageReceived;
        public event Action<int, string> PeerMessageReceived;
        public event Action Connected;
        public event Action Disconnected;
        public bool IsRunning => driver.IsCreated;
        public bool IsHost => role != NetworkRole.Client;

        public async Task StartAsync(NetworkConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            await StopAsync();
            role = configuration.Role;

            var settings = new NetworkSettings();
            settings.WithFragmentationStageParameters(PayloadCapacity);
            if (configuration.Type == NetworkType.Relay)
            {
                RelayServerData relay = role == NetworkRole.Client ? configuration.RelayClientData : configuration.RelayServerData;
                settings.WithRelayParameters(ref relay);
            }
            driver = NetworkDriver.Create(settings);
            reliablePipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            CreatePump();

            if (role == NetworkRole.Client)
            {
                serverConnection = configuration.Type == NetworkType.Relay
                    ? driver.Connect()
                    : driver.Connect(configuration.DirectNetworkPublishAddress);
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                while (driver.IsCreated && driver.GetConnectionState(serverConnection) == NetworkConnection.State.Connecting && DateTime.UtcNow < deadline)
                {
                    Poll();
                    await Task.Yield();
                }
                if (!driver.IsCreated || driver.GetConnectionState(serverConnection) != NetworkConnection.State.Connected)
                {
                    await StopAsync();
                    throw new InvalidOperationException("Unity Transport could not connect before the 15-second timeout.");
                }
            }
            else
            {
                NetworkEndpoint listen = configuration.Type == NetworkType.Relay ? NetworkEndpoint.AnyIpv4 : configuration.DirectNetworkListenAddress;
                if (driver.Bind(listen) < 0 || driver.Listen() < 0)
                {
                    await StopAsync();
                    throw new InvalidOperationException("Unity Transport could not bind and listen.");
                }
                if (configuration.Type == NetworkType.Direct && listen.Port == 0) configuration.UpdatePublishPort(driver.GetLocalEndpoint().Port);
                Connected?.Invoke();
            }
        }

        public async Task StartDirectHostAsync(ushort port)
        {
            if (port == 0) throw new ArgumentOutOfRangeException(nameof(port), "A non-zero UDP port is required.");
            await StopAsync();
            role = NetworkRole.Host;
            CreateDirectDriver();
            NetworkEndpoint listen = NetworkEndpoint.AnyIpv4.WithPort(port);
            if (driver.Bind(listen) < 0 || driver.Listen() < 0)
            {
                await StopAsync();
                throw new InvalidOperationException($"Unity Transport could not listen on UDP port {port}.");
            }
            Connected?.Invoke();
        }

        public async Task StartDirectClientAsync(string address, ushort port)
        {
            if (string.IsNullOrWhiteSpace(address) || !NetworkEndpoint.TryParse(address.Trim(), port, out NetworkEndpoint endpoint))
                throw new ArgumentException("Enter a numeric IPv4 or IPv6 address and UDP port.", nameof(address));
            await StopAsync();
            role = NetworkRole.Client;
            CreateDirectDriver();
            serverConnection = driver.Connect(endpoint);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (driver.IsCreated && driver.GetConnectionState(serverConnection) == NetworkConnection.State.Connecting && DateTime.UtcNow < deadline)
            {
                Poll();
                await Task.Yield();
            }
            if (!driver.IsCreated || driver.GetConnectionState(serverConnection) != NetworkConnection.State.Connected)
            {
                await StopAsync();
                throw new InvalidOperationException("Unity Transport could not connect before the 15-second timeout. Check the address, UDP port, firewall, and port forwarding.");
            }
        }

        public Task StopAsync()
        {
            if (driver.IsCreated)
            {
                foreach (NetworkConnection connection in clients)
                    if (connection.IsCreated) driver.Disconnect(connection);
                if (serverConnection.IsCreated) driver.Disconnect(serverConnection);
                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }
            clients.Clear();
            serverConnection = default;
            if (pump != null)
            {
                UnityEngine.Object.Destroy(pump.gameObject);
                pump = null;
            }
            return Task.CompletedTask;
        }

        public bool SendToAuthority(string json)
            => role == NetworkRole.Client && Send(serverConnection, json);

        public int Broadcast(string json)
        {
            if (role == NetworkRole.Client) return 0;
            int sent = 0;
            foreach (NetworkConnection connection in clients)
                if (Send(connection, json)) sent++;
            return sent;
        }

        public bool SendToPeer(int peerId, string json)
        {
            if (role == NetworkRole.Client) return false;
            foreach (NetworkConnection connection in clients)
                if (connection.GetHashCode() == peerId) return Send(connection, json);
            return false;
        }

        public void Poll()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (role != NetworkRole.Client)
            {
                NetworkConnection accepted;
                while ((accepted = driver.Accept()) != default)
                {
                    clients.Add(accepted);
                    Connected?.Invoke();
                }
            }

            if (role == NetworkRole.Client) ReadConnection(ref serverConnection);
            else
            {
                for (int index = clients.Count - 1; index >= 0; index--)
                {
                    NetworkConnection connection = clients[index];
                    ReadConnection(ref connection);
                    if (!connection.IsCreated) clients.RemoveAt(index);
                    else clients[index] = connection;
                }
            }
        }

        public void Dispose() => StopAsync().GetAwaiter().GetResult();

        private bool Send(NetworkConnection connection, string json)
        {
            if (!driver.IsCreated || !connection.IsCreated || driver.GetConnectionState(connection) != NetworkConnection.State.Connected) return false;
            byte[] bytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
            if (bytes.Length + sizeof(int) > PayloadCapacity) return false;
            int status = driver.BeginSend(reliablePipeline, connection, out DataStreamWriter writer, bytes.Length + sizeof(int));
            if (status < 0) return false;
            writer.WriteInt(bytes.Length);
            for (int index = 0; index < bytes.Length; index++) writer.WriteByte(bytes[index]);
            return driver.EndSend(writer) >= 0;
        }

        private void ReadConnection(ref NetworkConnection connection)
        {
            NetworkEvent.Type eventType;
            while (connection.IsCreated && (eventType = connection.PopEvent(driver, out DataStreamReader reader)) != NetworkEvent.Type.Empty)
            {
                if (eventType == NetworkEvent.Type.Connect) Connected?.Invoke();
                else if (eventType == NetworkEvent.Type.Disconnect)
                {
                    connection = default;
                    Disconnected?.Invoke();
                }
                else if (eventType == NetworkEvent.Type.Data)
                {
                    int length = reader.ReadInt();
                    if (length < 0 || length > reader.Length - sizeof(int)) continue;
                    byte[] bytes = new byte[length];
                    for (int index = 0; index < length; index++) bytes[index] = reader.ReadByte();
                    string payload = Encoding.UTF8.GetString(bytes);
                    MessageReceived?.Invoke(payload);
                    if (role != NetworkRole.Client) PeerMessageReceived?.Invoke(connection.GetHashCode(), payload);
                }
            }
        }

        private void CreatePump()
        {
            var host = new GameObject("Sea of Uncertainty - UTP Pump");
            UnityEngine.Object.DontDestroyOnLoad(host);
            pump = host.AddComponent<UtpUpdatePump>();
            pump.Transport = this;
        }

        private void CreateDirectDriver()
        {
            var settings = new NetworkSettings();
            settings.WithFragmentationStageParameters(PayloadCapacity);
            driver = NetworkDriver.Create(settings);
            reliablePipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            CreatePump();
        }
    }

    public sealed class UtpUpdatePump : MonoBehaviour
    {
        public AuthoritativeUtpTransport Transport { private get; set; }
        private void Update() => Transport?.Poll();
        private void OnApplicationQuit() => Transport?.Dispose();
    }

    /// <summary>Routes each authority response only to its originating Relay/UTP peer.</summary>
    public sealed class AuthoritativeNetworkHostBridge : IDisposable
    {
        private readonly AuthoritativeUtpTransport transport;
        private readonly AuthoritativeWireProtocol protocol;
        public AuthoritativeNetworkHostBridge(AuthoritativeUtpTransport transport, MultiplayerMatchLifecycle lifecycle)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            protocol = new AuthoritativeWireProtocol(lifecycle);
            transport.PeerMessageReceived += OnMessage;
        }
        private void OnMessage(int peerId, string payload) => transport.SendToPeer(peerId, protocol.Handle(payload));
        public void Dispose() => transport.PeerMessageReceived -= OnMessage;
    }
}
