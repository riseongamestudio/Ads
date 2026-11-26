# RiseOn Ads

Bộ package quảng cáo của RiseOn cho Unity. Mỗi thư mục là một package UPM riêng,
cài và đánh version độc lập.

| Thư mục | Package | Nội dung |
|---|---|---|
| [Core](Core/README.md) | `com.riseon.ads` | `AdsSdk` tự khởi động theo Project Settings, lớp manager của game (`IAdsManager`), không có thì `DefaultAdsManager`, interface mediation, danh sách ưu tiên (tại chỗ hoặc từ Remote Config), khởi động sau luồng consent của `com.riseon.consent`, chuỗi full-screen, chuỗi collapsible, màn che native, ad giả trong Editor |
| [AdMob](AdMob/README.md) | `com.riseon.ads.admob` | Mediation AdMob: banner, MRec, interstitial, rewarded, rewarded interstitial, app open và các native ad của `com.riseon.ads.admob.native` |
| [AdMob.Native](AdMob.Native/README.md) | `com.riseon.ads.admob.native` | Native ad của AdMob: full-screen, half-screen (collapsible), ô hình chữ nhật game đặt chỗ (in-feed, MRec). Đứng riêng, không biết core |
| [Max](Max/README.md) | `com.riseon.ads.max` | Mediation AppLovin MAX: banner, MRec, interstitial, rewarded, app open |
| [LevelPlay](LevelPlay/README.md) | `com.riseon.ads.levelplay` | Mediation Unity LevelPlay: banner, MRec, interstitial, rewarded |

Game cài `com.riseon.ads` và adapter của những mạng nó dùng. Cài adapter là kéo
SDK của mạng đó về theo `package.json`.

## Phụ thuộc

| Package | Tự cài theo `package.json` | Phải cài tay |
|---|---|---|
| `com.riseon.ads` | `com.riseon.consent`, `com.riseon.utils`, `com.riseon.serializables`, `com.cysharp.unitask`, `com.unity.ugui` | [Odin Inspector](https://odininspector.com) |
| `com.riseon.ads.admob` | `com.riseon.ads`, `com.riseon.ads.admob.native`, `com.riseon.utils`, `com.google.ads.mobile` | |
| `com.riseon.ads.admob.native` | `com.google.ads.mobile`, `com.unity.ugui` | |
| `com.riseon.ads.max` | `com.riseon.ads`, `com.riseon.utils`, `com.applovin.mediation.ads` | |
| `com.riseon.ads.levelplay` | `com.riseon.ads`, `com.riseon.utils`, `com.unity.services.levelplay` | |

Scoped registry cần có:

| Registry | URL | Scope |
|---|---|---|
| RiseOn | `https://upm.riseongamestudio.com` | `com.riseon` |
| OpenUPM | `https://package.openupm.com` | `com.google`, `com.cysharp` |
| AppLovin MAX (chỉ khi dùng MAX) | `https://unity.packages.applovin.com/` | `com.applovin` |

## Phát hành

Tag có tiền tố tên package: `com.riseon.ads/1.0.0`, `com.riseon.ads.admob/1.0.0`...

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).