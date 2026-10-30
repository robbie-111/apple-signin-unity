# Apple Sign-In for Unity

Android와 iOS용 Unity Apple 로그인 패키지. Unity 2022.3 이상을 대상으로 한다.

```csharp
using Robbie111.AppleSignIn;

if (AppleSignIn.Configuration == null)
{
    AppleSignIn.Configuration = new AppleSignInConfiguration
    {
        ClientId = "com.example.game.service",
        RedirectUri = "https://example.com/apple-auth-redirect"
    };
}
var result = await AppleSignIn.DefaultInstance.SignInAsync(cancellationToken);
if (result.IsSuccess)
{
    // result.IdToken을 게임 인증 서버에 전달한다. 토큰을 로깅하지 않는다.
}
```

## 설정과 호출

Unity 메인 스레드에서 호출한다. 별도 Initialize, 프리팹, Update 호출은 필요 없다. Android·iOS·Editor·Standalone 외 플랫폼에서는 DefaultInstance 접근 시 InvalidOperationException이 발생한다. DefaultInstance 최초 접근 시 설정을 검증·복사하며 이후 교체할 수 없다. 설정 객체를 나중에 변경해도 인스턴스에는 반영되지 않는다.

Android는 Services ID와 Apple에 등록된 HTTPS Return URL이 필요하다. callback은 `signinwithapple://callback`이며 Manifest에 자동 등록된다. 서버는 `package=`로 앱을 지정한 intent URL로 돌려보낸다. 별도 AAR 제작 없이 포함된 Java 소스를 빌드한다. AndroidX Browser 1.5.0은 EDM4U(External Dependency Manager, 패키지 의존성으로 함께 설치)가 `Editor/AppleSignInDependencies.xml`을 읽어 추가한다. Gradle 의존성 해결을 위해 Player Settings에서 Custom Main Gradle Template과 Custom Gradle Properties Template을 켜 두는 것을 권장한다. Custom Tabs 미지원 시 브라우저 기본 fallback을 사용한다.

Editor와 Windows/macOS/Linux Standalone은 Android와 같은 설정(ClientId, RedirectUri)을 사용한다. 시스템 브라우저로 RedirectServer의 로그인 페이지(RedirectUri와 같은 경로의 `apple-auth-login`)를 열고, 페이지가 Apple JS SDK 팝업으로 로그인한 결과를 `http://127.0.0.1:{빈 포트}/callback`의 로컬 HttpListener로 받는다. 팝업 방식이라 Apple 화면의 취소와 팝업 닫기도 Canceled로 감지한다(authorize URL 직접 이동 방식은 취소를 어디에도 알리지 않는다). 브라우저 탭이나 브라우저 자체를 닫으면 감지할 수 없어 타임아웃까지 기다린다. RedirectServer가 아래 "Editor·데스크톱 로그인 페이지"를 제공해야 동작한다. Editor에서는 Play 모드에서만 동작하며 Play 종료·스크립트 리로드 시 Canceled로 끝난다.

iOS는 Android 설정 없이 사용할 수 있다. 기존 AppleAuth 네이티브 패키지와 Xcode capability 후처리를 사용하며 Apple Developer의 App ID 및 서명 설정은 별도로 준비한다.

## 결과와 취소

Status는 Success/Canceled/Failed다. 성공 결과는 UserId, IdToken, AuthorizationCode(없을 수 있음), Audience를 제공한다. 게임 서버의 토큰 검증은 별도다. 사용자 취소는 Canceled, 제한 시간 초과는 Failed/Timeout, 중복 요청은 Failed/AlreadyInProgress다. Timeout 기본값은 180초이며 0초 초과 1시간 이하로 설정한다. CancellationToken 취소는 OperationCanceledException이다.

Android에서 실제 이탈 후 결과 없이 게임으로 돌아오면 취소한다. 홈 화면을 거쳐 돌아와도 동일하다. 타임아웃·명시적 취소가 브라우저를 강제로 닫지는 않는다. 프로세스 종료 후 인증은 복구하지 않는다. iOS에서 대기를 취소해도 네이티브 인증이 종료되기 전에는 새 인증을 시작하지 않는다.

## Android 리다이렉트 계약 (v1)

후속 RedirectServer 개발용 계약이다.

### 요청

Apple authorize endpoint에 `client_id`, HTTPS `redirect_uri`, `response_type=code id_token`, `response_mode=form_post`, `state`, `nonce`를 전달한다. 이름·이메일 scope는 요청하지 않는다.

`state`는 다음 JSON을 UTF-8 → Base64URL(패딩 없음)로 인코딩한다.

```json
{"version":1,"applicationId":"com.robbie.game","random":"로그인마다 생성하는 32자리 GUID"}
```

앱은 state 문자열 전체를 보관한다. applicationId는 Android 실제 packageName과 같은 Unity Application.identifier다. nonce는 별도의 암호학적 난수이며 SHA256 변환 없이 Apple에 전달한 문자열과 token claim을 비교한다.

### 서버가 구현할 응답

