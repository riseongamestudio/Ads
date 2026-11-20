using UnityEngine;

namespace RiseOn.Ads.AdMob.Native {
    internal interface IRectAdClient {
        void SetSize(Vector2Int sizePx, int roundCornerPx);
        void SetPosition(Vector2Int positionPx);
        void Show();
        void Hide();
        void Release();
    }
}