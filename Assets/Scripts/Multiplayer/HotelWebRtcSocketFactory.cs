using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Mirage.SocketLayer;
using UnityEngine;
using UnityEngine.Networking;

namespace HauntedFish.Multiplayer
{
    // Browser transport: Mirage packets travel over WebRTC data channels relayed by the hotel's coturn
    // server. The directory issues TURN credentials and carries offers and answers between a room's
    // owner and its joining players. Browsers cannot open the UDP sockets HotelTurnSocketFactory uses.
    public sealed class HotelWebRtcSocketFactory : SocketFactory, IHasPort, IHotelRelaySocketFactory
    {
        [Serializable] sealed class Session { public string username, password, token, icePolicy; public string[] urls; public int lease; }
        [Serializable] sealed class IceServer { public string[] urls; public string username, credential; }
        [Serializable] sealed class Signal { public string room, to, kind, sdp; }
        [Serializable] sealed class Inbound { public string from, room, kind, sdp; }
        [Serializable] sealed class Mailbox { public Inbound[] messages; }

        const float ConnectTimeout = 20f;
        const string WebRtcAddress = "webrtc";

        Session _Session;
        string _Directory, _IceJson, _LastHttpError;
        float _NextLease, _NextPoll;
        bool _Polling, _Hosting, _Claimed;
        string _AwaitingAnswerFrom;
        Inbound _Answer;
        // Host: one link per joining player, keyed by the directory's opaque session id.
        readonly Dictionary<string, int> _Joiners = new Dictionary<string, int>();
        readonly Dictionary<int, Inbound> _UnsentAnswers = new Dictionary<int, Inbound>();
        internal int HostLink { get; private set; }
        internal IEnumerable<int> JoinerLinks => _Joiners.Values;

        public string AuthorizationToken => _Session?.token;
        public string Failure { get; private set; }
        public float RetryDelay { get; private set; }
        public bool Ready => _Session != null && Failure == null;
        // Joiners reach a browser host by room code through signalling, so the endpoint is symbolic.
        public string RoomAddress => WebRtcAddress;
        public int RoomPort => 1;
        public int Port { get; set; } = 7777;
        public override int MaxPacketSize => 1200;
        public override bool IsSupported => Application.platform == RuntimePlatform.WebGLPlayer && HFRtc_Supported() == 1;

        public IEnumerator Prepare(string service)
        {
            yield return EndSession();
            Failure = null;
            _Directory = (service ?? string.Empty).TrimEnd('/');
            if (!Uri.TryCreate(_Directory, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !uri.IsLoopback))
            { Failure = "A verified HTTPS relay endpoint is required."; yield break; }
            string payload = null;
            yield return Http("/sessions", "POST", "{\"transport\":\"webrtc\"}", value => payload = value, false);
            if (payload == null) { Failure = _LastHttpError ?? "Unable to create relay session."; yield break; }
            try { _Session = JsonUtility.FromJson<Session>(payload); }
            catch (ArgumentException) { _Session = null; }
            if (_Session == null || string.IsNullOrEmpty(_Session.token)) { _Session = null; Failure = "Invalid relay session."; yield break; }
            _IceJson = _Session.urls == null || _Session.urls.Length == 0 ? "[]" :
                "[" + JsonUtility.ToJson(new IceServer { urls = _Session.urls, username = _Session.username, credential = _Session.password }) + "]";
            _NextLease = Time.realtimeSinceStartup + 20;
        }

