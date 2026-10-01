using System;
using BepInEx;
using BepInEx.Configuration;

namespace SPTGlobalChat
{
    /// <summary>
    /// Phase 1: connects, logs what arrives, and sends a line typed into the
    /// Configuration Manager. The chat box (phase 2) replaces both.
    /// </summary>
    [BepInPlugin(Guid, DisplayName, Version)]
    public sealed class ChatPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.mybutthasarash.sptglobalchat";
        public const string DisplayName = "SPT Global Chat";
        public const string Version = "0.1.0";

        // Frames handled per frame, so a history batch cannot stall one.
        const int FramesPerUpdate = 50;

        ConfigEntry<bool> _enabled;
        ConfigEntry<string> _say;
#if DEBUG
        ConfigEntry<string> _devRelay;
#endif
        RelayConnection _relay;

        void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", false,
                "Connect to the global chat relay. Off until you turn it on: while it is on, " +
                "what you send is passed to a server on the internet and shown to everyone " +
                "connected. Takes effect on the next game start.");

            _say = Config.Bind("Phase 1", "Say", "",
                "Temporary, until the chat box exists: type a line here and press Enter to send it.");
            _say.SettingChanged += (_, __) => SayFromConfig();

#if DEBUG
            _devRelay = Config.Bind("Development", "RelayUrl", "",
                "Debug builds only. A localhost address such as ws://localhost:8787/ws, for " +
                "`wrangler dev`. Anything else is refused. Release builds have no such setting.");
#endif

            if (!_enabled.Value)
            {
                Logger.LogInfo("off: set [General] Enabled to true and restart the game to connect");
                return;
            }

            var url = RelayUrl();
            if (RelayConnection.IsPlaceholder(url))
            {
                Logger.LogWarning("no relay address in this build yet; not connecting");
                return;
            }

            _relay = new RelayConnection(url, Logger);
            _relay.Start();
        }

        void Update()
        {
            if (_relay == null) return;
            for (var i = 0; i < FramesPerUpdate && _relay.TryReceive(out var raw); i++)
            {
                Show(Wire.Parse(raw));
            }
        }

        void OnDestroy() => _relay?.Dispose();

        // Phase 1 shows chat in the BepInEx log. That log gets pasted into bug reports,
        // so this goes the moment the chat box exists.
        void Show(ServerFrame frame)
        {
            if (frame == null) return;
            switch (frame.Type)
            {
                case "msg":
                    Logger.LogInfo($"[chat] {TextSafety.CleanName(frame.From)}#{TextSafety.CleanName(frame.Tag)}: " +
                                   TextSafety.Clean(frame.Text));
                    break;
                case "error":
                    Logger.LogWarning("[chat] the relay refused that: " + TextSafety.CleanName(frame.Reason));
                    break;
            }
        }

        void SayFromConfig()
        {
            var text = TextSafety.Clean(_say.Value);
            if (text.Length == 0) return;
            _say.Value = "";   // re-enters this handler with nothing to send

            if (_relay == null || !_relay.Send(Wire.Say(text)))
            {
                Logger.LogWarning("[chat] not connected; nothing was sent");
            }
        }

        string RelayUrl()
        {
#if DEBUG
            var dev = _devRelay.Value.Trim();
            if (dev.Length > 0)
            {
                if (Uri.TryCreate(dev, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "ws" || uri.Scheme == "wss") && uri.IsLoopback)
                {
                    Logger.LogWarning("DEBUG build: using the development relay at " + dev);
                    return dev;
                }
                Logger.LogWarning("[Development] RelayUrl ignored: only a localhost address is accepted");
            }
#endif
            return RelayConnection.ReleaseUrl;
        }
    }
}
