using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// The mediations a format is served by, best first; an empty list turns the format off.<br/>
    /// Items are <see cref="IAdMediation.Id"/>s, so remote config can name them too.
    /// </summary>
    [Serializable, InlineProperty]
    internal class AdPriorityList {
        [SerializeField]
        private List<string> mediationIds = new();

        public IReadOnlyList<string> MediationIds => mediationIds;

        // The Editor's drawer edits the list in place.
        internal List<string> EditableIds => mediationIds;

        public AdPriorityList() { }

        public AdPriorityList(params string[] mediationIds) {
            this.mediationIds = new List<string>(mediationIds);
        }
    }
}