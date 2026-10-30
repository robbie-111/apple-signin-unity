#if UNITY_IOS
using System;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

using AppleAuth.Editor;

namespace Robbie111.AppleSignIn.Editor
{
    public sealed class SignInWithApplePostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => Int32.MaxValue - (1 << 10);

        private PBXProject _pbxProj;
        private string _pbxProjPath;

        private void Initialize(string buildOutputPath)
        {
            this._pbxProj = new PBXProject();
            this._pbxProjPath = PBXProject.GetPBXProjectPath(buildOutputPath);
            this._pbxProj.ReadFromFile(this._pbxProjPath);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            this.Initialize(report.summary.outputPath);

            string mainTargetGuid = this._pbxProj.GetUnityMainTargetGuid();
            string entitlementsPath = this.GetEntitlementsPath(mainTargetGuid);

            var manager = new ProjectCapabilityManager(this._pbxProjPath, entitlementsPath, null, mainTargetGuid);

            manager.AddSignInWithAppleWithCompatibility();
            manager.WriteToFile();
        }

        // Reuse the entitlements file another plugin already set; otherwise create one at the Xcode project root,
        // as Unity's AddCapability example does (same default as the Percent Deeplink package).
        // The path stays relative to the Xcode project root because ProjectCapabilityManager writes it into CODE_SIGN_ENTITLEMENTS as is.
        private string GetEntitlementsPath(string mainTargetGuid)
        {
            var entitlementsPath = this._pbxProj.GetEntitlementFilePathForTarget(mainTargetGuid);
            return string.IsNullOrEmpty(entitlementsPath) ? "Unity-iPhone.entitlements" : entitlementsPath;
        }
    }
}
#endif
