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
        [SerializeField] float _IdleFinAngle = 3;
        [SerializeField] float _MovingFinAngle = 9;
        [SerializeField] float _IdleFinFrequency = .65f;
        [SerializeField] float _MovingFinFrequency = 1.8f;

        bool _FacingLeft, _Walking, _Talking;
        bool _FacingInitialized;
        Vector3 _RightFinPosition;
        Vector3 _FinRestScale;
        Quaternion _FinRestRotation;
        float _Phase, _FinAngle, _FinFrequency, _Activity;

        void Awake()
        {
            if (!_Body || !_FinPivot || !_Fin || !_LeftBody || !_RightBody)
            {
                Debug.LogError("Assign both body sprites and the authored fin pivot/renderer.", this);
                enabled = false;
                return;
            }
            _RightFinPosition = _FinPivot.localPosition;
            _FinRestScale = _FinPivot.localScale;
            _FinRestRotation = _FinPivot.localRotation;
            _Phase = Random.value;
            _FinAngle = _IdleFinAngle;
            _FinFrequency = _IdleFinFrequency;
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
            _FinPivot.localScale = Vector3.Scale(_FinRestScale, new Vector3(sign, 1, 1));
        }

        void LateUpdate()
        {
            var camera = _FaceCamera ? Camera.main : null;
            if (camera) transform.rotation = camera.transform.rotation;
            float sign = _FacingLeft ? -1 : 1;
            float blend = 1 - Mathf.Exp(-5 * Time.deltaTime);
            // Speaking gets a small expressive flutter, rather than a full swimming stroke.
            _Activity = Mathf.Lerp(_Activity, _Walking ? 1 : (_Talking ? .25f : 0), blend);
            _FinAngle = Mathf.Lerp(_IdleFinAngle, _MovingFinAngle, _Activity);
            _FinFrequency = Mathf.Lerp(_FinFrequency,
                Mathf.Lerp(_IdleFinFrequency, _MovingFinFrequency, _Activity), blend);
            _Phase = Mathf.Repeat(_Phase + Time.deltaTime * _FinFrequency, 1);
            float cycle = _Phase * Mathf.PI * 2;
            // A quicker push and softer recovery give the tail a paddling rhythm.
            float stroke = (Mathf.Sin(cycle) + .22f * Mathf.Sin(cycle * 2)) / 1.1f;
            float fold = Mathf.Sin(cycle - .45f);
            _FinPivot.localRotation = _FinRestRotation *
                Quaternion.Euler(fold * Mathf.Lerp(8, 24, _Activity), 0, stroke * _FinAngle * sign);
            // The fin opens on the push and relaxes on recovery, anchored at its root.
            _FinPivot.localScale = Vector3.Scale(_FinRestScale,
                new Vector3(sign * (1 + stroke * Mathf.Lerp(.025f, .07f, _Activity)),
                    1 - stroke * .025f * _Activity, 1));
        }
    }
}
