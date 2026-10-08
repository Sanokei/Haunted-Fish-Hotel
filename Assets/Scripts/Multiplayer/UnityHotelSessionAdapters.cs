using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using Mirage.SocketLayer;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    internal sealed class UnityHotelSessionTransport : IHotelSessionTransport
    {
        readonly Func<SocketFactory> _Socket;
        readonly Func<string> _DirectoryUrl;
        int _Port;
        string _Address;
        HotelTurnSocketFactory Relay => _Socket() as HotelTurnSocketFactory;
        public bool Supported => _Socket() && _Socket().IsSupported && _Socket() is IHasPort;
        public bool UsesRelay => Relay;
        public bool Ready => !Relay || Relay.Ready;
        public string Failure => Relay ? Relay.Failure : null;
        public string Address => Relay ? Relay.RelayEndPoint.Address.ToString() : _Address;
        public int Port => Relay ? Relay.RelayEndPoint.Port : _Port;
        public float RetryDelay => Relay ? Relay.RetryDelay : 0f;

        public UnityHotelSessionTransport(Func<SocketFactory> socket, Func<string> directoryUrl)
        {
            _Socket = socket;
            _DirectoryUrl = directoryUrl;
        }

        public IEnumerator Prepare(int port)
        {
            _Port = port;
            ((IHasPort)_Socket()).Port = port;
            _Address = LocalAddress();
            if (Relay) yield return Relay.Prepare(_DirectoryUrl());
        }

        public IEnumerator EndSession()
        {
            if (Relay) yield return Relay.EndSession();
        }

        internal static string LocalAddress()
        {
            try
            {
                foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                        return address.ToString();
            }
            catch (SocketException) { }
            return "127.0.0.1";
        }
    }

    // Converts typed room operations to the directory's JSON/HTTP protocol at the boundary.
    internal sealed class UnityHotelSessionDirectory : IHotelSessionDirectory
    {
        readonly Func<string> _Url, _Token;
        readonly UnityEngine.Object _LogContext;

        public UnityHotelSessionDirectory(Func<string> url, Func<string> token, UnityEngine.Object logContext)
        {
            _Url = url;
            _Token = token;
            _LogContext = logContext;
        }

        public IEnumerator Create(CreateRoom request, Action<Room, string> completed) =>
            Request("/rooms", "POST", JsonUtility.ToJson(request), completed);

        public IEnumerator Join(string code, bool relay, Action<Room, string> completed) =>
            Request("/rooms/" + code + (relay ? "/join" : string.Empty), relay ? "POST" : "GET", relay ? "{}" : null, completed);

        public IEnumerator Delete(Room room, Action<Room, string> completed) =>
            Request("/rooms/" + room.code, "DELETE", JsonUtility.ToJson(new RoomHeartbeat { ownerKey = room.ownerKey }), completed);

        public IEnumerator Heartbeat(string code, RoomHeartbeat request, Action<Room, string> completed) =>
            Request("/rooms/" + code + "/heartbeat", "POST", JsonUtility.ToJson(request), completed);

        IEnumerator Request(string path, string method, string payload, Action<Room, string> completed)
        {
            yield return HotelRoomDirectory.Request(_Url(), _Token(), path, method, payload, (room, error) =>
            {
                if (error != null) Debug.LogWarning("Room directory " + method + " request failed: " + error, _LogContext);
                completed(room, error);
            });
        }
    }
}
