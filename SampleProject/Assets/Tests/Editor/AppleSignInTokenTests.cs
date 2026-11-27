using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Robbie111.AppleSignIn.Impl;
namespace Robbie111.AppleSignIn.Tests.Editor
{
    public class AppleSignInTokenTests
    {
        [Serializable] private class State { public int version; public string applicationId; public string random; }
        [Test] public void StateUsesVersionedBase64UrlAndFreshGuid()
        {
            string first = AppleSignInToken.CreateState("com.robbie.test");
            string second = AppleSignInToken.CreateState("com.robbie.test");
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Does.Not.Contain("="));
            var value = JsonUtility.FromJson<State>(Encoding.UTF8.GetString(AppleSignInToken.Decode(first)));
            Assert.That(value.version, Is.EqualTo(1));
            Assert.That(value.applicationId, Is.EqualTo("com.robbie.test"));
            Assert.That(Guid.TryParseExact(value.random, "N", out _), Is.True);
        }
        [Test] public void NonceIsFresh256BitRandom()
        {
            var nonce = AppleSignInToken.CreateNonce();
            Assert.That(AppleSignInToken.Decode(nonce).Length, Is.EqualTo(32));
            Assert.That(nonce, Is.Not.EqualTo(AppleSignInToken.CreateNonce()));
        }
        [Test] public void DisabledNonceIsNull() => Assert.That(AppleSignInToken.CreateNonce(false), Is.Null);

        private static string Token(string payloadJson) =>
            "eyJhbGciOiJSUzI1NiJ9." + AppleSignInToken.Encode(Encoding.UTF8.GetBytes(payloadJson)) + ".sig";

        [Test] public void ParseSkipsNonceCheckWhenNotRequested()
        {
            var result = AppleSignInToken.Parse(Token("{\"sub\":\"user\",\"aud\":\"app\"}"), "code", null, null);
            Assert.That(result.Status, Is.EqualTo(AppleSignInStatus.Success));
        }
        [Test] public void ParseAcceptsMatchingNonce()
        {
            var result = AppleSignInToken.Parse(Token("{\"sub\":\"user\",\"aud\":\"app\",\"nonce\":\"n1\"}"), "code", "n1", null);
            Assert.That(result.Status, Is.EqualTo(AppleSignInStatus.Success));
        }
        [TestCase("{\"sub\":\"user\",\"aud\":\"app\",\"nonce\":\"other\"}")]
        [TestCase("{\"sub\":\"user\",\"aud\":\"app\"}")]
        public void ParseRejectsMismatchedOrMissingNonce(string payload)
        {
            var result = AppleSignInToken.Parse(Token(payload), "code", "n1", null);
            Assert.That(result.ErrorCode, Is.EqualTo(AppleSignInErrorCode.NonceMismatch));
        }
    }
}
