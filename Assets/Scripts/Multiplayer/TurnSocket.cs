using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Mirage.SocketLayer;

namespace HauntedFish.Multiplayer
{
    // Own exactly one authenticated TURN UDP allocation for one Mirage host or joining client.
    // Preparation is driven by Tick before Mirage starts; callers must wait for Ready or Failure.
    public sealed class TurnSocket : ISocket
    {
        readonly Socket udp;
        readonly IPEndPoint service;
        readonly string username, password;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Queue<Tuple<IConnectionHandle, byte[]>> received = new Queue<Tuple<IConnectionHandle, byte[]>>();
        readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
        byte[] key;
        string realm, nonce;
        int maximumPacket = 1200;
        double refreshAt, permissionAt;
        bool closed;
        public bool Ready { get; private set; }
        public string Failure { get; private set; }
        public IPEndPoint RelayEndPoint { get; private set; }
        sealed class Pending
        {
            public ushort Method;
            public byte[] Transaction, Wire;
            public List<TurnProtocol.Attribute> Attributes;
            public double Next, Deadline;
            public int Attempts, Challenges;
            public bool Authenticated;
        }
        public sealed class Handle : IConnectionHandle, IBindEndPoint, IConnectEndPoint
        {
            public readonly IPEndPoint Endpoint;
            public Handle(IPEndPoint endpoint) { Endpoint = endpoint; }
            public bool IsStateful => false;
            public bool SupportsGracefulDisconnect => false;
            public ISocketLayerConnection SocketLayerConnection { get; set; }
            public void Disconnect(string reason) { }
            public IConnectionHandle CreateCopy() => new Handle(new IPEndPoint(Endpoint.Address, Endpoint.Port));
            public override bool Equals(object other) => other is Handle handle && Endpoint.Equals(handle.Endpoint);
            public override int GetHashCode() => Endpoint.GetHashCode();
        }
        public TurnSocket(IPEndPoint service, string username, string password)
        {
            if (service.Address.AddressFamily != AddressFamily.InterNetwork || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) throw new ArgumentException("IPv4 TURN service and session credentials required.");
            this.service = service; this.username = username; this.password = password;
            udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udp.Bind(new IPEndPoint(IPAddress.Any, 0)); udp.Connect(service); udp.Blocking = false;
            Request(0x0003, new List<TurnProtocol.Attribute> { new TurnProtocol.Attribute(TurnProtocol.RequestedTransport, new byte[] { 17, 0, 0, 0 }) }, false);
        }
        void Request(ushort method, List<TurnProtocol.Attribute> attributes, bool authenticated)
        {
            if (pending.Count >= 8) { Fail("TURN transaction limit reached."); return; }
            var item = new Pending { Method = method, Attributes = attributes, Transaction = TurnProtocol.Transaction(), Authenticated = authenticated, Deadline = clock.Elapsed.TotalSeconds + 8 };
            Encode(item); pending.Add(Convert.ToBase64String(item.Transaction), item); Transmit(item);
        }
        void Encode(Pending item)
        {
            var attributes = new List<TurnProtocol.Attribute>(item.Attributes);
            if (item.Authenticated)
            {
                attributes.Add(TurnProtocol.Text(TurnProtocol.Username, username));
                attributes.Add(TurnProtocol.Text(TurnProtocol.Realm, realm));
                attributes.Add(TurnProtocol.Text(TurnProtocol.Nonce, nonce));
            }
            item.Wire = TurnProtocol.Encode(item.Method, item.Transaction, attributes, item.Authenticated ? key : null);
        }
        void Transmit(Pending item)
        {
            try { udp.Send(item.Wire); }
            catch (SocketException e) { if (e.SocketErrorCode != SocketError.WouldBlock) { Fail("TURN UDP send failed."); return; } }
            item.Next = clock.Elapsed.TotalSeconds + Math.Min(2, 0.5 * Math.Pow(2, item.Attempts++));
        }
        void Fail(string message) { Failure = message; Close(); }
        public void Bind(IBindEndPoint endpoint)
        {
            if (!Ready || !(endpoint is Handle handle) || !handle.Endpoint.Equals(RelayEndPoint)) throw new InvalidOperationException("TURN allocation must be ready before hosting.");
        }
        public IConnectionHandle Connect(IConnectEndPoint endpoint)
        {
            if (!Ready || !(endpoint is Handle handle) || !Allowed(handle.Endpoint)) throw new InvalidOperationException("Verified relay peer required.");
            return handle.CreateCopy();
        }
        bool Allowed(IPEndPoint peer) => peer.Address.Equals(service.Address) && peer.Port >= 49160 && peer.Port <= 49223;
        public void SetTickEvents(int maxPacketSize, OnData onData, OnDisconnect onDisconnect)
        {
            // This datagram implementation uses Poll/Receive; Mirage owns reliability and disconnects.
            if (maxPacketSize < 1 || maxPacketSize > 1200) throw new ArgumentOutOfRangeException(nameof(maxPacketSize));
            maximumPacket = maxPacketSize;
        }
        public void Tick()
        {
            if (closed) return;
            try
            {
                // Bound frame work and packet size; connected UDP accepts only the configured TURN server.
                for (int count = 0; count < 64 && udp.Poll(0, SelectMode.SelectRead); count++)
                {
                    var buffer = new byte[TurnProtocol.MaximumMessage + 1]; int size = udp.Receive(buffer);
                    if (size > TurnProtocol.MaximumMessage) continue;
                    Array.Resize(ref buffer, size); Process(buffer);
                    if (closed) return;
                }
                double now = clock.Elapsed.TotalSeconds;
                foreach (var item in new List<Pending>(pending.Values))
                {
                    if (now >= item.Deadline) { Fail("TURN transaction timed out."); return; }
                    if (now >= item.Next) Transmit(item);
                }
                if (!Ready) return;
                if (now >= refreshAt)
                {
                    refreshAt = now + 240;
                    Request(0x0004, new List<TurnProtocol.Attribute> { new TurnProtocol.Attribute(TurnProtocol.Lifetime, TurnProtocol.Number(600)) }, true);
                }
                if (now >= permissionAt) Permission();
            }
            catch (SocketException e) { if (e.SocketErrorCode != SocketError.WouldBlock) Fail("TURN receive failed."); }
            catch (FormatException) { Fail("Malformed authenticated TURN response."); }
        }
        void Permission()
        {
            // TURN permissions cover the relay IP rather than individual allocation ports.
            permissionAt = clock.Elapsed.TotalSeconds + 240;
            Request(0x0008, new List<TurnProtocol.Attribute> { TurnProtocol.Address(TurnProtocol.PeerAddress, new IPEndPoint(service.Address, 49160)) }, true);
        }
        void Process(byte[] wire)
        {
            if (!TurnProtocol.TryParse(wire, out var message)) return;
            if (message.Type == 0x0017)
            {
                // Data indications have no integrity attribute; pin source to the connected service and permitted relay pool.
                var address = message.Find(TurnProtocol.PeerAddress); var data = message.Find(TurnProtocol.Data);
                if (!Ready || address == null || data == null || data.Value.Length > maximumPacket || received.Count >= 128) return;
                var peer = TurnProtocol.DecodeAddress(address);
                if (Allowed(peer)) received.Enqueue(Tuple.Create<IConnectionHandle, byte[]>(new Handle(peer), data.Value));
                return;
            }
            string id = Convert.ToBase64String(message.Transaction);
            if (!pending.TryGetValue(id, out var item)) return;
            if (message.Type != (item.Method | 0x0100) && message.Type != (item.Method | 0x0110)) return;
            var error = message.Find(TurnProtocol.ErrorCode);
            if (error != null)
            {
                if (error.Value.Length < 4) return;
                int code = (error.Value[2] & 7) * 100 + error.Value[3];
                // Initial401 bootstraps realm/nonce. Stale438 must authenticate against the established key.
                if ((code != 401 || item.Authenticated) && (code != 438 || !item.Authenticated || !TurnProtocol.Verify(message, key))) { Fail("TURN authorization rejected."); return; }
                var r = message.Find(TurnProtocol.Realm); var n = message.Find(TurnProtocol.Nonce);
                if (r == null || n == null || r.Value.Length > 128 || n.Value.Length > 512 || ++item.Challenges > 2) { Fail("Invalid TURN challenge."); return; }
                string nextRealm = Encoding.UTF8.GetString(r.Value);
                if (realm != null && realm != nextRealm) { Fail("TURN realm changed."); return; }
                realm = nextRealm; nonce = Encoding.UTF8.GetString(n.Value); key = TurnProtocol.Key(username, realm, password);
                pending.Remove(id); item.Transaction = TurnProtocol.Transaction(); item.Authenticated = true; item.Attempts = 0;
                Encode(item); pending.Add(Convert.ToBase64String(item.Transaction), item); Transmit(item); return;
            }
            if (!item.Authenticated || !TurnProtocol.Verify(message, key)) return;
            pending.Remove(id);
            if (item.Method == 0x0003)
            {
                RelayEndPoint = TurnProtocol.DecodeAddress(message.Find(TurnProtocol.RelayedAddress));
                if (!Allowed(RelayEndPoint)) { Fail("Allocation is outside the approved relay pool."); return; }
                refreshAt = clock.Elapsed.TotalSeconds + 240; Permission();
            }
            else if (item.Method == 0x0008) Ready = true;
            // Refresh success must preserve a nonzero allocation lifetime; zero signals expiration.
            if (item.Method == 0x0003 || item.Method == 0x0004)
            {
                var lifetime = message.Find(TurnProtocol.Lifetime);
                if (lifetime == null || lifetime.Value.Length != 4 || TurnProtocol.Read32(lifetime.Value, 0) < 60) { Fail("TURN allocation lifetime invalid."); return; }
                refreshAt = clock.Elapsed.TotalSeconds + Math.Min(240, TurnProtocol.Read32(lifetime.Value, 0) / 2);
            }
        }
        public bool Poll() => received.Count > 0;
        public int Receive(Span<byte> buffer, out IConnectionHandle handle)
        {
            handle = null;
            if (received.Count == 0) return 0;
            var packet = received.Dequeue();
            if (packet.Item2.Length > buffer.Length) return 0;
            packet.Item2.AsSpan().CopyTo(buffer); handle = packet.Item1; return packet.Item2.Length;
        }
        public void Send(IConnectionHandle handle, ReadOnlySpan<byte> packet)
        {
            if (closed || !Ready || !(handle is Handle peer) || !Allowed(peer.Endpoint) || packet.Length > maximumPacket) return;
            var wire = TurnProtocol.Encode(0x0016, TurnProtocol.Transaction(), new List<TurnProtocol.Attribute> { TurnProtocol.Address(TurnProtocol.PeerAddress, peer.Endpoint), new TurnProtocol.Attribute(TurnProtocol.Data, packet.ToArray()) });
            try { udp.Send(wire); }
            catch (SocketException e) { if (e.SocketErrorCode != SocketError.WouldBlock) Fail("TURN data send failed."); }
        }
        public void Flush() { }
        public void Close()
        {
            if (closed) return;
            // Release best-effort on the same five-tuple; lifetime expiration remains the fallback.
            if (key != null && RelayEndPoint != null)
            {
                try
                {
                    var attrs = new List<TurnProtocol.Attribute> { new TurnProtocol.Attribute(TurnProtocol.Lifetime, TurnProtocol.Number(0)), TurnProtocol.Text(TurnProtocol.Username, username), TurnProtocol.Text(TurnProtocol.Realm, realm), TurnProtocol.Text(TurnProtocol.Nonce, nonce) };
                    udp.Send(TurnProtocol.Encode(0x0004, TurnProtocol.Transaction(), attrs, key));
                }
                catch (SocketException) { }
            }
            closed = true; Ready = false; pending.Clear(); received.Clear(); udp.Dispose();
        }
    }
}
