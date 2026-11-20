namespace RiseOn.Ads {
    public interface IAdMediationBanner : IAdMediation {
        void ShowBanner();
        void HideBanner();
    }

    public interface IAdMediationMRec : IAdMediation {
        void ShowMRec();
        void HideMRec();
    }
}