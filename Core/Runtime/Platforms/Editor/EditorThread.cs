using System;
using System.Threading;
using UnityEngine;

namespace RiseOn.Ads.Platforms.Editor {
    /// <summary>
    /// Hands a call from any thread to Unity's: the covers and placeholders promise any thread, as an ad SDK's completion is where a cover's hide belongs, and every Unity object has to be touched on Unity's thread.
    /// </summary>
    internal static class EditorThread {
        private static SynchronizationContext unityContext;
        private static int                    unityThreadId;

        // Before the first scene loads, on Unity's own thread. Not at SubsystemRegistration: Unity installs its
        // synchronization context after that, and there would be nothing to capture.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Capture() {
            unityContext  = SynchronizationContext.Current;
            unityThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static void Run(Action action) {
            if (unityContext == null || Thread.CurrentThread.ManagedThreadId == unityThreadId) {
                action();
                return;
            }

            unityContext.Post(static state => ((Action) state)(), action);
        }
    }
}