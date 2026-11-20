# RiseOn.Ads.Max

Mediation AppLovin MAX cho `com.riseon.ads`.

Package `com.riseon.ads.max`, namespace `RiseOn.Ads.Max`, mediation id `max`.

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
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Các mục của MAX trên trang Ads |
| `com.riseon.ads` 2.0.0 | Tự cài theo `package.json` | Lõi quảng cáo mà adapter cắm vào |
| `com.riseon.utils` 1.0.18 | Tự cài theo `package.json` | Các mục của MAX trên trang Ads |
| `com.applovin.mediation.ads` 8.6.5 | Tự cài theo `package.json`, từ registry AppLovin | AppLovin MAX SDK |

"Tự cài" là Package Manager tự lấy theo version từ Unity Registry và các registry khai
trong manifest, cả khi cài pack bằng git URL. Odin không có trên UPM nên phải cài vào
project trước. Thiếu Odin thì project báo một lỗi từ `RiseOn.Ads.Max.Requirements`.

## Cài đặt

**Registry RiseOn** (khuyên dùng): thêm registry RiseOn với scope `com.riseon`,
OpenUPM với scope `com.cysharp`, `com.google` (phụ thuộc của `com.riseon.ads`), và
registry AppLovin với scope `com.applovin`, vào `Packages/manifest.json`, rồi thêm
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
      "scopes": ["com.cysharp", "com.google"]
    },
    {
      "name": "AppLovin MAX",
      "url": "https://unity.packages.applovin.com/",
      "scopes": ["com.applovin"]
    }
  ],
  "dependencies": {
    "com.riseon.ads.max": "1.0.1"
  }
}
```

**Git URL**: phụ thuộc vẫn lấy theo version từ registry, nên manifest cần OpenUPM và
registry AppLovin như ví dụ trên; `com.riseon.utils`, `com.riseon.consent`,
`com.riseon.serializables` và `com.riseon.ads` lấy từ registry RiseOn, hoặc cài bằng git
URL trước theo thứ tự này. Rồi *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Ads.git?path=/Max
```

**Thư mục local**: `"com.riseon.ads.max": "file:D:/path/to/Ads/Max"`.

## Định dạng

`Banner`, `MRec`, `Interstitial`, `Rewarded`, `AppOpen`. Mỗi định dạng có ad unit id
riêng cho Android và iOS.

## Những điều cần biết

- Sự kiện của SDK đến trên thread của SDK (`InvokeEventsOnUnityMainThread = false`),
  nên ad MAX đứng đầu một `AdChain` được. MAX chỉ cho gọi API trên thread Unity, nên
  không đứng sau bước khác.
- Consent: `SetHasUserConsent` và `SetDoNotSell` theo trạng thái chung.
- Game cho trẻ em (Child Directed ở RiseOn > Consent): mediation không khởi động và
  log lỗi, vì điều khoản của AppLovin cấm khởi tạo SDK cho trẻ em. Bỏ MAX khỏi danh
  sách ưu tiên của game đó.
- Doanh thu MAX báo bằng USD.

## Setting của SDK

Dưới mediation trên trang **RiseOn > Ads** hiện, đọc và ghi thẳng vào file
`AppLovinSettings` của SDK:

| Mục | Ý nghĩa |
|---|---|
| SDK Key | Lấy ở MAX dashboard: Account > General > Keys |
| Ad Review | "Enable MAX Ad Review" của AppLovin |
| AdMob App ID | App id AdMob mà adapter Google của MAX cần. Chính là ô ở trang **RiseOn > Consent**: sửa ở đâu cũng là một bản, và pack Consent ghi nó vào `AppLovinSettings` |

**Consent flow của MAX phải tắt** (Integration Manager > Privacy Settings), vì nó hiện
form và popup ATT lần nữa. Khi nó bật, trang **RiseOn > Consent** báo lỗi kèm nút
**Turn it off**, và build dừng.

## Network

Mục con **Networks** của mediation (gập sẵn) là danh sách network trong Integration
Manager của AppLovin. Trang Ads mở mà chưa có danh sách thì pack đọc ngay từ server
của AppLovin, kể cả khi mục còn gập. Unity compile làm mất danh sách; pack đọc lại khi
trang mở hoặc khi thanh Networks hiện trên trang. Nút ↻ trên thanh đọc lại.

- Mỗi dòng: tên network; phiên bản adapter trong project theo từng nền tảng, ví dụ
  `Android 25.5.0.0, iOS 13.13.0.0`, hoặc **Not installed**; khi AppLovin báo có
  bản mới hơn thì thêm mũi tên tới bản đó. Tên có tooltip khi AppLovin báo adapter
  không chạy với plugin MAX trong project.
- Nút cuối dòng: **Install** khi chưa cài, **Update** khi có bản mới hơn,
  **Uninstall** khi đã cài. **Update All** trên thanh cập nhật lần lượt mọi adapter
  cũ, như **Upgrade All** của Integration Manager; mờ khi không có adapter nào cũ.
- Chọn nhiều dòng: bấm vào chỗ trống của dòng để chọn riêng dòng đó, bấm lại để bỏ
  chọn; Ctrl (macOS: Cmd) thêm hay bớt từng dòng; Shift chọn cả dải từ dòng bấm trước,
  Ctrl+Shift thêm cả dải đó vào các dòng đang chọn. Khi mục đang mở và có từ hai dòng
  được chọn, thanh thay **Update All** bằng các nút cho chúng: **Install n** (dòng
  chưa cài), **Update n** (dòng có bản mới hơn), **Uninstall n** (dòng gỡ được), mỗi
  nút chỉ hiện khi có dòng hợp; tooltip liệt kê tên. Cài và cập nhật lấy bản mới nhất.
  Cài và cập nhật chạy lần lượt như **Update All**; gỡ bỏ mọi dòng trong một request
  khi plugin là package. Bấm xong thì bỏ chọn.
- Cài và cập nhật gọi hàm cài của Integration Manager. Plugin MAX nằm trong `Assets`
  thì hàm đó tải và import adapter vào `Assets`. Plugin MAX là package thì hàm đó ghi
  các package của adapter vào manifest, rồi pack gửi một request Package Manager với
  đúng các package ấy, để biết lúc xong và lý do khi lỗi.
- Gỡ: plugin trong `Assets` thì gọi hàm gỡ của Integration Manager; plugin là package
  thì pack gửi request bỏ các package của network mà manifest ghi trực tiếp (hàm gỡ
  của AppLovin bỏ thêm bản beta của chúng, pack thì không). Plugin là package mà
  network không có package nào trong manifest thì dòng của nó không có **Uninstall**.
- Plugin là package thì trong lúc hàm cài của AppLovin resolve, Editor không phản hồi
  (đo trong PackageSandbox, cài Moloco: khoảng 14 giây); dòng quay đã hiện trước đó.
- Package của AppLovin không có chữ ký, nên sau khi cài Unity có thể hiện hộp
  **Missing Signature**. Đó là hộp của Unity, không phải của pack.
- Trong lúc chờ, mục hiện dòng quay ghi việc đang chạy và mọi nút cài, gỡ trên trang
  mờ đi. Plugin là package mà request lỗi thì Console báo `<việc> failed: …`,
  như `Installing Pangle failed: …`.
- Không chọn được phiên bản: Integration Manager chỉ đưa bản mới nhất của mỗi adapter.
- Không đọc được danh sách thì mục báo kiểm tra kết nối mạng.

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).