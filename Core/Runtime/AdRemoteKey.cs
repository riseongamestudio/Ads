using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A key of the Remote Config pack, by its number: a string key whose value replaces a format's mediations at launch, ids split by commas, best first; or a key
    /// of a manager setting's type, whose value replaces the setting.<br/>
    /// Kept as a number rather than the pack's enum, so the settings load the same whether that pack is installed or not.
    /// </summary>
    [Serializable]
    internal struct AdRemoteKey {
        [SerializeField]
        private int id;

        /// <summary>The key's number in <c>RemoteKey</c>; 0 for none, which keeps the page's value.</summary>
        public int Id { get => id; set => id = value; }
    }
}