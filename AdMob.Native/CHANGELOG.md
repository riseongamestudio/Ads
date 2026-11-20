# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads.admob.native` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

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