        public IEnumerator ConnectToHost(Room room)
        {
            if (!Ready) yield break;
            CloseLinks();
            HostLink = HFRtc_Offer(_IceJson, _Session.icePolicy ?? "relay");
            if (HostLink == 0) { Failure = "This browser cannot open WebRTC connections."; yield break; }
            float deadline = Time.realtimeSinceStartup + ConnectTimeout;
            string offer = null;
            while (offer == null && HFRtc_State(HostLink) != 2 && Time.realtimeSinceStartup < deadline)
            {
                offer = HFRtc_TakeLocal(HostLink);
                if (offer == null) yield return null;
            }
            if (offer == null) { Failure = "Unable to prepare a relay connection."; yield break; }
            _Answer = null;
            _AwaitingAnswerFrom = room.code;
            string sent = null;
            yield return Http("/signal", "POST", JsonUtility.ToJson(new Signal { room = room.code, to = "host", kind = "offer", sdp = offer }), value => sent = value);
            if (sent == null) { _AwaitingAnswerFrom = null; Failure = _LastHttpError ?? "The lobby host could not be reached."; yield break; }
            while (_Answer == null && Time.realtimeSinceStartup < deadline) yield return null;
            _AwaitingAnswerFrom = null;
            if (_Answer == null) { Failure = "The lobby host did not answer."; yield break; }
            HFRtc_Accept(HostLink, _Answer.sdp);
            while (HFRtc_State(HostLink) == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            if (HFRtc_State(HostLink) != 1) Failure = "Unable to connect to the lobby host through the relay.";
        }

        void Update()
        {
            if (_Session == null) return;
            if (_Session.lease > 0 && Time.realtimeSinceStartup >= _NextLease)
            {
                _NextLease = Time.realtimeSinceStartup + 20;
                StartCoroutine(Http("/sessions/heartbeat", "POST", "{}", value => { if (value == null) { Failure = "Relay session expired."; CloseLinks(); } }));
            }
            foreach (var pending in new List<KeyValuePair<int, Inbound>>(_UnsentAnswers))
            {
                var answer = HFRtc_TakeLocal(pending.Key);
                if (answer == null) continue;
                _UnsentAnswers.Remove(pending.Key);
                StartCoroutine(Http("/signal", "POST", JsonUtility.ToJson(new Signal { room = pending.Value.room, to = pending.Value.from, kind = "answer", sdp = answer }), _ => { }));
            }
            // A host answers joiners at any time; a joiner listens only while its offer is outstanding.
            bool listening = _Hosting || _AwaitingAnswerFrom != null;
            if (listening && !_Polling && Time.realtimeSinceStartup >= _NextPoll)
            {
                _NextPoll = Time.realtimeSinceStartup + (_Hosting ? 1f : .5f);
                StartCoroutine(Poll());
            }
        }

        IEnumerator Poll()
        {
            _Polling = true;
            string payload = null;
            yield return Http("/signal", "GET", null, value => payload = value);
            _Polling = false;
            Mailbox mailbox = null;
            try { if (payload != null) mailbox = JsonUtility.FromJson<Mailbox>(payload); }
            catch (ArgumentException) { }
            if (mailbox?.messages == null) yield break;
            foreach (var message in mailbox.messages)
            {
                if (message == null || string.IsNullOrEmpty(message.from)) continue;
                if (message.kind == "answer" && _AwaitingAnswerFrom != null && message.room == _AwaitingAnswerFrom) _Answer = message;
                else if (message.kind == "offer" && _Hosting && !string.IsNullOrEmpty(message.sdp)) Accept(message);
                else if (message.kind == "bye" && _Hosting && _Joiners.TryGetValue(message.from, out var link)) { _Joiners.Remove(message.from); HFRtc_Close(link); }
            }
        }

        void Accept(Inbound offer)
        {
            // A repeated offer means the joiner restarted its connection; replace the stale link.
            if (_Joiners.TryGetValue(offer.from, out var previous)) { _UnsentAnswers.Remove(previous); HFRtc_Close(previous); }
            int link = HFRtc_Answer(_IceJson, _Session.icePolicy ?? "relay", offer.sdp);
            if (link == 0) return;
            _Joiners[offer.from] = link;
            _UnsentAnswers[link] = offer;
        }

        IEnumerator Http(string path, string method, string json, Action<string> callback, bool authenticated = true)
        {
            using (var request = new UnityWebRequest(_Directory + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 15;
                if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
                if (authenticated && _Session != null) request.SetRequestHeader("Authorization", "Bearer " + _Session.token);
                yield return request.SendWebRequest();
                if (path == "/sessions" && method == "POST")
                    RetryDelay = request.responseCode == 400 || request.responseCode == 429 || request.responseCode == 503 ? 65f : 0f;
                _LastHttpError = request.result == UnityWebRequest.Result.Success ? null :
                    "Relay request failed (HTTP " + request.responseCode + ", " + request.result + ": " + request.error + ").";
                if (request.responseCode == 429)
                    _LastHttpError = "Relay rate limit reached. Waiting at least one minute before retrying.";
                if (_LastHttpError != null)
                    Debug.LogWarning("Relay control " + method + " " + path + ": " + _LastHttpError, this);
                callback(request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : null);
            }
        }

        public IEnumerator EndSession()
        {
            CloseLinks();
            _Hosting = _Claimed = false;
            _AwaitingAnswerFrom = null;
            if (_Session != null && !string.IsNullOrEmpty(_Directory)) yield return Http("/sessions", "DELETE", null, _ => { });
            _Session = null;
        }

        internal void CloseLinks()
        {
            foreach (var link in _Joiners.Values) HFRtc_Close(link);
            _Joiners.Clear();
            _UnsentAnswers.Clear();
            if (HostLink != 0) HFRtc_Close(HostLink);
            HostLink = 0;
        }

        ISocket Claim(bool hosting)
        {
            if (!Ready || _Claimed) throw new InvalidOperationException("A relay session is required before peer startup.");
            if (!hosting && (HostLink == 0 || HFRtc_State(HostLink) != 1)) throw new InvalidOperationException("Connect to the lobby host before starting the client.");
            _Claimed = true;
            _Hosting = hosting;
            return new WebRtcSocket(this, hosting);
        }

        public override ISocket CreateServerSocket() => Claim(true);
        public override ISocket CreateClientSocket() => Claim(false);
        public override IBindEndPoint GetBindEndPoint() => new WebRtcSocket.Handle(0);
        public override IConnectEndPoint GetConnectEndPoint(string address = null, ushort? port = null) => new WebRtcSocket.Handle(HostLink);
        void OnDestroy() => CloseLinks();

        // Mirage socket over the factory's links. Mirage owns reliability, ordering and timeouts.
        sealed class WebRtcSocket : ISocket
        {
            public sealed class Handle : IConnectionHandle, IBindEndPoint, IConnectEndPoint
            {
                public readonly int Link;
                public Handle(int link) { Link = link; }
                public bool IsStateful => false;
                public bool SupportsGracefulDisconnect => false;
                public ISocketLayerConnection SocketLayerConnection { get; set; }
                public void Disconnect(string reason) { }
                public IConnectionHandle CreateCopy() => new Handle(Link);
                public override bool Equals(object other) => other is Handle handle && handle.Link == Link;
                public override int GetHashCode() => Link;
            }

            readonly HotelWebRtcSocketFactory _Owner;
            readonly bool _Host;
            readonly Dictionary<int, Handle> _Handles = new Dictionary<int, Handle>();
            readonly byte[] _Buffer = new byte[2048], _Outgoing = new byte[1200];
            int _MaximumPacket = 1200;
            int _PendingLink, _PendingLength;
            bool _Closed;

            public WebRtcSocket(HotelWebRtcSocketFactory owner, bool host) { _Owner = owner; _Host = host; }

            public void Bind(IBindEndPoint endpoint) { }
            public IConnectionHandle Connect(IConnectEndPoint endpoint) => HandleFor(_Owner.HostLink);
            public void SetTickEvents(int maxPacketSize, OnData onData, OnDisconnect onDisconnect)
            {
                // Poll/Receive drives this socket like a datagram socket.
                if (maxPacketSize < 1 || maxPacketSize > 1200) throw new ArgumentOutOfRangeException(nameof(maxPacketSize));
                _MaximumPacket = maxPacketSize;
            }
            public void Tick() { }
            public void Flush() { }

            Handle HandleFor(int link)
            {
                if (!_Handles.TryGetValue(link, out var handle)) _Handles[link] = handle = new Handle(link);
                return handle;
            }

            IEnumerable<int> Links()
            {
                if (_Host) foreach (var link in _Owner.JoinerLinks) yield return link;
                else if (_Owner.HostLink != 0) yield return _Owner.HostLink;
            }

            public bool Poll()
            {
                if (_Closed) return false;
                if (_PendingLength > 0) return true;
                foreach (var link in Links())
                {
                    int length = HFRtc_Receive(link, _Buffer, _Buffer.Length);
                    if (length <= 0) continue;
                    _PendingLink = link;
                    _PendingLength = length;
                    return true;
                }
                return false;
            }

            public int Receive(Span<byte> buffer, out IConnectionHandle handle)
            {
                handle = null;
                if (_PendingLength == 0 && !Poll()) return 0;
                int length = _PendingLength;
                _PendingLength = 0;
                if (length > buffer.Length) return 0;
                new ReadOnlySpan<byte>(_Buffer, 0, length).CopyTo(buffer);
                handle = HandleFor(_PendingLink);
                return length;
            }

            public void Send(IConnectionHandle handle, ReadOnlySpan<byte> packet)
            {
                if (_Closed || !(handle is Handle peer) || packet.Length > _MaximumPacket) return;
                packet.CopyTo(_Outgoing);
                HFRtc_Send(peer.Link, _Outgoing, packet.Length);
            }

            public void Close()
            {
                if (_Closed) return;
                _Closed = true;
                _Owner.CloseLinks();
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int HFRtc_Supported();
        [DllImport("__Internal")] static extern int HFRtc_Offer(string iceJson, string policy);
        [DllImport("__Internal")] static extern int HFRtc_Answer(string iceJson, string policy, string offerSdp);
        [DllImport("__Internal")] static extern void HFRtc_Accept(int link, string answerSdp);
        [DllImport("__Internal")] static extern string HFRtc_TakeLocal(int link);
        [DllImport("__Internal")] static extern int HFRtc_State(int link);
        [DllImport("__Internal")] static extern void HFRtc_Send(int link, byte[] buffer, int length);
        [DllImport("__Internal")] static extern int HFRtc_Receive(int link, byte[] buffer, int capacity);
        [DllImport("__Internal")] static extern void HFRtc_Close(int link);
#else
        // Desktop and the editor use HotelTurnSocketFactory; these stubs keep the browser path compiling.
        static int HFRtc_Supported() => 0;
        static int HFRtc_Offer(string iceJson, string policy) => 0;
        static int HFRtc_Answer(string iceJson, string policy, string offerSdp) => 0;
        static void HFRtc_Accept(int link, string answerSdp) { }
        static string HFRtc_TakeLocal(int link) => null;
        static int HFRtc_State(int link) => 2;
        static void HFRtc_Send(int link, byte[] buffer, int length) { }
        static int HFRtc_Receive(int link, byte[] buffer, int capacity) => 0;
        static void HFRtc_Close(int link) { }
#endif
    }
}
