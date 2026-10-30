using System;
using System.IO;
using System.Linq;
using Robbie111.AppleSignIn.Samples;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Robbie111.AppleSignIn.SampleEditor
{
    public static class AppleSignInSampleTools
    {
        private const string ScenePath = "Assets/SignInSample/Scenes/SampleScene.unity";
        private const string PackagePath = "Packages/com.robbie111.apple-signin";

        [MenuItem("Tools/Apple Sign-In/Create Sample UI")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (scene.GetRootGameObjects().Any(go => go.name == "AppleSignInSample"))
                throw new InvalidOperationException("The sample UI already exists.");
            var root = new GameObject("AppleSignInSample", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            var backdrop = Rect("Background", root.transform);
            Stretch(backdrop);
            backdrop.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.black;
            var card = Rect("Card", root.transform);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(880, 0);
            card.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var layout = card.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(64, 64, 64, 64); layout.spacing = 28;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            card.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            Label("Title", card, "Apple Sign-In", 64, -1);
            var platform = Label("Platform", card, "플랫폼: 실행 시 표시", 32, -1);
            platform.color = new Color32(0x6E, 0x6E, 0x73, 0xFF);
            var signIn = Button("SignInButton", card, "Sign in with Apple", Color.black, Color.white);
            var divider = Rect("Divider", card);
            divider.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(0xE5, 0xE5, 0xEA, 0xFF);
            divider.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 2;
            var status = Label("Status", card, "상태: 준비", 40, -1);
            var detail = Label("Detail", card, "Inspector에서 브라우저 로그인 설정을 입력하세요.", 30, -1);
            detail.color = new Color32(0x6E, 0x6E, 0x73, 0xFF);
            var controller = root.AddComponent<AppleSignInSample>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_signInButton").objectReferenceValue = signIn;
            serialized.FindProperty("_platformText").objectReferenceValue = platform;
            serialized.FindProperty("_statusText").objectReferenceValue = status;
            serialized.FindProperty("_detailText").objectReferenceValue = detail;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!UnityEngine.Object.FindFirstObjectByType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.Text Label(string name, Transform parent, string text, int size, float height)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
#if UNITY_2022_2_OR_NEWER
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            label.text = text; label.fontSize = size; label.color = new Color32(0x1D, 0x1D, 0x1F, 0xFF);
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            if (height >= 0) rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return label;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string text, Color background, Color foreground)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = background;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 140;
            var label = Label("Label", rect, text, 44, -1); Stretch(label.rectTransform);
            label.alignment = TextAnchor.MiddleCenter; label.color = foreground;
            return button;
        }

        [MenuItem("Tools/Apple Sign-In/Configure Android Templates")]
        public static void ConfigureAndroidTemplates()
        {
            var settings = Resources.FindObjectsOfTypeAll<PlayerSettings>().First();
            var serialized = new SerializedObject(settings);
            foreach (var name in new[] { "useCustomMainGradleTemplate", "useCustomGradlePropertiesTemplate", "useCustomGradleSettingsTemplate" })
            {
                var property = serialized.FindProperty(name);
                if (property == null) throw new InvalidOperationException("Missing PlayerSettings property: " + name);
                property.boolValue = true;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-buildOutput");
            if (index < 0 || index + 1 == args.Length) throw new ArgumentException("-buildOutput is required.");
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            PlayerSettings.SetApplicationIdentifier(group, "com.example.applesignin.sample");
            if (target == BuildTarget.Android)
            {
                ConfigureAndroidTemplates();
                PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
                PlayerSettings.Android.minifyRelease = true;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes = new[] { ScenePath }, locationPathName = args[index + 1], target = target, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Sample build failed: " + report.summary.result);
        }
    }
}
