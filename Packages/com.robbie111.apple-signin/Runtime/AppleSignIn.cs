using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Robbie111.AppleSignIn.Impl;

[assembly: InternalsVisibleTo("Robbie111.AppleSignIn.Tests.Editor")]

namespace Robbie111.AppleSignIn
{
    public sealed class AppleSignIn
    {
        private static AppleSignIn _instance;
        private static AppleSignInConfiguration _configuration;
        private readonly IAppleSignInImpl _impl;

        public static AppleSignInConfiguration Configuration
        {
            get => _configuration;
            set
            {
                if (_instance != null)
                {
                    UnityEngine.Debug.LogError("Configuration was set after DefaultInstance was created; set it before first access.");
                    throw new InvalidOperationException("Set Configuration before accessing DefaultInstance.");
                }
                if (value == null)
                {
                    UnityEngine.Debug.LogError("Configuration cannot be null.");
                    throw new ArgumentNullException(nameof(value));
                }
                _configuration = value;
            }
        }

        public static AppleSignIn DefaultInstance
        {
            get
            {
                if (_instance != null) return _instance;
                var configuration = _configuration ?? new AppleSignInConfiguration();
                AppleSignIn instance;
                try
                {
                    if (configuration.Timeout <= TimeSpan.Zero || configuration.Timeout > TimeSpan.FromHours(1))
                        throw new ArgumentOutOfRangeException(nameof(configuration.Timeout), "Timeout must be between zero and one hour.");
                    // Each impl validates and copies the values it uses.
                    instance = new AppleSignIn(configuration);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Invalid Apple sign-in configuration (platform={Application.platform}, configured={_configuration != null}): {e.GetType().Name}: {e.Message}");
                    throw;
                }
                _configuration = configuration;
                UnityEngine.Debug.Log($"Created (platform={Application.platform}, impl={instance._impl.GetType().Name}, " +
                    $"clientId={(string.IsNullOrEmpty(configuration.ClientId) ? "-" : configuration.ClientId)}, redirectUri={configuration.RedirectUri}, timeout={configuration.Timeout.TotalSeconds}s)");
                return _instance = instance;
            }
        }

        private AppleSignIn(AppleSignInConfiguration configuration)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            _impl = new AppleSignInImplEditor(configuration);
#elif UNITY_ANDROID
            _impl = new AppleSignInImplAndroid(configuration);
#elif UNITY_IOS
            _impl = new AppleSignInImplIOS(configuration);
#else
            UnityEngine.Debug.LogError($"Apple sign-in is not supported on {Application.platform}.");
            throw new InvalidOperationException("Apple sign-in is not supported on this platform.");
#endif
        }

        /// <summary>Call on Unity's main thread. Success returns credentials, not a game-server session.</summary>
        public async Task<AppleSignInResult> SignInAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _impl.SignInAsync(cancellationToken);
                if (result.IsSuccess)
                    UnityEngine.Debug.Log($"Sign-in finished (status={result.Status}, audience={result.Audience}, code length={result.AuthorizationCode?.Length ?? 0}).");
                else if (result.Status == AppleSignInStatus.Canceled)
                    UnityEngine.Debug.LogWarning($"Sign-in finished (status={result.Status}, message={result.ErrorMessage}).");
                else
                    UnityEngine.Debug.LogError($"Sign-in finished (status={result.Status}, error={result.ErrorCode}, message={result.ErrorMessage}).");
                return result;
            }
            catch (OperationCanceledException)
            {
                UnityEngine.Debug.Log("Sign-in canceled by caller token.");
                throw;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _configuration = null; }
    }

    internal interface IAppleSignInImpl
    {
        Task<AppleSignInResult> SignInAsync(CancellationToken cancellationToken);
    }
}
