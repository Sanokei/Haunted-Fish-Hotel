using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Reusable authored package. Its injected definition supplies identity and item artwork.
    public sealed class GhostTrapSupply : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _Box, _Item;
        [SerializeField] Vector2 _BoxWorldSize=new Vector2(3.6f,2.06f), _ItemWorldSize=new Vector2(1.7f,1.4f);
        public int PackageId { get; private set; } = -1;
        public string FamilyTag { get; private set; } = "";
        public Sprite DisplayArt => _Item ? _Item.sprite : null;
        public Sprite BoxArt => _Box ? _Box.sprite : null;
        public void Configure(int id,TrapDefinition definition)
        {
            PackageId=id;FamilyTag=definition.Family;
            if(_Item){_Item.sprite=definition.Artwork;Fit(_Item,_ItemWorldSize);}
            if(_Box)
            {
                if(_Box.drawMode==SpriteDrawMode.Simple)Fit(_Box,_BoxWorldSize);
                else
                {
                    // 9-slice package: each trap family ships in its own box shape. The renderer is scaled so
                    // the painted corners stay crisp; the sliced size carries the box dimensions.
                    var size=BoxSize(definition.Family);
                    _Box.transform.localScale=Vector3.one*SliceScale;
                    _Box.size=size/SliceScale;
                }
            }
        }
        const float SliceScale=.2f;
        static Vector2 BoxSize(string family)=>family switch
        {
            "cart"=>new Vector2(3.6f,2.1f),
            "chandelier"=>new Vector2(2.4f,2.9f),
            "angler"=>new Vector2(3.1f,2.4f),
            "click"=>new Vector2(2.2f,1.9f),
            _=>new Vector2(2.8f,2.2f),
        };
        static void Fit(SpriteRenderer image,Vector2 size)
        {
            if(!image.sprite)return;var bounds=image.sprite.bounds.size;
            float scale=Mathf.Min(size.x/Mathf.Max(.001f,bounds.x),size.y/Mathf.Max(.001f,bounds.y));
            image.transform.localScale=Vector3.one*scale;
        }
        // Packages are the ghost's secret: the fish never sees what is coming down the conveyor.
        void LateUpdate()
        {
            var local=HotelPlayer.LocalPlayer;
            bool visible=!local||local.ControlMode==HotelControlMode.Ghost;
            if(_Box&&_Box.enabled!=visible)_Box.enabled=visible;
            if(_Item&&_Item.enabled!=visible)_Item.enabled=visible;
        }
        public void Highlight(bool selected)
        {
            if(_Item)_Item.color=selected?HotelPalette.Peach:Color.white;
        }
    }
}
