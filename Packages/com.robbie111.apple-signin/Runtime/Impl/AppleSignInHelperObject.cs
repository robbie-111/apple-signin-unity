using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Robbie111.AppleSignIn.Impl
{
    // Hidden scene object: forwards Update/pause/destroy events and receives native UnitySendMessage callbacks.
    [Preserve] internal sealed class AppleSignInHelperObject : MonoBehaviour
    {
        internal const string ObjectName = "AppleSignIn.Callbacks";
        internal static event Action Updated;
        internal static event Action<bool> Paused;
        internal static event Action<string> CallbackReceived;
        internal static event Action Destroyed;
        private static AppleSignInHelperObject _instance;

        internal static void EnsureCreated()
        {
            if (_instance) return;
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _instance = go.AddComponent<AppleSignInHelperObject>();
        }

        private void Update() => Updated?.Invoke();
        private void OnApplicationPause(bool paused) => Paused?.Invoke(paused);
        [Preserve] public void OnAppleSignInCallback(string json) => CallbackReceived?.Invoke(json);
        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Destroyed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Updated = null; Paused = null; CallbackReceived = null; Destroyed = null; _instance = null; }
    }
}
