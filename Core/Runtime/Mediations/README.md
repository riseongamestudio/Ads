# Mediations

Những điều không thấy ngay khi đọc từng file.

## Viết một mediation

Một mediation là class C# `[Serializable]` thuần, cài `IAdMediation` và các
interface định dạng nó phục vụ, có constructor rỗng và nằm trong assembly runtime:
trang settings tự thêm một instance của mỗi class như vậy vào danh sách Mediations.
Settings lưu nó bằng `SerRef<IAdMediation>`, nên đổi tên class, namespace hay
assembly của mediation phải sửa asset của từng project.

- `Id` là chuỗi danh sách ưu tiên và Remote Config dùng; đổi nó là các danh sách
  đã lưu trỏ vào khoảng trống.
- `InitializeAsync` chỉ được nhận các định dạng có tên mediation trong danh sách ưu
  tiên: trên máy là danh sách của nền tảng đang chạy; trong Editor, danh sách tách
  nền tảng thì là danh sách của nền tảng chọn ở ô **Editor uses** trên trang.
  Mediation chỉ tải những định dạng đó. `AdUnitId.Value` trả unit id của nền tảng
  đang chạy; trong Editor là của Android, ô Android trống thì của iOS.
- Cách định dạng hiện (vị trí, adaptive của banner, màu nền và nút đóng của
  native...) thuộc mục Formats của trang, không thuộc mediation: mediation đọc
  `IAdMediationContext.Formats` và đổi sang kiểu của SDK, không khai field riêng cho
  những thứ đó. `Formats` như nhau trên cả hai nền tảng.
- Định dạng nào SDK không phục vụ mà vẫn được giao thì log lỗi "take it off the
  priority list", không ném.
- Mọi lần show kết thúc bằng đúng một `OnClosed` hoặc `OnFailed`, kể cả show bị từ
  chối vì chưa sẵn sàng. Có thưởng thì `OnRewarded` đến trước `OnClosed`.
- Retry khi tải lỗi: 2, 4, 8... tới 32 giây. Native của
  `com.riseon.ads.admob.native` tự retry bên trong, adapter không retry chồng.
- Revenue báo bằng tiền thật (không phải micros) qua
  `IAdMediationContext.ReportPaid`, từ thread nào cũng được; pack đưa về thread
  Unity rồi báo `AdsSdk.Paid` và `AdsManagerBase.OnPaid`.
- `CallsBackOffLoop`, `ShowsFromAnyThread`: xem [Chain](../Chain/README.md). SDK
  nào chỉ báo sự kiện qua player loop thì không có màn che và không nối được.
- Trong Editor, pack không giao cho mediation các định dạng không phải native
  (banner, MRec, interstitial, rewarded, rewarded interstitial, app open):
  `InitializeAsync` chỉ nhận các định dạng native, và pack vẽ ad giả thay cho phần
  còn lại. Mediation không có định dạng native nào thì `InitializeAsync` không được
  gọi trong Editor.

## Công cụ debug của SDK

SDK có công cụ debug riêng thì mediation cài `IAdMediationDebugger`, và
[`AdsDebugger`](../Debugger/README.md) hiện một nút cho nó.

- `DebuggerName` là tên công cụ theo tài liệu của SDK ("Ad Inspector").
- `OpenDebugger(done)` được gọi trên thread Unity. Mở được thì gọi `done(null)`,
  không thì `done` với việc tester cần làm trước ("AdMob has not started"); `done`
  gọi từ thread nào cũng được.

## Vẽ setting của SDK

Runtime không tham chiếu được kiểu setting của SDK (chúng nằm ở assembly Editor),
nên mediation gọi `AdsEditorHooks.DrawMediation(this)` trong `[OnInspectorGUI]`, và
assembly Editor của adapter đăng ký cách vẽ bằng
`AdsEditorHooks.RegisterMediationDrawer`. `AdsSdkSettingsGui` (Editor của core) vẽ
file setting của SDK, ẩn những ô xung đột với consent của pack và hiện lỗi kèm nút
**Tắt** khi chúng bật.