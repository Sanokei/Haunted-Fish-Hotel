using System;
using System.Collections;
using System.Net;
using System.Text;
using Mirage.SocketLayer;
using UnityEngine;
using UnityEngine.Networking;

namespace HauntedFish.Multiplayer
{
    // This authored component prepares one verified allocation before Mirage starts its peer.
    public sealed class HotelTurnSocketFactory : SocketFactory, IHasPort
    {
        [Serializable] sealed class Session { public string address, username, password, token; public int port, lease; }
        [Serializable] sealed class EndpointProof { public string address; public int port, lease; }
        [Serializable] sealed class Reply { public string status; }
        TurnSocket _Prepared;
        Session _Session;
        bool _Claimed, _Verified;
        string _LastHttpError;
        string _Directory;
        float _NextLease;
        public string AuthorizationToken => _Session?.token;
        public string Failure { get; private set; }
        public float RetryDelay { get; private set; }
        public bool Ready => _Verified && Failure == null && _Prepared != null && _Prepared.Ready;
        public IPEndPoint RelayEndPoint => _Prepared?.RelayEndPoint;
        // The legacy lobby interface configures Port; real bind/connect endpoints use verified allocations.
        public int Port { get; set; } = 7777;
        public override int MaxPacketSize => 1200;
        public override bool IsSupported => Application.platform != RuntimePlatform.WebGLPlayer;
        public IEnumerator Prepare(string service)
        {
            // End previous credentials and allocation before obtaining a distinct room session.
            yield return EndSession(); Failure = null; _Directory = service.TrimEnd('/');
            if (!Uri.TryCreate(_Directory, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !IPAddress.TryParse(uri.Host, out var relay))
            { Failure = "A verified HTTPS relay endpoint is required."; yield break; }
            string payload = null;
            yield return Http("/sessions", "POST", "{}", value => payload = value, false);
            if (payload == null) { Failure = _LastHttpError ?? "Unable to create relay session."; yield break; }
            try
            {
                _Session = JsonUtility.FromJson<Session>(payload);
                // Pin control/data traffic to the service authenticated by HTTPS, never a claimed client address.
                if (_Session == null || _Session.address != uri.Host || _Session.port != 3478 || string.IsNullOrEmpty(_Session.token)) throw new ArgumentException();
                _Prepared = new TurnSocket(new IPEndPoint(relay, _Session.port), _Session.username, _Session.password);
                _NextLease=Time.realtimeSinceStartup+20;
            }
            catch (Exception) { Failure = "Invalid relay session."; }
            if (Failure != null) { yield return EndSession(); yield break; }
            float deadline = Time.realtimeSinceStartup + 12;
            while (!_Prepared.Ready && _Prepared.Failure == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (!_Prepared.Ready) { Failure = "Relay allocation failed."; yield return EndSession(); yield break; }
            payload = null;
            // The server sends a nonce to this allocation and accepts only its actual UDP reply.
            yield return Http("/sessions/verify", "POST", JsonUtility.ToJson(new EndpointProof { address = RelayEndPoint.Address.ToString(), port = RelayEndPoint.Port }), value => payload = value);
            bool accepted = false;
            try { accepted = payload != null && JsonUtility.FromJson<Reply>(payload)?.status == "verified"; }
            catch (ArgumentException) { }
            if (!accepted)
            { Failure = _LastHttpError ?? "Relay ownership verification failed."; yield return EndSession(); yield break; }
            _Verified = true;
        }
        void Update()
        {
            // Before Mirage claims the socket, keep allocation negotiation and verification nonblocking.
            // Renewable leases reap crashed clients rather than holding all anonymous slots for six hours.
            if (_Session != null && _Session.lease>0 && Ready && Time.realtimeSinceStartup>=_NextLease)
            { _NextLease=Time.realtimeSinceStartup+20; StartCoroutine(Http("/sessions/heartbeat", "POST", "{}", value => { if (value==null) { Failure="Relay session expired."; _Prepared?.Close(); } })); }
            if (_Prepared == null || _Claimed) return;
            _Prepared.Tick();
            var buffer = new byte[1200];
            while (_Prepared.Poll())
            {
                int length = _Prepared.Receive(buffer, out var peer);
                if (!(peer is TurnSocket.Handle handle) || handle.Endpoint.Port != 49160 || length < 10 || length > 80) continue;
                if (Encoding.ASCII.GetString(buffer, 0, length).StartsWith("HFVERIFY:", StringComparison.Ordinal))
                    _Prepared.Send(peer, new ReadOnlySpan<byte>(buffer, 0, length));
            }
        }
        IEnumerator Http(string path, string method, string json, Action<string> callback, bool authenticated = true)
        {
            using (var request = new UnityWebRequest(_Directory + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 15;
                request.useHttpContinue = false;
                if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
                if (authenticated && _Session != null) request.SetRequestHeader("Authorization", "Bearer " + _Session.token);
                yield return request.SendWebRequest();
                // Older relay versions report policy limits as 400. Back off for both
                // until the deployed API distinguishes rate limits with 429.
                if (path == "/sessions" && method == "POST")
                    RetryDelay = request.responseCode == 400 || request.responseCode == 429 || request.responseCode == 503 ? 65f : 0f;
                // Credentials and response bodies never enter diagnostics or authored assets.
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
            _Verified = false;
            if (_Prepared != null) _Prepared.Close(); _Prepared = null; _Claimed = false;
            if (_Session != null && !string.IsNullOrEmpty(_Directory)) yield return Http("/sessions", "DELETE", null, _ => { });
            _Session = null;
        }
        ISocket Claim()
        {
            if (!Ready || _Claimed) throw new InvalidOperationException("Verified relay allocation required before peer startup.");
            _Claimed = true; return _Prepared;
        }
        public override ISocket CreateServerSocket() => Claim();
        public override ISocket CreateClientSocket() => Claim();
        public override IBindEndPoint GetBindEndPoint() => new TurnSocket.Handle(RelayEndPoint);
        public override IConnectEndPoint GetConnectEndPoint(string address = null, ushort? port = null)
            => new TurnSocket.Handle(new IPEndPoint(IPAddress.Parse(address), port ?? (ushort)Port));
        void OnDestroy() { _Prepared?.Close(); }
    }
}
