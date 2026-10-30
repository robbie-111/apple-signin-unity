using System.Xml;
using System.IO;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
using UnityEditor.Build;

namespace Robbie111.AppleSignIn.Editor
{
    #if UNITY_ANDROID
    public sealed class AppleSignInAndroidBuildProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        public void OnPostGenerateGradleAndroidProject(string path) => AppleSignInAndroidGradle.Apply(path);
    }
    #endif

    internal static class AppleSignInAndroidGradle
    {
        private const string Marker = "// Apple Sign-In dependencies";
        // androidx.browser and android.useAndroidX are provided by EDM4U (AppleSignInDependencies.xml).
        // Only the androidlib module's own Gradle setup is patched here.
        internal static void Apply(string path)
        {
            var libs = Directory.GetDirectories(path, "AppleSignIn.androidlib", SearchOption.AllDirectories);
            if (libs.Length != 1) throw new BuildFailedException("Apple Sign-In Android library was not included exactly once.");
            var libraryGradle = Path.Combine(libs[0], "build.gradle");
            if (!File.Exists(libraryGradle)) throw new BuildFailedException("Missing Apple Sign-In library build.gradle.");
            var manifest = Path.Combine(libs[0], "AndroidManifest.xml");
            var xml = new XmlDocument();
            xml.Load(manifest);
            xml.DocumentElement.RemoveAttribute("package");
            xml.Save(manifest);
            var properties = Path.Combine(Directory.GetParent(path).FullName, "gradle.properties");
            var propertiesText = File.Exists(properties) ? File.ReadAllText(properties) : "";
            if (System.Text.RegularExpressions.Regex.IsMatch(propertiesText, @"(?m)^\s*android.useAndroidX\s*=\s*false\s*$"))
                throw new BuildFailedException("Apple Sign-In requires android.useAndroidX=true.");
            var libraryText = File.ReadAllText(libraryGradle);
            if (!libraryText.Contains(Marker))
                File.AppendAllText(libraryGradle, "\n" + Marker + @"
android {
    namespace 'com.robbie.applesignin'
    defaultConfig { consumerProguardFiles 'consumer-rules.pro' }
    compileOptions {
        sourceCompatibility JavaVersion.VERSION_1_8
        targetCompatibility JavaVersion.VERSION_1_8
    }
}
dependencies {
    // Compile-time only: EDM4U adds the runtime dependency to unityLibrary.
    compileOnly 'androidx.browser:browser:1.5.0'
    compileOnly fileTree(dir: '../libs', include: ['*.jar'])
}
");
        }
    }
}
