package com.robbie.applesignin;

import android.app.Activity;
import android.content.Intent;
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
            if (!AppleSignInBridge.receive(this, intent)) return;
            Activity unity = UnityPlayer.currentActivity;
            if (unity == null || unity.isFinishing()) return;
            Intent back = new Intent(this, unity.getClass());
            back.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
            startActivity(back);
        } catch (RuntimeException e) {
            // Invalid external intents must not crash or restart authentication.
            Log.e(TAG, "callback activity: failed to handle intent", e);
        } finally { finish(); }
    }
}
