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

        [SerializeField] float _RunningFinFrequency = 2.8f;
        bool _FacingLeft, _Walking, _Talking, _Running;
        bool _FacingInitialized, _Corpse;
        public void SetCorpse(bool corpse) => _Corpse = corpse;
        Vector3 _RightFinPosition;
        Vector3 _FinRestScale;
        Quaternion _FinRestRotation;
        float _Phase, _FinAngle, _FinFrequency, _Activity;
        Color _BodyColor, _FinColor;
        float _InspectionOpacity = 1;

        void Awake()
        {
            if (!_Body || !_FinPivot || !_Fin || !_LeftBody || !_RightBody)
            {
                Debug.LogError("Assign both body sprites and the authored fin pivot/renderer.", this);
                enabled = false;
                return;
            }
            _RightFinPosition = _FinPivot.localPosition;
            _BodyColor = _Body.color; _FinColor = _Fin.color;
            _FinRestScale = _FinPivot.localScale;
            _FinRestRotation = _FinPivot.localRotation;
            _Phase = Random.value;
            _FinAngle = _IdleFinAngle;
            _FinFrequency = _IdleFinFrequency;
            _Fin.sortingLayerID = _Body.sortingLayerID;
            _Fin.sortingOrder = _Body.sortingOrder - 1;
            ApplyFacing();
        }

        public void CopyAppearanceFrom(HotelFishSprite other)
        {
            if (!other || !_Body || !_Fin) return;
            _LeftBody = other._LeftBody; _RightBody = other._RightBody;
            _BodyColor = other._BodyColor; _FinColor = other._FinColor;
            _Body.color = _BodyColor; _Fin.color = _FinColor;
            _Body.sharedMaterial = other._Body.sharedMaterial;
            _Fin.sharedMaterial = other._Fin.sharedMaterial;
            _Fin.sprite = other._Fin.sprite;
            ApplyFacing();
        }

        public void Present(bool facingLeft, bool walking, bool talking, bool running = false)
        {
            var facingChanged = !_FacingInitialized || _FacingLeft != facingLeft;
            _FacingLeft = facingLeft;
            _Walking = walking;
            _Running = walking && running;
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
            var camera = _FaceCamera ? HotelViewCamera.Current : null;
            if (camera) transform.rotation = camera.transform.rotation;
            if (_Corpse)
            {
                _FinPivot.localRotation = _FinRestRotation;
                _FinPivot.localScale = Vector3.Scale(_FinRestScale, new Vector3(_FacingLeft ? -1 : 1, 1, 1));
                return;
            }
            var inspection = LobbyCursorManager.Instance;
            bool obstructing = false;
            if (camera && inspection && inspection.InspectingRadio)
            {
                var delta = inspection.RadioFocus - camera.transform.position;
                var ray = new Ray(camera.transform.position, delta.normalized);
                obstructing = _Body.bounds.IntersectRay(ray, out var distance) && distance < delta.magnitude;
            }
            _InspectionOpacity = Mathf.MoveTowards(_InspectionOpacity, obstructing ? .12f : 1, Time.unscaledDeltaTime * 6);
            var bodyColor = _BodyColor; bodyColor.a *= _InspectionOpacity; _Body.color = bodyColor;
            var finColor = _FinColor; finColor.a *= _InspectionOpacity; _Fin.color = finColor;
            float sign = _FacingLeft ? -1 : 1;
            float blend = 1 - Mathf.Exp(-5 * Time.deltaTime);
            // Speaking gets a small expressive flutter, rather than a full swimming stroke.
            _Activity = Mathf.Lerp(_Activity, _Walking ? 1 : (_Talking ? .25f : 0), blend);
            _FinAngle = Mathf.Lerp(_IdleFinAngle, _MovingFinAngle, _Activity);
            _FinFrequency = Mathf.Lerp(_FinFrequency,
                Mathf.Lerp(_IdleFinFrequency, _Running ? _RunningFinFrequency : _MovingFinFrequency, _Activity), blend);
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
        void OnDisable()
        {
            _InspectionOpacity = 1;
            if (_Body) _Body.color = _BodyColor;
            if (_Fin) _Fin.color = _FinColor;
        }
    }
}
