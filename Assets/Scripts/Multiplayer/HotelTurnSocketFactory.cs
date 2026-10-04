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
        TurnSocket prepared;
        Session session;
        bool claimed;
        string directory;
        float nextLease;
        public string AuthorizationToken => session?.token;
        public string Failure { get; private set; }
        public bool Ready => prepared != null && prepared.Ready;
        public IPEndPoint RelayEndPoint => prepared?.RelayEndPoint;
        // The legacy lobby interface configures Port; real bind/connect endpoints use verified allocations.
        public int Port { get; set; } = 7777;
        public override int MaxPacketSize => 1200;
        public override bool IsSupported => Application.platform != RuntimePlatform.WebGLPlayer;
        public IEnumerator Prepare(string service)
        {
            // End previous credentials and allocation before obtaining a distinct room session.
            yield return EndSession(); Failure = null; directory = service.TrimEnd('/');
            if (!Uri.TryCreate(directory, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !IPAddress.TryParse(uri.Host, out var relay))
            { Failure = "A verified HTTPS relay endpoint is required."; yield break; }
            string payload = null;
            yield return Http("/sessions", "POST", "{}", value => payload = value, false);
            if (payload == null) { Failure = "Unable to create relay session."; yield break; }
            try
            {
                session = JsonUtility.FromJson<Session>(payload);
                // Pin control/data traffic to the service authenticated by HTTPS, never a claimed client address.
                if (session == null || session.address != uri.Host || session.port != 3478 || string.IsNullOrEmpty(session.token)) throw new ArgumentException();
                prepared = new TurnSocket(new IPEndPoint(relay, session.port), session.username, session.password);
                nextLease=Time.realtimeSinceStartup+20;
            }
            catch (Exception) { Failure = "Invalid relay session."; }
            if (Failure != null) { yield return EndSession(); yield break; }
            float deadline = Time.realtimeSinceStartup + 12;
            while (!prepared.Ready && prepared.Failure == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (!prepared.Ready) { Failure = "Relay allocation failed."; yield return EndSession(); yield break; }
            payload = null;
            // The server sends a nonce to this allocation and accepts only its actual UDP reply.
            yield return Http("/sessions/verify", "POST", JsonUtility.ToJson(new EndpointProof { address = RelayEndPoint.Address.ToString(), port = RelayEndPoint.Port }), value => payload = value);
            if (payload == null || JsonUtility.FromJson<Reply>(payload)?.status != "verified")
            { Failure = "Relay ownership verification failed."; yield return EndSession(); }
        }
        void Update()
        {
            // Before Mirage claims the socket, keep allocation negotiation and verification nonblocking.
            // Renewable leases reap crashed clients rather than holding all anonymous slots for six hours.
            if (session != null && session.lease>0 && Ready && Time.realtimeSinceStartup>=nextLease)
            { nextLease=Time.realtimeSinceStartup+20; StartCoroutine(Http("/sessions/heartbeat", "POST", "{}", value => { if (value==null) { Failure="Relay session expired."; prepared?.Close(); } })); }
            if (prepared == null || claimed) return;
            prepared.Tick();
            var buffer = new byte[1200];
            while (prepared.Poll())
            {
                int length = prepared.Receive(buffer, out var peer);
                if (!(peer is TurnSocket.Handle handle) || handle.Endpoint.Port != 49160 || length < 10 || length > 80) continue;
                if (Encoding.ASCII.GetString(buffer, 0, length).StartsWith("HFVERIFY:", StringComparison.Ordinal))
                    prepared.Send(peer, new ReadOnlySpan<byte>(buffer, 0, length));
            }
        }
        IEnumerator Http(string path, string method, string json, Action<string> callback, bool authenticated = true)
        {
            using (var request = new UnityWebRequest(directory + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 8;
                if (json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
                if (authenticated && session != null) request.SetRequestHeader("Authorization", "Bearer " + session.token);
                yield return request.SendWebRequest();
                // Credentials and response bodies never enter diagnostics or authored assets.
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("Relay control " + method + " " + path + " failed (HTTP "
                        + request.responseCode + ", " + request.result + ").", this);
                callback(request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : null);
            }
        }
        public IEnumerator EndSession()
        {
            if (prepared != null) prepared.Close(); prepared = null; claimed = false;
            if (session != null && !string.IsNullOrEmpty(directory)) yield return Http("/sessions", "DELETE", null, _ => { });
            session = null;
        }
        ISocket Claim()
        {
            if (!Ready || claimed) throw new InvalidOperationException("Verified relay allocation required before peer startup.");
            claimed = true; return prepared;
        }
        public override ISocket CreateServerSocket() => Claim();
        public override ISocket CreateClientSocket() => Claim();
        public override IBindEndPoint GetBindEndPoint() => new TurnSocket.Handle(RelayEndPoint);
        public override IConnectEndPoint GetConnectEndPoint(string address = null, ushort? port = null)
            => new TurnSocket.Handle(new IPEndPoint(IPAddress.Parse(address), port ?? (ushort)Port));
        void OnDestroy() { prepared?.Close(); }
    }
}
