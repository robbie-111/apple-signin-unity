using System;
using System.Threading;
using UnityEngine;

namespace Robbie111.AppleSignIn.Samples
{
    public sealed class AppleSignInSample : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _signInButton;
        [SerializeField] private UnityEngine.UI.Text _platformText;
        [SerializeField] private UnityEngine.UI.Text _statusText;
        [SerializeField] private UnityEngine.UI.Text _detailText;
        [SerializeField] private string _clientId = "";
        [SerializeField] private string _redirectUri = "";
        private CancellationTokenSource _lifetime;
        private CancellationTokenSource _attempt;

        private void OnEnable()
        {
            _lifetime = new CancellationTokenSource();
            _signInButton.onClick.AddListener(SignIn);
            _platformText.text = "Platform: " + Application.platform;
            var configured = HasConfiguration();
            _signInButton.interactable = configured;
            Show(configured ? "Ready" : "Configuration required", configured ? "Sign in with your Apple account." :
                "Set Client Id and HTTPS Redirect Uri in the Inspector, then run again.", false);
        }

        private bool HasConfiguration()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return true;
#else
            try { new AppleSignInConfiguration { ClientId = _clientId, RedirectUri = _redirectUri }.SnapshotForSample(); return true; }
            catch (ArgumentException) { return false; }
#endif
        }

        public async void SignIn()
        {
            if (_attempt != null || _lifetime == null) return;
            var attempt = _attempt = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _signInButton.interactable = false;
            Show("Signing in...", "Complete sign-in, then return to the game.", false);
            try
            {
                if (AppleSignIn.Configuration == null)
                    AppleSignIn.Configuration = new AppleSignInConfiguration { ClientId = _clientId, RedirectUri = _redirectUri };
                var result = await AppleSignIn.DefaultInstance.SignInAsync(attempt.Token);
                if (!this || !isActiveAndEnabled) return;
                if (result.IsSuccess)
                    Show("Success", "UserId: " + result.UserId + "\nAudience: " + result.Audience +
                        "\nIdToken: received\nAuthorizationCode: " +
                        (string.IsNullOrEmpty(result.AuthorizationCode) ? "none" : "received"), false);
                else if (result.Status == AppleSignInStatus.Canceled)
                    Show("Canceled", "Sign-in was canceled.", false);
                else Show("Failed", "Error: " + result.ErrorCode + "\n" + result.ErrorMessage, true);
            }
            catch (OperationCanceledException)
            { if (this && isActiveAndEnabled) Show("Canceled", "Stopped waiting for sign-in.", false); }
            catch (Exception e)
            { if (this && isActiveAndEnabled) Show("Failed", e is ArgumentException ? "Check the Client Id and Redirect Uri settings." : "Could not start sign-in: " + e.GetType().Name, true); }
            finally
            {
                if (_attempt == attempt)
                {
                    _attempt = null;
                    if (this && isActiveAndEnabled)
                        _signInButton.interactable = HasConfiguration();
                }
                attempt.Dispose();
            }
        }

        private void Show(string status, string detail, bool error)
        {
            _statusText.text = "Status: " + status;
            _statusText.color = error ? new Color32(0xFF, 0x3B, 0x30, 0xFF) : new Color32(0x1D, 0x1D, 0x1F, 0xFF);
            _detailText.text = detail;
        }

        private void OnDisable()
        {
            _signInButton.onClick.RemoveListener(SignIn);
            _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null;
        }
    }

    internal static class SampleConfigurationValidation
    {
        // Keep sample validation public-API-only so it can be copied into another project.
        internal static void SnapshotForSample(this AppleSignInConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.ClientId) ||
                !Uri.TryCreate(configuration.RedirectUri, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Invalid browser configuration.");
        }
    }
}
