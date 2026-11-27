# Changelog

## 0.2.0

- Add `AppleSignInConfiguration.UseNonce` (default `true`). When `false`, iOS, Android, and Editor/desktop requests omit the nonce and the token nonce check is skipped.

## 0.1.0

Initial open-source release for Unity 2022.3 and later.

- Sign in with Apple for iOS, Android, Unity Editor, and desktop standalone builds.
- Async sign-in API with cancellation, timeouts, and state/nonce validation.
- AppleAuth and Unity External Dependency Manager integration with Android/iOS build post-processing.
- uGUI sample using legacy Text components.
