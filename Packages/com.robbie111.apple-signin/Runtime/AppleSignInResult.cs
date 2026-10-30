namespace Robbie111.AppleSignIn
{
    public enum AppleSignInStatus
    {
        Success,
        Canceled,
        Failed
    }

    public enum AppleSignInErrorCode
    {
        None,
        UnsupportedPlatform,
        AlreadyInProgress,
        BrowserUnavailable,
        Timeout,
        InvalidResponse,
        StateMismatch,
        NonceMismatch,
        ProviderError,
        InternalError,
        InvalidConfiguration
    }

    public sealed class AppleSignInResult
    {
        public AppleSignInStatus Status { get; }
        public bool IsSuccess => Status == AppleSignInStatus.Success;
        public string UserId { get; }
        public string IdToken { get; }
        public string AuthorizationCode { get; }
        public string Audience { get; }
        public AppleSignInErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }

        private AppleSignInResult(AppleSignInStatus status, AppleSignInErrorCode error,
            string message, string user = "", string token = "", string code = "", string audience = "")
        {
            Status = status;
            ErrorCode = error;
            ErrorMessage = message;
            UserId = user;
            IdToken = token;
            AuthorizationCode = code ?? "";
            Audience = audience;
        }

        internal static AppleSignInResult Success(string user, string token, string code, string audience) =>
            new AppleSignInResult(AppleSignInStatus.Success, AppleSignInErrorCode.None, "", user, token, code,
                audience);

        internal static AppleSignInResult Failure(AppleSignInErrorCode error, string message) =>
            new AppleSignInResult(AppleSignInStatus.Failed, error, message);

        internal static AppleSignInResult Canceled() =>
            new AppleSignInResult(AppleSignInStatus.Canceled, AppleSignInErrorCode.None, "Sign-in canceled.");
    }
}