using System;

namespace RiseOn.Ads {
    /// <summary>
    /// The mediations list as a section like the page's others: a row for every mediation in the project, which the page adopts, and one for every
    /// adapter of the pack's not installed; each adapter's row has its version, Install, Update and Uninstall, as a network's row.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class MediationListAttribute : Attribute { }
}