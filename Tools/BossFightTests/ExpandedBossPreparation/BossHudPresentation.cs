using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace HauntedFish.BossFight
{
    public sealed class BossHudPresentation : MonoBehaviour
    {
        [SerializeField] Canvas _Canvas;
        [SerializeField] Text _Lives;
        [SerializeField] TMP_Text _Hint;
        public void Present(BossArenaSnapshot state,BossViewerSide side,bool watching,bool practice)
        {
            if(_Canvas)_Canvas.enabled=watching;
            if(!watching||!_Lives||!_Hint)return;
            if(practice)
            {
                _Lives.text=side==BossViewerSide.Ghost?"YOU "+Mathf.Max(0,BossMatch.FishGoalsToWin-(state?.FishScore??0))+" LIVES | FISH "+Mathf.Max(0,BossMatch.GhostGoalsToWin-(state?.GhostScore??0))+" LIFE":"YOU "+Mathf.Max(0,BossMatch.GhostGoalsToWin-(state?.GhostScore??0))+" LIFE | TRAINING "+Mathf.Max(0,BossMatch.FishGoalsToWin-(state?.FishScore??0))+" LIVES";
                _Hint.text=state!=null&&state.ResultIssued?(state.WinnerFish?"TRAINING WIN - no rewards; restarting":"TRAINING LOSS - no rewards; restarting"):"PRACTICE: drag your striker to hit the cue ball. No rewards.";
                return;
            }
            int fish=Mathf.Max(0,BossMatch.GhostGoalsToWin-(state?.GhostScore??0));
            int ghost=Mathf.Max(0,BossMatch.FishGoalsToWin-(state?.FishScore??0));
            _Lives.text=side==BossViewerSide.Fish?"YOU "+fish+" LIFE | GHOST "+ghost+" LIVES":
                side==BossViewerSide.Ghost?"YOU "+ghost+" LIVES | FISH "+fish+" LIFE":"FISH "+fish+" LIFE | GHOST "+ghost+" LIVES";
            _Hint.text=state!=null&&state.ResultIssued?(state.Cancelled?"Match cancelled":state.WinnerFish?"FISH WINS":"GHOST WINS"):
                state==null||!state.ParticipantsReady?"Waiting for both players":side==BossViewerSide.Spectator?"WATCHING - participants control striker and defenses":"Drag your striker to hit the cue ball. Drag sideline defenses onto your half.";
        }
    }
}