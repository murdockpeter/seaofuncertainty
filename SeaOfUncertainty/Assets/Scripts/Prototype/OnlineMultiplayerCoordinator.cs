using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SeaOfUncertainty.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    public enum OnlinePreviewState { Offline, Working, Lobby, Starting, Connected, Error }

    /// <summary>
    /// Developer-facing activation shell for the production Sessions/Relay stack. It intentionally
    /// keeps online play behind a launch flag until the two-build privacy gate is signed off.
    /// </summary>
    public sealed class OnlineMultiplayerCoordinator : MonoBehaviour
    {
        public event Action Changed;

        public OnlinePreviewState State { get; private set; } = OnlinePreviewState.Offline;
        public string Status { get; private set; } = "No online session is active.";
        public string JoinCode => gateway?.JoinCode ?? string.Empty;
        public string DirectJoinCode => lifecycle?.Lobby?.JoinCode ?? directJoinCode ?? string.Empty;
        public bool IsDirect { get; private set; }
        public bool IsHost => IsDirect ? directHost : gateway?.IsHost ?? false;
        public bool IsReady { get; private set; }
        public long LastRoundTripMilliseconds { get; private set; } = -1;
        public ISession Session => gateway?.Session;
        public int PlayerCount => Session?.PlayerCount ?? 0;
        public bool CanStart => IsHost && (IsDirect ? lifecycle?.Lobby?.Players.Count == 2 && lifecycle.Lobby.Players.All(player => player.IsReady) : PlayerCount == 2 && AllPlayersReady);
        public bool AllPlayersReady => IsDirect ? lifecycle?.Lobby?.Players.Count == 2 && lifecycle.Lobby.Players.All(player => player.IsReady) : Session != null && Session.Players.Count == 2 && Session.Players.All(PlayerReady);

        private UnityMultiplayerSessionGateway gateway;
        private MultiplayerMatchLifecycle lifecycle;
        private MultiplayerAccess hostAccess;
        private AuthoritativeNetworkHostBridge hostBridge;
        private AuthoritativeUtpTransport directTransport;
        private AuthoritativeWireProtocol directProtocol;
        private MultiplayerAccess directGuestAccess;
        private string directPlayerId;
        private string directCredential;
        private string directJoinCode;
        private bool directHost;
        private long pingSentAt;

        public static bool IsDeveloperPreviewEnabled(string[] arguments = null, bool? isEditor = null, bool? isDebugBuild = null)
        {
            string[] args = arguments ?? Environment.GetCommandLineArgs();
            bool explicitFlag = args.Any(item => string.Equals(item, "-enableOnlinePreview", StringComparison.OrdinalIgnoreCase));
            return explicitFlag || (isEditor ?? Application.isEditor) || (isDebugBuild ?? Debug.isDebugBuild);
        }

        public async Task HostAsync(string commanderName, string scenarioId)
        {
            await RunAsync(async () =>
            {
                lifecycle = new MultiplayerMatchLifecycle();
                hostAccess = lifecycle.Create("Sea of Uncertainty Preview", scenarioId, 1978, commanderName, true);
                if (!hostAccess.Success) throw new InvalidOperationException(hostAccess.Message);
                gateway = CreateGateway();
                await gateway.HostAsync(hostAccess.Lobby, commanderName);
                AttachTransportDiagnostics();
                State = OnlinePreviewState.Lobby;
                Status = "Private lobby created. Share the six-character join code with the opposing commander.";
            }, "Creating a private Unity Multiplayer Services lobby...");
        }

        public async Task HostDirectAsync(string commanderName, ushort port)
        {
            await RunAsync(async () =>
            {
                IsDirect = true;
                directHost = true;
                lifecycle = new MultiplayerMatchLifecycle();
                hostAccess = lifecycle.Create("Direct IP Match", "meridian-veil", 1978, commanderName, true);
                if (!hostAccess.Success) throw new InvalidOperationException(hostAccess.Message);
                directTransport = new AuthoritativeUtpTransport();
                AttachDirectTransportDiagnostics();
                directProtocol = new AuthoritativeWireProtocol(lifecycle);
                directTransport.PeerMessageReceived += OnDirectHostMessage;
                await directTransport.StartDirectHostAsync(port);
                State = OnlinePreviewState.Lobby;
                Status = $"Listening on UDP port {port}. Share the address, port, and join code {DirectJoinCode}. Use a LAN/VPN or configure UDP port forwarding.";
            }, $"Opening direct UDP port {port}...");
        }

        public async Task JoinDirectAsync(string address, ushort port, string joinCode, string commanderName)
        {
            await RunAsync(async () =>
            {
                IsDirect = true;
                directHost = false;
                directJoinCode = NormalizeCode(joinCode);
                directTransport = new AuthoritativeUtpTransport();
                AttachDirectTransportDiagnostics();
                await directTransport.StartDirectClientAsync(address, port);
                var join = new MultiplayerWirePacket { Kind = MultiplayerWireKind.Join, Code = directJoinCode, Message = commanderName };
                if (!directTransport.SendToAuthority(JsonUtility.ToJson(join, false))) throw new InvalidOperationException("Direct join request could not be queued.");
                State = OnlinePreviewState.Lobby;
                Status = "Connected to the direct host; authenticating the Red seat...";
            }, $"Connecting directly to {address}:{port}...");
        }

        public async Task JoinAsync(string joinCode, string commanderName)
        {
            await RunAsync(async () =>
            {
                gateway = CreateGateway();
                await gateway.JoinByCodeAsync(NormalizeCode(joinCode), commanderName);
                AttachTransportDiagnostics();
                State = OnlinePreviewState.Lobby;
                Status = "Joined as Red command. Mark ready when setup is confirmed.";
            }, "Joining the private lobby...");
        }

        public async Task RefreshAsync()
        {
            if (Session == null) return;
            await RunAsync(async () =>
            {
                await Session.RefreshAsync();
                State = gateway.Transport != null && gateway.Transport.IsRunning ? OnlinePreviewState.Connected : OnlinePreviewState.Lobby;
                Status = LobbySummary();
            }, "Refreshing lobby membership...");
        }

        public async Task SetReadyAsync(bool ready)
        {
            if (IsDirect)
            {
                if (directHost)
                {
                    MultiplayerAccess result = lifecycle.SetReady(hostAccess.PlayerId, hostAccess.ReconnectCredential, ready);
                    IsReady = result.Success && ready;
                    Status = result.Message;
                    State = OnlinePreviewState.Lobby;
                    Changed?.Invoke();
                }
                else if (!string.IsNullOrEmpty(directPlayerId))
                {
                    IsReady = ready;
                    var packet = new MultiplayerWirePacket { Kind = MultiplayerWireKind.Ready, PlayerId = directPlayerId, Credential = directCredential, Code = ready ? "true" : "false" };
                    directTransport.SendToAuthority(JsonUtility.ToJson(packet, false));
                    Status = "Readiness update sent to the direct authority.";
                    Changed?.Invoke();
                }
                return;
            }
            if (Session == null) return;
            await RunAsync(async () =>
            {
                await gateway.SetReadyAsync(ready);
                IsReady = ready;
                await Session.RefreshAsync();
                State = OnlinePreviewState.Lobby;
                Status = ready ? "Ready locked. Waiting for the other commander or host start." : "Readiness cleared.";
            }, ready ? "Locking readiness..." : "Clearing readiness...");
        }

        public async Task StartAsync()
        {
            if (!CanStart)
            {
                Status = "Both authenticated commanders must be present and ready before Relay starts.";
                Changed?.Invoke();
                return;
            }
            await RunAsync(async () =>
            {
                State = OnlinePreviewState.Starting;
                Changed?.Invoke();
                if (IsDirect)
                {
                    MultiplayerAccess started = lifecycle.Start(hostAccess.PlayerId, hostAccess.ReconnectCredential);
                    if (!started.Success) throw new InvalidOperationException(started.Message);
                    directProtocol = new AuthoritativeWireProtocol(lifecycle);
                    var start = new MultiplayerWirePacket
                    {
                        Kind = MultiplayerWireKind.StartResult, Code = "STARTED", Message = "Direct authoritative match started.",
                        ReconnectJson = JsonUtility.ToJson(lifecycle.Authority.Reconnect(Side.Red, -1, string.Empty), false)
                    };
                    directTransport.Broadcast(JsonUtility.ToJson(start, false));
                    State = OnlinePreviewState.Connected;
                    Status = "Direct authoritative match started. Cloud Services was not used.";
                    return;
                }
                MirrorServiceLobbyIntoAuthority();
                await gateway.StartRelayAsync();
                AttachTransportDiagnostics();
                hostBridge?.Dispose();
                hostBridge = new AuthoritativeNetworkHostBridge(gateway.Transport, lifecycle);
                State = OnlinePreviewState.Connected;
                Status = "DTLS Relay is running and the host authority is accepting authenticated protocol traffic.";
            }, "Starting DTLS Relay and authoritative match...");
        }

        public void SendPing()
        {
            AuthoritativeUtpTransport transport = ActiveTransport;
            if (transport == null || !transport.IsRunning)
            {
                Status = "Relay transport is not connected yet.";
                Changed?.Invoke();
                return;
            }
            pingSentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var packet = new MultiplayerWirePacket { Kind = MultiplayerWireKind.Ping, ClientTimestamp = pingSentAt };
            if (!transport.SendToAuthority(JsonUtility.ToJson(packet, false)))
                Status = IsHost ? "Host authority is local; run latency diagnostics from the guest build." : "Ping could not be queued.";
            else Status = "Latency probe sent...";
            Changed?.Invoke();
        }

        public async Task LeaveAsync()
        {
            if (gateway == null && directTransport == null) return;
            await RunAsync(async () =>
            {
                hostBridge?.Dispose();
                hostBridge = null;
                if (gateway != null) await gateway.LeaveAsync();
                if (directTransport != null)
                {
                    directTransport.PeerMessageReceived -= OnDirectHostMessage;
                    await directTransport.StopAsync();
                }
                gateway = null;
                directTransport = null;
                lifecycle = null;
                hostAccess = null;
                IsReady = false;
                LastRoundTripMilliseconds = -1;
                IsDirect = false;
                directHost = false;
                directJoinCode = null;
                State = OnlinePreviewState.Offline;
                Status = "Online session closed.";
            }, "Leaving online session...");
        }

        public string PlayerSummary()
        {
            if (IsDirect)
            {
                if (lifecycle?.Lobby?.Players == null) return string.IsNullOrEmpty(directPlayerId) ? "Direct seat authentication pending." : "RED  •  authenticated direct client";
                return string.Join("\n", lifecycle.Lobby.Players.Select(player => $"{player.Side.ToString().ToUpperInvariant()}  •  {player.DisplayName}  •  {(player.IsReady ? "READY" : "NOT READY")}{(player.IsHost ? " • HOST" : string.Empty)}"));
            }
            if (Session == null || Session.Players.Count == 0) return "No authenticated commanders.";
            return string.Join("\n", Session.Players.Select(player =>
            {
                string name = Property(player, "name", "Commander");
                string side = Property(player, "side", player.Id == Session.Host ? "Blue" : "Red");
                string ready = PlayerReady(player) ? "READY" : "NOT READY";
                string host = player.Id == Session.Host ? " • HOST" : string.Empty;
                return $"{side.ToUpperInvariant()}  •  {name}  •  {ready}{host}";
            }));
        }

        private UnityMultiplayerSessionGateway CreateGateway()
            => new UnityMultiplayerSessionGateway(CreateMigrationBytes, ApplyMigrationBytes);

        private byte[] CreateMigrationBytes()
        {
            MultiplayerRecoveryCheckpoint checkpoint = lifecycle?.CreateCheckpoint(hostAccess?.PlayerId, hostAccess?.ReconnectCredential, MigrationKey());
            return checkpoint == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(JsonUtility.ToJson(checkpoint, false));
        }

        private void ApplyMigrationBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            MultiplayerRecoveryCheckpoint checkpoint = JsonUtility.FromJson<MultiplayerRecoveryCheckpoint>(Encoding.UTF8.GetString(bytes));
            MultiplayerMatchLifecycle restored = MultiplayerMatchLifecycle.RestoreCheckpoint(checkpoint, MigrationKey());
            if (restored != null) lifecycle = restored;
        }

        private void MirrorServiceLobbyIntoAuthority()
        {
            if (lifecycle?.Lobby == null || hostAccess == null) throw new InvalidOperationException("Host authority lifecycle is unavailable.");
            MultiplayerAccess guest = lifecycle.JoinByCode(lifecycle.Lobby.JoinCode, Session.Players.First(player => player.Id != Session.Host).Properties.TryGetValue("name", out PlayerProperty name) ? name.Value : "Remote Commander");
            if (!guest.Success) throw new InvalidOperationException(guest.Message);
            if (!lifecycle.SetReady(hostAccess.PlayerId, hostAccess.ReconnectCredential, true).Success ||
                !lifecycle.SetReady(guest.PlayerId, guest.ReconnectCredential, true).Success ||
                !lifecycle.Start(hostAccess.PlayerId, hostAccess.ReconnectCredential).Success)
                throw new InvalidOperationException("The authoritative lobby could not mirror the ready service session.");
        }

        private void AttachTransportDiagnostics()
        {
            if (gateway?.Transport == null) return;
            gateway.Transport.MessageReceived -= OnTransportMessage;
            gateway.Transport.MessageReceived += OnTransportMessage;
            gateway.Transport.Connected -= OnTransportConnected;
            gateway.Transport.Connected += OnTransportConnected;
            gateway.Transport.Disconnected -= OnTransportDisconnected;
            gateway.Transport.Disconnected += OnTransportDisconnected;
        }

        private AuthoritativeUtpTransport ActiveTransport => IsDirect ? directTransport : gateway?.Transport;

        private void AttachDirectTransportDiagnostics()
        {
            if (directTransport == null) return;
            directTransport.MessageReceived -= OnTransportMessage;
            directTransport.MessageReceived += OnTransportMessage;
            directTransport.Connected -= OnTransportConnected;
            directTransport.Connected += OnTransportConnected;
            directTransport.Disconnected -= OnTransportDisconnected;
            directTransport.Disconnected += OnTransportDisconnected;
        }

        private void OnDirectHostMessage(int peerId, string json)
        {
            MultiplayerWirePacket packet = JsonUtility.FromJson<MultiplayerWirePacket>(json);
            if (packet == null) return;
            if (packet.Kind == MultiplayerWireKind.Join)
            {
                directGuestAccess = lifecycle.JoinByCode(packet.Code, packet.Message);
                MultiplayerWirePacket result = directGuestAccess.Success
                    ? new MultiplayerWirePacket { Kind = MultiplayerWireKind.JoinResult, PlayerId = directGuestAccess.PlayerId, Credential = directGuestAccess.ReconnectCredential, Code = directGuestAccess.Code, Message = directGuestAccess.Message }
                    : new MultiplayerWirePacket { Kind = MultiplayerWireKind.Error, Code = directGuestAccess.Code, Message = directGuestAccess.Message };
                directTransport.SendToPeer(peerId, JsonUtility.ToJson(result, false));
                Status = directGuestAccess.Message;
                Changed?.Invoke();
                return;
            }
            if (packet.Kind == MultiplayerWireKind.Ready)
            {
                MultiplayerAccess ready = lifecycle.SetReady(packet.PlayerId, packet.Credential, string.Equals(packet.Code, "true", StringComparison.OrdinalIgnoreCase));
                directTransport.SendToPeer(peerId, JsonUtility.ToJson(new MultiplayerWirePacket { Kind = ready.Success ? MultiplayerWireKind.ReadyResult : MultiplayerWireKind.Error, Code = ready.Code, Message = ready.Message }, false));
                Status = ready.Message;
                Changed?.Invoke();
                return;
            }
            directTransport.SendToPeer(peerId, directProtocol.Handle(json));
        }

        private void OnTransportConnected()
        {
            State = OnlinePreviewState.Connected;
            Status = IsHost ? "Relay authority transport connected." : "Connected to the Relay authority.";
            Changed?.Invoke();
        }

        private void OnTransportDisconnected()
        {
            State = OnlinePreviewState.Lobby;
            Status = "Relay transport disconnected. The authenticated Session seat remains available for reconnect.";
            Changed?.Invoke();
        }

        private void OnTransportMessage(string json)
        {
            MultiplayerWirePacket packet = JsonUtility.FromJson<MultiplayerWirePacket>(json);
            if (packet == null) return;
            if (packet.Kind == MultiplayerWireKind.JoinResult)
            {
                directPlayerId = packet.PlayerId;
                directCredential = packet.Credential;
                Status = packet.Message + " Mark ready when setup is confirmed.";
                State = OnlinePreviewState.Lobby;
                Changed?.Invoke();
                return;
            }
            if (packet.Kind == MultiplayerWireKind.ReadyResult || packet.Kind == MultiplayerWireKind.StartResult || packet.Kind == MultiplayerWireKind.Error)
            {
                Status = packet.Message;
                State = packet.Kind == MultiplayerWireKind.StartResult ? OnlinePreviewState.Connected : packet.Kind == MultiplayerWireKind.Error ? OnlinePreviewState.Error : OnlinePreviewState.Lobby;
                Changed?.Invoke();
                return;
            }
            if (packet.Kind != MultiplayerWireKind.Pong) return;
            LastRoundTripMilliseconds = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - packet.ClientTimestamp);
            Status = $"Authority round trip: {LastRoundTripMilliseconds} ms.";
            Changed?.Invoke();
        }

        private async Task RunAsync(Func<Task> operation, string workingStatus)
        {
            State = OnlinePreviewState.Working;
            Status = workingStatus;
            Changed?.Invoke();
            try { await operation(); }
            catch (Exception exception)
            {
                State = OnlinePreviewState.Error;
                Status = FriendlyError(exception);
                Debug.LogException(exception);
            }
            Changed?.Invoke();
        }

        private string LobbySummary() => $"Private lobby active • {PlayerCount}/2 commanders • {(AllPlayersReady ? "both ready" : "readiness pending")}.";
        private string MigrationKey() => hostAccess?.ReconnectCredential ?? "online-preview-not-started";
        private static bool PlayerReady(IReadOnlyPlayer player) => string.Equals(Property(player, "ready", "false"), "true", StringComparison.OrdinalIgnoreCase);
        private static string Property(IReadOnlyPlayer player, string key, string fallback) => player.Properties.TryGetValue(key, out PlayerProperty value) ? value.Value : fallback;
        private static string NormalizeCode(string value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

        private static string FriendlyError(Exception exception)
        {
            string message = exception?.GetBaseException().Message ?? "Unknown multiplayer error.";
            if (message.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("environment", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Unity Cloud Services is not linked for this project yet. Link a Cloud project and environment, then retry. Details: " + message;
            return "Online preview error: " + message;
        }

        private void OnDestroy()
        {
            hostBridge?.Dispose();
            if (ActiveTransport != null) ActiveTransport.MessageReceived -= OnTransportMessage;
        }
    }
}
