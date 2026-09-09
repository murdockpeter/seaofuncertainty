using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SeaOfUncertainty.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Relay.Models;

namespace SeaOfUncertainty.Prototype
{
    /// <summary>Production UGS Sessions/Lobby/Relay gateway for the authoritative command transport.</summary>
    public sealed class UnityMultiplayerSessionGateway
    {
        public const string SessionType = "sea-of-uncertainty-authority-v1";
        public ISession Session { get; private set; }
        public AuthoritativeUtpTransport Transport { get; private set; }
        public bool IsHost => Session != null && Session.CurrentPlayer != null && Session.CurrentPlayer.Id == Session.Host;
        public string JoinCode => Session?.Code ?? string.Empty;

        private readonly IMigrationDataHandler migrationData;

        public UnityMultiplayerSessionGateway(Func<byte[]> generateRecovery, Action<byte[]> applyRecovery)
        {
            migrationData = new AuthorityMigrationDataHandler(generateRecovery, applyRecovery);
        }

        public async Task InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async Task<IHostSession> HostAsync(MultiplayerLobby lobby, string playerName)
        {
            if (lobby == null) throw new ArgumentNullException(nameof(lobby));
            await InitializeAsync();
            Transport = new AuthoritativeUtpTransport();
            var options = new SessionOptions
            {
                Type = SessionType,
                Name = lobby.Name,
                MaxPlayers = 2,
                IsPrivate = lobby.IsPrivate,
                SessionProperties = SessionProperties(lobby),
                PlayerProperties = PlayerProperties(playerName, Side.Blue)
            };
            options.WithNetworkHandler(Transport).WithHostMigration(migrationData);
            IHostSession created = await MultiplayerService.Instance.CreateSessionAsync(options);
            created.ConcurrencyControlEnabled = true;
            Session = created;
            return created;
        }

        public async Task<ISession> JoinByCodeAsync(string code, string playerName)
        {
            await InitializeAsync();
            Transport = new AuthoritativeUtpTransport();
            var options = new JoinSessionOptions
            {
                Type = SessionType,
                PlayerProperties = PlayerProperties(playerName, Side.Red)
            };
            options.WithNetworkHandler(Transport).WithHostMigration(migrationData);
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
            Session.ConcurrencyControlEnabled = true;
            return Session;
        }

        public async Task SetReadyAsync(bool ready)
        {
            EnsureSession();
            Session.CurrentPlayer.SetProperty("ready", new PlayerProperty(ready ? "true" : "false", VisibilityPropertyOptions.Member));
            await Session.SaveCurrentPlayerDataAsync();
        }

        public async Task StartRelayAsync(string region = null)
        {
            EnsureSession();
            if (!IsHost) throw new InvalidOperationException("Only the session host can start Relay.");
            IHostSession host = Session.AsHost();
            host.IsLocked = true;
            host.SetProperty("phase", new SessionProperty("in-match", VisibilityPropertyOptions.Member));
            await host.SavePropertiesAsync();
            await host.Network.StartRelayNetworkAsync(new RelayNetworkOptions(RelayProtocol.DTLS, region, true));
        }

        public async Task<ISession> ReconnectAsync()
        {
            EnsureSession();
            await Session.ReconnectAsync();
            return Session;
        }

        public async Task LeaveAsync()
        {
            if (Session == null) return;
            await Session.LeaveAsync();
            Session = null;
            Transport = null;
        }

        private static Dictionary<string, SessionProperty> SessionProperties(MultiplayerLobby lobby)
            => new Dictionary<string, SessionProperty>
            {
                { "scenario", new SessionProperty(lobby.ScenarioId, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                { "rules", new SessionProperty("authority-v1", VisibilityPropertyOptions.Public, PropertyIndex.String2) },
                { "seed", new SessionProperty(lobby.Seed.ToString(), VisibilityPropertyOptions.Member) },
                { "phase", new SessionProperty("lobby", VisibilityPropertyOptions.Member) }
            };

        private static Dictionary<string, PlayerProperty> PlayerProperties(string name, Side side)
            => new Dictionary<string, PlayerProperty>
            {
                { "name", new PlayerProperty(string.IsNullOrWhiteSpace(name) ? "Commander" : name.Trim(), VisibilityPropertyOptions.Member) },
                { "side", new PlayerProperty(side.ToString(), VisibilityPropertyOptions.Member) },
                { "ready", new PlayerProperty("false", VisibilityPropertyOptions.Member) }
            };

        private void EnsureSession()
        {
            if (Session == null) throw new InvalidOperationException("No multiplayer session is active.");
        }

        private sealed class AuthorityMigrationDataHandler : IMigrationDataHandler
        {
            private readonly Func<byte[]> generate;
            private readonly Action<byte[]> apply;
            public AuthorityMigrationDataHandler(Func<byte[]> generate, Action<byte[]> apply)
            {
                this.generate = generate ?? (() => Array.Empty<byte>());
                this.apply = apply ?? (_ => { });
            }
            public byte[] Generate() => generate() ?? Array.Empty<byte>();
            public void Apply(byte[] migrationData) => apply(migrationData ?? Array.Empty<byte>());
        }
    }
}
