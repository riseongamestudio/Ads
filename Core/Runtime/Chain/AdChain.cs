using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RiseOn.Ads {
    public enum AdChainSkip {
        None
      , NoStep
      , NotReady

        /// <summary><see cref="AdsSdk.SetSuppressed"/> holds the format back.</summary>
      , Suppressed
    }

    public readonly struct AdChainResult {
        /// <summary>How many steps displayed.</summary>
        public readonly int Displayed;

        /// <summary>A rewarding step earned its reward, or a rewarding step was suppressed and so granted it.</summary>
        public readonly bool Rewarded;

        /// <summary>Why nothing showed, when nothing did.</summary>
        public readonly AdChainSkip Skip;

        internal AdChainResult(int displayed, bool rewarded, AdChainSkip skip) {
            Displayed = displayed;
            Rewarded  = rewarded;
            Skip      = skip;
        }

        public bool AnyDisplayed => Displayed > 0;
    }

    internal interface IAdChainHost {
        /// <summary>The mediations for a full-screen format, best first.</summary>
        IReadOnlyList<IAdMediationFullScreen> FullScreenCandidates(AdFormat format);

        /// <summary>Any thread.</summary>
        void BeginFullScreen();

        /// <summary>Any thread.</summary>
        void EndFullScreen();

        /// <summary>Unity's thread, once a step displays.</summary>
        void Displayed(AdFormat format, string placement);

        /// <summary>The page's cover color.</summary>
        Color CoverColor { get; }
    }

    /// <summary>
    /// Full-screen ads shown one after the other, such as an interstitial and the end card after it.<br/>
    /// The full-screen cover lies under them, so the game never shows where an ad does not reach nor between two steps, and keeps it paused; each step starts from the previous one's callback without the player loop, and the cover comes down after the last step, whatever failed.<br/>
    /// A first step whose mediation calls back through the player loop shows alone and without the cover, which its close could never take down.<br/>
    /// A chain whose first step is not ready shows nothing; a later step that is not ready is skipped.
    /// </summary>
    public sealed class AdChain {
        private readonly IAdChainHost                host;
        private readonly Func<AdFullScreenFormat, AdChainSkip> skip;
        private readonly List<Step>                  steps = new();
        private          AdChainSkip                 skipped;
        private          bool                        rewardedWithoutAd;
        private          bool                        ran;

        /// <param name="skip">Why a format is held back, <see cref="AdChainSkip.None"/> when it is not; such steps are left out as they are added.</param>
        internal AdChain(IAdChainHost host, Func<AdFullScreenFormat, AdChainSkip> skip) {
            this.host = host;
            this.skip = skip;
        }

        /// <param name="placement">Reaches <see cref="AdsSdk.Displayed"/> and the mediation, which reports it with the impression where its SDK takes one.</param>
        /// <param name="onDisplayed">Called on Unity's thread once the step displays.</param>
        public AdChain Then(AdFullScreenFormat fullScreenFormat, string placement = null, Action onDisplayed = null) {
            var format = fullScreenFormat.ToFormat();
            var reason = skip?.Invoke(fullScreenFormat) ?? AdChainSkip.None;
            if (reason is AdChainSkip.None) {
                steps.Add(new Step(format, placement, onDisplayed));
                return this;
            }

            if (skipped is AdChainSkip.None) skipped = reason;

            // Suppression takes the ad away, not the reward.
            if (reason is AdChainSkip.Suppressed && format.IsRewarding()) rewardedWithoutAd = true;
            return this;
        }

        public AdChain ThenIf(bool condition, AdFullScreenFormat format, string placement = null, Action onDisplayed = null) {
            return condition ? Then(format, placement, onDisplayed) : this;
        }

        public UniTask<AdChainResult> Run() {
            if (ran) throw new InvalidOperationException($"An {nameof(AdChain)} runs once");

            ran = true;
            return new Runner(host, steps, rewardedWithoutAd).Start(skipped);
        }

        private readonly struct Step {
            public readonly AdFormat Format;
            public readonly string   Placement;
            public readonly Action   OnDisplayed;

            public Step(AdFormat format, string placement, Action onDisplayed) {
                Format      = format;
                Placement   = placement;
                OnDisplayed = onDisplayed;
            }
        }

        private sealed class Runner {
            private readonly IAdChainHost                          host;
            private readonly List<Step>                            steps;
            private readonly IReadOnlyList<IAdMediationFullScreen>[] candidates;
            private readonly UniTaskCompletionSource<AdChainResult> completion = new();
            private readonly bool                                    rewardedWithoutAd;

            private bool usesCover;
            private int  displayed;
            private int  rewarded;
            private int  finished;

            public Runner(IAdChainHost host, List<Step> steps, bool rewardedWithoutAd) {
                this.host              = host;
                this.steps             = new List<Step>(steps);
                this.rewardedWithoutAd = rewardedWithoutAd;
                candidates             = new IReadOnlyList<IAdMediationFullScreen>[steps.Count];
            }

            /// <param name="skipped">Why the first step the rules held back was, to tell when no step is left.</param>
            public UniTask<AdChainResult> Start(AdChainSkip skipped) {
                if (steps.Count == 0) return UniTask.FromResult(new AdChainResult(0, rewardedWithoutAd, skipped is AdChainSkip.None ? AdChainSkip.NoStep : skipped));

                for (var i = 0; i < steps.Count; ++i) candidates[i] = host.FullScreenCandidates(steps[i].Format);

                var first = FirstReady(0, follower: false);
                if (first == null) {
                    Debug.Log($"{nameof(AdChain)}: {steps[0].Format} not ready, nothing shows");
                    return UniTask.FromResult(new AdChainResult(0, rewardedWithoutAd, AdChainSkip.NotReady));
                }

                usesCover = first.CallsBackOffLoop(steps[0].Format);
                if (!usesCover && steps.Count > 1) {
                    // Its close callback would wait for the player loop a cover stops, so nothing may follow it.
                    Debug.LogWarning($"{nameof(AdChain)}: {first.Id} {steps[0].Format} does not call back off Unity's thread, the steps after it are dropped");
                    steps.RemoveRange(1, steps.Count - 1);
                }

                host.BeginFullScreen();
                if (usesCover) FullScreenCover.Show(host.CoverColor);

                ShowStep(0, first);
                return completion.Task;
            }

            private IAdMediationFullScreen FirstReady(int index, bool follower) {
                var format = steps[index].Format;
                foreach (var mediation in candidates[index]) {
                    if (!mediation.IsInitialized) continue;
                    if (follower && !(mediation.ShowsFromAnyThread(format) && mediation.CallsBackOffLoop(format))) continue;
                    if (!mediation.IsReady(format)) continue;

                    return mediation;
                }

                return null;
            }

            private void ShowStep(int index, IAdMediationFullScreen mediation) {
                var listener = new StepListener(this, index);
                try {
                    mediation.Show(steps[index].Format, steps[index].Placement, listener);
                } catch (Exception exception) {
                    Debug.LogException(exception);
                    listener.OnFailed(new AdError(-1, exception.Message));
                }
            }

            // Any thread: a step's end may come while Unity sleeps under the cover.
            private void Next(int index) {
                for (var next = index + 1; next < steps.Count; ++next) {
                    var mediation = FirstReady(next, follower: true);
                    if (mediation != null) {
                        ShowStep(next, mediation);
                        return;
                    }

                    Debug.Log($"{nameof(AdChain)}: {steps[next].Format} not ready, skipped");
                }

                Finish();
            }

            private void Finish() {
                if (Interlocked.Exchange(ref finished, 1) != 0) return;

                if (usesCover) FullScreenCover.Hide();
                host.EndFullScreen();

                var result = new AdChainResult(Volatile.Read(ref displayed), rewardedWithoutAd || Volatile.Read(ref rewarded) != 0, AdChainSkip.None);
                UniTask.Post(() => completion.TrySetResult(result));
            }

            private sealed class StepListener : IAdShowListener {
                private readonly Runner runner;
                private readonly int    index;
                private          int    ended;

                public StepListener(Runner runner, int index) {
                    this.runner = runner;
                    this.index  = index;
                }

                public void OnDisplayed() {
                    Interlocked.Increment(ref runner.displayed);

                    var step = runner.steps[index];
                    var host = runner.host;
                    UniTask.Post(() => {
                        host.Displayed(step.Format, step.Placement);
                        step.OnDisplayed?.Invoke();
                    });
                }

                public void OnRewarded() {
                    Volatile.Write(ref runner.rewarded, 1);
                }

                public void OnClosed() {
                    End();
                }

                public void OnFailed(in AdError error) {
                    Debug.LogWarning($"{nameof(AdChain)}: {runner.steps[index].Format} failed ({error})");
                    End();
                }

                private void End() {
                    if (Interlocked.Exchange(ref ended, 1) != 0) return;

                    try {
                        runner.Next(index);
                    } catch (Exception exception) {
                        // Whatever broke, the cover still has to come down.
                        Debug.LogException(exception);
                        runner.Finish();
                    }
                }
            }
        }
    }
}