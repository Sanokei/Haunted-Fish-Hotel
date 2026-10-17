using System.Collections;

namespace HauntedFish.Multiplayer
{
    // A socket factory that must obtain directory credentials before Mirage starts a peer:
    // UDP TURN allocations on desktop, WebRTC relay sessions in the browser.
    public interface IHotelRelaySocketFactory
    {
        string AuthorizationToken { get; }
        string Failure { get; }
        float RetryDelay { get; }
        bool Ready { get; }
        // The room endpoint the directory publishes for this host.
        string RoomAddress { get; }
        int RoomPort { get; }
        IEnumerator Prepare(string service);
        // Completes a joining player's link to the host before Mirage connects; no-op where packets need no handshake.
        IEnumerator ConnectToHost(Room room);
        IEnumerator EndSession();
    }
}
