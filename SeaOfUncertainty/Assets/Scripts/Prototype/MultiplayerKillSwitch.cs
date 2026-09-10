using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    /// <summary>
    /// Remote-controlled admission switch for new Relay sessions. Direct IP never calls this and
    /// remains available. The last successful value is cached so an already-disabled client does
    /// not silently re-enable Relay during a later Remote Config outage.
    /// </summary>
    public static class MultiplayerKillSwitch
    {
        private const string EnabledKey = "multiplayer_enabled";
        private const string MessageKey = "multiplayer_disabled_message";
        private const string CachedEnabledKey = "MultiplayerRelayEnabled";
        private const string CachedMessageKey = "MultiplayerRelayDisabledMessage";
        private const string DefaultDisabledMessage = "Online Relay multiplayer is temporarily disabled by the developer. Direct IP play is unaffected.";

        public static bool IsEnabled { get; private set; }
        public static string DisabledMessage { get; private set; }

        private struct UserAttributes { }
        private struct AppAttributes { }

        static MultiplayerKillSwitch()
        {
            IsEnabled = PlayerPrefs.GetInt(CachedEnabledKey, 1) == 1;
            DisabledMessage = PlayerPrefs.GetString(CachedMessageKey, DefaultDisabledMessage);
        }

        public static async Task<bool> RefreshAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();

                RuntimeConfig config = await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
                Apply(config.GetBool(EnabledKey, IsEnabled), config.GetString(MessageKey, DisabledMessage));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Multiplayer kill switch check failed; keeping the last known state ({(IsEnabled ? "enabled" : "disabled")}). {exception.Message}");
            }
            return IsEnabled;
        }

        private static void Apply(bool enabled, string disabledMessage)
        {
            IsEnabled = enabled;
            DisabledMessage = string.IsNullOrWhiteSpace(disabledMessage) ? DefaultDisabledMessage : disabledMessage.Trim();
            PlayerPrefs.SetInt(CachedEnabledKey, IsEnabled ? 1 : 0);
            PlayerPrefs.SetString(CachedMessageKey, DisabledMessage);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        public static void EditorApplyForTests(bool enabled, string disabledMessage) => Apply(enabled, disabledMessage);
#endif
    }
}
