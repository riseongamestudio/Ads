# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.ads` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [Unreleased]

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