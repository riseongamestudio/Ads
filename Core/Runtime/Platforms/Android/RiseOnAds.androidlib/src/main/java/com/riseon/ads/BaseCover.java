package com.riseon.ads;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;

// What both covers share, and nothing more. The two covers themselves are
// FullScreenCover and HalfScreenCover, named for what they do and not for the
// family they belong to.
final class BaseCover {
    private static final Handler MAIN =
            new Handler(Looper.getMainLooper());

    private BaseCover() {}

    // One hop for both covers, and it does not need an Activity to make it.
    // Activity.runOnUiThread is the same logic - run inline when already on
    // the UI thread, post otherwise - but it has nothing to post to when the
    // Activity is null, and window work on Unity's thread is a crash waiting
    // for the one call that arrives with no Activity in hand.
    //
    // Posting keeps the order calls were made in, and that order is half of
    // the half-screen contract: the cover's window has to be added before the
    // ad's. It only holds while both are posted from the same side of the UI
    // thread - from Unity's thread, say, which is where C# runs.
    static void RunOnMainThread(Runnable action) {
        if (action == null) return;

        if (Looper.myLooper() == Looper.getMainLooper()) {
            action.run();
            return;
        }
        MAIN.post(action);
    }

    static boolean IsActivityUsable(Activity activity) {
        return activity != null
                && !activity.isFinishing()
                && !activity.isDestroyed();
    }
}