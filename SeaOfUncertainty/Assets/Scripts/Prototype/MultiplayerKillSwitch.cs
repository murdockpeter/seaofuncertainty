using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    /// <summary>
    /// Remote-controlled emergency stop for Relay-based multiplayer. Flip "multiplayer_enabled" to
    /// false in the Unity Dashboard Remote Config screen to disable Relay hosting/joining for every
    /// client within seconds, with no rebuild -- the actionable response to a Unity budget alert.
    /// Direct IP play never calls this and is unaffected.
    /// </summary>
    public static class MultiplayerKillSwitch
    {
        private const string EnabledKey = "multiplayer_enabled";
        private const string MessageKey = "multiplayer_disabled_message";
        private const string DefaultDisabledMessage = "Online Relay multiplayer is temporarily disabled by the developer. Direct IP play is unaffected.";

        public static bool IsEnabled { get; private set; } = true;
        public static string DisabledMessage { get; private set; } = DefaultDisabledMessage;

        private struct UserAttributes { }
        private struct AppAttributes { }

        public static async Task<bool> RefreshAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();

                RuntimeConfig config = await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
                IsEnabled = config.GetBool(EnabledKey, true);
                DisabledMessage = config.GetString(MessageKey, DefaultDisabledMessage);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Multiplayer kill switch check failed; keeping the last known state ({(IsEnabled ? "enabled" : "disabled")}). {exception.Message}");
            }
            return IsEnabled;
        }
    }
}
