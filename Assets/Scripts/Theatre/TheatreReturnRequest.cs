using System;
using Mirage;
using HauntedFish.Multiplayer;
using UnityEngine;
namespace HauntedFish.Theatre
{
    [RequireComponent(typeof(HotelPlayer))]
    public sealed class TheatreReturnRequest : NetworkBehaviour
    {
        public event Action<int,bool> ReturnCompleted;
        public bool ReturnToArchway(int visitVersion)
        {
            var player=GetComponent<HotelPlayer>();
            if(!player||player!=HotelPlayer.LocalPlayer||!player.IsRelevantPlayer)return false;
            if(player.Networked)RequestReturn(visitVersion);
            else ReceiveReturn(visitVersion,TheatrePortal.Current&&TheatrePortal.Current.ReturnOwnedPlayer(player));
            return true;
        }
        [ServerRpc(requireAuthority=true)]
        void RequestReturn(int visitVersion)
        {
            var player=GetComponent<HotelPlayer>();
            bool success=player&&TheatrePortal.Current&&TheatrePortal.Current.ReturnOwnedPlayer(player);
            ReturnAcknowledged(visitVersion,success);
        }
        [ClientRpc(target=RpcTarget.Owner)]
        void ReturnAcknowledged(int visitVersion,bool success)=>ReceiveReturn(visitVersion,success);
        void ReceiveReturn(int version,bool success)
        {
            var player=GetComponent<HotelPlayer>();
            if(player&&player==HotelPlayer.LocalPlayer&&player.IsRelevantPlayer)ReturnCompleted?.Invoke(version,success);
        }
    }
}