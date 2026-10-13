using Mirage;
using HauntedFish.BossFight;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Append to authored HotelNetworkPlayer; endpoint bound by existing Boss coordinator lifecycle.
    [RequireComponent(typeof(HotelPlayer))]
    public sealed class BossDefenseRequest : NetworkBehaviour
    {
        IHotelBossDefenseCommands _Commands;
        public void Bind(IHotelBossDefenseCommands commands)=>_Commands=commands;
        public void Unbind(IHotelBossDefenseCommands commands){if(ReferenceEquals(_Commands,commands))_Commands=null;}
        public void Submit(int slot,uint sequence,Vector2 target,string key,int version,uint epoch)
        {
            var player=GetComponent<HotelPlayer>();if(!player||player!=HotelPlayer.LocalPlayer||!player.IsRelevantPlayer)return;
            if(player.Networked)RequestMove(slot,sequence,target,key,version,epoch);
            else _Commands?.DefenseReply(player,slot,sequence,epoch,_Commands.AcceptDefense(player,slot,sequence,target,key,version,epoch));
        }
        [ServerRpc(requireAuthority=true)]
        void RequestMove(int slot,uint sequence,Vector2 target,string key,int version,uint epoch)
        {
            var player=GetComponent<HotelPlayer>();
            bool accepted=_Commands!=null&&_Commands.AcceptDefense(player,slot,sequence,target,key,version,epoch);
            Reply(slot,sequence,epoch,accepted);
        }
        [ClientRpc(target=RpcTarget.Owner)]
        void Reply(int slot,uint sequence,uint epoch,bool accepted)
        {
            var player=GetComponent<HotelPlayer>();if(player&&player==HotelPlayer.LocalPlayer&&player.IsRelevantPlayer)_Commands?.DefenseReply(player,slot,sequence,epoch,accepted);
        }
    }
}