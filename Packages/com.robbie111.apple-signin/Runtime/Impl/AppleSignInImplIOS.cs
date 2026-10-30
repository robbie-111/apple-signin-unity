#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;

namespace Robbie111.AppleSignIn.Impl
{
    // Native Sign in with Apple through AppleAuth (com.lupidan.apple-signin-unity).
    internal sealed class AppleSignInImplIOS : IAppleSignInImpl
    {
        private readonly TimeSpan _timeout;

        private readonly IAppleAuthManager _manager;

        // Cleared only by the native callback, so a new sign-in waits until the native UI has finished.
        private TaskCompletionSource<AppleSignInResult> _pending;

        internal AppleSignInImplIOS(AppleSignInConfiguration configuration)
        {
            _timeout = configuration.Timeout;
            if (!AppleAuthManager.IsCurrentPlatformSupported)
            {
                UnityEngine.Debug.LogError("Native Apple sign-in is not supported on this device or OS version.");
                return;
            }

            _manager = new AppleAuthManager(new PayloadDeserializer());
            AppleSignInHelperObject.EnsureCreated();
            // AppleAuth delivers callbacks only from Update().
            AppleSignInHelperObject.Updated += _manager.Update;
            AppleSignInHelperObject.Destroyed += () => _pending?.TrySetResult(AppleSignInResult.Canceled());
        }

        public async Task<AppleSignInResult> SignInAsync(CancellationToken cancellationToken)
        {
            if (_manager == null)
                return AppleSignInResult.Failure(AppleSignInErrorCode.UnsupportedPlatform,
                    "Native Apple sign-in is unavailable.");
            if (_pending != null)
            {
                UnityEngine.Debug.LogWarning("Sign-in already in progress (native authorization has not finished).");
                return AppleSignInResult.Failure(AppleSignInErrorCode.AlreadyInProgress,
                    "Apple sign-in is already in progress.");
            }

            var id = Guid.NewGuid().ToString("N");
            var state = AppleSignInToken.CreateState(Application.identifier);
            var nonce = AppleSignInToken.CreateNonce();
            var pending = _pending =
                new TaskCompletionSource<AppleSignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityEngine.Debug.Log($"Sign-in started (request={id}, applicationId={Application.identifier}).");
            using (var timeout = new CancellationTokenSource(_timeout))
            using (timeout.Token.Register(() =>
                   {
                       if (pending.TrySetResult(AppleSignInResult.Failure(AppleSignInErrorCode.Timeout,
                               "Apple sign-in timed out.")))
                           UnityEngine.Debug.LogWarning(
                               $"Sign-in timed out (request={id}, timeout={_timeout.TotalSeconds}s).");
                   }))
            using (cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken)))
            {
                // An already-canceled caller token completes pending above; don't show the native UI then.
                if (pending.Task.IsCompleted) _pending = null;
                else
                    try
                    {
                        _manager.LoginWithAppleId(new AppleAuthLoginArgs(LoginOptions.None, nonce, state),
                            credential => Finish(pending, ToResult(id, credential, state, nonce)),
                            error => Finish(pending, ToResult(id, error)));
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"Native LoginWithAppleId threw (request={id}): {e}");
                        Finish(pending,
                            AppleSignInResult.Failure(AppleSignInErrorCode.InternalError,
                                "Could not start Apple sign-in."));
                    }

                return await pending.Task;
            }
        }

        private void Finish(TaskCompletionSource<AppleSignInResult> pending, AppleSignInResult result)
        {
            if (_pending == pending) _pending = null;
            if (!pending.TrySetResult(result))
                UnityEngine.Debug.Log("Native response arrived after the caller stopped waiting; ignored.");
        }

        private static AppleSignInResult ToResult(string id, ICredential credential, string state, string nonce)
        {
            if (!(credential is IAppleIDCredential apple))
            {
                UnityEngine.Debug.LogError(
                    $"Native credential is not an Apple ID credential (request={id}, type={credential?.GetType().Name ?? "null"}).");
                return AppleSignInResult.Failure(AppleSignInErrorCode.InvalidResponse, "Missing Apple credential.");
            }

            if (apple.State != state)
            {
                UnityEngine.Debug.LogError(
                    $"Native state mismatch (request={id}, received length={apple.State?.Length ?? 0}).");
                return AppleSignInResult.Failure(AppleSignInErrorCode.StateMismatch, "Native state mismatch.");
            }

            return AppleSignInToken.Parse(Decode(apple.IdentityToken), Decode(apple.AuthorizationCode), nonce, null);
        }

        private static AppleSignInResult ToResult(string id, IAppleError error)
        {
            if (error != null && error.Code == (int)AuthorizationErrorCode.Canceled)
            {
                UnityEngine.Debug.LogWarning($"Native sign-in canceled by user (request={id}, code={error.Code}).");
                return AppleSignInResult.Canceled();
            }

            UnityEngine.Debug.LogError(error == null
                ? $"Native sign-in failed with no error object (request={id})."
                : $"Native sign-in failed (request={id}, code={error.Code}, domain={error.Domain}, description={error.LocalizedDescription}, reason={error.LocalizedFailureReason}).");
            return AppleSignInResult.Failure(AppleSignInErrorCode.ProviderError, "Native Apple authorization failed.");
        }

        private static string Decode(byte[] bytes) => bytes == null ? "" : Encoding.UTF8.GetString(bytes);
    }
}
#endif