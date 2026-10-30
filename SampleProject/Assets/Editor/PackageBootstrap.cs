using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Robbie111.AppleSignIn.SampleEditor
{
    // Explicit developer utility; never runs automatically or ships in a player.
    public static class PackageBootstrap
    {
        private static AddAndRemoveRequest _request;
        private static double _deadline;
        public static void Install()
        {
            _request = Client.AddAndRemove(new[] { "com.unity.ugui@2.0.0", "com.unity.test-framework@2.0.1-exp.2" });
            WaitAndExit();
        }

        public static void RemoveVerificationTools()
        {
            _request = Client.AddAndRemove(Array.Empty<string>(), new[] { "com.unity.pipeline" });
            WaitAndExit();
        }

        private static void WaitAndExit()
        {
            _deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!_request.IsCompleted && EditorApplication.timeSinceStartup < _deadline) return;
            EditorApplication.update -= Poll;
            if (!_request.IsCompleted || _request.Status != StatusCode.Success)
            {
                UnityEngine.Debug.LogError(_request.IsCompleted ? _request.Error.message : "Package resolution timed out.");
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }
    }
}
