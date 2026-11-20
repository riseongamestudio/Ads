namespace RiseOn.Ads {
    /// <summary>The revenue one ad impression earned, as its network reported it.</summary>
    public readonly struct AdImpression {
        public readonly AdFormat Format;

        /// <summary>The <see cref="IAdMediation.Id"/> of the mediation that served it, e.g. "admob".</summary>
        public readonly string Mediation;

        /// <summary>The network that won the impression, as the mediation names it.</summary>
        public readonly string Network;

        public readonly string UnitId;

        /// <summary>In <see cref="Currency"/>, not micros.</summary>
        public readonly double Value;

        public readonly string Currency;

        public AdImpression(AdFormat format, string mediation, string network, string unitId, double value, string currency) {
            Format    = format;
            Mediation = mediation;
            Network   = network;
            UnitId    = unitId;
            Value     = value;
            Currency  = currency;
        }

        public override string ToString() {
            return $"{Mediation}/{Network} {Format} {UnitId}: {Value} {Currency}";
        }
    }
}