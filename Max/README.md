# RiseOn Ads MAX

Mediation AppLovin MAX cho `com.riseon.ads`.

Package `com.riseon.ads.max`, namespace `RiseOn.Ads.Max`, mediation id `max`.

## Yêu cầu

| Phụ thuộc | Cách có |
|---|---|
| `com.riseon.ads` | Tự cài theo `package.json` |
| `com.applovin.mediation.ads` 8.6.5 | Tự cài theo `package.json`; cần scoped registry AppLovin (`https://unity.packages.applovin.com/`) |

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

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).