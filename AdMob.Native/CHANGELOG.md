# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads.admob.native` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [Unreleased]

## [3.0.0] - 2026-11-27

Bản lớn: màu của ad theo theme sáng tối của máy, các setting màu không còn.

### Đổi

- `Interstitial` phủ cả màn hình, như ad full-screen của các mạng. Nút đóng và đồng hồ
  ở hai góc trên. Ảnh to nhất có thể ở giữa: dưới hàng control với cả bề rộng, hoặc
  vươn lên đỉnh màn hình giữa hai cột control, cách nào cho ảnh to hơn (màn ngang: ảnh
  bên trái). Dải app ở chân đọc từ trái: icon cạnh tên, mô tả, nút bấm. Chữ và nút rộng
  tối đa 560dp trên tablet. `AppOpen` vẫn là thẻ giữa màn hình, game tối đi phía sau.
- `EndCard` là trang store của app: creative làm banner trên cùng (tối đa 40% chiều cao),
  đặt trên bản mờ tối của chính nó trải từ mép trên màn hình tới chân ảnh; dưới đó icon
  cạnh tên và nhà quảng cáo, hàng số liệu sao | giá | store (chỉ những thứ ad có), mô
  tả, nút bấm ở đáy. Màn ngang: banner bên trái, trang store bên phải. Android dưới 12
  không làm mờ được (`RenderEffect` có từ API 31): bản nền chỉ tối đi.
- `FullScreenAd.Settings.BackgroundColor`, `HalfScreenAd.Settings.BackgroundColor`,
  `RectAd.Settings.BackgroundColor` và `AdColor` không còn. Ad toàn màn hình, panel
  nửa màn hình, ô hình chữ nhật và thanh app open mang màu mặt của theme máy (sáng
  `#FFFFFF`, tối `#29292B`), chữ và nút theo cùng theme; nút bấm `#0A57D1` ở theme
  sáng, `#1A73E8` ở theme tối.
- Ảnh (không phải video) cần ít nhất 120dp mỗi chiều ở mọi format: ô hay panel nhỏ hơn
  thì ad hiện không có ảnh. Ô nào cần ảnh thì cho ô ít nhất 120dp.
- Thẻ app open có viền trắng 20%, để thẻ tối hay ảnh tối vẫn tách khỏi game tối đi.
- Thanh app open cao 48dp, ôm icon, tên game (tối đa 40% bề rộng màn hình) và trạng
  thái, neo mép phải, đầu phải chạy ra ngoài màn hình.
- Panel nửa màn hình chừa dải control cao 42dp cho nút đóng và đồng hồ.
- Chữ của ad toàn màn hình theo cỡ chữ của máy, tối đa gấp 1.3; tên app trên thẻ tối đa
  3 dòng.
- Rating vẽ một kiểu ở mọi ad: năm ngôi sao tô vàng `#FBBC04` tới điểm của app, sao
  cuối tô một phần, sao trống xám theo theme, rồi điểm số màu xám, ví dụ ★★★★☆ 4.3.
- Badge "Ad" là ô xám `#5F6368` chữ trắng, góc bo 4dp (trước là ô vàng chữ đen); chữ
  đậm cỡ 11 ở ad toàn màn hình và nửa màn hình, cỡ theo ô ở ô hình chữ nhật.
- Badge "Ad" và dấu AdChoices nằm sát hai góc trên của ảnh ở ad toàn màn hình, panel nửa
  màn hình xếp ảnh trên chữ, và template ảnh trái/phải của ô hình chữ nhật; "Ad" khi đó
  vuông theo hai cạnh góc ảnh, chỉ bo góc thò vào ad. Template ảnh nền của ô hình chữ
  nhật: tên ngắn hơn icon đủ chỗ thì badge đứng cạnh icon, trên tên, thay vì ở góc ô.
- Ảnh chạm góc bo (thẻ app open, template ảnh nền của ô hình chữ nhật) đứng lùi khỏi góc
  bo một khoảng R(1−1/√2), 6dp với góc 20dp, nên không pixel nào của creative bị cắt:
  chính sách native chỉ cho ảnh mất bề ngang, video không được mất gì.
