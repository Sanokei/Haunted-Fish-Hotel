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
            if(_Box)Fit(_Box,_BoxWorldSize);
        }
        static void Fit(SpriteRenderer image,Vector2 size)
        {
            if(!image.sprite)return;var bounds=image.sprite.bounds.size;
            float scale=Mathf.Min(size.x/Mathf.Max(.001f,bounds.x),size.y/Mathf.Max(.001f,bounds.y));
            image.transform.localScale=Vector3.one*scale;
        }
        public void Highlight(bool selected)
        {
            if(_Item)_Item.color=selected?HotelPalette.Peach:Color.white;
        }
    }
}
