# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads.admob` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [Unreleased]

## [1.2.0] - 2026-11-27

Cần `com.riseon.ads` 4.0.0: adapter bản này không chạy với Core 3.0.0.

### Đổi

- Theo `com.riseon.ads` mới: `Show` của mediation nhận `close`.
- Native toàn màn hình đặt nút đóng theo `close` của lần hiện, không còn đọc **Close**
  từ mục Formats; màu theo theme máy, adapter không còn truyền màu.
- Phụ thuộc lên bản phát hành cùng đợt: `com.riseon.ads` 4.0.0,
  `com.riseon.ads.admob.native` 3.0.0.

## [1.1.0] - 2026-11-26

Cần `com.riseon.ads` 3.0.0: adapter bản này không chạy với Core 2.0.0.

### Đổi

- Theo `com.riseon.ads` mới: `Show` của mediation nhận placement.
- Placement của lần show gần nhất của mỗi format đi cùng `AdImpression.Placement`, cả với native.
- Native app open và native end card dùng layout riêng của `com.riseon.ads.admob.native`
  (`FullScreenLayout.AppOpen`, `EndCard`); native interstitial giữ `Interstitial`.
- Trong mục Networks, nút vừa bấm (hay chữ phiên bản vừa chọn) quay tại chỗ trong lúc
  Package Manager chạy, thay cho dòng quay ở đầu mục.
- `Editor/MediationIcon.png`: logo AdMob, Core vẽ nó trên ad giả trong Editor.
- Phụ thuộc lên bản phát hành cùng đợt: `com.riseon.ads` 3.0.0,
  `com.riseon.ads.admob.native` 2.1.0 (cần `FullScreenLayout`), `com.riseon.utils` 2.0.0.

## [1.0.1] - 2026-11-20

### Đổi

- Cần Unity 6000.3, bản đang dùng để phát triển, thay cho 6000.0 chưa từng được thử:
  `package.json` ghi `unity`, nên Package Manager không cài pack vào project Unity cũ
  hơn.
- Phụ thuộc RiseOn lên bản phát hành cùng đợt: `com.riseon.ads` 2.0.0,
  `com.riseon.ads.admob.native` 2.0.1, `com.riseon.utils` 1.0.18.

## [1.0.0] - 2026-11-20

Bản đầu tiên.