#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Robbie111.AppleSignIn.Impl
{
    // Custom Tabs sign-in through the Java AppleSignInBridge; the result returns via UnitySendMessage.
    internal sealed class AppleSignInImplAndroid : IAppleSignInImpl
    {
        private const int ReturnGraceMilliseconds = 500;
        private readonly string _clientId, _redirectUri;
        private readonly TimeSpan _timeout;
        private readonly bool _useNonce;
        private TaskCompletionSource<AppleSignInResult> _pending;
        private string _requestId, _state, _nonce;
        private bool _leftApp;
        private int _pauseCount;

        internal AppleSignInImplAndroid(AppleSignInConfiguration configuration)
        {
            configuration.ValidateForBrowser();
            _clientId = configuration.ClientId;
            _redirectUri = configuration.RedirectUri;
            _timeout = configuration.Timeout;
            _useNonce = configuration.UseNonce;
            AppleSignInHelperObject.EnsureCreated();
            AppleSignInHelperObject.Paused += OnPause;
            AppleSignInHelperObject.CallbackReceived += OnCallback;
            AppleSignInHelperObject.Destroyed += OnDestroyed;
        }

        public async Task<AppleSignInResult> SignInAsync(CancellationToken cancellationToken)
        {
            if (_pending != null)
                return AppleSignInResult.Failure(AppleSignInErrorCode.AlreadyInProgress,
                    "Apple sign-in is already in progress.");

            var id = _requestId = Guid.NewGuid().ToString("N");
            _state = AppleSignInToken.CreateState(Application.identifier);
            _nonce = AppleSignInToken.CreateNonce(_useNonce);
            _leftApp = false;
            var pending = _pending =
                new TaskCompletionSource<AppleSignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var timeout = new CancellationTokenSource(_timeout))
            using (timeout.Token.Register(() => pending.TrySetResult(
                       AppleSignInResult.Failure(AppleSignInErrorCode.Timeout, "Apple sign-in timed out."))))
            using (cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken)))
            {
                try
                {
                    // An already-canceled caller token completes pending above; don't open the browser then.
                    if (!pending.Task.IsCompleted)
                        using (var bridge = Bridge())
                            bridge.CallStatic("signIn", id, _clientId, _redirectUri, _state, _nonce,
                                AppleSignInHelperObject.ObjectName);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Java signIn threw (request={id}): {e}");
                    pending.TrySetResult(AppleSignInResult.Failure(AppleSignInErrorCode.InternalError,
                        "Could not start Apple sign-in."));
                }

                try
                {
                    return await pending.Task;
                }
                finally
                {
                    _pending = null;
                    _requestId = null;
                    try
                    {
                        using (var bridge = Bridge()) bridge.CallStatic("cancel", id);
                    }
                    catch (Exception)
                    {
                        // Unity/Android may already be shutting down.
                    }
                }
            }
        }

        // Returning from the browser without a callback means the user backed out.
        private async void OnPause(bool paused)
        {
            if (_pending == null) return;
            var id = _requestId;
            var count = ++_pauseCount;
            if (paused)
            {
                _leftApp = true;
                return;
            }

            if (!_leftApp) return;
            await Task.Delay(ReturnGraceMilliseconds);
            if (_requestId != id || _pauseCount != count) return;
            try
            {
                using (var bridge = Bridge())
                    if (bridge.CallStatic<bool>("hasCallback", id))
                        return;

                UnityEngine.Debug.LogWarning(
                    $"Returned to the app without a callback; completing as canceled (request={id}). " +
                    "If Apple sign-in was finished in the browser, check that RedirectServer redirects to the app callback URI.");
                _pending?.TrySetResult(AppleSignInResult.Canceled());
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Could not query Java callback state (request={id}): {e}");
                _pending?.TrySetResult(AppleSignInResult.Failure(AppleSignInErrorCode.InternalError,
                    "Could not query Android sign-in state."));
            }
        }

        private void OnCallback(string json)
        {
            var callback = AppleSignInCallback.Parse(json);
            if (callback == null)
            {
                UnityEngine.Debug.LogError($"Ignored unreadable callback payload (length={json?.Length ?? 0}).");
                return;
            }

            if (_pending == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"Ignored callback with no sign-in in progress (callback={callback.requestId}).");
                return;
            }

            if (callback.requestId != _requestId)
            {
                UnityEngine.Debug.LogWarning(
                    $"Ignored callback for another request (callback={callback.requestId}, current={_requestId}).");
                return;
            }

            _pending.TrySetResult(callback.ToResult(_requestId, _state, _nonce, _clientId));
        }

        private void OnDestroyed()
        {
            _pending?.TrySetResult(AppleSignInResult.Canceled());
        }

        private static AndroidJavaClass Bridge() => new AndroidJavaClass("com.robbie.applesignin.AppleSignInBridge");
    }
}
#endif