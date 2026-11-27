# RiseOn.Ads.AdMob

Mediation Google AdMob cho `com.riseon.ads`.

Package `com.riseon.ads.admob`, namespace `RiseOn.Ads.AdMob`, mediation id `admob`.

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
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Các mục của AdMob trên trang Ads |
| `com.riseon.ads` 3.0.0 | Tự cài theo `package.json` | Lõi quảng cáo mà adapter cắm vào |
| `com.riseon.ads.admob.native` 2.1.0 | Tự cài theo `package.json` | Các format native |
| `com.riseon.utils` 2.0.0 | Tự cài theo `package.json` | Các mục của AdMob trên trang Ads |
| `com.google.ads.mobile` 11.5.0 | Tự cài theo `package.json`, từ OpenUPM | Google Mobile Ads SDK |

"Tự cài" là Package Manager tự lấy theo version từ Unity Registry và các registry khai
trong manifest, cả khi cài pack bằng git URL. Odin không có trên UPM nên phải cài vào
project trước. Thiếu Odin thì project báo một lỗi từ `RiseOn.Ads.AdMob.Requirements`.

## Cài đặt

**Registry RiseOn** (khuyên dùng): thêm registry RiseOn với scope `com.riseon`, và
OpenUPM với scope `com.google`, `com.cysharp`, vào `Packages/manifest.json`, rồi thêm
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
      "scopes": ["com.google", "com.cysharp"]
    }
  ],
  "dependencies": {
    "com.riseon.ads.admob": "1.2.0"
  }
}
```

**Git URL**: phụ thuộc vẫn lấy theo version từ registry, nên manifest cần OpenUPM như ví
dụ trên; `com.riseon.utils`, `com.riseon.consent`, `com.riseon.serializables`,
`com.riseon.ads` và `com.riseon.ads.admob.native` lấy từ registry RiseOn, hoặc cài bằng
git URL trước theo thứ tự này. Rồi *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Ads.git?path=/AdMob
```

**Thư mục local**: `"com.riseon.ads.admob": "file:D:/path/to/Ads/AdMob"`.

## Định dạng

| `AdFormat` | Cách hiện |
|---|---|
| `Banner` | `BannerView` anchored adaptive hoặc 320x50 |
| `MRec` | `BannerView` 300x250 |
| `Interstitial`, `Rewarded`, `RewardedInterstitial`, `AppOpen` | Định dạng full-screen của Google |
| `NativeInterstitial`, `NativeAppOpen`, `NativeEndCard` | `FullScreenAd` của `com.riseon.ads.admob.native` |
| `NativeCollapsible` | `HalfScreenAd` của `com.riseon.ads.admob.native` |
| `NativeInFeed`, `NativeMRec` | `RectAd` của `com.riseon.ads.admob.native` |

Mỗi định dạng có ad unit id riêng cho Android và iOS. Ô trống ở nền tảng nào thì
định dạng đó không tải ở nền tảng đó (có log lỗi). Vị trí, kiểu banner và
nút đóng của native đặt ở mục **Formats** của trang (README `com.riseon.ads`);
mục **Formats** của AdMob giữ unit id của từng format, format native có thêm
**Cache Size**.

## Những điều cần biết

- Sự kiện của SDK đến trên thread của SDK (`RaiseAdEventsOnUnityMainThread` tắt), để
  interstitial AdMob nối được end card native trong `AdChain`.
- iOS: mediation gọi `MobileAds.SetiOSAppPauseOnBackground(true)`, để Unity dừng dưới
  ad full-screen như trên Android (ở đó Activity của ad lấy focus là Unity dừng).
- Rewarded và rewarded interstitial trên iOS: plugin đưa sự kiện nhận thưởng về thread
  Unity, có khi đến sau sự kiện đóng. Mediation chờ một frame sau khi đóng rồi mới báo
  kết thúc, và trên iOS các định dạng này không có bước sau trong `AdChain`.
- Ad full-screen của Google quá hạn (1 giờ, app open 4 giờ) thì `IsReady` trả
  `false` và tải ad mới.
- Native MRec và in-feed: mỗi ô game tạo là một `RectAd` riêng, trên kho của format
  đó. Ô tắt không giữ ad chưa hiện: ad đó về kho cho ô khác; ad vừa hiện chưa đủ 4
  giây thì ở lại cho lần bật sau của chính ô đó. Mỗi format thêm một `RectAd` không
  bao giờ hiện, tạo lúc khởi động, để kho có ad sẵn trước khi game tạo ô đầu tiên.
- Mỗi format native có ô **Cache Size** trong mục của nó: số ad format nhờ kho giữ
  sẵn. Mặc định 1; collapsible 2, vì chuỗi collapsible cần một ad cho mỗi bước;
  in-feed thì tăng khi một màn hiện nhiều ô cùng lúc.
- Mọi format native cùng ad unit id và cùng chế độ tắt tiếng dùng chung một kho
  ad đã tải, giữ số lớn nhất trong các **Cache Size** của chúng: ad không vừa ô này
  vẫn còn cho ô hay format khác (README `com.riseon.ads.admob.native`, mục B2).
- Consent: trẻ em → `AgeRestrictedTreatment.Child`; không cho bán dữ liệu →
  extra `rdp=1`. Phần cá nhân hóa SDK tự đọc từ chuỗi TCF UMP đã lưu.
