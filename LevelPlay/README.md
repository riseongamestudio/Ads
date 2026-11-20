# RiseOn Ads LevelPlay

Mediation Unity LevelPlay cho `com.riseon.ads`.

Package `com.riseon.ads.levelplay`, namespace `RiseOn.Ads.LevelPlay`, mediation id
`levelplay`.

## Yêu cầu

| Phụ thuộc | Cách có |
|---|---|
| `com.riseon.ads` | Tự cài theo `package.json` |
| `com.unity.services.levelplay` 9.5.1 | Tự cài theo `package.json` (Unity Registry, tên hiển thị "Ads Mediation") |

## Định dạng

`Banner` (adaptive hoặc 320x50), `MRec` (banner cỡ 300x250), `Interstitial`,
`Rewarded`. App key và ad unit id riêng cho Android và iOS.

## Những điều cần biết

- API LevelPlay của Unity đưa mọi sự kiện qua player loop, mà màn che dưới ad
  full-screen dừng player loop. Interstitial và rewarded trên Android và iOS vì vậy
  đi qua cầu nối của pack tới API native của LevelPlay
  (`Plugins/Android/RiseOnAdsLevelPlay.androidlib`, `Plugins/iOS/ROLevelPlayFullScreenAd.m`):
  sự kiện đến theo thứ tự trên thread riêng của cầu nối (iOS: hàng đợi tuần tự), nên
  ad có màn che và nối được với các bước khác trong `AdChain`. Banner và MRec vẫn dùng
  API của Unity.
- LevelPlay không đảm bảo thưởng đến trước sự kiện đóng. Cầu nối giữ sự kiện đóng
  của rewarded tối đa 1 giây chờ thưởng.
- Unity dừng dưới interstitial và rewarded nhờ màn che của pack. Cờ
  `LevelPlay.SetPauseGame` không dùng: LevelPlay chỉ đọc nó trong delegate của API
  Unity, mà cầu nối thay delegate đó.
- Consent: `SetGDPRConsent` khi GDPR áp dụng, `SetCCPA` theo do-not-sell,
  `SetCOPPA` theo cài đặt trẻ em, tất cả trước `LevelPlay.Init`. LevelPlay cũng tự
  đọc chuỗi TCF UMP đã lưu.
- Doanh thu LevelPlay báo bằng USD.
- Test Suite: LevelPlay chỉ mở nó khi `SetMetaData("is_test_suite", "enable")` chạy
  trước `LevelPlay.Init`. Nút Test Suite trong
  [`AdsDebugger`](../Core/Runtime/Debugger/README.md) lần đầu trên một máy chỉ lưu cờ
  `RiseOn.Ads.LevelPlay.TestSuite` vào `PlayerPrefs`; lần mở app sau, pack đặt
  metadata đó trước `Init` và nút mở Test Suite.

## Setting của SDK

Dưới mediation trên trang **RiseOn > Ads** hiện, đọc và ghi thẳng vào hai file settings
của LevelPlay:

| Mục | Ý nghĩa |
|---|---|
| AD_ID Permission | Thêm quyền AD_ID vào manifest Android; thiếu nó thì Android 13 trở lên không cho advertising id |
| Adapter Debug, Integration Helper | "Enable Adapter Debug" và "Enable Integration Helper" của LevelPlay |
| AdMob Network | LevelPlay mediate AdMob ("AdMob Integration"). Bật thì hiện thêm **AdMob App ID**: chính là ô ở trang **RiseOn > Consent**, sửa ở đâu cũng là một bản, và pack Consent ghi nó vào file của LevelPlay |

Tự khởi tạo của LevelPlay và app key của nó không hiện, vì mediation khởi tạo LevelPlay
bằng app key của mediation.

## Network

Mục con **Networks** của mediation (gập sẵn) có nút ↗ trên thanh, mở Network
Manager của LevelPlay (**Ads Mediation > Network Manager**). Cài và cập nhật adapter
của LevelPlay làm trong cửa sổ đó: LevelPlay không cho package khác gọi phần cài
adapter của nó.

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).