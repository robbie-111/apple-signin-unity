# Apple Sign-In for Unity

iOS 네이티브 로그인과 Android·Unity Editor·Windows/macOS/Linux 브라우저 로그인을 제공하는 UPM 패키지입니다. 공개 AppleAuth와 EDM4U를 사용합니다. 기본 인증 서버 주소는 직접 설정합니다.

## 설치

Package Manager의 **Add package from git URL**에 입력합니다.

```text
https://github.com/robbie-111/apple-signin-unity.git?path=Packages/com.robbie111.apple-signin
```

먼저 대상 Unity 프로젝트에서 다음 명령을 실행해 OpenUPM 의존성을 설정합니다.

```sh
openupm add com.lupidan.apple-signin-unity@1.5.0 com.google.external-dependency-manager@1.2.190
```

특정 리비전은 끝에 `#<태그 또는 커밋>`을 붙입니다. AppleAuth·EDM4U 의존성은 패키지에 선언되어 있습니다. AndroidX Browser는 EDM4U XML로 해결합니다.

```csharp
using Robbie111.AppleSignIn;

// Android / Editor / Standalone에서 사용합니다. iOS는 이 설정 없이 사용할 수 있습니다.
AppleSignIn.Configuration = new AppleSignInConfiguration
{
    ClientId = "com.example.game.service",
    RedirectUri = "https://example.com/apple-auth-redirect"
};
var result = await AppleSignIn.DefaultInstance.SignInAsync(cancellationToken);
if (result.IsSuccess)
{
    // result.IdToken을 게임 서버에서 검증합니다. 토큰을 로그에 남기지 않습니다.
}
```

Unity 메인 스레드에서 호출합니다. Editor에서는 Play Mode가 필요합니다. 최초 `DefaultInstance` 접근 시 설정을 복사하며 이후 교체할 수 없습니다. 사용자 취소는 `Canceled`, 호출자의 `CancellationToken` 취소는 `OperationCanceledException`입니다.

## 샘플

저장소 루트는 Unity 프로젝트가 아닙니다. Unity Hub에서 `SampleProject/`를 Unity `6000.3.18f1`로 엽니다.

1. `Assets/SignInSample/Scenes/SampleScene.unity`를 엽니다.
2. `AppleSignInSample` 객체의 Inspector에서 `Client Id`, `Redirect Uri`를 설정합니다. iOS 기기에서는 불필요합니다.
3. Play Mode 또는 기기 빌드에서 로그인 버튼을 사용합니다.

샘플은 **uGUI + 레거시 `UnityEngine.UI.Text`**를 사용하며, 토큰·인증 코드 원문 대신 수신 여부를 표시합니다. 설정을 변경했다면 Play Mode를 재시작합니다.

Package Manager의 **Samples → SignIn Sample → Import**로도 가져올 수 있습니다. 대상 프로젝트에는 Unity 버전에 맞는 `com.unity.ugui`가 필요합니다. `SampleProject`에는 이미 같은 샘플이 있으므로 중복 임포트하지 않습니다.

## 플랫폼 설정

- **iOS:** Apple Developer의 App ID에 Sign in with Apple을 활성화하고 서명·프로비저닝을 준비합니다. Xcode capability·entitlements·프레임워크 연결은 후처리가 추가하며 기존 entitlements를 보존합니다.
- **Android:** 참고 프로젝트의 Java 소스와 EDM4U 의존성 XML을 사용합니다. Gradle의 `androidx.browser:browser:1.5.0` 해결을 위해 Google Maven 접근이 필요합니다.
- **Android·Editor·Standalone:** Apple Services ID와 호환 HTTPS 서버가 필요합니다. 서버 구현·배포는 포함하지 않습니다.
- **WebGL 등:** `DefaultInstance` 접근 시 `InvalidOperationException`이 발생합니다.

API, 플랫폼별 제한과 서버 연동 규격은 [패키지 README](Packages/com.robbie111.apple-signin/README.md)를 참고하세요.

## 개발과 검증

