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
                    throw new InvalidOperationException("Set Configuration before accessing DefaultInstance.");
                _configuration = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        public static AppleSignIn DefaultInstance
        {
            get
            {
                if (_instance != null) return _instance;
                var configuration = _configuration ?? new AppleSignInConfiguration();
                if (configuration.Timeout <= TimeSpan.Zero || configuration.Timeout > TimeSpan.FromHours(1))
                    throw new ArgumentOutOfRangeException(nameof(configuration.Timeout), "Timeout must be between zero and one hour.");
                // Each impl validates and copies the values it uses.
                var instance = new AppleSignIn(configuration);
                _configuration = configuration;
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
            throw new InvalidOperationException("Apple sign-in is not supported on this platform.");
#endif
        }

        /// <summary>Call on Unity's main thread. Success returns credentials, not a game-server session.</summary>
        public Task<AppleSignInResult> SignInAsync(CancellationToken cancellationToken = default) =>
            _impl.SignInAsync(cancellationToken);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _configuration = null; }
    }

    internal interface IAppleSignInImpl
    {
        Task<AppleSignInResult> SignInAsync(CancellationToken cancellationToken);
    }
}
