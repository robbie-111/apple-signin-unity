using System;

namespace Robbie111.AppleSignIn
{
    public sealed class AppleSignInConfiguration
    {
        // Services ID from Apple Developer (e.g. com.example.game.service), not the app's bundle ID.
        // Apple Developer의 Services ID (예: com.example.game.service). 번들 ID(App ID)가 아니다.
        public string ClientId { get; set; }
        public string RedirectUri { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(180);

        // Browser-based flows (Android, Editor/desktop) send ClientId and RedirectUri to Apple.
        internal void ValidateForBrowser()
        {
            if (string.IsNullOrWhiteSpace(ClientId)) throw new ArgumentException("ClientId is required.");
            if (!Uri.TryCreate(RedirectUri, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.Fragment) ||
                !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException(
                    "RedirectUri must be an absolute HTTPS URL without user info or fragment.");
        }
    }
}