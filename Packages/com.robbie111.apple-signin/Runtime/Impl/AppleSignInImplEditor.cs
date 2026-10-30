#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Robbie111.AppleSignIn.Impl
{
    // Editor and desktop players: system browser + loopback HttpListener.
    // The RedirectServer login page ({RedirectUri sibling}/apple-auth-login) runs the Apple JS SDK in popup mode,
    // which reports success, cancel and popup close, then navigates to http://127.0.0.1:{port}/callback
    // using the port carried in state. (A plain authorize redirect never reports a cancel.)
    internal sealed class AppleSignInImplEditor : IAppleSignInImpl
    {
        private const string CallbackPath = "/callback";
        private readonly string _clientId, _redirectUri;
        private readonly TimeSpan _timeout;
        private TaskCompletionSource<AppleSignInResult> _pending;
        private string _requestId;

        internal AppleSignInImplEditor(AppleSignInConfiguration configuration)
        {
            configuration.ValidateForBrowser();
            _clientId = configuration.ClientId;
            _redirectUri = configuration.RedirectUri;
            _timeout = configuration.Timeout;
            AppleSignInHelperObject.EnsureCreated();
            AppleSignInHelperObject.Destroyed += Cancel;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
#endif
        }

        public async Task<AppleSignInResult> SignInAsync(CancellationToken cancellationToken)
        {
            if (_pending != null)
            {
                UnityEngine.Debug.LogWarning($"Sign-in already in progress (request={_requestId}).");
                return AppleSignInResult.Failure(AppleSignInErrorCode.AlreadyInProgress,
                    "Apple sign-in is already in progress.");
            }

            var id = _requestId = Guid.NewGuid().ToString("N");
            var listener = new HttpListener();
            int port;
            try
            {
                port = FreeLoopbackPort();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Could not start the loopback listener (request={id}): {e}");
                listener.Close();
                _requestId = null;
                return AppleSignInResult.Failure(AppleSignInErrorCode.InternalError,
                    "Could not start the local callback listener.");
            }

            var state = AppleSignInToken.CreateState(Application.identifier, port);
            var nonce = AppleSignInToken.CreateNonce();
            var pending = _pending =
                new TaskCompletionSource<AppleSignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityEngine.Debug.Log($"Sign-in started (request={id}, applicationId={Application.identifier}, " +
                                 $"callback=http://127.0.0.1:{port}{CallbackPath}).");
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
                try
                {
                    // An already-canceled caller token completes pending above; don't open the browser then.
                    if (!pending.Task.IsCompleted)
                    {
                        _ = ReceiveAsync(listener, id, state, nonce, _clientId, pending);
                        Application.OpenURL(LoginPageUrl(state, nonce));
                        UnityEngine.Debug.Log($"Opened the system browser (request={id}).");
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Could not open the system browser (request={id}): {e}");
                    pending.TrySetResult(AppleSignInResult.Failure(AppleSignInErrorCode.BrowserUnavailable,
                        "Could not open the system browser."));
                }

                try
                {
                    return await pending.Task;
                }
                finally
                {
                    if (_pending == pending)
                    {
                        _pending = null;
                        _requestId = null;
                    }

                    listener.Close();
                    UnityEngine.Debug.Log($"Loopback listener closed (request={id}).");
                }
            }
        }

        // Runs off the main thread so the browser gets its reply even while an unfocused Editor is paused.
        // Completing pending resumes SignInAsync on the Unity SynchronizationContext. Ends when the listener closes.
        private static async Task ReceiveAsync(HttpListener listener, string id, string state, string nonce,
            string clientId, TaskCompletionSource<AppleSignInResult> pending)
        {
            while (!pending.Task.IsCompleted)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is ObjectDisposedException || e is HttpListenerException ||
                                          e is InvalidOperationException)
                {
                    return; // Listener closed after completion, timeout or cancellation.
                }

                try
                {
                    var request = context.Request;
                    var remote = request.RemoteEndPoint?.Address;
                    if (remote == null || !IPAddress.IsLoopback(remote))
                    {
                        UnityEngine.Debug.LogWarning($"Rejected non-loopback request (request={id}, remote={remote}).");
                        Reply(context, 403, "Forbidden");
                        continue;
                    }

                    if (request.HttpMethod != "GET" || request.Url.AbsolutePath != CallbackPath)
                    {
                        UnityEngine.Debug.Log(
                            $"Ignored request (request={id}, method={request.HttpMethod}, path={request.Url.AbsolutePath}).");
                        Reply(context, 404, "Not found");
                        continue;
                    }

                    var query = request.QueryString;
                    foreach (var key in new[] { "state", "id_token", "code", "error" })
                    {
                        if (query.GetValues(key)?.Length > 1)
                        {
                            UnityEngine.Debug.LogError($"Callback rejected: duplicate '{key}' parameter (request={id}).");
                            Reply(context, 400, ResultPage(false));
                            pending.TrySetResult(AppleSignInResult.Failure(AppleSignInErrorCode.InvalidResponse,
                                "Duplicate callback parameter."));
                            return;
                        }
                    }

                    var callback = new AppleSignInCallback
                    {
                        state = query["state"], id_token = query["id_token"], code = query["code"],
                        error = query["error"]
                    };
                    var result = callback.ToResult(id, state, nonce, clientId);
                    Reply(context, 200, ResultPage(result.IsSuccess));
                    pending.TrySetResult(result);
                    return;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Could not handle the loopback request (request={id}): {e}");
                    try { Reply(context, 500, "Internal error"); }
                    catch (Exception) { /* Browser may have disconnected. */ }
                }
            }
        }

        private void Cancel()
        {
            if (_pending == null) return;
            UnityEngine.Debug.LogWarning($"Shutting down with sign-in in progress; completing as canceled (request={_requestId}).");
            _pending.TrySetResult(AppleSignInResult.Canceled());
        }

        // Convention: the login page is the RedirectUri's sibling path, e.g. .../apple-auth-redirect -> .../apple-auth-login.
        private string LoginPageUrl(string state, string nonce)
        {
            var redirect = new Uri(_redirectUri);
            var path = redirect.AbsolutePath;
            var loginPath = path.Substring(0, path.LastIndexOf('/') + 1) + "apple-auth-login";
            return redirect.GetLeftPart(UriPartial.Authority) + loginPath +
                   "?client_id=" + Uri.EscapeDataString(_clientId) +
                   "&state=" + Uri.EscapeDataString(state) +
                   "&nonce=" + Uri.EscapeDataString(nonce);
        }

        private static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
            finally { probe.Stop(); }
        }

        private static void Reply(HttpListenerContext context, int status, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var response = context.Response;
            response.StatusCode = status;
            response.ContentType = body.StartsWith("<!doctype", StringComparison.Ordinal)
                ? "text/html; charset=utf-8"
                : "text/plain; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["Referrer-Policy"] = "no-referrer";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private static string ResultPage(bool success) =>
            "<!doctype html><html lang='ko'><head><meta charset='utf-8'>" +
            "<meta name='viewport' content='width=device-width, initial-scale=1'><title>Apple 로그인</title>" +
            "<style>body{margin:0;min-height:100vh;display:grid;place-items:center;font-family:system-ui,sans-serif;" +
            "background:#f5f5f7;color:#1d1d1f}main{padding:32px;background:#fff;border-radius:16px;text-align:center}</style>" +
            "</head><body><main><h1>" + (success ? "로그인 완료" : "로그인 실패") + "</h1><p>" +
            (success ? "이 창을 닫고 게임으로 돌아가세요." : "게임으로 돌아가 다시 시도해 주세요.") +
            "</p></main><script>history.replaceState(null,'','/');</script></body></html>";
    }
}
#endif
