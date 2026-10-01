using System;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using BepInEx.Logging;
using WebSocketSharp;

namespace SPTGlobalChat
{
    /// <summary>
    /// One connection to the relay, kept open and reconnected with backoff. Everything
    /// here runs on websocket-sharp's threads, so nothing touches Unity: incoming frames
    /// go on a queue that <see cref="ChatPlugin"/> empties from Update.
    /// </summary>
    public sealed class RelayConnection : IDisposable
    {
        /// <summary>
        /// Fixed in the build, never in the config: a relay address players can change is
        /// one a scam guide can tell them to change. Set once the relay is deployed.
        /// </summary>
        public const string ReleaseUrl = "wss://spt-global-chat.REPLACE-ME.workers.dev/ws";

        public static bool IsPlaceholder(string url) => url.Contains("REPLACE-ME");

        // The relay sends at most 16 KB (a history batch). Anything bigger is not ours.
        const int MaxIncomingBytes = 16 * 1024;

        // Frames held for Update. Past this the game is not draining them; drop.
        const int MaxQueued = 500;

        const int MaxBackoffSeconds = 60;

        readonly string _url;
        readonly ManualLogSource _log;
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        readonly object _gate = new object();

        WebSocket _ws;
        Timer _retry;
        int _backoffSeconds = 1;
        bool _disposed;

        public RelayConnection(string url, ManualLogSource log)
        {
            _url = url;
            _log = log;
        }

        public void Start() => Connect();

        public bool TryReceive(out string frame) => _incoming.TryDequeue(out frame);

        /// <summary>False when not connected; the caller says so to the player.</summary>
        public bool Send(string frame)
        {
            var ws = _ws;
            if (ws == null || ws.ReadyState != WebSocketState.Open) return false;
            ws.SendAsync(frame, null);
            return true;
        }

        void Connect()
        {
            lock (_gate)
            {
                if (_disposed) return;

                var ws = new WebSocket(_url);

                // websocket-sharp silently accepts EVERY certificate unless told
                // otherwise. This callback is the whole of TLS here: the relay is who
                // it says it is, or nothing is sent. Never loosen it -- if a valid
                // certificate fails in game, pin the relay's key instead (CLAUDE.md).
                if (_url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                {
                    ws.SslConfiguration.ServerCertificateValidationCallback = StrictCertificate;
                    ws.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
                }

                ws.OnOpen += (s, e) =>
                {
                    _backoffSeconds = 1;
                    _log.LogInfo("connected to the relay");
                };

                ws.OnMessage += (s, e) =>
                {
                    if (!e.IsText || e.RawData.Length > MaxIncomingBytes)
                    {
                        _log.LogWarning("relay sent a frame this plugin does not accept; disconnecting");
                        ws.CloseAsync(CloseStatusCode.PolicyViolation);
                        return;
                    }
                    if (_incoming.Count < MaxQueued) _incoming.Enqueue(e.Data);
                };

                ws.OnError += (s, e) => _log.LogWarning("relay: " + e.Message);
                ws.OnClose += (s, e) => ScheduleReconnect(ws, e.Code);

                _ws = ws;
                ws.ConnectAsync();
            }
        }

        void ScheduleReconnect(WebSocket closed, ushort code)
        {
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(closed, _ws)) return;

                var delay = _backoffSeconds;
                _backoffSeconds = Math.Min(_backoffSeconds * 2, MaxBackoffSeconds);
                _log.LogInfo($"disconnected from the relay ({code}); retrying in {delay}s");

                _retry?.Dispose();
                _retry = new Timer(_ => Connect(), null, delay * 1000, Timeout.Infinite);
            }
        }

        static bool StrictCertificate(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors errors) => errors == SslPolicyErrors.None;

        public void Dispose()
        {
            WebSocket ws;
            lock (_gate)
            {
                _disposed = true;
                _retry?.Dispose();
                ws = _ws;
            }
            ws?.CloseAsync(CloseStatusCode.Normal);
        }
    }
}
