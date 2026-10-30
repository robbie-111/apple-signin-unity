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
    }
}
