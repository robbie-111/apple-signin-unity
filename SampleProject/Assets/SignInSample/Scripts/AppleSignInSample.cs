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
            _platformText.text = "플랫폼: " + Application.platform;
            var configured = HasConfiguration();
            _signInButton.interactable = configured;
            Show(configured ? "준비" : "설정 필요", configured ? "Apple 계정으로 로그인하세요." :
                "Inspector에서 Client Id와 HTTPS Redirect Uri를 설정한 뒤 다시 실행하세요.", false);
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
            Show("로그인 중", "인증 화면에서 로그인한 뒤 게임으로 돌아오세요.", false);
            try
            {
                if (AppleSignIn.Configuration == null)
                    AppleSignIn.Configuration = new AppleSignInConfiguration { ClientId = _clientId, RedirectUri = _redirectUri };
                var result = await AppleSignIn.DefaultInstance.SignInAsync(attempt.Token);
                if (!this || !isActiveAndEnabled) return;
                if (result.IsSuccess)
                    Show("성공", "UserId: " + result.UserId + "\nAudience: " + result.Audience +
                        "\nIdToken: 수신됨\nAuthorizationCode: " +
                        (string.IsNullOrEmpty(result.AuthorizationCode) ? "없음" : "수신됨"), false);
                else if (result.Status == AppleSignInStatus.Canceled)
                    Show("취소", "로그인이 취소되었습니다.", false);
                else Show("실패", "오류: " + result.ErrorCode + "\n" + result.ErrorMessage, true);
            }
            catch (OperationCanceledException)
            { if (this && isActiveAndEnabled) Show("취소", "로그인 대기를 취소했습니다.", false); }
            catch (Exception e)
            { if (this && isActiveAndEnabled) Show("실패", e is ArgumentException ? "Client Id와 Redirect Uri 설정을 확인하세요." : "로그인을 시작할 수 없습니다: " + e.GetType().Name, true); }
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
            _statusText.text = "상태: " + status;
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
