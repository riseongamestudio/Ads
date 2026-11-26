# RiseOn.Ads.LevelPlay

Mediation Unity LevelPlay cho `com.riseon.ads`.

Package `com.riseon.ads.levelplay`, namespace `RiseOn.Ads.LevelPlay`, mediation id
`levelplay`.

## Mục lục

- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Định dạng](#định-dạng)
- [Những điều cần biết](#những-điều-cần-biết)
- [Setting của SDK](#setting-của-sdk)
- [Network](#network)
- [Lịch sử thay đổi](#lịch-sử-thay-đổi)
- [Giấy phép](#giấy-phép)

## Yêu cầu

| Phụ thuộc | Cách có | Dùng cho |
|---|---|---|
| Unity 6000.3 | | Bản đang dùng để phát triển |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Các mục của LevelPlay trên trang Ads |
| `com.riseon.ads` 3.0.0 | Tự cài theo `package.json` | Lõi quảng cáo mà adapter cắm vào |
| `com.riseon.utils` 2.0.0 | Tự cài theo `package.json` | Các mục của LevelPlay trên trang Ads |
| `com.unity.services.levelplay` 9.5.1 | Tự cài theo `package.json`, từ Unity Registry (tên hiển thị "Ads Mediation") | LevelPlay SDK |

"Tự cài" là Package Manager tự lấy theo version từ Unity Registry và các registry khai
trong manifest, cả khi cài pack bằng git URL. Odin không có trên UPM nên phải cài vào
project trước. Thiếu Odin thì project báo một lỗi từ
`RiseOn.Ads.LevelPlay.Requirements`.

## Cài đặt

**Registry RiseOn** (khuyên dùng): thêm registry RiseOn với scope `com.riseon`, và
OpenUPM với scope `com.cysharp`, `com.google` (phụ thuộc của `com.riseon.ads`), vào
`Packages/manifest.json`, rồi thêm package:

```json
{
  "scopedRegistries": [
    {
      "name": "RiseOn",
      "url": "https://upm.riseongamestudio.com",
      "scopes": ["com.riseon"]
    },
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.cysharp", "com.google"]
    }
  ],
  "dependencies": {
    "com.riseon.ads.levelplay": "1.1.0"
  }
}
```

**Git URL**: phụ thuộc vẫn lấy theo version từ registry, nên manifest cần OpenUPM như ví
dụ trên; `com.riseon.utils`, `com.riseon.consent`, `com.riseon.serializables` và
`com.riseon.ads` lấy từ registry RiseOn, hoặc cài bằng git URL trước theo thứ tự này.
Rồi *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Ads.git?path=/LevelPlay
```

**Thư mục local**: `"com.riseon.ads.levelplay": "file:D:/path/to/Ads/LevelPlay"`.

## Định dạng

`Banner` (adaptive hoặc 320x50), `MRec` (banner cỡ 300x250), `Interstitial`,
`Rewarded`. App key và ad unit id riêng cho Android và iOS.

## Những điều cần biết

- API LevelPlay của Unity đưa mọi sự kiện qua player loop, mà màn che dưới ad
  full-screen dừng player loop. Interstitial và rewarded trên Android và iOS vì vậy đi
  qua cầu nối của pack tới API native của LevelPlay
  (`Plugins/Android/RiseOnAdsLevelPlay.androidlib`,
  `Plugins/iOS/ROLevelPlayFullScreenAd.m`): sự kiện đến theo thứ tự trên thread riêng
  của cầu nối (iOS: hàng đợi tuần tự), nên ad có màn che và nối được với các bước khác
  trong `AdChain`. Banner và MRec vẫn dùng API của Unity.
- Placement game truyền khi hiện ad chỉ đi cùng `AdImpression.Placement`; pack hiện ad
  với placement mặc định của LevelPlay. Các placement khác trên dashboard LevelPlay (và
  capping của chúng) không áp dụng; capping đặt cho placement mặc định vẫn áp, nên để
  trống để giãn cách chỉ do lớp manager quyết.
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

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).