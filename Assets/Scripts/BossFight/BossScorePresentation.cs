using UnityEngine;
namespace HauntedFish.BossFight
{
    [RequireComponent(typeof(TextMesh),typeof(MeshRenderer))]
    public sealed class BossScorePresentation : MonoBehaviour
    {
        [SerializeField] TextMesh _Text;
        [SerializeField] MeshRenderer _Renderer;
        Material _OwnedMaterial;
        void Awake()
        {
            _OwnedMaterial = new Material(_Renderer.sharedMaterial);
            _Renderer.sharedMaterial = _OwnedMaterial;
            Font.textureRebuilt += OnFontTextureRebuilt;
            RefreshTexture();
        }
        void OnFontTextureRebuilt(Font font) { if (_Text && font == _Text.font) RefreshTexture(); }
        void RefreshTexture()
        {
            if (_OwnedMaterial && _Text && _Text.font)
                _OwnedMaterial.mainTexture = _Text.font.material.mainTexture;
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            if (_OwnedMaterial) Destroy(_OwnedMaterial);
        }
    }
}