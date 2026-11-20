# RiseOn Ads AdMob Native

Native ad của AdMob hiển thị theo ba kiểu: toàn màn hình, nửa màn hình
(collapsible) và ô hình chữ nhật game đặt chỗ (ô trong danh sách, MRec). Chạy trên
Android và iOS, có bản xem trước trong Editor.

Package `com.riseon.ads.admob.native`, namespace `RiseOn.Ads.AdMob.Native`. Pack
đứng riêng: không phụ thuộc package quảng cáo nào khác ngoài plugin Google Mobile
Ads, và không biết gì về thứ được vẽ trên dưới nó ngoài bảng
[thứ tự lớp](#thứ-tự-lớp).

## Mục lục

- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Tổng quan](#tổng-quan)
- [Hướng dẫn nhanh](#hướng-dẫn-nhanh)
  - [Tạo placement](#tạo-placement)
  - [Nửa màn hình: chiều cao và trượt](#nửa-màn-hình-chiều-cao-và-trượt)
  - [Pause](#pause)
  - [Callback đến trên thread native](#callback-đến-trên-thread-native)
  - [Ô hình chữ nhật](#ô-hình-chữ-nhật)
  - [Trong Editor](#trong-editor)
- [Thứ tự lớp](#thứ-tự-lớp)
- [Tài liệu chi tiết](#tài-liệu-chi-tiết)
- [Lịch sử thay đổi](#lịch-sử-thay-đổi)
- [Giấy phép](#giấy-phép)

## Yêu cầu

| Phụ thuộc | Cách có | Dùng cho |
|---|---|---|
| Unity 6000.3 | | Bản đang dùng để phát triển |
| `com.google.ads.mobile` 11.5.0 | Tự cài theo `package.json` (trên OpenUPM cần scope `com.google`) | Plugin Google Mobile Ads cho Unity. Plugin này mang theo SDK native (Android `play-services-ads`, iOS pod `Google-Mobile-Ads-SDK`) |
| `com.unity.ugui` 2.0.0 | Tự cài theo `package.json` | Bản xem trước trong Editor |

## Cài đặt

**Registry RiseOn**: thêm registry RiseOn với scope `com.riseon`, và registry OpenUPM với scope `com.google` (để
kéo được plugin Google Mobile Ads) vào `Packages/manifest.json`, rồi thêm
package:

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
      "scopes": ["com.google"]
    }
  ],
  "dependencies": {
    "com.riseon.ads.admob.native": "1.0.0"
  }
}
```

**Git URL**: *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Ads.git?path=/AdMob.Native
```

Cài bằng git thì plugin Google Mobile Ads vẫn phải lấy được từ một registry (vd.
OpenUPM như trên), vì Package Manager không tự kéo phụ thuộc qua git.

**Thư mục local** (khi đang sửa pack): `"com.riseon.ads.admob.native":
"file:<đường dẫn repo>/AdMob.Native"`.

## Tổng quan

| Kiểu | Lớp | Dùng cho |
|---|---|---|
| Toàn màn hình | `FullScreenAd` | App open, interstitial, end card |
| Nửa màn hình | `HalfScreenAd` | Collapsible ở nửa dưới màn hình; `HeightRatio` là phần bị che |
| Ô hình chữ nhật | `RectAd` | Một ô game đặt chỗ và cỡ: một ô trong danh sách (in-feed), một khối như MRec; mỗi ô một instance |

Ad được vẽ bằng view native của hệ điều hành, đè lên Unity. Assembly core giữ API
và máy trạng thái; ba assembly nền tảng (Android, iOS, Editor) mỗi cái chỉ biên
dịch cho nền tảng của nó và tự cắm vào core lúc khởi động. Pack chỉ lo nạp và
hiển thị; chính sách của game (gói bỏ quảng cáo, tần suất, remote config) nằm ở
phía game, trước khi gọi pack.

## Hướng dẫn nhanh

### Tạo placement

Mỗi placement toàn màn hình hay nửa màn hình là một instance sống trọn đời
app, tạo một lần lúc khởi động, sau khi Google Mobile Ads đã khởi tạo:

```csharp
using RiseOn.Ads.AdMob.Native;
using UnityEngine;

var interstitial = new FullScreenAd(new FullScreenAd.Settings {
    AdUnitId        = "ca-app-pub-xxxxxxxx/yyyyyyyy",
    BackgroundColor = Color.black,
    Format          = "interstitial",                 // tên placement, đi kèm mọi OnAdPaid
    Close           = new CloseSettings { Cooldown = 3 },
});
interstitial.OnAdPaid   += info => LogRevenue(info.Format, info.Value, info.Currency);
interstitial.OnAdHidden += OnInterstitialClosed;
interstitial.Load();      // gọi một lần; pack tự nạp tiếp và tự thử lại

// khi cần hiện
if (interstitial.IsReady()) interstitial.Show();
```

- Kết quả hiển thị về qua `OnAdDisplayed`, `OnAdDisplayFailed`, `OnAdHidden`.
- Placement nào hiện ad thứ hai ngay khi ad đầu đóng thì đặt `CacheSize = 2`.
- `CloseSettings` quyết nút đóng: thời gian chờ (`Cooldown`), cạnh đặt nút
  (`CloseSide`), vị trí đồng hồ (`TimerSide`), bấm đóng có mở quảng cáo không
  (`RedirectOnClose`); đổi sau được bằng `SetClose`.

### Nửa màn hình: chiều cao và trượt

```csharp
var collapsible = new HalfScreenAd(new HalfScreenAd.Settings {
    AdUnitId    = "ca-app-pub-xxxxxxxx/wwwwwwww",
    HeightRatio = .5f,
    BackgroundColor = Color.black,
});

float height = collapsible.PanelHeight;   // có ngay, trước cả khi nạp ad
collapsible.Show(slideIn: true, slideOutOnClose: true);
collapsible.Hide(slideOut: true);         // ẩn bằng code
```

- `PanelHeight` là chiều cao panel theo **đơn vị của nền tảng**: pixel trên
  Android, point trên iOS, pixel `Screen` trong Editor. Nó chỉ phụ thuộc
  `HeightRatio` và màn hình (sàn 48dp/pt), không phụ thuộc creative, nên thứ gì
  phải đứng khít sau panel thì lấy đúng số này, đừng tự tính lại.
- `slideIn`: panel trượt lên từ mép dưới. `OnAdDisplayed` bắn khi trượt xong,
  đồng hồ của nút đóng bắt đầu đếm từ lúc đó; trong lúc trượt mọi chạm lên panel
  bị nuốt.
- `slideOutOnClose`: người chơi bấm đóng thì panel trượt xuống, trượt xong mới
  bắn `OnAdHidden`. Đóng kiểu `RedirectOnClose` luôn ẩn ngay, không trượt.
- Panel không đổi kích thước khi trượt, chỉ dịch nội dung; mép màn hình cắt phần
  đã ra ngoài.

### Pause

Ad toàn màn hình cư xử như mọi SDK quảng cáo: trên Android nó là một Activity
nên hệ thống tự pause Unity; trên iOS nó gọi `UnityPause(1)` khi hiện và
`UnityPause(0)` khi đóng. `UnityPause` là cờ chứ không đếm, nên thứ khác muốn giữ
game dừng xuyên qua ad thì phải tự giữ pause của mình và đặt lại sau khi ad đóng.
Bản xem trước trong Editor **không** đổi `Time.timeScale`. Ad nửa màn hình và ô
hình chữ nhật không pause gì.

### Callback đến trên thread native

Mọi event của pack đến trên thread native, **không** phải thread Unity. Việc gì
đụng tới Unity trong handler phải tự đưa về main thread (`UniTask.Post`,
`SynchronizationContext`...). Giá trị cần đọc trong callback thì tính sẵn từ
trước, đừng để nó được ghi qua một lệnh post sang main thread.

### Ô hình chữ nhật

```csharp
var cell = new RectAd(new RectAd.Settings {
    AdUnitId  = "ca-app-pub-xxxxxxxx/zzzzzzzz",
    Format    = "in_feed",                                         // tên placement, đi kèm mọi OnAdPaid
    CacheSize = 2,                                                  // ad nhờ kho giữ sẵn
});

cell.SetSize(new Vector2Int(widthPx, heightPx), roundCornerPx: 24);  // khi đã đo được ô, gọi lại khi ô đổi cỡ
cell.SetPosition(new Vector2Int(xPx, yPxFromTop));                   // tọa độ pixel, gốc trên-trái
cell.Show();
cell.Hide();                                                          // khi ô rời màn hình
cell.Dispose();                                                       // khi chỗ đó không còn
```

- Ô tự nạp từ lúc tạo, kể cả khi chưa đặt cỡ; tạo sớm để ad kịp sẵn trước khi ô
  hiện. Các ô cùng ad unit dùng chung một kho, giữ số lớn nhất trong các
  `CacheSize` của chúng: màn hiện ba ô cùng lúc thì đặt `CacheSize = 3`.
- Ô ẩn không giữ ad chưa hiện: `Hide` trả ad đó về kho cho ô khác; ad vừa hiện
  chưa đủ 4s thì ở lại cho lần hiện sau của chính ô đó.
- Phải `SetSize` trước `SetPosition` và `Show`; `SetSize` lần nữa thì ô ẩn đi, gọi
  `Show` lại để hiện theo cỡ mới.
- Chỉ show khi ô nằm trọn trong khung nhìn. Pack không vẽ gì khi chưa có ad, nên
  giữ một placeholder của game bên dưới ô.
- Cách đo ô từ uGUI và đổi bán kính bo góc sang pixel:
  [tài liệu core, mục A11](Runtime/README.md#a11-tích-hợp).

### Trong Editor

Editor vẽ bản xem trước bằng uGUI, mỗi lớp một canvas Screen Space Overlay với
sorting order lấy từ `NativeAdEditorLayers` (xem [thứ tự lớp](#thứ-tự-lớp)). Canvas
của game phải nằm dưới số nhỏ nhất trong đó. Chi tiết:
[Runtime/Platforms/Editor/README.md](Runtime/Platforms/Editor/README.md).

## Thứ tự lớp

Nhiều package cùng vẽ lên Unity (pack này, màn che của một package khác, banner
của SDK khác...). Không package nào gọi package nào; thứ tự giữ bằng một bảng
chung, mỗi package tự đặt lớp của mình theo bảng:

| Lớp | iOS: bậc trong `layer.name` | Editor: `NativeAdEditorLayers` | Android |
|---|---|---|---|
| Ad toàn màn hình (pack này) | không có bậc: controller được present luôn trên mọi subview | `FullScreen` | Activity |
| Màn che toàn màn hình | `3000` | bên hiện ad đặt, giữa `FullScreen` và `HalfScreen` | Activity |
| Ad nửa màn hình (pack này) | `2000` | `HalfScreen` | window `TYPE_APPLICATION_SUB_PANEL` |
| Màn che nửa màn hình | `1000` | bên hiện ad đặt, giữa `HalfScreen` và `RectAd` | window `TYPE_APPLICATION_SUB_PANEL` |
| View không đánh dấu (vd. banner của SDK khác) | `0` | bên hiện ad đặt, dưới `RectAd` | |
| Ô hình chữ nhật (pack này) | `-1000` | `RectAd` | window `TYPE_APPLICATION_PANEL` |

- **iOS**: mọi lớp là subview trực tiếp của view Unity. View đặt
  `layer.name = @"riseon.layer.<bậc>"` rồi chèn ngay dưới subview đầu tiên (đếm
  từ dưới lên) có bậc **cao hơn**; không có thì `addSubview`. View không đánh
  dấu tính là bậc `0`. Cùng bậc thì cái đến sau nằm trên. Mỗi package tự viết
  hàm chèn của mình (ở đây là `RANLayerOrder.h`).
- **Android**: window xếp theo type rồi theo thứ tự add, không có API chèn. Hai
  lớp nửa màn hình cùng type, nên window nào muốn nằm dưới ad nửa màn hình phải
  được **add trước** khi ad `Show`, và từ đó tới khi gỡ chỉ được ẩn/hiện, không
  được add lại. Hợp đồng này do bên điều phối giữ.
- **Editor**: ba lớp của pack lấy sorting order từ field cùng tên của
  `NativeAdEditorLayers`, mặc định `short.MaxValue`, `short.MaxValue - 2`,
  `short.MaxValue - 4`: trên game vẽ từ 0 trở lên. Bên hiện ad đặt lại ba số trước
  khi tạo ad, để màn che của nó chen vào giữa (mediation `com.riseon.ads.admob` đặt
  theo **Editor Sorting Order** trên trang Ads).

## Tài liệu chi tiết

| Tài liệu | Nội dung |
|---|---|
| [Runtime/README.md](Runtime/README.md) | Kiến trúc, vòng đời ad, engine layout, template, luật chung (cache, xếp lớp, pause, layout, media, AdChoices...), quy trình và sổ sự cố |
| [Runtime/Platforms/Android/README.md](Runtime/Platforms/Android/README.md) | Riêng Android: Activity, window type, luồng giao sự kiện, cổng javac, adb |
| [Runtime/Platforms/iOS/README.md](Runtime/Platforms/iOS/README.md) | Riêng iOS: bảng đối chiếu file, quy ước pt/px, subview và bậc lớp, UnityPause |
| [Runtime/Platforms/Editor/README.md](Runtime/Platforms/Editor/README.md) | Bản xem trước trong Editor: ảnh chụp thay vì mô phỏng, sorting order |

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).