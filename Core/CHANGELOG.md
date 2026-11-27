# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [Unreleased]

## [4.0.0] - 2026-11-27

Bản lớn: màu của ad không còn là setting, cách đóng ad native đi theo từng lần hiện,
và `IAdMediationFullScreen.Show` đổi chữ ký. Xem "Nâng cấp từ 3.0.0" ở cuối bản này.

### Thêm

- `AdsSdk` nhận cách đóng của riêng một lần hiện: `Show(format, close, ...)`,
  `ShowNativeInterstitialAsync(close, ...)`, `ShowNativeAppOpenAsync(close, ...)`,
  `ShowNativeEndCardAsync(close, ...)`, `ShowCollapsibleAsync(close, ...)`, và trong chuỗi
  `AdChain.Then(format, close, ...)`, `ThenIf(condition, format, close, ...)`,
  `AdCollapsibleChain.Then(close, ...)`. Không truyền thì ad đóng theo **Close** của
  format trong mục **Formats**.
- `AdsSdk.ShowCollapsibleAsync(onDisplayed)`: một collapsible, không cần plan.
  `DefaultAdsManager` không có plan nào rút được thì hiện đúng một collapsible như vậy,
  thay vì không hiện gì.
- Mục **Native Collapsible** của Formats có **Close**, như ba format native toàn màn hình.

### Đổi

- `IAdMediationFullScreen.Show(format, placement, listener)` thành
  `Show(format, placement, in AdCloseSettings close, listener)`: `close` là cách đóng của
  lần hiện (của lời gọi, không có thì của trang), cho format native; format khác bỏ qua.
- Màu đi theo theme sáng tối của máy, không còn setting: ô **Cover** của trang, ô màu
  nền của các format native (`AdNativeFullScreenSettings.BackgroundColor`,
  `AdCollapsibleSettings.BackgroundColor`) và lớp `AdNativeRectSettings` (Native MRec,
  Native In-Feed) không còn. Màn che dưới chuỗi ad toàn màn hình là màu đen, như sau ad
  toàn màn hình của các mạng; màn che dưới chuỗi collapsible là màu mặt của theme, đúng
  màu panel collapsible. Ô Native MRec, Native In-Feed mặc định trong suốt, giờ luôn
  mang màu mặt đục của theme: hình game vẽ dưới ô không còn lộ ra.
- Các lớp ad trong Editor cách nhau 100 sorting order (trước là 1), để canvas của game
  chen được giữa hai lớp. `AdsEditorLayers.Step` (100) cho mediation vẽ lớp riêng.
- Trang settings: mục **Display** không còn; **Editor Sorting Order** là một mục riêng,
  ô số nằm trên thanh, mở ra thì thấy từng lớp ad với sorting order của nó.
- Mục **Formats**: thanh của mỗi format là một dòng gồm nút tách, ô mediation, ô key
  Remote Config; cột tên rộng bằng tên format dài nhất. Bấm ô mediation mở drop-down
  chọn như mọi ô chọn của trang: bấm một dòng để thêm hay bỏ mediation, kéo dòng có dấu
  tích để đổi thứ tự. File asset ghi xuống đĩa một lần khi drop-down đóng. Ô Android và
  ô iOS của danh sách tách nền tảng mang icon nền tảng thay cho chữ.
- Ad giả interstitial trong Editor phủ cả màn hình trên màu mặt của theme, như ad
  full-screen của các mạng; app open giữ thẻ giữa màn hình, game tối đi phía sau.
- Ad giả trong Editor: nút đóng của mọi format cùng một dấu ✕ (cỡ và nét như của
  Google); nhà quảng cáo và rating nằm một dòng, cách nhau bằng dấu `·`.
- Phụ thuộc `com.riseon.utils` 2.1.0.

### Sửa

- Hiệu ứng trên trang (gập mở, chuyển giữa một và hai dòng) không còn đứng giữa chừng
  tới khi di chuột.

### Nâng cấp từ 3.0.0

1. Mediation game tự viết: thêm tham số `in AdCloseSettings close` vào
   `IAdMediationFullScreen.Show`; format native dùng nó để đặt nút đóng.
