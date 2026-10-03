using Mirage;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Attach alongside existing world scripts; those scripts check CanSimulate.
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class NetworkWorldObject : NetworkBehaviour
    {
        public bool Networked = true;
        public GameObject Visual;
        [SyncVar] Vector3 position;
        [SyncVar] Quaternion rotation;
        [SyncVar] bool visible = true;
        public bool CanSimulate => !Networked || !Identity.IsSpawned || IsServer;
        void Awake()
        {
            Identity.OnStartServer.AddListener(() => { position=transform.position; rotation=transform.rotation; });
            Identity.OnStartClient.AddListener(() => {
                if (!IsServer) { transform.SetPositionAndRotation(position, rotation); ApplyVisibility(); }
            });
        }
        void LateUpdate()
        {
            if (!Networked || !Identity.IsSpawned) return;
            if (IsServer) { position = transform.position; rotation = transform.rotation; }
            else
            {
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, 1-Mathf.Exp(-18*Time.deltaTime)),
                    Quaternion.Slerp(transform.rotation, rotation, 1-Mathf.Exp(-18*Time.deltaTime)));
            }
            ApplyVisibility();
        }
        void ApplyVisibility() { if (Visual && Visual != gameObject) Visual.SetActive(visible); }
        public void SetVisible(bool value)
        {
            if (!CanSimulate) return;
            visible = value;
            ApplyVisibility();
        }
        public void Despawn()
        {
            if (!CanSimulate) return;
            if (Networked && Identity.IsSpawned) ServerObjectManager.Destroy(gameObject);
            else Destroy(gameObject);
        }
    }
}

