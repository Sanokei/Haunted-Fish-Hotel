using Mirage;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Attach alongside existing world scripts; those scripts check CanSimulate.
    // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
    [RequireComponent(typeof(NetworkIdentity))]
    // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
    public sealed class NetworkWorldObject : NetworkBehaviour
    {
        // This opt-in setting decides whether the object participates in shared network authority or existing local behavior.
        public bool Networked = true;
        // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
        public GameObject Visual;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] Vector3 position;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] Quaternion rotation;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] bool visible = true;
        // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
        public bool CanSimulate => !Networked || !Identity.IsSpawned || IsServer;
        // Resolve the authored dependencies early; the scene and prefab data determine what exists.
        void Awake()
        {
            // Keep this small operation on the existing component so callers share one state transition.
            Identity.OnStartServer.AddListener(() => { position=transform.position; rotation=transform.rotation; });
            // Keep this small operation on the existing component so callers share one state transition.
            Identity.OnStartClient.AddListener(() => {
                // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
                if (!IsServer) { transform.SetPositionAndRotation(position, rotation); ApplyVisibility(); }
            });
        }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        void LateUpdate()
        {
            // This opt-in setting decides whether the object participates in shared network authority or existing local behavior.
            if (!Networked || !Identity.IsSpawned) return;
            // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
            if (IsServer) { position = transform.position; rotation = transform.rotation; }
            else
            {
                // Interpolate toward the received authoritative position to hide network update steps on observing clients.
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, 1-Mathf.Exp(-18*Time.deltaTime)),
                    // Smooth visible facing between authoritative rotations without giving the observer simulation authority.
                    Quaternion.Slerp(transform.rotation, rotation, 1-Mathf.Exp(-18*Time.deltaTime)));
            }
            // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
            ApplyVisibility();
        }
        // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
        void ApplyVisibility() { if (Visual && Visual != gameObject) Visual.SetActive(visible); }
        // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
        public void SetVisible(bool value)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!CanSimulate) return;
            // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
            visible = value;
            // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
            ApplyVisibility();
        }
        // Shared world simulation belongs to the server; observers apply synchronized pose and visibility without simulating a second world.
        public void Despawn()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!CanSimulate) return;
            // Clean up this existing object or duplicate so obsolete presentation/session state does not survive into the next session.
            if (Networked && Identity.IsSpawned) ServerObjectManager.Destroy(gameObject);
            // Clean up this existing object or duplicate so obsolete presentation/session state does not survive into the next session.
            else Destroy(gameObject);
        }
    }
}

