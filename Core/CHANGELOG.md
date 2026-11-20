# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [2.0.0] - 2026-11-20

### Đổi

- `AdFormatExt` đổi tên thành `AdFormatUtils`, theo luật đặt tên lớp extension của các
  pack RiseOn. Gọi kiểu extension (`format.IsFullScreen()`) không phải sửa; code gọi
  `AdFormatExt.X(...)` thì đổi tên lớp.
- `AdUnitTestAttribute.Ios` đổi tên thành `IOS`, theo luật viết hoa từ viết tắt hai chữ.
  Adapter tự viết đọc field này thì đổi theo.
- Cần Unity 6000.3, bản đang dùng để phát triển, thay cho 6000.0 chưa từng được thử:
  `package.json` ghi `unity`, nên Package Manager không cài pack vào project Unity cũ
  hơn.
- Phụ thuộc RiseOn lên bản phát hành cùng đợt: `com.riseon.consent` 1.0.2,
  `com.riseon.utils` 1.0.18, `com.riseon.serializables` 1.0.5.

## [1.0.1] - 2026-11-20

### Sửa

- Project chưa có Odin Inspector: mọi assembly của pack dùng Odin được bỏ qua, và chỉ còn một lỗi
  nói pack cần Odin; trước đó project báo thêm hàng loạt lỗi compile khó hiểu.

## [1.0.0] - 2026-11-20

Bản đầu tiên.