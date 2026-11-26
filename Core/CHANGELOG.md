# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [Unreleased]

### Đổi

- Pack không còn luật của game: `AdsSdk` hiện đúng cái được gọi, như API của một SDK
  mediation. Remove-ads, giãn cách (First After, Min Interval, After Rewarded), end
  card nối sau ad, ad khi quay lại game và lúc mở game (mục App Open), Auto
  Show của banner và các plan collapsible rời trang settings và lớp nền; chúng nằm
  trong sample `AdsManager`, game chép về và sửa. Ô **Included In Remove Ads**, **Use
  Native End Card** và các ô giãn cách của mục Formats không còn; giá trị đã đặt trên
  trang không chuyển sang sample, game đặt lại trong mục Manager.
- `AdsManagerBase` thay bằng interface `IAdsManager`. Lớp manager cài nó (`public
  partial class AdsManager : IAdsManager`) và cài tường minh `PauseInEditor`
  (`void IAdsManager.PauseInEditor(bool paused)`), member duy nhất, mà chỉ pack gọi
  được. Source generator gắn thêm `sealed`. Các member `virtual` của lớp nền
  (`AdsRemoved`, `PacingAllows`, `OnDisplayed`...) không còn: game viết luật của mình
  trong lớp, và nghe sự kiện thẳng từ `AdsSdk`.
- `AdsSdk` có hàm show riêng cho từng format full-screen, nhận placement:
  `ShowInterstitialAsync`, `ShowRewardedAsync`, `ShowRewardedInterstitialAsync`,
  `ShowAppOpenAsync`, `ShowNativeInterstitialAsync`, `ShowNativeAppOpenAsync`,
  `ShowNativeEndCardAsync`; `ShowCollapsibleAsync(plan, onDisplayed)` hiện các bước
  của một plan.
- `CreateNativeMRec`, `CreateNativeInFeed` gọi trước `IsInitialized` không còn ném:
  ô nhận khối khi mediation phục vụ format khởi động xong, giữ chỗ và trạng thái bật
  game đã đặt.
- Danh sách mediation tách nền tảng không còn ô **Editor uses**: Editor dùng danh
  sách của build target đang chọn (iOS, hay Android cho mọi target khác).

### Thêm

- `AdsSdk.SetSuppressed(format, on)`, `IsSuppressed(format)`: chặn một format, như
  khi người chơi mua remove-ads. Format full-screen bị chặn thì bị bỏ khỏi mọi chuỗi,
  format có thưởng thì thưởng luôn; banner, MRec, collapsible và ô native rời màn
  hình và quay lại khi thả.
- `AdPacing`: field giãn cách một format cho lớp manager, mỗi ô có key Remote Config,
  trang vẽ mỗi field một mục gập mở; `AdCollapsiblePlan.Pick(plans)` bốc một plan theo
  weight.
- `AdsSdk.Launches`, `AdsSdk.SecondsSinceDisplay(format)`, và `EditorGamePause.Apply`
  (cách dừng game mặc định trong Editor) thành public.
- Sample `AdsManager` (Package Manager → Samples): các luật phần lớn game của studio
  chạy, với giá trị mặc định cũ của trang. Remove Ads là một ô tick cho mỗi định dạng;
  Pacing là `EnumMap<PaceableFormat, AdPacing>`, theo thứ tự định dạng của mục Formats.
- `AdImpression.Placement`: placement của lần show đi cùng doanh thu.
- Mọi field `AdRemoteSetting` của lớp manager có ô chọn key Remote Config, và build
  dừng khi key của nó không còn trong Remote Config. Project không có
  `com.riseon.firebase.remoteconfig` thì ô đó mờ, ghi **No Remote Config**.
- Ô chọn key Remote Config cho danh sách mediation của một format nằm ở cuối dòng
  **Mediations** (dòng tên khi đã tách theo nền tảng), như ô key của mọi field khác;
  dòng **Mediations Remote Key** riêng không còn, và không còn bị ẩn khi thiếu pack
  Remote Config.

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