1. state의 크기·형식·version과 applicationId 허용 목록을 검사한다. 임의 scheme으로 리다이렉트하지 않는다.
2. 성공·오류 모두 받은 state를 변경 없이 보존한다.
3. `303 Location: intent://callback?<query>#Intent;package={applicationId};scheme=signinwithapple;end`로 응답한다. `package`는 허용 목록을 통과한 state의 applicationId다.
4. query에는 `state`, `id_token`, `code`, `error` 중 존재하는 필드만 표준 URI 인코딩으로 한 번씩 포함한다. `user`는 필요하지 않다.
5. `Cache-Control: no-store`를 설정하고 토큰·code·callback URL을 로깅하지 않는다.
6. 신규 v1 state만 새 경로로 처리하고 기존 GUID state의 웹뷰 경로는 보존한다.

### Editor·데스크톱 로그인 페이지

Editor·Standalone의 state에는 로컬 수신 포트가 추가된다.

```json
{"version":1,"applicationId":"com.robbie.game","random":"32자리 GUID","port":51234}
```

앱은 `GET {RedirectUri와 같은 경로}/apple-auth-login?client_id=…&state=…&nonce=…`를 시스템 브라우저로 연다. 서버는 다음을 구현한다.

1. state를 위와 같이 검사하고 `port`(1024–65535)를 필수로 확인한다.
2. `Cache-Control: no-store` HTML을 응답한다. 값은 HTML/JS 이스케이프해서 넣는다.
3. 페이지는 `https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/ko_KR/appleid.auth.js`를 불러오고, 사용자가 버튼을 누르면(팝업 차단 회피) `AppleID.auth.init({ clientId, scope: '', redirectURI: <등록된 /apple-auth-redirect>, state, nonce, usePopup: true })` 후 `AppleID.auth.signIn()`을 호출한다.
4. 성공하면 `authorization`의 `state`, `id_token`, `code`를, 실패하면 `state`와 `error`(`user_cancelled_authorize`, `popup_closed_by_user` 등)를 query로 `http://127.0.0.1:{port}/callback`에 이동한다. query 규칙은 위 4번과 같다.

Services ID의 Domains에 서버 도메인이 등록되어 있어야 한다. 앱은 루프백 주소에서 온 `GET /callback`만 받고, 브라우저에 완료 페이지를 돌려준 뒤 리스너를 닫는다. 토큰이 루프백 URL 쿼리로 전달되므로 브라우저 기록에 남을 수 있고, 같은 PC의 다른 프로세스가 포트를 선점할 수 있다. 게임 서버 검증이 필수다.

토큰이 없는 취소·실패에서도 state로 복귀한다. state가 없거나 잘못되었다면 임의 앱으로 보내지 않고 서버 오류로 종료한다. 앱은 타임아웃 또는 복귀 취소로 종료한다.

### 검증 책임

앱은 callback URI, state, nonce, token sub/aud를 확인한다. 게임 서버는 JWT 서명, issuer, audience, 만료 등 최종 인증을 검증해야 한다. 클라이언트의 파싱 성공은 계정 인증 완료를 뜻하지 않는다. `signinwithapple`은 다른 앱도 쓰는 공용 scheme이라 intent의 `package=`가 대상 앱을 정한다. `package=`를 무시하는 브라우저에서는 다른 앱이 후보가 될 수 있으며, state/nonce가 callback 토큰 가로채기를 완전히 방지하지 않는다.

### 수명

요청은 메모리에만 보관한다. 프로세스 종료 후 callback은 폐기한다. 실제 앱 이탈 후 결과 없이 돌아오면 500ms 유예 후 취소한다. 홈 경유 복귀도 취소하며, 완료된 요청의 응답은 무시한다.

## 검증

패키지 EditMode 테스트는 Unity Test Runner에서 실행한다. Android callback의 정상/오류/중복 Intent, 뒤로 가기·홈 복귀, 브라우저 fallback과 프로세스 종료는 기기에서 확인한다. 서버 연결 전 합성 callback 테스트는 실제 Apple 인증 검증을 대신하지 않는다.

## 공개 패키지 설치

프로젝트 폴더에서 다음 명령으로 OpenUPM 의존성과 레지스트리를 설정합니다.

```sh
openupm add com.lupidan.apple-signin-unity@1.5.0 com.google.external-dependency-manager@1.2.190
```

iOS는 공개 AppleAuth 1.5.0을 사용합니다. 사내 로깅 패키지 대신 UnityEngine.Debug를 사용합니다. RedirectUri 기본값은 없으므로 브라우저 로그인 전에 반드시 지정합니다.

샘플은 현재 uGUI와 레거시 Text 화면을 사용하며 로그인 버튼과 플랫폼·상태·결과 표시를 제공합니다. 토큰과 인증 코드는 원문 대신 수신 여부를 표시합니다.

Android 샘플을 Editor에서 빌드하기 전 **Tools → Apple Sign-In → Configure Android Templates**를 실행하고 **Assets → External Dependency Manager → Android Resolver → Force Resolve**로 의존성을 해결합니다. 제공된 CLI 빌드 유틸리티는 Gradle 템플릿 설정을 적용합니다.

iOS 내보내기 후 CocoaPods workspace가 생성되면 `Unity-iPhone.xcworkspace`를 열어 빌드합니다.