2. Code đọc `AdNativeFullScreenSettings.BackgroundColor`,
   `AdCollapsibleSettings.BackgroundColor`, `AdFormatSettings.NativeMRec`,
   `AdFormatSettings.NativeInFeed` hay `AdNativeRectSettings` thì bỏ đi; màu theo theme
   máy, không đặt được. `new AdNativeFullScreenSettings(color, close)` thành
   `new AdNativeFullScreenSettings(close)`.
3. `AdsSdk.ShowCollapsibleAsync(null)` báo CS0121 (cả `(Action)` lẫn
   `(AdCollapsiblePlan)` đều nhận `null`): bỏ đối số, hoặc ghi rõ kiểu, ví dụ
   `ShowCollapsibleAsync((Action)null)`.
4. Ô Native MRec, Native In-Feed giờ đục: game nào để hình game lộ qua nền trong suốt
   của ô thì hình đó không còn lộ. Placeholder cho lúc chưa có ad vẫn cần.
5. Canvas của game đặt sorting order theo bước 1 giữa các lớp ad trong Editor thì đặt lại
   theo bước 100 (xem bảng lớp trong README).
6. Giá trị **Cover** và màu nền đã đặt trên trang mất khi asset lưu lại; không cần làm gì.

## [3.0.0] - 2026-11-26

Bản lớn: luật của game rời pack sang lớp manager, lớp nền `AdsManagerBase` không còn.
Xem "Nâng cấp từ 2.0.0" ở cuối bản này.

### Đổi

- Pack không còn luật của game: `AdsSdk` hiện đúng cái được gọi. Remove-ads, giãn cách
  (First After, Min Interval, After Rewarded), end card nối sau ad, ad khi quay lại game
  và lúc mở game (mục **On Return To Game** của trang, giờ là Manager > App Open của
  `DefaultAdsManager`), Auto Show của banner và các plan collapsible rời trang settings và
  lớp nền; chúng nằm trong `DefaultAdsManager`, lớp manager pack chạy khi game không có
  lớp riêng. Ô **Included In Remove Ads**, **Use Native End Card** và các ô giãn cách của
  mục Formats không còn; giá trị đã đặt trên trang không chuyển sang, game đặt lại trong
  mục Manager.
- `AdsManagerBase` thay bằng interface `IAdsManager`. Lớp manager cài nó (`public
  partial class AdsManager : IAdsManager`) và cài tường minh `Started` (`void
  IAdsManager.Started()`), chỉ pack gọi được, một lần khi scene đầu đã load. Dừng game
  trong Editor dưới ad full-screen là `IEditorPauseHandler` của `com.riseon.utils`.
  Source generator gắn thêm `sealed`; lỗi mới: lớp abstract (`RISEONADS005`), interface
  kế thừa `IAdsManager` (`RISEONADS004`); `RISEONADS006` xem `DefaultAdsManager` ở
  dưới. Các member `virtual` của lớp nền
  (`AdsRemoved`, `PacingAllows`, `OnDisplayed`...) không còn: game viết luật của mình
  trong lớp, và nghe sự kiện thẳng từ `AdsSdk`.
- `AdsSdk` có hàm show riêng cho từng format full-screen, nhận placement:
  `ShowInterstitialAsync`, `ShowRewardedAsync`, `ShowRewardedInterstitialAsync`,
  `ShowAppOpenAsync`, `ShowNativeInterstitialAsync`, `ShowNativeAppOpenAsync`,
  `ShowNativeEndCardAsync`; `ShowCollapsibleAsync(plan, onDisplayed)` hiện các bước
  của một plan.
- `CreateNativeMRec`, `CreateNativeInFeed` gọi trước `IsInitialized` không còn ném:
  ô nhận khối khi mediation phục vụ format khởi động xong, giữ chỗ và trạng thái bật
  game đã đặt. Vì vậy `HasCell` cũng là `false` tới lúc đó, không chỉ khi format tắt;
  game muốn biết format có mediation phục vụ thì dùng `AdsSdk.Serves(format)`.
- Danh sách mediation tách nền tảng không còn ô **Editor uses**: Editor dùng danh
  sách của build target đang chọn (iOS, hay Android cho mọi target khác).
