# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads.admob.native` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [Unreleased]

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