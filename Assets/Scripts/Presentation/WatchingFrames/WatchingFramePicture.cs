using System.Collections.Generic;
using UnityEngine;

namespace HauntedFish.Presentation
{
    /// <summary>Fills a filled watching frame with one randomly chosen picture from an injected list.</summary>
    [DisallowMultipleComponent]
    public sealed class WatchingFramePicture : MonoBehaviour
    {
        [SerializeField] Renderer _Renderer;
        [SerializeField] List<Texture2D> _Pictures = new List<Texture2D>();
        static readonly int PictureId = Shader.PropertyToID("_PictureTex");
        static readonly int RectId = Shader.PropertyToID("_PictureRect");
        static readonly int HasPictureId = Shader.PropertyToID("_HasPicture");
        MaterialPropertyBlock _Properties;

        void Awake()
        {
            if (!_Renderer) _Renderer = GetComponent<Renderer>();
            Apply();
        }

        /// <summary>Replace the picture pool (dependency injection) and pick again.</summary>
        public void SetPictures(IEnumerable<Texture2D> pictures)
        {
            _Pictures = new List<Texture2D>(pictures);
            Apply();
        }

        void Apply()
        {
            if (!_Renderer) return;
            var candidates = _Pictures.FindAll(t => t);
            if (candidates.Count == 0) return;
            var picture = candidates[Random.Range(0, candidates.Count)];
            // Cover-fit against the frame quad so the opening is always filled and never stretched.
            float aspect = (float)picture.width / Mathf.Max(1, picture.height);
            var scale = _Renderer.transform.lossyScale;
            float frameAspect = Mathf.Abs(scale.x) / Mathf.Max(.0001f, Mathf.Abs(scale.y));
            float relative = aspect / Mathf.Max(.0001f, frameAspect);
            var size = relative >= 1 ? new Vector2(relative, 1) : new Vector2(1, 1 / relative);
            _Properties = _Properties ?? new MaterialPropertyBlock();
            _Renderer.GetPropertyBlock(_Properties);
            _Properties.SetTexture(PictureId, picture);
            _Properties.SetVector(RectId, new Vector4(.5f, .5f, size.x, size.y));
            _Properties.SetFloat(HasPictureId, 1);
            _Renderer.SetPropertyBlock(_Properties);
        }
    }
}