- Ad giả trong Editor vẽ lại: interstitial là một thẻ (ảnh, logo, tên, mô tả, nút
  Install hình viên thuốc) cùng bố cục với ad native của AdMob.Native, nút đóng tròn;
  rewarded là trang app với nhãn "Reward in N seconds" và end card, màn dọc thì dải
  video cao hết phần trống của trang; app open có thanh
  "Continue to app" trên cùng thẻ đó, game tối đi phía sau; banner và MRec có logo,
  chữ, nút và dấu AdChoices; màu theo theme sáng/tối của Editor. Game view nằm ngang
  thì ad giả xếp ngang (ảnh hay video bên trái); thẻ không rộng quá 440 point nên trên
  tablet đứng giữa. Nút Install mở trang của mediation phục vụ ad.
- `IAdMediationFullScreen.Show(format, listener)` thành `Show(format, placement,
  listener)`; constructor `AdImpression` nhận thêm `placement` (tùy chọn). Mediation
  game tự viết thêm tham số này và truyền placement cho SDK nếu SDK nhận.
- `AdChainSkip`: `Removed` và `Pacing` bỏ, thêm `Suppressed` (format bị
  `AdsSdk.SetSuppressed` chặn). Chuỗi lấy từ `AdsSdk.Chain()`; giãn cách là việc của
  lớp manager (`DefaultAdsManager.Allows`), không còn trong chuỗi.
- Chuỗi chỉ có một ad App Open không có màn che: game lộ ra quanh ad, banner, MRec và
  ô native rời màn hình trong lúc nó hiện.
- Trên trang, khi cài, cập nhật, gỡ adapter hay network, nút vừa bấm quay tại chỗ thay
  cho dòng quay ở đầu danh sách (`PackageJobs` của `com.riseon.utils` 2.0.0).
- Phụ thuộc lên bản phát hành cùng đợt: `com.riseon.utils` 2.0.0,
  `com.riseon.serializables` 1.0.6.

### Thêm

- `AdsSdk.SetSuppressed(format, on)`, `IsSuppressed(format)`: chặn một format, như
  khi người chơi mua remove-ads. Format full-screen bị chặn thì bị bỏ khỏi mọi chuỗi,
  format có thưởng thì thưởng luôn; banner, MRec và ô native rời màn hình và quay
  lại khi thả; chuỗi collapsible đang chạy bị cắt.
- `AdPacing`: field giãn cách một format cho lớp manager, mỗi ô có key Remote Config,
  trang vẽ mỗi field một mục gập mở; `Allows(format, sharing)` cho hai format cùng lấp
  một lúc dùng chung một giãn cách. `AdCollapsiblePlan.Pick(plans)` bốc một plan theo
  weight.
- `AdsSdk.Launches` và `AdsSdk.SecondsSinceDisplay(format)` thành public.
- `AdsSdk.Serves(format)`: danh sách ưu tiên của format có mediation trên nền tảng này,
  cho lựa chọn của game như ẩn nút của format không mediation nào phục vụ.
- `DefaultAdsManager`: lớp manager pack chạy khi game không có lớp riêng, các luật
  phần lớn game của studio chạy, game gọi qua `DefaultAdsManager.Ins`. Ngoài các luật
  rời lớp nền ở trên: interstitial từ level nào (**From Level**), interstitial và
  native interstitial chung một giãn cách (app open và native app open cũng vậy),
  `AdsOff` cho tester, sự kiện `Opportunity` mỗi lần luật cho một lần hiện đi qua.
  Nút **Create Own**, bên phải ô script trên thanh Manager, chép nó thành lớp của game
  cùng các giá trị đang đặt, và đổi các chỗ gọi `DefaultAdsManager` trong `Assets` sang
  lớp đó; lời gọi còn sót trong assembly thấy được lớp của game báo lỗi `RISEONADS006`.
  Trang không còn hộp vàng kèm nút **New script** khi project chưa có manager: thay vào đó
  là hộp thông tin, không dừng build, không in cảnh báo.
- `AdImpression.Placement`: placement của lần show đi cùng doanh thu.
- Mọi field `AdRemoteSetting` của lớp manager có ô chọn key Remote Config, và build
  dừng khi key của nó không còn trong Remote Config. Project không có
  `com.riseon.firebase.remoteconfig` thì ô đó mờ, ghi **No Remote Config**.
- Ô chọn key Remote Config cho danh sách mediation của một format nằm ở cuối dòng
  **Mediations** (dòng tên khi đã tách theo nền tảng), như ô key của mọi field khác;
  dòng **Mediations Remote Key** riêng không còn, và không còn bị ẩn khi thiếu pack
  Remote Config.

