using System;
using System.Collections.Generic;
using RiseOn.Serializables;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>The page's Formats section: for each format, the mediations that serve it, the Remote Config key that may replace them, and how it shows.</summary>
    [Serializable]
    internal sealed class AdFormatSetup {
        [SerializeField]
        private EnumMap<AdFormat, AdPlatformValue<AdPriorityList>> priorities = NewPriorities();

        // One key for every platform: a Remote Config condition tells the platforms apart.
        [SerializeField]
        private EnumMap<AdFormat, AdRemoteKey> remoteKeys = new();

        [SerializeField]
        private AdFormatSettings settings = new();

        public EnumMap<AdFormat, AdPlatformValue<AdPriorityList>> Priorities => priorities;
        public EnumMap<AdFormat, AdRemoteKey> RemoteKeys => remoteKeys;
        public AdFormatSettings Settings => settings;

        private static EnumMap<AdFormat, AdPlatformValue<AdPriorityList>> NewPriorities() {
            var map = new EnumMap<AdFormat, AdPlatformValue<AdPriorityList>>();
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat))) map[format] = new AdPlatformValue<AdPriorityList>(new AdPriorityList());
            return map;
        }
    }
}