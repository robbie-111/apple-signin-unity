package com.robbie.applesignin;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.util.Log;
import androidx.browser.customtabs.CustomTabsIntent;
import com.unity3d.player.UnityPlayer;
import org.json.JSONObject;

/** Unity-specific bridge; no credential persistence and no browser package pinning. */
public final class AppleSignInBridge {
    static final String TAG = "AppleSignIn";
    /** Fixed callback scheme; the server's intent URL pins the target app with package=. */
    static final String CALLBACK_SCHEME = "signinwithapple";
    private static String requestId;
    private static String expectedState;
    private static String receiver;
    private static boolean callbackReceived;

    private AppleSignInBridge() { }

    public static void signIn(String id, String clientId, String redirectUri,
                              String state, String nonce, String gameObject) {
        Activity activity = UnityPlayer.currentActivity;
        synchronized (AppleSignInBridge.class) {
            if (requestId != null) {
                Log.w(TAG, "signIn rejected: request " + requestId + " is still running (new=" + id + ")");
                throw new IllegalStateException("Sign-in already running");
            }
            requestId = id;
            expectedState = state;
            receiver = gameObject;
            callbackReceived = false;
        }
        Log.i(TAG, "signIn start (request=" + id + ", client_id=" + clientId +
            ", redirect_uri=" + redirectUri + ", receiver=" + gameObject + ")");
        if (activity == null) {
            Log.e(TAG, "signIn failed: UnityPlayer.currentActivity is null (request=" + id + ")");
            fail(id, "InternalError");
            return;
        }
        activity.runOnUiThread(() -> {
            if (!isActive(id)) {
                Log.w(TAG, "signIn skipped on UI thread: request " + id + " is no longer active");
                return;
            }
            try {
                Uri callback = Uri.parse(CALLBACK_SCHEME + "://callback");
                Intent probe = new Intent(Intent.ACTION_VIEW, callback).setPackage(activity.getPackageName());
                boolean found = false;
                int candidates = 0;
                for (android.content.pm.ResolveInfo info : activity.getPackageManager()
                        .queryIntentActivities(probe, PackageManager.MATCH_DEFAULT_ONLY)) {
                    candidates++;
                    ActivityInfo target = info.activityInfo;
                    if (target != null && target.exported &&
                        AppleSignInCallbackActivity.class.getName().equals(target.name)) found = true;
                }
                if (!found) {
                    Log.e(TAG, "signIn failed: callback activity " + AppleSignInCallbackActivity.class.getName() +
                        " is not registered for " + CALLBACK_SCHEME + "://callback (package=" + activity.getPackageName() +
                        ", resolved=" + candidates + "). Check the merged AndroidManifest.");
                    fail(id, "InvalidConfiguration");
                    return;
                }
                Uri uri = Uri.parse("https://appleid.apple.com/auth/authorize").buildUpon()
                    .appendQueryParameter("client_id", clientId)
                    .appendQueryParameter("redirect_uri", redirectUri)
                    .appendQueryParameter("response_type", "code id_token")
                    .appendQueryParameter("response_mode", "form_post")
                    .appendQueryParameter("state", state)
                    .appendQueryParameter("nonce", nonce).build();
                new CustomTabsIntent.Builder().setShareState(CustomTabsIntent.SHARE_STATE_OFF)
                    .build().launchUrl(activity, uri);
                Log.i(TAG, "Custom Tab launched (request=" + id + ")");
            } catch (ActivityNotFoundException e) {
                Log.e(TAG, "signIn failed: no browser can open the Apple authorize URL (request=" + id + ")", e);
                fail(id, "BrowserUnavailable");
            } catch (Exception e) {
                Log.e(TAG, "signIn failed: unexpected exception (request=" + id + ")", e);
                fail(id, "InternalError");
            }
        });
    }

    private static synchronized boolean isActive(String id) { return id.equals(requestId); }
    public static synchronized boolean hasCallback(String id) {
        return id.equals(requestId) && callbackReceived;
    }
    public static synchronized void cancel(String id) {
        if (!id.equals(requestId)) {
            Log.d(TAG, "cancel ignored: request " + id + " is not active (active=" + requestId + ")");
            return;
        }
        Log.d(TAG, "cancel (request=" + id + ", callbackReceived=" + callbackReceived + ")");
        requestId = null; expectedState = null; receiver = null; callbackReceived = false;
    }

