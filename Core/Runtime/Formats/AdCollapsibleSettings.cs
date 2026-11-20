using System;
using System.Collections.Generic;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// How the native collapsible shows, whichever mediation serves it, on both platforms; how it closes comes with each show.<br/>
    /// The pacing and the plans are the game's manager's, for <see cref="AdsManagerBase.ShowCollapsibleAsync"/>.
    /// </summary>
    [Serializable]
    public sealed class AdCollapsibleSettings {
        // No network gives a figure; 0 leaves the pacing to the game until it picks one.
        [SerializeField]
        // What most of the studio's games run in Remote Config: the first at once, then 15 seconds apart.
        private AdPacingSettings pacing = new(0, 15);

        [SerializeField, PropertyRange(0, 1)]
        [Tooltip("The part of the screen's height the panel covers, from the bottom.")]
        private float heightRatio = .5f;

        [SerializeField]
        [Tooltip("The panel's backdrop where the creative does not reach, alpha included.")]
        private Color backgroundColor = Color.black;

        // Get In The Bowl's three plans; only the first draws until the others' weights are raised, on the page or from Remote Config.
        [SerializeField, SectionList(Item = "plan", Foldable = true)]
        [Tooltip("The ways ShowCollapsibleAsync may run: each show draws one, each plan's chance its weight over all the weights.")]
        private AdCollapsiblePlan[] plans = {
            new("Plain", .6f, new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose })
          , new("Chained", .3f
              , new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Right, TimerSide = AdTimerSide.Right }
              , new AdCloseSettings { CloseSide = AdCloseSide.Left, TimerSide = AdTimerSide.Left })
          , new("Redirect", .1f, new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Right, TimerSide = AdTimerSide.Right, RedirectOnClose = true })
        };

        public AdPacingSettings Pacing => pacing;
        public float HeightRatio => Mathf.Clamp01(heightRatio);
        public Color BackgroundColor => backgroundColor;
        public IReadOnlyList<AdCollapsiblePlan> Plans => plans;

        internal IEnumerable<(string Setting, int KeyId, Type Type)> RemoteKeyUses {
            get {
                foreach (var plan in plans) {
                    if (plan != null) yield return ($"Native Collapsible plan {plan.Name}", plan.WeightKeyId, typeof(float));
                }
            }
        }
    }
}