using System;
using NUnit.Framework;
namespace Robbie111.AppleSignIn.Tests.Editor
{
    public class AppleSignInConfigurationTests
    {
        [Test] public void ValidBrowserSettingsPass() => Assert.DoesNotThrow(() =>
            new AppleSignInConfiguration { ClientId = "service", RedirectUri = "https://example.com/callback" }.ValidateForBrowser());
        [Test] public void BrowserRequiresClientId() => Assert.Throws<ArgumentException>(() =>
            new AppleSignInConfiguration().ValidateForBrowser());
        [TestCase("http://example.com")] [TestCase("invalid")] [TestCase("https://user@example.com")] [TestCase("https://example.com/#token")]
        public void InvalidRedirectIsRejected(string redirect) => Assert.Throws<ArgumentException>(() =>
            new AppleSignInConfiguration { ClientId = "service", RedirectUri = redirect }.ValidateForBrowser());
    }
}