    static synchronized boolean receive(Activity activity, Intent intent) {
        if (requestId == null) { Log.w(TAG, "callback ignored: no sign-in in progress"); return false; }
        if (callbackReceived) { Log.w(TAG, "callback ignored: already received for request " + requestId); return false; }
        if (intent == null) { Log.w(TAG, "callback ignored: intent is null"); return false; }
        if (!Intent.ACTION_VIEW.equals(intent.getAction())) {
            Log.w(TAG, "callback ignored: unexpected action " + intent.getAction());
            return false;
        }
        Uri uri = intent.getData();
        if (uri == null) { Log.w(TAG, "callback ignored: intent has no data URI"); return false; }
        if (!CALLBACK_SCHEME.equals(uri.getScheme()) ||
            !"callback".equals(uri.getHost()) || uri.getPort() != -1 || uri.getUserInfo() != null ||
            (uri.getPath() != null && !uri.getPath().isEmpty()) || uri.getFragment() != null) {
            Log.w(TAG, "callback ignored: unexpected URI shape (scheme=" + uri.getScheme() + ", host=" + uri.getHost() +
                ", expected=" + CALLBACK_SCHEME + "://callback)");
            return false;
        }
        // A late/foreign request must not terminate the current sign-in.
        String state = uri.getQueryParameter("state");
        if (!expectedState.equals(state)) {
            Log.w(TAG, "callback ignored: state does not match request " + requestId + " (state=" + describe(state) + ")");
            return false;
        }
        try {
            for (String key : new String[]{"state", "id_token", "code", "error"}) {
                if (uri.getQueryParameters(key).size() > 1) {
                    Log.e(TAG, "callback rejected: duplicate '" + key + "' parameter (request=" + requestId + ")");
                    fail(requestId, "InvalidResponse");
                    return true;
                }
            }
            int length = uri.toString().length();
            if (length > 65536) {
                Log.e(TAG, "callback rejected: URI too long (" + length + " chars, request=" + requestId + ")");
                fail(requestId, "InvalidResponse");
                return true;
            }
            JSONObject payload = new JSONObject();
            payload.put("requestId", requestId);
            for (String key : new String[]{"state", "id_token", "code", "error"})
                payload.put(key, uri.getQueryParameter(key));
            callbackReceived = true;
            String error = uri.getQueryParameter("error");
            Log.i(TAG, "callback accepted (request=" + requestId + ", id_token=" + describe(uri.getQueryParameter("id_token")) +
                ", code=" + describe(uri.getQueryParameter("code")) + ", error=" + (error == null ? "-" : error) + ")");
            send(payload.toString());
            return true;
        } catch (Exception e) {
            Log.e(TAG, "callback rejected: could not build payload (request=" + requestId + ")", e);
            fail(requestId, "InvalidResponse");
            return true;
        }
    }

    private static synchronized void fail(String id, String error) {
        if (id == null || !id.equals(requestId) || callbackReceived) {
            Log.w(TAG, "fail(" + error + ") ignored (request=" + id + ", active=" + requestId +
                ", callbackReceived=" + callbackReceived + ")");
            return;
        }
        Log.e(TAG, "sign-in failed natively: " + error + " (request=" + id + ")");
        try {
            JSONObject payload = new JSONObject();
            payload.put("requestId", id); payload.put("nativeError", error);
            callbackReceived = true;
            send(payload.toString());
        } catch (org.json.JSONException impossible) {
            Log.e(TAG, "could not build failure payload", impossible);
            throw new IllegalStateException(impossible);
        }
    }

    private static void send(String json) {
        Log.d(TAG, "UnitySendMessage -> " + receiver + ".OnAppleSignInCallback (length=" + json.length() + ")");
        UnityPlayer.UnitySendMessage(receiver, "OnAppleSignInCallback", json);
    }

    /** Sensitive values are logged by presence and length only. */
    static String describe(String value) {
        return value == null || value.isEmpty() ? "absent" : "present(" + value.length() + ")";
    }

}
