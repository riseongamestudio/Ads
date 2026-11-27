using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RiseOn.Ads {
    internal interface IAdCollapsibleHost {
        IReadOnlyList<IAdMediationNativeCollapsible> CollapsibleCandidates { get; }

        /// <summary>Hides whatever collapsible is up and returns the new run's session.</summary>
        int BeginCollapsible(IAdMediationNativeCollapsible mediation, bool usesCover);

        bool IsCurrentCollapsible(int session);

        /// <summary>How a collapsible offers to close when the show brings no close: the page's.</summary>
        AdCloseSettings CollapsibleClose { get; }

        void EndCollapsible(int session);

        /// <summary>Unity's thread, once an ad of the current run slid in or swapped in; <paramref name="first"/> for the run's first.</summary>
        void Displayed(bool first);
    }

    /// <summary>
    /// Collapsibles shown one after the other.<br/>
    /// The first slides in and the last slides out; in between each ad swaps in place over the half-screen cover, which only shows while a non-last ad is up, so nothing but the swap ever reveals it.<br/>
    /// <see cref="AdsSdk.HideCollapsible"/> ends a run early.
    /// </summary>
    public sealed class AdCollapsibleChain {
        private readonly IAdCollapsibleHost host;
        private readonly bool               off;
        private readonly List<Step>         steps = new();
        private          bool               ran;

        /// <param name="off">The collapsible is suppressed: the chain shows nothing.</param>
        internal AdCollapsibleChain(IAdCollapsibleHost host, bool off) {
            this.host = host;
            this.off  = off;
        }

        /// <summary>An ad that closes as the page's Formats section says.</summary>
        /// <param name="onDisplayed">Called on Unity's thread once this ad has slid in or swapped in.</param>
        public AdCollapsibleChain Then(Action onDisplayed = null) => Then(host.CollapsibleClose, onDisplayed);

        /// <summary>An ad that closes as <paramref name="close"/> says.</summary>
        /// <param name="onDisplayed">Called on Unity's thread once this ad has slid in or swapped in.</param>
        public AdCollapsibleChain Then(in AdCloseSettings close, Action onDisplayed = null) {
            if (!off) steps.Add(new Step(close, onDisplayed));
            return this;
        }

        /// <summary>Resolves to how many ads displayed.</summary>
        public UniTask<int> Run() {
            if (ran) throw new InvalidOperationException($"An {nameof(AdCollapsibleChain)} runs once");

            ran = true;
            return new Runner(host, steps).Start();
        }

        private readonly struct Step {
            public readonly AdCloseSettings Close;
            public readonly Action          OnDisplayed;

            public Step(AdCloseSettings close, Action onDisplayed) {
                Close       = close;
                OnDisplayed = onDisplayed;
            }
        }

        private sealed class Runner {
            private readonly IAdCollapsibleHost          host;
            private readonly List<Step>                  steps;
            private readonly UniTaskCompletionSource<int> completion = new();

            private IAdMediationNativeCollapsible mediation;
            private int                          session;
            private bool                         usesCover;
            private int                          displayed;
            private bool                         finished;

            public Runner(IAdCollapsibleHost host, List<Step> steps) {
                this.host  = host;
                this.steps = new List<Step>(steps);
            }

            public UniTask<int> Start() {
                if (steps.Count == 0) return UniTask.FromResult(0);

                foreach (var candidate in host.CollapsibleCandidates) {
                    if (!candidate.IsInitialized || !candidate.IsCollapsibleReady) continue;

                    mediation = candidate;
                    break;
                }

                if (mediation == null) {
                    Debug.Log($"{nameof(AdCollapsibleChain)}: no collapsible ready");
                    return UniTask.FromResult(0);
                }

                usesCover = steps.Count > 1;
                session   = host.BeginCollapsible(mediation, usesCover);

                // Added before the ad, hidden: an Android window added later stays above it.
                if (usesCover) HalfScreenCover.Prepare(mediation.PanelHeight);

                ShowStep(0);
                return completion.Task;
            }

            private void ShowStep(int index) {
                var listener = new StepListener(this, index);
                try {
                    mediation.ShowCollapsible(
                        close: steps[index].Close
                      , slideIn: index == 0
                      , slideOutOnClose: index == steps.Count - 1
                      , listener: listener);
                } catch (Exception exception) {
                    Debug.LogException(exception);
                    listener.OnFailed(new AdError(-1, exception.Message));
                }
            }

            // Unity's thread: the half-screen cover does not pause the game, so the swap can wait for the loop.
            private void Next(int index) {
                if (!host.IsCurrentCollapsible(session)) {
                    Finish();
                    return;
                }

                var next = index + 1;
                if (next < steps.Count && mediation.IsCollapsibleReady) {
                    ShowStep(next);
                    return;
                }

                if (next < steps.Count) Debug.Log($"{nameof(AdCollapsibleChain)}: stopped at {next}/{steps.Count}, no ad ready");
                Finish();
            }

            private void Finish() {
                if (finished) return;

                finished = true;
                if (usesCover) {
                    HalfScreenCover.Conceal();
                    HalfScreenCover.Remove();
                }

                host.EndCollapsible(session);
                completion.TrySetResult(displayed);
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
                    UniTask.Post(() => {
                        if (!runner.host.IsCurrentCollapsible(runner.session)) return;

                        ++runner.displayed;
                        runner.host.Displayed(runner.displayed == 1);

                        // The cover only shows under an ad that another will replace in place.
                        if (runner.usesCover) {
                            if (index < runner.steps.Count - 1) HalfScreenCover.Reveal();
                            else HalfScreenCover.Conceal();
                        }

                        runner.steps[index].OnDisplayed?.Invoke();
                    });
                }

                public void OnRewarded() { }

                public void OnClosed() {
                    End();
                }

                public void OnFailed(in AdError error) {
                    Debug.LogWarning($"{nameof(AdCollapsibleChain)}: ad {index} failed ({error})");
                    End();
                }

                private void End() {
                    if (System.Threading.Interlocked.Exchange(ref ended, 1) != 0) return;

                    UniTask.Post(() => runner.Next(index));
                }
            }
        }
    }
}