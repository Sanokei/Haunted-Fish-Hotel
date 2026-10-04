using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // One authored root per scene supplies the scene's UI, spawn definition and dialogue dependency.
    public sealed class HotelSceneBindings : MonoBehaviour
    {
        [SerializeField] HotelLobbyPanel _LobbyPanel;
        [SerializeField] HotelConnectionScreen _ConnectionScreen;
        [SerializeField] GameSceneDefinition _GameDefinition;
        [SerializeField] DialogueManager _Dialogue;

        public bool HasSpawnDefinition => _GameDefinition || _LobbyPanel;

        public void Bind(HauntedHotelMultiplayer session)
        {
            if (_Dialogue)
                session.BindDialogue(_Dialogue);
            if (_LobbyPanel)
                _LobbyPanel.Bind(session);
            if (_ConnectionScreen)
                _ConnectionScreen.Bind(session);
        }

        public Vector3 SpawnPosition(int index)
        {
            return _GameDefinition ? _GameDefinition.Spawn(index) : new Vector3(index * 2.2f, 1.1f, 2f);
        }

    }
}
