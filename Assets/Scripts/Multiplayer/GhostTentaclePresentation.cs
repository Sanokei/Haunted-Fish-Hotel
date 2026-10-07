using UnityEngine;
using UnityEngine.UI;
namespace HauntedFish.Multiplayer
{
    // Same sprite and world position, drawn on an authored Canvas above the trap overlay.
    public sealed class GhostTentaclePresentation : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _Source;
        [SerializeField] Image _Image;
        void LateUpdate()
        {
            var camera=HotelViewCamera.Current;
            bool visible=_Source&&_Source.gameObject.activeInHierarchy&&camera;
            _Image.gameObject.SetActive(visible);if(!visible)return;
            _Source.enabled=false;_Image.sprite=_Source.sprite;_Image.color=_Source.color;
            var size=Vector3.Scale(_Source.sprite.bounds.size,_Source.transform.lossyScale);
            var point=camera.WorldToScreenPoint(_Source.transform.position);
            _Image.rectTransform.position=point;
            _Image.rectTransform.sizeDelta=new Vector2(Mathf.Abs(camera.WorldToScreenPoint(_Source.transform.position+Vector3.right*size.x).x-point.x),Mathf.Abs(camera.WorldToScreenPoint(_Source.transform.position+Vector3.up*size.y).y-point.y));
            _Image.rectTransform.localScale=new Vector3(_Source.flipX?-1:1,1,1);
        }
        void OnDisable(){if(_Source)_Source.enabled=true;if(_Image)_Image.gameObject.SetActive(false);}
    }
}
