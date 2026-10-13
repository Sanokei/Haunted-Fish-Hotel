using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace HauntedFish.BossFight
{
 // Preserve authored bindings for compatibility; requested Boss presentation has no bottom text.
 public sealed class BossHudPresentation : MonoBehaviour
 {
  [SerializeField] Canvas _Canvas;
  [SerializeField] Text _Lives;
  [SerializeField] TMP_Text _Hint;
  public void Present(BossArenaSnapshot state,BossViewerSide side,bool watching,bool practice)
  {if(_Canvas)_Canvas.enabled=false;}
 }
}
