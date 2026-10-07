using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public interface IHotelScene
    {
        void Enter(HauntedHotelMultiplayer session);
        void Exit();
        Vector3 SpawnPosition(int index);
    }
}