### Bỏ

- `AdsManagerBase` cùng mọi member của nó (xem Nâng cấp).
- `AdPacingSettings` (thay bằng `AdPacing`), `AdReturnSettings`, `AdReturnFormat`
  (thay bằng `DefaultAdsManager.ReturnAd`), `AdPlatform`.
- `AdFormatSettings.RemoveAdsTakes`, `PacingOf`, `EndCardFollows`, và các property
  giãn cách `Interstitial`, `RewardedInterstitial`, `AppOpen`;
  `AdBannerSettings.AutoShow`; `AdCollapsibleSettings.Pacing`, `Plans`;
  `AdNativeFullScreenSettings.Pacing` và tham số `pacing` của constructor;
  `AdsSettings.ReturnToGame`. `AdsSettings.Manager` có kiểu `IAdsManager`.

### Nâng cấp từ 2.0.0

1. Cất code riêng trong lớp `AdsManager : AdsManagerBase` của game ra chỗ khác rồi xóa
   lớp đó. Pack chạy `DefaultAdsManager`; đặt lại giãn cách, end card, remove-ads, ad
   khi quay lại game trong mục Manager của trang (giá trị cũ trên trang không chuyển
   sang).
2. Đổi lời gọi `AdsManager.Ins.…` để project compile lại:
   - sang `DefaultAdsManager.Ins`: `ShowInterstitialAsync`, `ShowRewardedAsync`,
     `ShowRewardedInterstitialAsync`, `ShowNativeInterstitialAsync`, `CanShowRewarded`,
     `CanShowRewardedInterstitial`, `ShowCollapsibleAsync`, `CanShowCollapsible`,
     `ShowBanner`, `HideBanner`, `ReturnAdBlocked`; `ShowAppOpenAsync`,
     `ShowNativeAppOpenAsync`, `ShowNativeEndCardAsync` thành
     `ShowAsync(AdFullScreenFormat.AppOpen, placement)` (`NativeAppOpen`,
     `NativeEndCard`), vẫn theo giãn cách;
     `CanShowInterstitial`, `CanShowNativeInterstitial` giờ là hàm nhận `level`;
   - sang `AdsSdk`: `IsInitialized`, `WaitUntilInitializedAsync`, `IsShowingFullScreen`,
     `ShowMRec`, `HideMRec`, `CreateNativeMRec`, `CreateNativeInFeed`, `Chain()`,
     `CollapsibleChain()`, `HideCollapsible()`, và các sự kiện (`Paid`, `Displayed`,
     `PlayerReturned`...), cùng tên;
   - `CanShowAppOpen`, `CanShowNativeAppOpen`, `CanShowNativeEndCard`: dùng
     `DefaultAdsManager.Ins.ReturnFormat`, hay `AdsSdk.IsReady(format) &&
     DefaultAdsManager.Ins.Allows(format)`;
   - `override AdsRemoved` và `RefreshAdsRemoved()`: gọi `SetAdsRemoved(owned)` mỗi lần
     mở game và khi quyền sở hữu đổi;
   - `OnShowRequested`: nghe `DefaultAdsManager.Ins.Opportunity`, đến sau khi luật cho
     qua (không còn trước luật);
   - `Launches`, `SecondsSinceDisplay`, `SecondsSinceCollapsible`: `AdsSdk.Launches`,
     `AdsSdk.SecondsSinceDisplay(format)` (`AdFormat.NativeCollapsible` cho collapsible);
     `DrawCollapsiblePlan`: `AdCollapsiblePlan.Pick`.
3. Game cần luật riêng thì bấm **Create Own** trên thanh Manager (nút chỉ hiện khi
   project chưa có lớp manager, và từ chối khi đã có type cùng tên). Nút chép
   `DefaultAdsManager` thành lớp của game và đổi các lời gọi `DefaultAdsManager` ở bước 2
   sang lớp đó; đưa code đã cất ở bước 1 vào lớp này.
4. `OnAttached` thay bằng `void IAdsManager.Started()`, chạy sau khi scene đầu load
   (không còn trước scene đầu).
5. `override PauseInEditor` thay bằng một lớp cài `IEditorPauseHandler`
   (`OnPause`, `OnResume`) của `com.riseon.utils`.
6. Code đọc `AdChainResult.Skip` thì đổi `Removed`, `Pacing` sang `Suppressed`.

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