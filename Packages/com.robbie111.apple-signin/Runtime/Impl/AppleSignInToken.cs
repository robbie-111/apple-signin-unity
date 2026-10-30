using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Robbie111.AppleSignIn.Impl
{
    // state/nonce generation and identity-token claim inspection.
    // Claim inspection only. The game server MUST verify the JWT signature and claims.
    internal static class AppleSignInToken
    {
        [Serializable]
        private sealed class StatePayload
        {
            public int version = 1;
            public string applicationId;
            public string random;
        }

        // Editor/desktop: the RedirectServer sends the result to http://127.0.0.1:{port}/callback.
        [Serializable]
        private sealed class LoopbackStatePayload
        {
            public int version = 1;
            public string applicationId;
            public string random;
            public int port;
        }

        [Serializable]
        private sealed class Claims
        {
            public string sub;
            public string aud;
            public string nonce;
        }

        internal static string CreateState(string applicationId) => Encode(Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(
                new StatePayload { applicationId = applicationId, random = Guid.NewGuid().ToString("N") })));

        internal static string CreateState(string applicationId, int port) => Encode(Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(new LoopbackStatePayload
                { applicationId = applicationId, random = Guid.NewGuid().ToString("N"), port = port })));

        internal static string CreateNonce()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Encode(bytes);
        }

        internal static string Encode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        internal static byte[] Decode(string value)
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
        }

        internal static AppleSignInResult Parse(string token, string code, string expectedNonce,
            string expectedAudience)
        {
            string step = "length";
            try
            {
                if (string.IsNullOrEmpty(token) || token.Length > 32768)
                    throw new FormatException(
                        $"Identity token length is {token?.Length ?? 0}; expected 1..32768 characters.");
                step = "segments";
                var parts = token.Split('.');
                if (parts.Length != 3 || parts[0].Length == 0 || parts[2].Length == 0)
                    throw new FormatException(
                        $"Identity token has {parts.Length} segment(s); expected header.payload.signature.");
                step = "payload decode";
                var json = Encoding.UTF8.GetString(Decode(parts[1]));
                step = "claims";
                var claims = JsonUtility.FromJson<Claims>(json);
                if (claims == null || string.IsNullOrEmpty(claims.sub) || string.IsNullOrEmpty(claims.aud))
                    throw new FormatException(
                        $"Identity token claims missing (sub length={claims?.sub?.Length ?? 0}, aud length={claims?.aud?.Length ?? 0}).");
                if (claims.nonce != expectedNonce)
                {
                    UnityEngine.Debug.LogError(
                        $"Identity token nonce mismatch (token nonce length={claims.nonce?.Length ?? 0}).");
                    return AppleSignInResult.Failure(AppleSignInErrorCode.NonceMismatch, "Token nonce mismatch.");
                }

                if (!string.IsNullOrEmpty(expectedAudience) && claims.aud != expectedAudience)
                {
                    UnityEngine.Debug.LogError(
                        $"Identity token audience mismatch (expected={expectedAudience}, actual={claims.aud}). Check AppleSignInConfiguration.ClientId.");
                    return AppleSignInResult.Failure(AppleSignInErrorCode.InvalidResponse, "Token audience mismatch.");
                }

                UnityEngine.Debug.Log(
                    $"Identity token claims accepted (aud={claims.aud}, code length={code?.Length ?? 0}).");
                return AppleSignInResult.Success(claims.sub, token, code, claims.aud);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Malformed identity token at step '{step}': {e.GetType().Name}: {e.Message}");
                return AppleSignInResult.Failure(AppleSignInErrorCode.InvalidResponse, "Malformed identity token.");
            }
        }
    }

    // Redirect callback payload (state/id_token/code/error) delivered by the browser-based flow.
    [Serializable]
    internal sealed class AppleSignInCallback
    {
        public string requestId;
        public string state;
        public string id_token;
        public string code;
        public string error;
        public string nativeError;

        internal AppleSignInResult ToResult(string id, string expectedState, string expectedNonce, string audience)
        {
            UnityEngine.Debug.Log(
                $"Callback received (request={id}, state length={state?.Length ?? 0}, id_token length={id_token?.Length ?? 0}, " +
                $"code length={code?.Length ?? 0}, error={(string.IsNullOrEmpty(error) ? "-" : error)}, nativeError={(string.IsNullOrEmpty(nativeError) ? "-" : nativeError)}).");
            if (!string.IsNullOrEmpty(nativeError))
            {
                if (!Enum.TryParse(nativeError, out AppleSignInErrorCode errorCode) ||
                    errorCode == AppleSignInErrorCode.None)
                {
                    UnityEngine.Debug.LogError(
                        $"Unknown Android native error '{nativeError}'; reporting InternalError (request={id}).");
                    errorCode = AppleSignInErrorCode.InternalError;
                }

                UnityEngine.Debug.LogError(
                    $"Android sign-in failed natively (request={id}, error={errorCode}). See logcat tag AppleSignIn for details.");
                return AppleSignInResult.Failure(errorCode, "Android sign-in failed: " + errorCode);
            }

            if (string.IsNullOrEmpty(state) || state != expectedState)
            {
                UnityEngine.Debug.LogError($"Callback state mismatch (request={id}, received length={state?.Length ?? 0}).");
                return AppleSignInResult.Failure(AppleSignInErrorCode.StateMismatch, "Callback state mismatch.");
            }

            if (!string.IsNullOrEmpty(error))
            {
                if (error == "user_cancelled_authorize" || error == "popup_closed_by_user" || error == "access_denied")
                {
                    UnityEngine.Debug.LogWarning($"Apple reported user cancellation (request={id}, error={error}).");
                    return AppleSignInResult.Canceled();
                }

                UnityEngine.Debug.LogError($"Apple authorization returned an error (request={id}, error={error}).");
                return AppleSignInResult.Failure(AppleSignInErrorCode.ProviderError, "Apple authorization failed.");
            }

            return AppleSignInToken.Parse(id_token, code, expectedNonce, audience);
        }

        internal static AppleSignInCallback Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                UnityEngine.Debug.LogError("Callback payload is empty.");
                return null;
            }

            if (json.Length > 65536)
            {
                UnityEngine.Debug.LogError($"Callback payload is too large (length={json.Length}).");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<AppleSignInCallback>(json);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(
                    $"Could not parse callback payload (length={json.Length}): {e.GetType().Name}: {e.Message}");
                return null;
            }
        }
    }
}