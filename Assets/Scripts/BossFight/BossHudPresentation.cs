using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace HauntedFish.BossFight
{
 public sealed class BossHudPresentation : MonoBehaviour
 {
  [SerializeField] Canvas _Canvas;
  // Retain historical serialized identities without re-enabling the removed bottom HUD.
  [SerializeField, HideInInspector] Text _Lives;
  [SerializeField, HideInInspector] TMP_Text _Hint;
  [SerializeField] TMP_Text _LifeLabel;
  [SerializeField] GameObject _FishRow, _GhostRow;
  [SerializeField] Image _FishLife;
  [SerializeField] Image[] _GhostLives;

  public void Present(BossArenaSnapshot state,BossViewerSide side,bool watching,bool practice)
  {
   var lives=BossLivesState.ForViewer(side,watching,state!=null,state!=null&&state.Cancelled,
    state==null?0:state.FishScore,state==null?0:state.GhostScore);
   if(_Canvas)_Canvas.enabled=lives.Visible;
   if(_FishRow)_FishRow.SetActive(lives.Visible&&side==BossViewerSide.Ghost);
   if(_GhostRow)_GhostRow.SetActive(lives.Visible&&side==BossViewerSide.Fish);
   if(_FishLife)_FishLife.enabled=lives.Visible&&side==BossViewerSide.Ghost&&lives.Remaining>0;
   if(_GhostLives!=null)
    for(int i=0;i<_GhostLives.Length;i++)
     if(_GhostLives[i])_GhostLives[i].enabled=lives.Visible&&side==BossViewerSide.Fish&&i<lives.Remaining;
  }

  void OnDisable(){if(_Canvas)_Canvas.enabled=false;}
 }
}
