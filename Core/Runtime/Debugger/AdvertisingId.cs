using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;

namespace RiseOn.Ads {
    /// <summary>The device's advertising ID, the one the networks' dashboards register a test device by: the GAID on Android, the IDFA on iOS.</summary>
    internal static class AdvertisingId {
        // What both platforms hand out once the player took the ID away.
        private const string Zeros = "00000000-0000-0000-0000-000000000000";

        /// <summary>The ID, or null and why there is none. Unity's thread.</summary>
        public static async UniTask<(string Id, string Problem)> ReadAsync(CancellationToken cancellationToken) {
            switch (Application.platform) {
                case RuntimePlatform.Android: {
                    var (id, problem) = await ReadGaidAsync(cancellationToken);
                    if (id is Zeros) return (null, "The advertising ID was deleted on this device: Settings > Privacy > Ads (Settings > Google > Ads before Android 12)");
                    return (id, problem);
                }
                case RuntimePlatform.IPhonePlayer: {
                    // UnityEngine.iOS is in the iOS player only.
#if UNITY_IOS
                    var id = UnityEngine.iOS.Device.advertisingIdentifier;
#else
                    string id = null;
#endif
                    if (string.IsNullOrEmpty(id) || id is Zeros) return (null, "No IDFA: this app is not allowed to track");
                    return (id, null);
                }
                default:
                    return (null, $"No advertising ID on {Application.platform}");
            }
        }

        // Google Play services blocks on the lookup, so it runs on a thread of its own, attached to the JVM for the call.
        private static async UniTask<(string, string)> ReadGaidAsync(CancellationToken cancellationToken) {
            var activity = AndroidApplication.currentActivity;
            var result   = new UniTaskCompletionSource<(string, string)>();
            new Thread(() => {
                AndroidJNI.AttachCurrentThread();
                try {
                    using var client = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
                    using var info   = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", activity);
                    result.TrySetResult((info.Call<string>("getId"), null));
                } catch (Exception exception) {
                    result.TrySetResult((null, $"Google Play services gave no advertising ID: {exception.Message}"));
                } finally {
                    AndroidJNI.DetachCurrentThread();
                }
            }) { IsBackground = true }.Start();

            var read = await result.Task;
            await UniTask.SwitchToMainThread(cancellationToken);
            return read;
        }
    }
}