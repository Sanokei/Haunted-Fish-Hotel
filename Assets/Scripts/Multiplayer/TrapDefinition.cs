using System;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    [Serializable]
    public sealed class TrapDefinition
    {
        [Tooltip("Authored trap prefab. Its family ID must be unique in this catalog.")]
        public GhostTrap Prefab;
        [Tooltip("Artwork printed on its supply box. Falls back to the prefab icon.")]
        public Sprite DisplayArt;
        [Tooltip("Relative chance per package. Zero disables supply; invalid weights are ignored.")]
        public float Weight = 1;
        public string Family => Prefab ? Prefab.FamilyTag : "";
        public Sprite Artwork => DisplayArt ? DisplayArt : Prefab ? Prefab.Icon : null;
        public bool Valid => Prefab && !string.IsNullOrWhiteSpace(Family) && Artwork;
    }
}
