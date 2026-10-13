using System;
using HauntedFish.Multiplayer;
using UnityEngine;
namespace HauntedFish.BossFight
{
    public interface IHotelBossDefenseCommands
    {
        bool AcceptDefense(HotelPlayer player,int slot,uint sequence,Vector2 target,string key,int version,uint epoch);
        void DefenseReply(HotelPlayer player,int slot,uint sequence,uint epoch,bool accepted);
    }
}