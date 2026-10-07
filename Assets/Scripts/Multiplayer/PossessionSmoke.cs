using System.Collections;
using Monologue.Dialogue;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Authored cloud images and Ink layouts are reusable; replay restores the authored initial poses.
    public sealed class PossessionSmoke : MonoBehaviour
    {
        [SerializeField] StoryUI _UI;
        [SerializeField] TextAsset _Sequence;
        [SerializeField] RectTransform _Anchor;
        [SerializeField, Min(.01f)] float _WorldWidth = 2.5f, _TimingScale = 1;
        [SerializeField] bool _DestroyOnComplete = true;
        Vector3 _Position;
        RectTransform[] _Clouds;
        Vector2[] _InitialPositions;
        Vector3[] _InitialScales;
        void Awake()
        {
            _Clouds = _Anchor.GetComponentsInChildren<RectTransform>(true);
            _InitialPositions = new Vector2[_Clouds.Length]; _InitialScales = new Vector3[_Clouds.Length];
            for (int i=0;i<_Clouds.Length;i++) { _InitialPositions[i]=_Clouds[i].anchoredPosition; _InitialScales[i]=_Clouds[i].localScale; }
        }
        public void PlayAt(Vector3 position)
        {
            StopAllCoroutines(); _UI.Close(); _Position=position;
            for (int i=1;i<_Clouds.Length;i++) { _Clouds[i].anchoredPosition=_InitialPositions[i];_Clouds[i].localScale=_InitialScales[i]; }
            _UI.Prepare(); StartCoroutine(Animate());
        }
        IEnumerator Animate()
        {
            var steps=StoryFunctions.ReadAnimationSequence(_Sequence.text);_UI.ValidateSequence(steps);
            foreach(var step in steps)
                yield return _UI.Play(new StorySequenceStep(step.Kind,step.Target,step.Variant,step.Seconds*_TimingScale,step.X,step.Y,step.Count,step.Visible,step.Parent));
            _UI.Close();if(_DestroyOnComplete)Destroy(gameObject);
        }
        void LateUpdate()
        {
            var camera=HotelViewCamera.Current;if(!camera)return;
            _Anchor.position=camera.WorldToScreenPoint(_Position);
            float pixels=Mathf.Abs(camera.WorldToScreenPoint(_Position+Vector3.right*_WorldWidth).x-_Anchor.position.x);
            _Anchor.localScale=Vector3.one*(pixels/250);
        }
        void OnDisable(){StopAllCoroutines();if(_UI)_UI.Close();}
    }
}
