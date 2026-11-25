using System;
using System.Collections.Generic;
using RiseOn.Utils;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A tool for the game's collapsible: one way to run it, its steps one after the other, each closing as its settings say, for <see cref="AdsSdk.ShowCollapsibleAsync"/>.<br/>
    /// A list of them in the game's manager, edited on the page, is drawn from with <see cref="Pick"/>, each plan's chance its weight over all the weights.
    /// </summary>
    [Serializable]
    public sealed class AdCollapsiblePlan {
        [SerializeField, Tooltip("The plan's name, which tells it apart on the page and in the build's check of Remote Config keys.")]
        private string name;

        [SerializeField, Tooltip("The plan's share of the draw: its chance is its weight over all the plans' weights; 0 never draws it.")]
        private AdRemoteSetting<float> weight = new(1);

        [SerializeField, SectionList(Item = "step", TitleField = nameof(name), BarField = nameof(weight)), Tooltip("The collapsibles of one show, one after the other: how each one's close button behaves.")]
        private AdCloseSettings[] steps = { new() { Cooldown = 3, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose } };

        public AdCollapsiblePlan() { }

        public AdCollapsiblePlan(string name, float weight, params AdCloseSettings[] steps) {
            this.name   = name;
            this.weight = new AdRemoteSetting<float>(weight);
            this.steps  = steps;
        }

        public string Name => name;
        public float Weight => weight.Value;
        public AdCloseSettings[] Steps => steps;

        /// <summary>
        /// A plan with steps, each one's chance its weight over all the weights; none when no plan has steps and a weight above 0.
        /// </summary>
        public static AdCollapsiblePlan Pick(IReadOnlyList<AdCollapsiblePlan> plans) {
            var total = 0f;
            if (plans != null) {
                foreach (var plan in plans) {
                    if (IsDrawable(plan)) total += plan.Weight;
                }
            }

            if (total <= 0) return null;

            var draw = UnityEngine.Random.value * total;
            AdCollapsiblePlan last = null;
            foreach (var plan in plans) {
                if (!IsDrawable(plan)) continue;

                last =  plan;
                draw -= plan.Weight;
                if (draw < 0) break;
            }

            return last;

            static bool IsDrawable(AdCollapsiblePlan plan) => plan is { Steps: { Length: > 0 } } && plan.Weight > 0;
        }
    }
}