- `Packages/com.robbie111.apple-signin/`: 배포 패키지. 최소 Unity 선언은 `2022.3`입니다.
- `SampleProject/`: 로컬 `file:` 패키지를 참조하는 Unity `6000.3.18f1` 프로젝트입니다.
- `SampleProject/Assets/Tests/Editor/`: 설정, 토큰·콜백, 요청 수명, 루프백 HTTP, Android 후처리 테스트입니다.
- `SampleProject/Assets/Editor/AppleSignInSampleTools.cs`: 샘플 씬 생성과 빌드 유틸리티입니다.

Unity Test Runner에서 EditMode 테스트를 실행하거나, 대상 프로젝트를 Editor에서 닫고 Unity CLI로 실행합니다.

```sh
unity test SampleProject --mode EditMode --output /tmp/apple-signin-editmode.xml
```

샘플 빌드는 아래 유틸리티를 사용합니다. Android는 ARM64 IL2CPP와 Release 코드 축소를 적용합니다. iOS는 Xcode 프로젝트를 내보냅니다.

```sh
unity build SampleProject --target Android --execute-method Robbie111.AppleSignIn.SampleEditor.AppleSignInSampleTools.Build --output-path /tmp/apple-signin-build/Android/sample.apk --allow-dirty-build
unity build SampleProject --target iOS --execute-method Robbie111.AppleSignIn.SampleEditor.AppleSignInSampleTools.Build --output-path /tmp/apple-signin-build/iOS --allow-dirty-build
unity build SampleProject --target StandaloneOSX --execute-method Robbie111.AppleSignIn.SampleEditor.AppleSignInSampleTools.Build --output-path /tmp/apple-signin-build/macOS/AppleSignInSample.app --allow-dirty-build
```

인증 런타임과 빌드 후처리는 참고 프로젝트 2.0.0을 기준으로 구성합니다. 기존 독립 구현의 검증 결과는 현재 구현에 적용되지 않습니다. 실제 Apple 인증에는 Services ID·HTTPS 서버·계정·서명 설정이 필요합니다.

Android 샘플을 Editor에서 빌드하기 전 **Tools → Apple Sign-In → Configure Android Templates**를 실행하고 **Assets → External Dependency Manager → Android Resolver → Force Resolve**로 의존성을 해결합니다. 제공된 CLI 빌드 유틸리티는 Gradle 템플릿 설정을 적용합니다.

EDM4U 의존성 XML의 이름을 바꾸거나 새 검증 프로젝트를 만든 경우, Android 빌드 전에 **Force Resolve**를 다시 실행합니다.

공개 의존성을 해결한 별도 검증 프로젝트에서 참고 EditMode 테스트 8개가 통과했습니다.

이번 참고 구현 적용 후 Unity 6000.3.18f1의 별도 검증 프로젝트에서 확인한 결과입니다.

- 참고 EditMode 테스트 8개 통과.
- Android ARM64 IL2CPP Release APK 빌드 성공. EDM4U가 추가한 AndroidX Browser와 R8 이후 브리지·Custom Tabs 코드를 확인했습니다.
- iOS 내보내기 및 Xcode 27.0의 서명 없는 기기 대상 컴파일·링크 성공. EDM4U가 생성한 `Unity-iPhone.xcworkspace`를 사용합니다.
- macOS Standalone 앱 빌드 성공.
- 실제 Apple 계정 로그인과 기기 복귀 동작, Windows/Linux 및 Unity 2022.3 실행은 별도 검증이 필요합니다.

## 명칭 정리 검증

Android Java 패키지를 `com.robbie.applesignin`으로 변경하고 라이브러리·EDM4U XML·콜백·테마 이름을 정리했습니다. C# Logging 래퍼를 제거하고 UnityEngine.Debug를 직접 호출합니다. 로컬 샘플 씬의 설정과 UI는 유지했습니다.

EditMode 테스트 8개, Android ARM64 IL2CPP Release APK, iOS의 서명 없는 Xcode workspace 빌드를 확인했습니다. Android Resolver를 강제로 다시 실행한 뒤 APK에 Custom Tabs 구현이 포함되는지도 확인했습니다.

이번 산출물과 검증 로그는 `SampleProject/Builds/naming-cleanup/`에 있습니다. 해당 폴더는 Git 추적에서 제외됩니다.