- Nút **Test** của mỗi ô unit id dùng demo unit Google công bố
  ([Android](https://developers.google.com/admob/android/test-ads),
  [iOS](https://developers.google.com/admob/ios/test-ads)): banner dùng unit
  anchored adaptive, MRec dùng unit banner cố định, mọi format native dùng unit
  native ảnh. Không dùng unit native video của Google: trên iPhone thật, ô in-feed
  không chứa nổi MediaView 120 thì unit đó ngừng fill (AdMob.Native README, C3).
  Demo unit chạy được với App ID thật của app.

## Setting của SDK

Dưới mediation trên trang **RiseOn > Ads** hiện:

| Mục | Ý nghĩa |
|---|---|
| App ID | App id AdMob của project, Android và iOS. Chính là ô ở trang **RiseOn > Consent** (pack `com.riseon.consent`): sửa ở đâu cũng là một bản, và pack Consent ghi nó vào file settings của Google Mobile Ads |
| Next-Gen SDK | "Upgrade to the GMA Next-Gen SDK" của Google, chỉ Android |
| Background Init, Background Loading | Ngược với "Disable initialization optimization" và "Disable ad loading optimization" của Google |
| Gradle Fixes, Coroutines Fix | "Enable Gradle build pre-processor" và "Enable kotlinx.coroutines packaging option" của Google |

Các ô này đọc và ghi thẳng vào file settings của Google Mobile Ads (Assets > Google
Mobile Ads > Settings), không có bản riêng. Ô câu xin tracking của file không hiện:
câu đó do pack Consent ghi, và ô đó phải trống; trang Consent có nút **Clear it**, và
build iOS dừng khi chưa xóa.

## Network

Mục con **Networks** của mediation (gập sẵn) liệt kê các adapter mediation của
Google. Mỗi network là một package `com.google.ads.mobile.mediation.*` trên OpenUPM.

- Trang Ads mở mà chưa có danh sách thì pack tìm ngay các package đó trong mọi
  registry của manifest, kể cả khi mục còn gập. Unity compile làm mất danh sách; pack
  tìm lại khi trang mở hoặc khi thanh Networks hiện trên trang. Nút ↻ trên thanh tìm
  lại. Manifest phải có OpenUPM với scope `com.google` (phần Yêu cầu đã cần nó); thiếu
  thì mục báo thiếu registry.
- Mỗi dòng: tên network; phiên bản trong project, hoặc **Not installed**; khi có bản
  mới hơn thì thêm mũi tên tới bản đó, ví dụ `7.2.2 → 7.3.0`.
- Nút cuối dòng: **Install** khi chưa cài, **Update** khi có bản mới hơn,
  **Uninstall** khi manifest ghi package đó trực tiếp. **Update All** trên thanh
  cập nhật mọi adapter cũ trong một request; mờ khi không có adapter nào cũ.
- Chọn nhiều dòng: bấm vào chỗ trống của dòng để chọn riêng dòng đó, bấm lại để bỏ
  chọn; Ctrl (macOS: Cmd) thêm hay bớt từng dòng; Shift chọn cả dải từ dòng bấm trước,
  Ctrl+Shift thêm cả dải đó vào các dòng đang chọn. Khi mục đang mở và có từ hai dòng
  được chọn, thanh thay **Update All** bằng các nút cho chúng: **Install n** (dòng
  chưa cài), **Update n** (dòng có bản mới hơn), **Uninstall n** (dòng gỡ được), mỗi
  nút chỉ hiện khi có dòng hợp; tooltip liệt kê tên. Cài và cập nhật lấy bản mới nhất.
  Mỗi nút gửi một request cho mọi dòng của nó. Bấm xong thì bỏ chọn.
- Chọn phiên bản: **Install** mở danh sách (có ô tìm) mọi bản tương thích với Unity
  của project mà registry có, mới nhất trên cùng và được chọn sẵn; chọn một bản thì
  cài đúng bản đó. Adapter đã cài thì bấm vào chữ phiên bản (có dấu ▾) để mở cùng danh
  sách và đổi sang bản khác, cũ hơn cũng được; trong lúc đổi, chữ phiên bản quay tại
  chỗ (di chuột lên ghi việc đang chạy, như **Changing Pangle to 7.2.1**), và bản đang
  có trong project mờ trong danh sách. Adapter đã cài mà lần tìm
  không thấy (ví dụ offline) thì chữ phiên bản không có ▾.
- Cài, cập nhật và gỡ đi qua Package Manager. Trong lúc chờ, nút vừa bấm quay tại
  chỗ (di chuột lên ghi việc đang chạy, như **Installing Pangle**), và mọi nút cài, gỡ
  trên trang mờ đi; xong thì Unity compile lại. Lỗi thì Console báo `<việc> failed: …`, như
  `Updating Pangle failed: …`.
- Package của OpenUPM không có chữ ký, nên sau khi cài Unity có thể hiện hộp
  **Missing Signature**. Đó là hộp của Unity, không phải của pack.
- Không tìm được registry (ví dụ offline) thì mục ghi lỗi và chỉ còn các adapter
  đã cài, không kèm bản mới.

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).