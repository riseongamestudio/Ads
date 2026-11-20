using System;
using RiseOn.Utils;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// One way <see cref="AdsManagerBase.ShowCollapsibleAsync"/> may run the collapsible: its steps one after the other, each closing as its settings say.<br/>
    /// Each show draws a plan, each plan's chance its weight over all the weights.
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

        internal int WeightKeyId => weight.KeyId;
    }
}