- Nút đóng và đồng hồ dùng chung một đĩa, theo tông của thứ nằm dưới: trên nền tối
  (game tối đi quanh thẻ app open, bản mờ của end card ở màn dọc, theme tối) đĩa trắng
  mờ, dấu sáng `#E3E3E3`; trên theme sáng đĩa đen mờ, dấu tối `#444746`. Dấu ✕ cùng cỡ
  và nét ở mọi format, như của Google và ad giả của `com.riseon.ads`.
- Dấu AdChoices không bị góc bo cắt. iOS: dấu vẽ trong `GADAdChoicesView` của pack, gán
  vào `GADNativeAdView.adChoicesView`.
- Bản xem trước trong Editor vẽ theo theme sáng tối của Editor, cùng màu với máy.

### Nâng cấp từ 2.1.0

1. Bỏ các dòng gán `BackgroundColor` trong `FullScreenAd.Settings`,
   `HalfScreenAd.Settings` và `RectAd.Settings`, và mọi chỗ dùng `AdColor`.
2. Ô hình chữ nhật giờ luôn đục, mang màu mặt của theme: game nào để ô trong suốt cho
   hình game lộ qua ad thì hình đó không còn lộ. Placeholder cho lúc chưa có ad vẫn cần.
3. Ô hay panel nhỏ hơn 120dp mà cần ảnh thì nới ra ít nhất 120dp.

## [2.1.0] - 2026-11-26

### Thêm

- `FullScreenAd.Settings.Layout` (`FullScreenLayout`): `Interstitial`, `AppOpen`,
  `EndCard`. Khi `RedirectOnClose` tắt, `AppOpen` có thanh "Continue to app" ở đầu
  màn hình với icon và tên game, thay nút đóng và đồng hồ; bật thì như
  `Interstitial`. `EndCard` đưa app lên trước ảnh.

### Đổi

- Ad toàn màn hình là một tấm thẻ trắng bo góc giữa màn hình:
  ảnh, icon, tên, mô tả căn giữa và nút bấm hình viên thuốc; màn hình ngang thì ảnh
  bên trái, chữ bên phải; màn hình xoay sau khi ad đã nạp, hay cửa sổ đổi cỡ khi ad
  đang hiện (xoay, máy gập, chia đôi màn hình), thì thẻ xếp lại theo cỡ mới.
- Ad toàn màn hình trên Android xoay theo game như trên iOS, thay vì luôn dọc.
- Nút bấm là viên thuốc `#1A73E8` không viền, icon bo góc, badge "Ad" bo góc, ở
  mọi kiểu ad. Nút đóng và đồng hồ của ad toàn màn hình và nửa màn hình là hình tròn.
- Sao trống của rating thành xám trên nền sáng (trước là trắng, mất hút trên nền
  sáng và trên thẻ).
- Bản xem trước trong Editor vẽ theo kiểu mới: thẻ, thanh app open, dấu AdChoices
  và creative mẫu. Bấm vào ad mở trang AdMob.

### Sửa

- iOS: game văng khi lấy một ad native đã nạp ra dùng, thường ngay lúc một ad toàn
  màn hình hiện và nạp ad kế: ad bị giải phóng trước khi được đọc.

## [2.0.1] - 2026-11-20

### Đổi

- Cần Unity 6000.3, bản đang dùng để phát triển: `package.json` ghi `unity`, nên Package
  Manager không cài pack vào project Unity cũ hơn.

## [2.0.0] - 2026-11-20

### Đổi

- Cần Odin Inspector như mọi pack RiseOn: mọi assembly chỉ compile khi có
  `ODIN_INSPECTOR`, thiếu Odin thì project báo một lỗi từ
  `RiseOn.Ads.AdMob.Native.Requirements`. Pack không dùng Odin; luật chung để
  mọi pack cài đặt như nhau.

## [1.0.0] - 2026-11-20

Bản đầu tiên.