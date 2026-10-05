using UnityEngine;

namespace HauntedFish.Multiplayer
{
    public sealed class HotelFishSprite : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _Body;
        [SerializeField] Sprite _LeftBody;
        [SerializeField] Sprite _RightBody;
        [SerializeField] Transform _FinPivot;
        [SerializeField] SpriteRenderer _Fin;
        [SerializeField] bool _FaceCamera = true;
        [SerializeField] float _IdleFinAngle = 5;
        [SerializeField] float _MovingFinAngle = 18;
        [SerializeField] float _IdleFinFrequency = .8f;
        [SerializeField] float _MovingFinFrequency = 3;

        bool _FacingLeft, _Walking, _Talking;
        bool _FacingInitialized;
        Vector3 _RightFinPosition;
        Quaternion _FinRestRotation;
        float _Phase, _FinAngle;

        void Awake()
        {
            if (!_Body || !_FinPivot || !_Fin || !_LeftBody || !_RightBody)
            {
                Debug.LogError("Assign both body sprites and the authored fin pivot/renderer.", this);
                enabled = false;
                return;
            }
            _RightFinPosition = _FinPivot.localPosition;
            _FinRestRotation = _FinPivot.localRotation;
            _Fin.sortingLayerID = _Body.sortingLayerID;
            _Fin.sortingOrder = _Body.sortingOrder - 1;
            ApplyFacing();
        }

        public void Present(bool facingLeft, bool walking, bool talking)
        {
            var facingChanged = !_FacingInitialized || _FacingLeft != facingLeft;
            _FacingLeft = facingLeft;
            _Walking = walking;
            _Talking = talking;
            if (facingChanged && _Body && _FinPivot) ApplyFacing();
        }

        void ApplyFacing()
        {
            _FacingInitialized = true;
            _Body.sprite = _FacingLeft ? _LeftBody : _RightBody;
            float sign = _FacingLeft ? -1 : 1;
            _FinPivot.localPosition = new Vector3(_RightFinPosition.x * sign, _RightFinPosition.y, _RightFinPosition.z);
            _FinPivot.localScale = new Vector3(sign, 1, 1);
        }

        void LateUpdate()
        {
            var camera = _FaceCamera ? Camera.main : null;
            if (camera) transform.rotation = camera.transform.rotation;
            float sign = _FacingLeft ? -1 : 1;
            bool animated = _Walking || _Talking;
            _Phase = Mathf.Repeat(_Phase + Time.deltaTime * (animated ? _MovingFinFrequency : _IdleFinFrequency), 1);
            _FinAngle = Mathf.Lerp(_FinAngle, animated ? _MovingFinAngle : _IdleFinAngle,
                1 - Mathf.Exp(-10 * Time.deltaTime));
            _FinPivot.localRotation = _FinRestRotation *
                Quaternion.Euler(0, 0, Mathf.Sin(_Phase * Mathf.PI * 2) * _FinAngle * sign);
        }
    }
}
