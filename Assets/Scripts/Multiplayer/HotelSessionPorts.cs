using System;
using System.Collections;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Session policy depends on these ports, never on a scene object, HTTP request or Mirage peer.
    internal interface IHotelSessionNetwork
    {
        bool IsHost { get; }
        bool IsRunning { get; }
        bool IsAdmitted { get; }
        bool HasLocalCharacter { get; }
        int PlayerCount { get; }
        bool AllReady { get; }
        bool Quickplay { get; }
        void StartHost(Room room, int capacity, IEnumerable<string> kickedPlayerIds = null);
        void StartClient(Room room, int capacity);
        void Stop();
    }

    internal interface IHotelSessionTransport
    {
        bool Supported { get; }
        bool UsesRelay { get; }
        bool Ready { get; }
        string Failure { get; }
        string Address { get; }
        int Port { get; }
        float RetryDelay { get; }
        IEnumerator Prepare(int port);
        IEnumerator EndSession();
    }

    internal interface IHotelSessionDirectory
    {
        IEnumerator Create(CreateRoom request, Action<Room, string> completed);
        IEnumerator Join(string code, bool relay, Action<Room, string> completed);
        IEnumerator Delete(Room room, Action<Room, string> completed);
        IEnumerator Heartbeat(string code, RoomHeartbeat request, Action<Room, string> completed);
    }

    internal interface IHotelSessionRuntime
    {
        float Time { get; }
        object Start(IEnumerator routine);
        void Stop(object handle);
        object Delay(float seconds);
    }

    internal interface IHotelSessionScene
    {
        bool Loading { get; }
        bool IntroductionPlaying { get; }
        bool IsEditor { get; }
        string TargetScene { get; }
        void PrepareIntroduction();
        void ResetInputFocus();
        void SessionEnded();
    }
}
