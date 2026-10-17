using System.Runtime.InteropServices;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // References HotelPointerLock.jslib so the WebGL linker keeps its rejected-pointer-lock filter.
    static class HotelBrowserGuards
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int HFPointerLock_Installed();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() => HFPointerLock_Installed();
#endif
    }
}
