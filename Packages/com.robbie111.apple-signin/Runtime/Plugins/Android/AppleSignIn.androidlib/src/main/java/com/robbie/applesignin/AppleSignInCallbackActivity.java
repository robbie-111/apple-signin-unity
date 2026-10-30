package com.robbie.applesignin;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;
import com.unity3d.player.UnityPlayer;

public final class AppleSignInCallbackActivity extends Activity {
    private static final String TAG = AppleSignInBridge.TAG;

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        handle(getIntent());
    }
    @Override protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        handle(intent);
    }
    private void handle(Intent intent) {
        try {
            Uri data = intent == null ? null : intent.getData();
            Log.d(TAG, "callback activity received intent (action=" + (intent == null ? "null" : intent.getAction()) +
                ", scheme=" + (data == null ? "-" : data.getScheme()) + ", host=" + (data == null ? "-" : data.getHost()) + ")");
            boolean accepted = AppleSignInBridge.receive(this, intent);
            if (!accepted) {
                Log.w(TAG, "callback activity: intent was not accepted; not returning to Unity");
                return;
            }
            Activity unity = UnityPlayer.currentActivity;
            if (unity == null || unity.isFinishing()) {
                Log.w(TAG, "callback activity: Unity activity is " + (unity == null ? "null" : "finishing") + "; cannot bring it to front");
                return;
            }
            Intent back = new Intent(this, unity.getClass());
            back.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
            startActivity(back);
            Log.d(TAG, "callback activity: returned to " + unity.getClass().getName());
        } catch (RuntimeException e) {
            // Invalid external intents must not crash or restart authentication.
            Log.e(TAG, "callback activity: failed to handle intent", e);
        } finally { finish(); }
    }
}
