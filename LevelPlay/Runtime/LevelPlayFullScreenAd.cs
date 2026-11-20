using System;
using UnityEngine;
#if UNITY_IOS
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
#endif

namespace RiseOn.Ads.LevelPlay {
    /// <summary>
    /// What a LevelPlay full-screen ad tells, in order, on the bridge's own event thread (a serial queue on iOS), never through Unity's player loop.
    /// The reward comes before the close.
    /// </summary>
    internal interface ILevelPlayFullScreenEvents {
        void OnLoaded();
        void OnLoadFailed(int code, string message);
        void OnDisplayed();
        void OnDisplayFailed(int code, string message);
        void OnRewarded();
        void OnClosed();

        /// <summary>Revenue in US dollars.</summary>
        void OnPaid(string adNetwork, string adUnitId, double revenue);
    }

    /// <summary>
    /// A LevelPlay interstitial or rewarded ad, through the pack's own bridge to LevelPlay's native API on Android and iOS, so that its events, the close
    /// above all, arrive while a full-screen cover keeps Unity paused: Unity's LevelPlay API hands every event to the player loop.
    /// </summary>
    internal abstract class LevelPlayFullScreenAd {
        public static LevelPlayFullScreenAd Create(string adUnitId, bool rewarding, ILevelPlayFullScreenEvents events) {
            switch (Application.platform) {
                case RuntimePlatform.Android:
                    return new AndroidAd(adUnitId, rewarding, events);
                case RuntimePlatform.IPhonePlayer:
#if UNITY_IOS
                    return new IOSAd(adUnitId, rewarding, events);
#else
                    throw new PlatformNotSupportedException("an iOS player built without UNITY_IOS");
#endif
                default:
                    // The Editor shows the pack's placeholders instead.
                    throw new PlatformNotSupportedException($"LevelPlay's full-screen ads run on Android and iOS, not {Application.platform}");
            }
        }

        public abstract void Load();
        public abstract bool IsReady();
        public abstract void Show(string placement);

        /// <summary>No event comes after.</summary>
        public abstract void Destroy();

        private sealed class AndroidAd : LevelPlayFullScreenAd {
            private readonly AndroidJavaObject ad;

            public AndroidAd(string adUnitId, bool rewarding, ILevelPlayFullScreenEvents events) {
                ad = new AndroidJavaObject("com.riseon.ads.levelplay.FullScreenAd", adUnitId, rewarding, new ListenerProxy(events));
            }

            public override void Load() => ad.Call("load");
            public override bool IsReady() => ad.Call<bool>("isReady");
            public override void Show(string placement) => ad.Call("show", UnityEngine.Android.AndroidApplication.currentActivity, placement);

            // LevelPlay's Android ads have no destroy: dropped, they go with the listener.
            public override void Destroy() {
                ad.Call("destroy");
                ad.Dispose();
            }

            // Dispatched by name: in step with com.riseon.ads.levelplay.FullScreenAdListener.
            private sealed class ListenerProxy : AndroidJavaProxy {
                private readonly ILevelPlayFullScreenEvents events;

                public ListenerProxy(ILevelPlayFullScreenEvents events) : base("com.riseon.ads.levelplay.FullScreenAdListener") {
                    this.events = events;
                }

                public void onLoaded() => events.OnLoaded();
                public void onLoadFailed(int code, string message) => events.OnLoadFailed(code, message);
                public void onDisplayed() => events.OnDisplayed();
                public void onDisplayFailed(int code, string message) => events.OnDisplayFailed(code, message);
                public void onRewarded() => events.OnRewarded();
                public void onClosed() => events.OnClosed();
                public void onPaid(string adNetwork, string adUnitId, double revenue) => events.OnPaid(adNetwork, adUnitId, revenue);
            }
        }

#if UNITY_IOS
        // The native side's symbols exist in iOS builds only, so this compiles there only.
        private sealed class IOSAd : LevelPlayFullScreenAd {
            private delegate void Callback(IntPtr owner, int ev, int code, string first, string second, double revenue);

            private static readonly Dictionary<IntPtr, ILevelPlayFullScreenEvents> owners = new();
            private static int lastOwner;

            private readonly IntPtr ad;
            private readonly IntPtr owner;

            public IOSAd(string adUnitId, bool rewarding, ILevelPlayFullScreenEvents events) {
                owner = (IntPtr) System.Threading.Interlocked.Increment(ref lastOwner);
                lock (owners) owners[owner] = events;
                ad = ROLevelPlayFullScreenAdCreate(adUnitId, rewarding, owner, Receive);
            }

            public override void Load() => ROLevelPlayFullScreenAdLoad(ad);
            public override bool IsReady() => ROLevelPlayFullScreenAdIsReady(ad);
            public override void Show(string placement) => ROLevelPlayFullScreenAdShow(ad, placement);

            public override void Destroy() {
                lock (owners) owners.Remove(owner);
                ROLevelPlayFullScreenAdDestroy(ad);
            }

            // In step with ROLevelPlayEvent of ROLevelPlayFullScreenAd.m.
            [MonoPInvokeCallback(typeof(Callback))]
            private static void Receive(IntPtr owner, int ev, int code, string first, string second, double revenue) {
                ILevelPlayFullScreenEvents events;
                lock (owners) {
                    if (!owners.TryGetValue(owner, out events)) return;
                }

                switch (ev) {
                    case 0: events.OnLoaded(); break;
                    case 1: events.OnLoadFailed(code, first); break;
                    case 2: events.OnDisplayed(); break;
                    case 3: events.OnDisplayFailed(code, first); break;
                    case 4: events.OnRewarded(); break;
                    case 5: events.OnClosed(); break;
                    case 6: events.OnPaid(first, second, revenue); break;
                    default: Debug.LogError($"{nameof(LevelPlayFullScreenAd)}: unknown event {ev}"); break;
                }
            }

            [DllImport("__Internal")]
            private static extern IntPtr ROLevelPlayFullScreenAdCreate(string adUnitId, bool rewarding, IntPtr owner, Callback callback);

            [DllImport("__Internal")]
            private static extern void ROLevelPlayFullScreenAdLoad(IntPtr ad);

            [DllImport("__Internal")]
            [return: MarshalAs(UnmanagedType.I1)]
            private static extern bool ROLevelPlayFullScreenAdIsReady(IntPtr ad);

            [DllImport("__Internal")]
            private static extern void ROLevelPlayFullScreenAdShow(IntPtr ad, string placement);

            [DllImport("__Internal")]
            private static extern void ROLevelPlayFullScreenAdDestroy(IntPtr ad);
        }
#endif
    }
}