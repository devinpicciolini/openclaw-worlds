using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenClawWorlds.Gateway
{
    /// <summary>
    /// Low-level WebSocket transport to the OpenClaw gateway.
    /// Handles connect / reconnect / send / receive. Messages are queued and
    /// dispatched on the main thread via Update().
    /// </summary>
    public class GatewayConnection : MonoBehaviour
    {
        public bool Connected => connected;

        ClientWebSocket ws;
        bool connected;
        bool connecting;
        readonly Queue<string> incoming = new Queue<string>();
        CancellationTokenSource cts;

        /// <summary>Called on the main thread for every complete message received.</summary>
        public Action<string> OnMessage;

        /// <summary>Call after setting OnMessage to begin the auto-reconnect loop.</summary>
        public void Begin() => StartCoroutine(ConnectLoop());
        void OnDisable() => Disconnect();
        void OnDestroy() => Disconnect();

        void Update()
        {
            lock (incoming)
            {
                while (incoming.Count > 0)
                    OnMessage?.Invoke(incoming.Dequeue());
            }
        }

        // ─── Public API ──────────────────────────────────────────────

        public void SendRaw(string json)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            Task.Run(async () =>
            {
                try
                {
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[OpenClaw] WebSocket send error: {e.Message}");
                }
            });
        }

        public bool IsOpen => ws != null && ws.State == WebSocketState.Open;

        // ─── Connection lifecycle ────────────────────────────────────

        void Disconnect()
        {
            cts?.Cancel();
            if (ws != null && ws.State == WebSocketState.Open)
            {
                try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                catch { }
            }
            ws = null;
            connected = false;
            connecting = false;
        }

        IEnumerator ConnectLoop()
        {
            while (true)
            {
                if (!connected && !connecting)
                {
                    connecting = true;
                    StartCoroutine(DoConnect());
                }
                yield return new WaitForSeconds(3f);
            }
        }

        IEnumerator DoConnect()
        {
            var config = AIConfig.Instance;
            if (config == null)
            {
                connecting = false;
                yield break;
            }

            string url = config.gatewayWsUrl;
            Debug.Log($"[OpenClaw] Connecting to gateway: {url}");

            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();

            // Derive Origin header from gateway URL
            try
            {
                var uri = new Uri(url);
                ws.Options.SetRequestHeader("Origin", $"http://{uri.Host}:{uri.Port}");
            }
            catch
            {
                ws.Options.SetRequestHeader("Origin", "http://127.0.0.1:18789");
            }

            bool connectDone = false;
            bool connectFailed = false;
            string connectError = "";

            Task.Run(async () =>
            {
                try
                {
                    await ws.ConnectAsync(new Uri(url), cts.Token);
                    connectDone = true;
                }
                catch (Exception e)
                {
                    connectFailed = true;
                    connectError = e.Message;
                }
            });

            float timeout = 10f;
            while (!connectDone && !connectFailed && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (connectFailed || !connectDone)
            {
                Debug.LogWarning($"[OpenClaw] WebSocket connect failed: {connectError}");
                connecting = false;
                ws = null;
                yield break;
            }

            connected = true;
            connecting = false;
            Debug.Log("[OpenClaw] WebSocket connected, starting receive loop...");
            StartReceiveLoop();
        }

        void StartReceiveLoop()
        {
            Task.Run(async () =>
            {
                var buffer = new byte[65536];
                var sb = new StringBuilder();
                try
                {
                    while (ws != null && ws.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
                    {
                        var segment = new ArraySegment<byte>(buffer);
                        var result = await ws.ReceiveAsync(segment, cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            connected = false;
                            break;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                        if (result.EndOfMessage)
                        {
                            string msg = sb.ToString();
                            sb.Clear();
                            lock (incoming)
                                incoming.Enqueue(msg);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!cts.Token.IsCancellationRequested)
                        Debug.LogWarning($"[OpenClaw] WebSocket receive error: {e.Message}");
                }
                connected = false;
            });
        }

        /// <summary>Force-reset connection state (called after auth failure, etc.)</summary>
        public void ResetConnection()
        {
            connected = false;
        }
    }
}
