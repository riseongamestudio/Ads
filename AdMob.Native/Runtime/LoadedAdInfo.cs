namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// What a successful load has to say for itself.<br/>
    /// It arrives with <c>OnAdLoaded</c> so a caller can log or measure the fill without asking the ad anything afterwards - by then the numbers have already moved on, because a load that leaves room in the cache starts the next one immediately.<br/>
    /// The load is the unit's pool's, shared by every format on the same ad unit id and mute policy: each of them hears it, and any of them may take the ad.
    /// </summary>
    public readonly struct LoadedAdInfo {
        /// <summary>Warm ads in the pool after this load landed, not counting those a format already took.</summary>
        public int CachedCount { get; }

        /// <summary>How many the pool keeps warm when it is full: the most any of its formats wants.</summary>
        public int CacheSize { get; }

        internal LoadedAdInfo(int cachedCount, int cacheSize) {
            CachedCount = cachedCount;
            CacheSize = cacheSize;
        }
    }
}