# RiseOn Ads AdMob Native: core và bộ luật chung

[← RiseOn.Ads.AdMob.Native](../README.md)

Tài liệu chi tiết của assembly core `RiseOn.Ads.AdMob.Native` và bộ luật chung cho ba
nền tảng. Cách cài đặt và hướng dẫn nhanh ở [README gốc](../README.md).

Tài liệu này viết để **một người (hay một AI) mất sạch ngữ cảnh vẫn làm việc
được ngay**: không chỉ luật, mà cả cách mọi thứ vận hành, ý tưởng layout,
từng đánh đổi đã chọn kèm phương án bị bác, và trạng thái bàn giao. Vấn đề
riêng từng nền tảng ở README nền tảng:

- [Platforms/Android/README.md](Platforms/Android/README.md) — Activity,
  window type, luồng giao sự kiện, cổng javac, adb.
- [Platforms/iOS/README.md](Platforms/iOS/README.md) — bảng đối chiếu file,
  quy ước pt/px, subview và bậc lớp, UnityPause, giới hạn chưa compile.
- [Platforms/Editor/README.md](Platforms/Editor/README.md) — preview là ảnh
  chụp chứ không phải mô phỏng, asmdef, sorting order.

Ba nền tảng là ba hiện thực **ngang hàng** của bộ luật này — không bên nào là
bản sao của bên nào. **Mỗi thay đổi layout hay hành vi phải làm ở cả ba bên**,
trừ khi README nền tảng nói rõ bên đó không vẽ được thứ ấy. Không bao giờ hạ
cấp bản máy thật cho khớp với Editor.

## Cách đọc

- **10 phút để làm việc được**: A1 → A2 → A8 (thuật ngữ) → A9 (đánh đổi) →
  C4 (bàn giao).
- **Trước khi sửa layout**: A5, A6, A7, rồi B6–B12, rồi A10 để tìm hàm.
- **Trước khi sửa luồng load/show/callback**: A3, A4, B1–B4, README nền tảng.
- **Khi thấy một hành vi "kỳ"**: C2 sổ sự cố trước, rất có thể đã gặp.
- **Mã commit** (8 ký tự) là của repo `NativeAdMob`, nơi pack sống (với tên
  `com.riseon.nativeadmob`) trước khi chuyển vào monorepo `Ads` tháng
  2026-11. Monorepo không mang lịch sử đó; tra mã ở repo cũ.

Mục lục — Phần A (bức tranh): A1 bản đồ · A2 kiến trúc C# · A3 vòng đời
overlay · A4 vòng đời ô hình chữ nhật · A5 engine ô hình chữ nhật · A6 dựng
layout overlay · A7 template · A8 thuật ngữ · A9 đánh đổi · A10 bản đồ đọc
code · A11 tích hợp. Phần B (luật): B1 sự kiện/luồng · B2 cache · B3 xếp lớp ·
B4 pause · B5 RedirectOnClose · B6 tháp ưu tiên/chữ · B7 media · B8 side-media
· B9 padding 5dp · B10 sân media/né control · B11 bo góc · B12 badge/AdChoices
· B13 ô hình chữ nhật · B14 tên/phong cách. Phần C (vận hành): C1 quy
trình/cổng · C2 sổ sự cố · C3 việc còn mở · C4 bàn giao.

---

# Phần A — Bức tranh

## A1. Bản đồ package

```
AdMob.Native/                       một package trong monorepo Ads
  package.json                      com.riseon.ads.admob.native; phụ thuộc com.google.ads.mobile
  README.md                         giới thiệu, cài đặt, hướng dẫn nhanh, thứ tự lớp
  CHANGELOG.md, LICENSE.md
  Runtime/
    README.md                       luật chung (file này)
    RiseOn.Ads.AdMob.Native.asmdef  core: API công khai + máy trạng thái + SPI internal
    AssemblyInfo.cs                 InternalsVisibleTo ba assembly nền tảng
    BaseAd.cs                       xương sống: cổng release, DispatchFromNative, OnAdLoaded/OnAdLoadFailed/OnAdPaid
    RectAd.cs                       một ô game đặt chỗ và cỡ (ô in-feed, MRec): lấy ad từ pool chỉ lúc hiện
    OverlayAd.cs                    máy trạng thái show/hide chung; FullScreenAd + HalfScreenAd là hai mặt của nó
    I*Client.cs / I*Callbacks.cs / IAdPlatform.cs   SPI internal
    AdPlatformRegistry.cs           nơi assembly nền tảng cắm factory vào
    Platforms/Android/              AdPlatform + RectAdClient + OverlayAdClient (AndroidJavaObject)
      Bridge/RiseOnAdsAdMobNative.androidlib   Java: com.riseon.ads.admobnative.*
    Platforms/iOS/                  cùng bộ client qua DllImport "__Internal"
      Bridge/Native/                Obj-C++: RAN*.h/.mm
    Platforms/Editor/               cùng bộ client, vẽ bằng uGUI; assembly runtime chỉ có trong Editor
      Preview/                      EditorAd, EditorSortingOrder…
```

Ba tầng vẽ đè lên game, theo bảng thứ tự lớp ở [README gốc](../README.md#thứ-tự-lớp):

    ô hình chữ nhật  →  half-screen  →  full-screen

Giữa các tầng có thể có lớp của package khác (màn che, banner…); pack không biết
tới chúng ngoài bảng đó (B3).

Hai bản native đặt tên lớp giống nhau (Java `RectAd`, Obj-C++ `RANRectAd`). Ad
thô đến từ **pool** (`NativeAdPool` / `RANNativeAdPool`): một pool cho mỗi cặp
ad unit id + chế độ tắt tiếng, dùng chung cho mọi format trên unit đó (B2).
Pool lo load, backoff no-fill và hết hạn; format lấy ad ra và trả lại ad chưa
từng hiện. Ô hình chữ nhật tách làm hai lớp: **ô** (`RectAd`: vào pool, lấy và
trả ad, báo sự kiện) và **slot** của nó (`RectAdSlot`: mọi thứ có hình chữ
nhật: layout, dwell, xoay vòng, watchdog). Slot dựng giao diện qua
`*Presentation`, chọn layout bằng `*LayoutEngine` (chấm điểm ứng viên), dựng
view bằng `*ViewFactory`, bị `*LayoutValidator` gác về chính sách. Overlay chỉ
có một lớp native `OverlayAd` cho cả full và half; nội dung dựng trong
`OverlayAdContentView` (một cây view tự co giãn, không có engine chấm điểm),
trình bày qua `OverlayAdActivity` (full) hoặc `OverlayAdPresentation` (Dialog,
half). Bảng đối chiếu tên file giữa hai bản native ở README iOS.

## A2. Kiến trúc C#: một core, ba assembly nền tảng

- **Không `#if`, không partial trong C# của pack** (dab57106). Core
  `RiseOn.Ads.AdMob.Native` giữ API và máy trạng thái, nói chuyện với nền tảng qua
  SPI internal (`IRectAdClient`, `IOverlayAdClient`, hai interface callback,
  `IAdPlatform`). Ba assembly `RiseOn.Ads.AdMob.Native.Platforms.Android` /
  `.iOS` / `.Editor` mỗi cái chỉ biên dịch cho nền tảng của nó và tự cắm
  factory bằng `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` gác
  `Application.platform`. Bản build nào cũng chỉ có đúng một assembly nền
  tảng. Chủ dự án thích asm hơn `#if`; chỉ dùng `#if` khi bắt buộc.
- Không assembly nào tham chiếu tới assembly nền tảng, nên mỗi cái khai
  `[assembly: AlwaysLinkAssembly]` (file `AssemblyInfo.cs` trong thư mục của
  nó). Thiếu dòng này, UnityLinker bỏ cả assembly khi pack cài dạng package,
  kể cả ở mức stripping Minimal: bootstrap không chạy và không ad nào hiện.
- Client là **transport câm**: không giữ trạng thái, không gác generation.
  Cổng release (`releasedManaged` + `nativeAdStateLock`), `showGeneration`,
  cờ ready/loading đều nằm trong core. Thêm hành vi thì thêm ở core, một lần
  cho ba bên.
- **Máy trạng thái `OverlayAd` (C#)**: `ShowOverlay(slideIn, slideOutOnClose)`
  (`FullScreenAd.Show()` gọi với hai `false`, `HalfScreenAd.Show(...)`
  truyền thẳng) — từ chối nếu released /
  `showPendingOrActive` (raise `OnAdDisplayFailed` mã −1 ngay), không thì
  `showPendingOrActive = true`, `++showGeneration`, gọi client với generation
  làm `showId`. `OnShowCompleted(showId, message, adConsumed, cachedCount)` →
  `TryTakeShow` (chỉ khớp generation hiện tại) → `OnAdHidden` hoặc
  `OnAdDisplayFailed` → nếu `adConsumed` thì `Load()`. `OnDisplayed` →
  `OnAdDisplayed` + `Load()` (prefetch). `OnStateChanged(isReady, isLoading)`
  ghi đồng bộ dưới lock; `IsReady()` = không released, không đang show, có
  client, `cachedAdReady`. `Release()` hủy show đang treo bằng một
  DisplayFailed.
- `RectAd` (C#): ô phải có cỡ trước Show/SetPosition (ô không có kích thước
  thì không có chỗ vẽ; gọi sớm thì log lỗi và bỏ qua):
  `SetSize(sizePx, roundCornerPx)`, gọi lại khi ô đổi cỡ. Mỗi
  `Show(onDisplayed)` giữ callback tới `OnDisplayed` của nền tảng; `Hide`,
  `SetSize` bỏ callback đang chờ. `Dispose()` gỡ ô khỏi màn hình và rời pool.
  Ô chưa từng đặt cỡ vẫn vào pool với `CacheSize` của nó, nên giữ ấm unit
  được từ trước khi game đặt ô nào.
- `OverlayAd` abstract, constructor `private protected`: `FullScreenAd` (app
  open / interstitial / end card) và `HalfScreenAd` (collapsible,
  `HeightRatio`) là hai mặt duy nhất. Mỗi mặt tự đặt `Show`/`Hide` của mình:
  chỉ half trượt, nên chỉ half nhận cờ trượt. `HalfScreenAd.PanelHeight` hỏi
  thẳng nền tảng (`IOverlayAdClient.PanelHeight`).
- Settings là struct nhận lúc sinh; đổi sau thì có API riêng (`SetClose`).
  `Format` là chuỗi placement do game đặt, đi kèm mọi `OnAdPaid` (`AdInfo` ráp
  ở `BaseAd.RaiseAdPaid`, rời pack đã đầy đủ).
- `OnAdLoaded` là tin của pool: pool của unit vừa tải được một ad, mọi format
  trong pool cùng nghe và format nào cũng có thể lấy mất. `LoadedAdInfo` mang
  số ad đang nằm trong pool (không tính ad format đã lấy) và sức chứa của
  pool, mọi format báo cùng hai số đó.
- Bề mặt public ngoài assembly đúng bằng: các class, `Settings`, event, method
  public. `protected` đã hạ hết xuống `private protected`, trừ override
  `Graphic.OnPopulateMesh` bên Editor (CS0507, c37e0eb9).
- `using X;` **không** resolve namespace con — vụ `NativeAdMob.InFeed` từng vỡ
  compile; dùng alias hoặc tên đầy đủ.
- Lỗi: `AdError{Code, Message}` chỉ đi kèm event thất bại. Mã −1 là show bị
  từ chối ngay phía C#; no-fill Android 3, iOS 1 — C# chỉ phân biệt 0/khác 0.
- Màu đi qua cầu là một `int` ARGB (`AdColor`). **Trong suốt hoàn toàn là câu
  trả lời thật**, không phải "chưa đặt".

## A3. Vòng đời một overlay ad (native)

Đọc theo `OverlayAd.java` (iOS: `RANOverlayAd.mm`, cùng tên hàm không prefix):

1. `Configure(...)` → `configuredStyle` (`OverlayAdStyle`, bất biến;
   `SetClose` tạo bản mới bằng `WithClose`). `Random` của close side được
   **roll một lần mỗi presentation** (`ResolveCloseOnLeft`), timer đọc kết
   quả đó (`ResolveTimerOnLeft`), không roll lại.
   `Configure` cũng vào pool của unit: tắt tiếng khi style không dừng game
   (`!PausesGame()`), có tiếng khi dừng (B2).
2. `Load(activity)` → `Refill`: đưa Activity cho pool; chưa giữ head thì
   `pool.Take(SizeKey())` lấy ad cũ nhất có thể vừa panel, không có thì
   `pool.RequestFor(SizeKey())`. Overlay chỉ giữ **một** ad (head), phần còn
   lại nằm trong pool cho ai cần trước.
3. Giữ được head → `AddCachedAd` (`ownedAds` cho paid event),
   `BindPaidEvent` theo **identity ad**, `PrepareFace`: half-screen dựng cả
   `OverlayAdPresentation` và `Prepare()`, full-screen dựng
   `OverlayAdContentView` (`PrepareFullScreenContent`), kèm `MediaSignature`
   (video|mainImage|anyImage|aspect). Creative không layout được bị
   `DropUnrenderableAd` ngay tại đây — lúc không tốn gì của người chơi: ad
   về pool, đánh dấu không vừa `SizeKey()` của panel này, format khác vẫn lấy
   được. Pool báo load cho mọi format trong pool qua
   `OnLoadStarted`/`OnLoadCompleted` → `NotifyStateChanged` +
   `NotifyLoadingCompleted(cachedCount, cacheSize)`: số ad đang nằm trong pool
   và sức chứa của pool (`OnAdLoaded`, A2).
4. Load hỏng là việc của pool (B2). Overlay chỉ giữ chuỗi retry layout
   (`ScheduleLayoutRetry`, `BackoffDelayMs(streak, 2)`: hai lượt đầu ngay,
   rồi `Refill`). Chưa có gì từng layout được (`layoutProven == false`) mà
   cứ hỏng → panel là nghi phạm → poll chậm.
5. `Show(activity, onCompleted, slideIn, slideOutOnClose)` (hop main): từ
   chối nếu released / chưa configure / đang show; **quét hết hạn trước**
   (`RemoveExpiredCachedAds` → `NotifyCurrentState`, `Refill`); không giữ head
   → "Ad not ready"; `TakeCachedAd` → `activeNativeAd`, giữ cả thời điểm tải
   của nó; post: `Refill` lấy head mới và dựng face cho nó (chờ một nhịp để
   show này lấy đúng face đã hứa).
   - Full: `TakePreparedFullScreenContent` → `OverlayAdActivity.RegisterSession`
     + `StartSession` (kèm `VerifyInitialAttach`, xem README Android). Không
     có trạm "unrenderable" ở đường này (chủ dự án chốt).
   - Half: `TakePreparedPresentation` hoặc dựng tươi (`CreatePresentation` +
     `Prepare`), `IsLayoutUnrenderable` → `ScheduleLayoutRetry` + complete
     lỗi, ad chưa hiện về pool với thời điểm tải cũ, đánh dấu không vừa;
     `SetArrival(slideIn, slideOutOnClose, onDisplayed)` rồi `Show()`.
     Không trượt: displayed bắn khi window đã add (show message). Trượt:
     `onStart` đặt content view xuống dưới mép (translationY = chiều cao
     panel) và khóa chạm *trước* frame đầu; show message mới chạy animation;
     xong thì mở khóa, `OnPresented()` (đồng hồ bắt đầu) rồi mới displayed.
     Panel không đổi cỡ, window giữ nguyên chiều cao. Người chơi đóng
     (`CloseByPlayer`): `slideOutOnClose` và không `RedirectOnClose` thì trượt
     xuống rồi `dismiss`, còn lại đóng ngay. Bị gọi trượt ra giữa lúc đang
     trượt vào thì quay đầu tại chỗ.
6. Kết thúc: `CompletePresentation(ad, message)` → `ForgetAd` (pool hủy ad),
   `NotifyCompleted(onCompleted, message, adConsumed)` với `cachedCount`
   (overlay còn giữ head hay không: 0 hoặc 1) chụp tại chỗ, rồi
   `NotifyCurrentState`. Message rỗng = đóng bình thường; khác
   rỗng = C# đổi thành `OnAdDisplayFailed`. `Hide(slideOut)` ép đóng (full
   bỏ qua `slideOut`).
7. Hết hạn: `ScheduleCacheExpiry` theo head; `HandleCacheExpiry` hủy head
   cùng face dựng trên nó rồi `Refill`. `RequestPreparedFaceRebuild` khi
   asset về muộn đổi `MediaSignature`.
8. `Release()`: head chưa hiện về pool cho format khác; overlay rời pool,
   format cuối cùng rời thì pool hủy hết ad của nó.

Tất cả `Notify*` đi qua `AdEventDispatcher` (B1). Tham số chụp local trước
khi post.

## A4. Vòng đời một ô hình chữ nhật (native)

`RectAd.java` (ô) + `RectAdSlot.java` + `RectAdPresentation.java`:

1. Constructor(activity, adUnitId, cacheSize, backgroundColor) → vào pool tắt
   tiếng của unit ở nhịp main đầu tiên, pool tải ngay khi có Activity (không
   có `Load()` công khai). cacheSize là số ad ô nhờ pool giữ ấm, tối thiểu 1.
2. `Configure(activity, x, y, w, h, roundCornerPx)` → `AdoptActivity`
   (Activity đổi → `HandleActivityChanged`: bỏ hết đã materialize, ad đang
   dựng về pool) → `slot.Configure`. Configure ẩn ô (`visibleRequested` về
   `false`) rồi nhả ad như `Hide` (bước 4). Rect khác phải "chứng minh lại"
   (streak layout về 0); ad đang dựng về pool, ad đang hiện bị hủy. Bán kính
   đi theo rect (B11).
3. `Show(activity)` → `ShowCore`: `visibleRequested = true`; có entry active
   đã layout → bật window lên thẳng; không → `PresentCachedAd`: lấy khỏi pool
   ad cũ nhất có thể vừa cỡ ô (`TakeCachedAd(SizeKey())`, pool tải bù ngay;
   không có thì `RequestLoadFor`) → tạo `DisplayEntry` **materializing** với
   một `RectAdPresentation` → `Show()` của presentation (A5 bước 5) →
   `HandlePresentationReady` → promote thành **active** →
   `HandlePresentationDisplayed` → `NotifyDisplayed`.
   Cặp active/materializing là hai mặt của một ô: ad đang hiện và ad đang
   được dựng ngầm để thay. Presentation hỏng trước khi hiện (`Show()` trả
   `false`, hoặc dismiss kèm lỗi khi chưa displayed) → ad về pool, đánh dấu
   không vừa cỡ ô này; ô cỡ khác vẫn lấy được.
4. `Hide()` → ẩn window → `ReleaseHeldAds`: ô ẩn không giữ ad nào chỗ khác
   dùng được. Ad đang dựng (chưa hiện) về pool, giữ thời điểm tải cũ; ad đang
   hiện đã đứng đủ `MIN_DWELL_MS` 4s thì bị hủy; chưa đủ thì ở lại cho lần
   `Show` sau của chính ô này (impression đã tính, chỗ khác không hiện được
   nó).
5. Timer: `HandleRefresh` (dwell 30s cho ô không bao giờ ẩn) →
   `TrySwapActiveEntry` (ad đã đứng đủ 4s, cách lần đổi trước ≥
   `MIN_SWAP_INTERVAL_MS` 3s); `HandleEntryExpiry` (1 giờ, bỏ qua dwell);
   `HandleWatchdog` (1s) dọn entry kẹt; `HandleForegroundRecheck` (1s khi
   Activity nền) — chỉ present cho ô đang hiện mà trống; `HandleLayoutRetry`.
   Timer nào cũng chỉ lấy ad từ pool khi ô đang hiện.
6. Ô: pool có ad mới hay ad trả về → `OnPoolChanged` → ô đã configure, đang
   hiện, trống (`WantsCachedAd`) và có ad có thể vừa cỡ thì present; `OwnsAd`
   (set concurrent) cho paid event; click của pool chuyển tới đúng ô đang giữ
   ad (`CommitAdClick(ad)`), latch chống double-click nhả theo window
   visibility.

## A5. Engine ô hình chữ nhật: layout được chọn thế nào

`RectAdPresentation.Show()`:

1. Chờ `android.R.id.content` sẵn sàng (tối đa 10s, hết thì dismiss bình
   thường — không được park ô). Creative thiếu headline hoặc CTA → fail;
   icon SDK đưa mà không có ảnh (`getDrawable()` / `image` rỗng) thì coi như
   không có icon: mọi chỗ dựng icon đều hỏi `HasRenderableIcon`.
2. `viewFactory.FindMainImage()`, `HasVideoContent()` →
   `layoutEngine.ChoosePlans(screenW, screenH, hasVideo, hasMainImage)`.
3. **`ChoosePlans`**: với mỗi tier ∈ {COMPACT, REGULAR, ROOMY} × mỗi tổ hợp
   asset tùy chọn (body, advertiser, rating, icon — 16 mask, bỏ mask đòi asset
   không có) → `EvaluatePlan` cho từng template: `COMPACT_ROW`,
   `COMPACT_COLUMN` luôn; có ảnh chính → thêm `MEDIA_LEFT`, `MEDIA_TOP`,
   `MEDIA_BACKGROUND`, `MEDIA_RIGHT`; có video → chỉ `MEDIA_LEFT` video
   (không ảnh tĩnh, không không-media, không nền). Không plan nào → layout
   fail, ô bỏ ad và tải ad khác (B7).
   `UpdateVariantBest` giữ bản tốt nhất mỗi (template, tier, mask).
4. **`EvaluatePlan`**: chặn bề rộng tối thiểu (`MinimumWidth(tier)` = sàn
   media + 2×thụt góc, trừ compact không media) → `ResolveMediaDimensions`
   (cỡ media theo tier/aspect, `ResolveVideoSlot` cho video) → **thang chữ**
   `TEXT_LADDER_MODES/SCALES` (B6): mỗi nấc lấy cây probe cache theo
   (template, tier, media, video, mask) từ `GetProbeLayout`, áp cỡ/chế độ
   chữ (`ConfigureProbeLayout`), né dấu AdChoices (`DodgeAdChoicesMark`, iOS
   `dodgeAdChoicesMarkInProbe:`, B12), đo chiều cao tự nhiên
   (`MeasureNaturalContentHeight`, UNSPECIFIED), ảnh tĩnh ở MEDIA_TOP,
   MEDIA_LEFT, MEDIA_RIGHT thì `ShrinkImageToFitHeight` (co media về sàn rồi
   nhị phân lên), đo EXACT,
   `NudgeCutTextsWhole` (co riêng dòng cắt 0.92/bước tới 0.7), rồi
   `validator.ValidateAssetGeometry(probe)`; nấc đầu tiên qua thì tính điểm
   (bảng ở B6), ghi `measuredContentHeight`. Không nấc nào qua →
   `RecordRejection` (đọc được qua `DescribeLastRejections` trong log fail).
5. `FinalizePlanList`: sort theo điểm, `PruneDominatedPlans` (mỗi họ
   template/tier/media giữ bản tốt nhất + một bản "chỉ asset bắt buộc" làm
   dự phòng), `ExpandMediaWithinPlan` cho vài plan media đầu (nở media trong
   `ContentWidth/Height`, giữ chỗ chữ `RequiredTextSlotWidth`, chấm lại phần
   diện tích media và khoảng trống theo cỡ đã nở), rồi sort lại.
6. Presentation đi **theo thứ tự** danh sách: `ActivateNextLayoutPlan` →
   `viewFactory.BuildNativeAdView(plan, mainImage)` (dựng cây thật, đăng ký
   asset, `setNativeAd`) → `CreateAndShowHostDialog` (window
   `TYPE_APPLICATION_PANEL`, không focusable, dưới nav bar) →
   `ObserveFinalAssetGeometry`: chờ hình học ổn định (`GeometrySignature`
   giống nhau 3 pass, quan sát 80–500ms), validate lần cuối trên cây thật
   (SDK có thể đã thêm view của nó); hỏng → `RejectActivePlanAndTryNext`;
   qua → `HandlePresentationReady`. `FitHostInsideContentRoot` /
   `UpdateHostLayoutForActivePlan` đặt window đúng rect.

Log iOS `Rect ad layout ready <plan>` (Android không in dòng này) là bản tóm
tắt bước 6 — đọc nó trước khi đoán vì sao ô trông như vậy.

## A6. Overlay: layout được dựng thế nào

`OverlayAdContentView.Build(requestedPanelHeight)` — một cây `LinearLayout`
tự co giãn, quyết định theo thứ tự:

1. Media có gì: video / `mainImage` / ảnh fallback (`FindFallbackMediaImage`);
   `minimumMediaSize` = 120dp (video) hay 48dp (ảnh); aspect từ
   `GetMediaAspectRatio` (cờ "có báo" hay không).
2. Panel: full = cả màn; half = `ResolveHalfScreenPanelHeight(ratio)` (một
   nơi duy nhất, công bố ra ngoài qua `PanelHeight`). `panelHostsMedia` = full hoặc panel ≥
   sàn media + 88dp nội dung dưới. `tickerLayout` = half và panel < 120dp.
   `iconHero` = half, không ticker, (không media hoặc không chứa nổi) và có
   icon. Creative video mà panel không chứa nổi media (ticker, hoặc
   `panelHostsMedia` sai) → `layoutUnrenderable`: bỏ ad, xin ad khác, không
   hiện bằng icon hay ticker.
   `sideMediaLayout` = có media và `ShouldUseSideMedia` (aspect < 0.85,
   panel ≤ 1.3×bề rộng, media ≥ sàn, rail ≥ 120dp; bề rộng media =
   min(panelH×aspect, 0.56×W)).
3. Dựng: `NativeAdView` → `contentColumn` (padding 5dp hai bên, 30dp dải
   control trên, `clipToPadding false` để media bleed bằng margin âm) →
   `MediaView` (nền đen cố ý, FIT_CENTER, min = sàn) → identity row (icon
   36dp + headline) → advertiser/rating → body → CTA (44–56dp) →
   `CreateAttributionView`, `CreateControlView` ×2 (close vẽ bằng
   `CloseGlyphDrawable`, countdown) → `BindAssets` → `setNativeAd`.
4. Chọn đường:
   - **Side-media**: `ConfigureResponsiveSideRail` (ngân sách B8; rail: icon
     đứng riêng ≤30% rail, chữ full width, `ApplyRailContentScale`,
     `GrowTextLines`, `GrowRailIcon`, `RailFits`); media để thừa height thì
     CTA/body **tràn xuống dưới** full width.
   - **Half-screen xếp chồng**: `ConfigureResponsiveCollapsibleContent`:
     thử **né dải control** trước (`RunCollapsibleFitPipeline` với media thụt
     lề, nếu hụt thì `ApplyStripAvoidingFloors` CTA 36 / icon 28 / bỏ
     padding phụ rồi `ContentFitsWithMinimumMedia`); vừa → giữ bộ sàn gọn
     làm **trang phục mặc định** + `EnableControlAvoidance`; không vừa →
     bleed media + chạy lại pipeline; vẫn không → ảnh: **scrim**
     (`ApplyScrimLayout`), video: không có đường lùi; scrim không được hoặc
     là video → `layoutUnrenderable` (bỏ ad, xin ad khác).
   - **Full-screen**: **thẻ** (`BuildCard`), không qua pipeline nào ở trên.
     Một thẻ trắng bo 20dp giữa màn hình, rộng tối đa 440dp (ngang 760dp),
     chừa trên cho control (hoặc thanh app open) và 16dp mỗi mép. Dọc: ảnh
     trên, rồi icon 56dp, headline, advertiser/rating, body ≤3 dòng, CTA pill
     52dp, tất cả căn giữa; end card đưa app lên đầu (icon 88dp, headline)
     rồi mới ảnh. Ngang: ảnh bên trái (≤58% bề rộng thẻ), cột chữ bên phải,
     thẻ cao ≤420dp. `FitCard` chạy **sau `BindAssets`** (đo chữ thật): ảnh
     lấy phần chữ để lại, tối đa đúng tỉ lệ creative và 60% chiều cao; hụt
     sàn media thì nhường theo thứ tự body 1 dòng → bỏ body → icon 40dp; vẫn
     hụt thì media giữ đúng sàn (full-screen không có trạm unrenderable, xem
     A9): Android để thẻ cao hơn chỗ, tràn mép màn hình; iOS và Editor kẹp
     thẻ trong chỗ, phần đáy (có thể cả CTA) bị cắt. Ngang: ảnh giữ bề rộng,
     cột chữ nhường cùng thứ tự tới khi vừa chiều cao thẻ (`ShrinkCardText`,
     iOS `ran_fitCardInSize`). Badge Ad và AdChoices nằm trên góc thẻ
     (`NativeAdView` đúng cỡ thẻ); close/timer là đĩa ở góc màn hình, đĩa sáng
     dấu tối khi panel tối. Thẻ có viền 1dp vẽ đè lên media: `#DADCE0` trên
     panel sáng (thẻ trắng không có mép), trắng 20% trên panel tối (ảnh tối
     hay video tối lẫn vào panel thì thẻ mất mép trên). Dải thừa quanh media,
     ảnh hay video, màu `#F1F3F4` của thẻ: dải đen hai bên video dọc lẫn vào
     panel tối và cắt thẻ làm hai (đo trên emulator 2026-11-26). Mặt dựng
     sẵn gắn với cỡ màn hình lúc dựng: màn hình đã xoay thì Android dựng lại
     lúc show (`TakePreparedFullScreenContent`), iOS xếp lại thẻ trong
     `ran_layoutCard`. Cửa sổ đổi cỡ khi ad đang hiện (xoay, máy gập, chia
     màn hình): Android xếp và fit lại thẻ trong `RefitCard` (từ
     `onConfigurationChanged`), giữ MediaView tại chỗ vì MediaView đã bind
     không được rời cha (`PlaceCardMedia`); iOS cũng qua `ran_layoutCard`. Layout
     do C# chọn (`FullScreenLayout`, qua `SetLayout`). App open không
     `RedirectOnClose` có **thanh app** (`BuildAppBar`): trắng, đầu trái bo
     tròn, chạy ra ngoài mép phải, icon + tên game, spinner trong lúc đếm,
     xong thì "Continue to app" + chevron; thanh là lối ra nên close/timer ẩn
     (`SyncAppBar`). Thẻ không qua settle, né control hay `ResolveContentHeight`.
5. **Né control** (`EnableControlAvoidance` → `RecomputeMediaControlAvoidance`):
   stack dồn đáy (`Gravity.BOTTOM`), đo phần dưới (`MeasureLowerContent`),
   bốn ứng viên vị trí media (B10), chọn to nhất chạm tường nhìn thấy,
   control di chuyển theo; box thắng bị chặn chiều cao mà chưa chạm ngang →
   `TrimChromeForStarvedMedia` rồi tính lại; không ứng viên nào chứa nổi sàn
   → `avoidanceFoundNoBand` → scrim (ảnh) / `layoutUnrenderable` (video).
   **Kế hoạch được lập trong
   `onMeasure`** theo kích thước thật của panel, idempotent
   (`plannedPanelWidth/Height`, `avoidancePlanDirty`, `planningInMeasure`
   chặn `requestLayout` thừa) — xem B6 mục "layout pass".
6. `SettleContentBeforeLayout` trước lần layout đầu: `SettleIconSize`
   (icon theo chữ, trừ khi pipeline đã "tiêu" icon), `SettleTextFitting`
   (mỗi text co tối đa một lần), tối đa 4 vòng; `MatchMediaSizeToAvailableSpace`,
   `ObserveMediaSize`, `ObserveBadgePositions`, `UpdateControlInsets`.
7. Trình bày: half → `OverlayAdPresentation` (Dialog, `TYPE_APPLICATION_SUB_PANEL`,
   `FLAG_NOT_FOCUSABLE`, dưới nav bar); full → `OverlayAdActivity` attach
   content dựng sẵn từ `Session`. `OnPresented(remainingMs)` bật countdown;
   `OnPaused`/visibility đóng băng nó.

## A7. Các template

Ô hình chữ nhật (`RectAdLayoutEngine.TEMPLATE_*`), mỗi ô có badge "Ad" góc trái
trên và AdChoices góc phải trên, nội dung thụt theo bo góc:

```
COMPACT_ROW            COMPACT_COLUMN           MEDIA_TOP
┌──────────────────┐   ┌──────────────────┐     ┌──────────────────┐
│Ad            (i) │   │Ad   [ icon ]  (i)│     │Ad   ┌──────┐ (i)│
│[ic] Headline     │   │  Headline        │     │     │media │    │
│     body… [ CTA ]│   │  body            │     │     └──────┘    │
└──────────────────┘   │[ic][   CTA     ] │     │Headline / body   │
 hàng, căn giữa dọc    └──────────────────┘     │[ic][   CTA     ] │
                        icon nuốt height dư     └──────────────────┘
                                                 media nuốt height dư
MEDIA_LEFT (RIGHT là gương)                    MEDIA_BACKGROUND (scrim)
┌──────────────────┐                           ┌──────────────────┐
│Ad            (i) │                           │Ad  ảnh phủ ô (i) │
│┌─────┐ [ic] Head │                           │  + ambient sau    │
││media│ body      │                           │  + veil 70% đen   │
│└─────┘ advertiser│                           │[ic] Headline      │
│[      CTA       ]│ ← CTA xuống dưới khi     │body / advertiser  │
└──────────────────┘   media để thừa height   │[      CTA       ] │← khối neo đáy
                                               └──────────────────┘
```

Video chỉ có `MEDIA_LEFT` (rail: icon / spacer / CTA, headline dưới); không
có scrim, không có ảnh tĩnh, không có bản không media.

Full-screen (`OverlayAdContentView`, thẻ): close/timer là đĩa ở góc màn hình,
badge Ad và AdChoices trên góc thẻ:

```
Interstitial               AppOpen, RedirectOnClose tắt   EndCard
                  ( ✕ )    ╭─ [ic] Game   Continue ›                       ( ✕ )
╭────────────────────╮     ╭────────────────────╮         ╭────────────────────╮
│Ad               (i)│     │Ad               (i)│         │Ad    [ icon ]   (i)│
│       media        │     │       media        │         │      Headline      │
│                    │     │                    │         │     advertiser     │
├────────────────────┤     ├────────────────────┤         ├────────────────────┤
│      [ icon ]      │     │      [ icon ]      │         │       media        │
│      Headline      │     │      Headline      │         ├────────────────────┤
│     advertiser     │     │     advertiser     │         │    body ≤3 dòng    │
│    body ≤3 dòng    │     │    body ≤3 dòng    │         │ (       CTA      ) │
│ (       CTA      ) │     │ (       CTA      ) │         ╰────────────────────╯
╰────────────────────╯     ╰────────────────────╯

Ngang (mọi layout xếp như nhau)
                                    ( ✕ )
╭───────────────┬──────────────────╮
│Ad             │    [ icon ]   (i)│
│               │    Headline      │
│     media     │   advertiser     │
│               │      body        │
│               │ (     CTA      ) │
╰───────────────┴──────────────────╯
```

Media rộng hết thẻ (dọc) hoặc cao hết thẻ, sát mép trái (ngang); badge Ad đè
lên ảnh, thụt 12dp. `( ✕ )` là close/timer ở góc màn hình theo `CloseSide`,
`TimerSide`. AppOpen bật `RedirectOnClose` xếp như Interstitial; thanh app
thay close/timer.

Half-screen (`OverlayAdContentView`), dải control 30dp trên cùng với badge Ad,
close, timer:

```
Xếp chồng (mặc định)      Side-media (creative dọc)   Ticker (panel < 120dp)
┌────────────────────┐    ┌────────────────────┐      ┌────────────────────┐
│Ad  [t]        [✕]  │    │Ad [t]         [✕]  │      │Ad[ic]Headline…[CTA]│
│ ┌────────────────┐ │    │┌──────┐  [ icon ]  │      └────────────────────┘
│ │  media (né     │ │    ││      │  Headline  │       icon-hero: icon thế
│ │  control)      │ │    ││media │  body      │       chỗ media khi không
│ └────────────────┘ │    ││      │  advertiser│       media / panel thấp
│ [ic] Headline      │    │└──────┘  [ CTA ]   │
│ body               │    │[  CTA tràn xuống  ]│  ← khi media thừa height
│ [      CTA       ] │    └────────────────────┘
└────────────────────┘
 stack dồn đáy, media mọc lên tới tường nhìn thấy
```

Scrim overlay = ảnh phủ panel + veil + chữ trên veil, chỉ khi xếp chồng đã
hết mọi nấc, chỉ cho ảnh; video không có scrim (B7).

## A8. Thuật ngữ

| Từ | Nghĩa trong pack |
|---|---|
| **pool** | ad thô của một ad unit id + chế độ tắt tiếng, dùng chung cho mọi format trên unit đó (B2) |
| **ô / slot** | `RectAd`: một rect game đặt (ô in-feed, MRec), vào pool và báo sự kiện / `RectAdSlot`: phần lo hình chữ nhật của ô đó |
| **entry active / materializing** | ad đang hiện trong ô / ad đang được dựng ngầm để thay |
| **face, bản dựng sẵn** | presentation (half) hay content view (full) dựng lúc load cho ad đầu hàng |
| **head** | ad overlay giữ từ pool — cái `Show` kế tiếp lấy |
| **plan** | một ứng viên layout của ô hình chữ nhật: template + tier + bộ asset + cỡ chữ + cờ marquee + điểm |
| **probe** | cây view thăm dò để đo plan, cache theo cấu trúc, không bind ad |
| **tier** | thang cỡ chữ/icon COMPACT/REGULAR/ROOMY — **không** phải cỡ ô |
| **nấc (rung)** | một bước của thang chữ (chế độ + scale) |
| **wrapped / marquee** | chữ nằm trọn trong số dòng cho phép / một dòng cuộn ngang |
| **nudge** | co riêng một dòng bị cắt trước khi đổi nấc |
| **band** | dải media ngang (MEDIA_TOP, overlay xếp chồng) |
| **rail** | cột chữ+nút bên cạnh media dọc (MEDIA_LEFT, side-media) |
| **scrim / veil** | template ảnh làm nền + tấm phủ đen 70% + khối chữ trên tấm phủ |
| **ambient** | bản copy ảnh crop-phủ, dim 55%, đứng sau ảnh FIT trong scrim |
| **ticker / icon-hero** | overlay panel thấp: một hàng cuộn / icon thế chỗ media |
| **control strip** | dải 30dp chứa badge Ad, close, timer của overlay |
| **badge** | "Ad" và AdChoices; được đè media/icon, không đè chữ |
| **reserve** | ô giữ chỗ trong suốt ở góc AdChoices — cho layout và validator, không phải dấu thật (dấu thật do SDK vẽ) |
| **né control (avoidance)** | pass chọn vị trí media để không nằm dưới close/timer |
| **tường nhìn thấy** | trần panel, vạch badge, cột close/timer, lề sync — thứ media được chạm |
| **lề sync** | lề 8dp hai bên mà media và mọi element dưới cùng tôn trọng |
| **sàn chính sách** | 120dp video / 48dp ảnh cho MediaView đã đăng ký |
| **session / restore** | một lượt show full-screen Activity và máy mở lại nó khi bị hệ thống vứt |
| **màn che (cover)** | lớp màu trơn của package khác đứng sau/trên ad; pack này không biết tới nó ngoài bảng thứ tự lớp |
| **bậc lớp (tier)** | số trong `layer.name = "riseon.layer.<bậc>"` quyết vị trí subview trên iOS (B3) |
| **dwell** | thời gian ad đã đứng trên màn — điều kiện để được xoay |
| **generation** | bộ đếm show (C#) / load (native) để callback trễ không nhận nhầm |
| **gate** | script biên dịch kiểm tra trước khi nói "xong" (javac / csc / balance) |

## A9. Đánh đổi đã chọn (và phương án bị bác)

| Quyết định | Bị bác | Vì sao | Ở đâu |
|---|---|---|---|
| Full-screen là **Activity** | Dialog trên Unity Activity (33cf6634) | Activity cho pause miễn phí và đúng thang; delay thật là bind creative → dựng sẵn lúc load (d64fdc88) | README Android §3 |
| Sự kiện Java→C# qua **HandlerThread riêng** | gọi thẳng trên thread SDK / main | main thread parked ở GC suspend của IL2CPP khi Unity pause = ANR (9d6667bc) | B1 |
| Pack **không marshal** về thread Unity | marshal mọi callback | caller trễ một frame dù không cần; thứ gì đang dừng Unity phải được gỡ từ thread không qua player loop | B1, B4 |
| Readiness **chỉ native công bố**, count đi kèm completion | C# tự suy từ consumed | suy sai đè lên sự thật; thông báo riêng trễ 1.5 frame | B1 |
| Prefetch **khi displayed**, không hủy ad ấm | load khi đóng; hủy để nạp mới | miss collapsible lần 2; prefetch tự vứt việc | B2 |
| Retry ở **native**, bên gọi không chồng | retry ở bên gọi | nhân đôi request lúc no-fill | B2 |
| Hết hạn **1 giờ**, quét cả trước show | 4 giờ (AppOpenAd) | native ad hết hạn sau 1 giờ theo docs; timer uptime đứng khi máy ngủ | B2 |
| Chữ **trọn hoặc cuộn**, không cắt | ellipsize | chính sách + đọc được; cuộn là giá trả cho element | B6 |
| **Nguyên vẹn > cỡ chữ > cuộn**; cuộn phụ trước headline | cuộn ngay khi hụt | chữ tĩnh dễ đọc hơn; headline là thứ cuối cùng được động | B6 |
| Điểm: element −10 000, cuộn +3 000, lệch tier +1 000/nấc, trống +100/px | tối ưu trống trước | tháp ưu tiên của chủ dự án: nhiều element nhất trước; mỗi khoản riêng dưới một element, nhiều khoản cộng lại (cuộn, scrim, lệch tier, cỡ) có thể vượt | B6 |
| Thưởng **cỡ headline thật** (−400/sp, iOS /pt) và **diện tích media nhìn thấy** (−50/%), cộng cả hai vẫn dưới một element; không thưởng tier | thưởng theo tier (cộng chỉ số tier vào richness) | tier to thu chữ còn 0.55 nhỏ hơn tier nhỏ ở 0.85 (9,4sp với 10,2sp) mà vẫn thắng; ảnh nền dưới lớp phủ thắng ảnh đặt cạnh chữ (đo trên Redmi, unit thật ở test mode: 30/30 ô ra ROOMY, 8 ô cuộn cả headline, mọi ô cao từ 58dp ra ảnh nền) | B6 |
| Chữ **né dấu AdChoices theo bề ngang** (margin phải vừa đủ) | dải trên cao bằng dấu suốt bề ngang ô | ô 44dp không còn chỗ cho dải 15dp; chỉ chữ nằm dưới dấu mới trả giá (đo trên Redmi: reserve tính theo 10% cạnh ngắn để dấu thật đè chữ ở 12/15 ô, né rồi còn 0/15) | B12 |
| Cột chữ cạnh media **cao theo nội dung, tối thiểu bằng media** | cao đúng bằng media | cột bị ép cắt CTA mất nửa trong khi chiều cao đo vẫn vừa (đo trên Redmi, ô 364×73dp) | B6 |
| Spacing **chặn theo cạnh ngắn ô** | spacing theo tier | ô 96dp chạy ROOMY được padding 5% mỗi bên | B6 |
| Asset rỗng **GONE**, thiếu asset **ẩn** không loại ad | chừa chỗ / xin ad khác | chính sách chỉ đòi hiện thứ SDK có gửi; Google template cũng ẩn | B6 |
| MediaView **luôn đăng ký** asset chính; ảnh fallback là **con** của nó | `setImageView` | mã 3 "Asset uses ImageView" — unit ngừng fill | B7 |
| iOS: asset view **không nhận chạm** (`userInteractionEnabled = NO`), cả nút CTA | CTA nhận chạm để có hiệu ứng nhấn | hướng dẫn native của Google: chạm phải tới `GADNativeAdView` để SDK báo click; không tắt thì SDK tự tắt và log "User interactions must be disabled on the asset view to enable click handling" (iPhone, unit thật ở test mode: 86 lần trong 15 ô) | README iOS |
| Sàn **120dp chỉ video**, ảnh 48dp | 120dp mọi media | warning SDK có điều kiện "shows video"; ô 96dp mất fill | B7 |
| Video không vừa → **bỏ ad, tải ad khác** | video nhỏ / ảnh tĩnh / scrim / không media | không render / đăng ký dưới sàn là vi phạm / veil trên video = viewability / ad video thiếu MediaView 120 bị validator báo lỗi | B7 |
| Nền MediaView **đen** | màu panel | tell cho creative render không kín | B7 |
| **Scrim là phương án cuối** (penalty 6 000), ảnh crop-phủ + ambient | scrim ưu tiên vì diện tích media | không được thắng band cùng element; đen mép xấu | B7 |
| Ambient **chỉ scrim** | ambient mọi band | user thu hẹp: band giữ đúng cỡ creative, letterbox màu panel | B7 |
| Media **trái**, phải chỉ dự phòng | phải | thuận tay phải: chữ+nút dưới ngón cái | B7 |
| Side-media **chỉ aspect < 0.85** | nới 1.05 | ad vuông Meta lọt rail, xấu | B7 |
| **Không hardcode ratio fallback** | 1.0 / 16:9 | không báo → nguyên dải; có báo → đúng cỡ (cả hai đều từng bị làm sai) | B7 |
| Media **không bleed**, lề 8dp sync | bleed hết bề ngang | user đòi lại lề sau khi bị đục | B10 |
| Chỉ **tường nhìn thấy** mới tính | mọi vạch | media dừng ở vạch vô hình = "chưa chạm gì mà không to" | B10 |
| **Cả hai vị trí control** là vật cản | chỉ nút đang hiện | đổi neo khi timer→close bị cấm tuyệt đối | B10 |
| Không đệm buffer | 2dp quanh control | "sát sàn sạt miễn là không overlap" | B10 |
| Bộ sàn gọn là **mặc định** của stacked-avoid | phương án cuối | height dôi dồn hết cho media | B10 |
| Kế hoạch né lập **trong `onMeasure`**, idempotent | `post()` từ `onSizeChanged` (ad67dcb4) | bản post là cú nhảy sau frame đầu trên máy có cutout khác | A6, B6 |
| Panel thấp: **tôn trọng ratio**, thang media→icon-hero→ticker→nới tối thiểu | nới panel cho vừa media | panel là chiều cao game xin | B10 |
| Full-screen là **một thẻ giữa màn hình** (trắng, bo 20dp, CTA pill) | media tràn viền + chữ trên nền panel, né control | một khối gọn không phải né control; chữ và màu nhà quảng cáo đọc đúng trên nền trắng | A6, A7 |
| Thẻ hụt sàn media sau khi nhường hết → **giữ sàn** (Android tràn mép, iOS và Editor cắt đáy thẻ) | đánh unrenderable, bỏ ad | full-screen không có trạm unrenderable (chủ dự án chốt 2026-08, comment ở `OverlayAd.Show`); màn hình điện thoại nào cũng dư chỗ cho thẻ | A6 |
| Overlay Activity xin orientation **BEHIND** | khóa portrait | ad xoay theo game như iOS; BEHIND không phải orientation cố định nên API 26 không crash | README Android §1 |
| Icon pipeline "tiêu" thì settle **không nở lại** | icon theo chữ | chứng chỉ fit bị vô hiệu sau lưng pipeline | A6 |
| Bo góc: thụt = **sàn**, không cộng | cộng thêm | tiêu một lề hai lần | B11 |
| Bán kính đi theo **rect** (`SetSize`) | Settings lúc sinh | ô ấm từ lúc tạo, đo sau | B11 |
| AdChoices ô hình chữ nhật Android: **padding `NativeAdView`** để lớp của SDK co vào | đăng ký `AdChoicesView`; với vào view con của SDK | đăng ký không dời được dấu (đo trên máy); với vào con của SDK phụ thuộc thứ tự/thời điểm | B12 |
| AdChoices ô hình chữ nhật iOS: **sửa hai constraint ghim khung của SDK** | gán `adChoicesView` (header); thu nhỏ `GADNativeAdView` | gán: trên máy giống thất bại Android, khung 9pt nhỏ hơn dấu; thu nhỏ: MediaView scrim ra ngoài ad view (SDK coi là lỗi tích hợp), mất chạm ở viền | B12 |
| Ô **chỉ giữ ad lúc hiện**; ad đã hiện chưa đủ dwell ở lại cho lần hiện sau của ô | ô ẩn giữ sẵn ad thay thế | ad giữ trong ô ẩn là ad chỗ khác đang thiếu; ad đã hiện thì chỗ khác không hiện được | B13 |
| Dwell 4s trước khi ad bị thay | xoay theo đồng hồ / mỗi hide | liếc không phải lượt; foreground không phải rotation | B13 |
| Android: half ad type 1002, window nào muốn nằm dưới nó cũng 1002 và **add trước** | type 1003 cho ad | 1003 vẽ **dưới** 1002 (dumpsys) | B3, README Android |
| iOS: thứ tự lớp bằng **bậc trong `layer.name`**, mỗi package tự chèn | neo vào view của nhau (`frontmostInFeedView`, `HalfScreenCover_View`) | hai package phải biết nhau; `zPosition` không đổi thứ tự hit-test | B3, README iOS |
| iOS: ad full tự `UnityPause(1/0)` như SDK | chung cờ pause với lớp khác | pack đứng riêng; ai cần giữ pause xuyên qua ad tự canh | B4, README iOS |
| Chiều cao half **công bố ra ngoài** (`PanelHeight`) | lớp phía sau tự đo | `decorView.getHeight()` lệch trên máy cutout → dải đen | B10 |
| Trượt bằng **dịch content view** | window animation / đổi cỡ panel | thứ đứng sau phải khớp mép panel suốt lúc trượt; window animation không quay đầu giữa chừng được | A3 |
| Editor là **ảnh chụp**, không mô phỏng | lifecycle giả (e81149c2) | user chốt: để nhìn, không để tái tạo độ trễ | README Editor |
| Editor **không port engine** | port | "vài layout tiêu biểu" là đủ | README Editor |
| asm Editor: `defineConstraints UNITY_EDITOR` | `includePlatforms Editor` | Unity cấm AddComponent từ asm editor-only | README Editor |
| C#: **asm-per-platform**, không `#if` | `#if` + partial | user thích asm; một đường code | A2 |
| Java method **PascalCase**, phẩy đầu dòng | camelCase | đọc như C#, ba ngôn ngữ một nhịp | B14 |
| Icon **không có ảnh = không có icon**, ad vẫn hiện | loại creative vì icon "required if provided" | icon không có ảnh là do SDK tải ảnh hỏng; mẫu native của Google cũng chỉ ẩn view đó; loại thì SDK trả lại đúng creative ấy và ô không bao giờ hiện (đo trên iOS simulator, unit thật ở test mode: 69 lần loại / 30 lần hiện) | A5 |

## A10. Bản đồ đọc code (câu hỏi → hàm)

Tên là của Java; Obj-C++ cùng tên với prefix `ran_`/không prefix (README iOS).

| Câu hỏi | Đọc |
|---|---|
| Vì sao ô chọn template/tier này | `RectAdLayoutEngine.EvaluatePlan` (điểm), `ChoosePlans` (ứng viên), log iOS `Rect ad layout ready` |
| Vì sao plan bị loại | `RecordRejection`/`DescribeLastRejections`, `RectAdLayoutValidator.ValidateAssetGeometry`, `ValidatePolicyVisibleText` |
| Vì sao media của ô to/nhỏ | `ResolveMediaDimensions`, `ExpandMediaWithinPlan`, `ShrinkImageToFitHeight`, `MediaSizeForTier`, `MediaPolicyFloorPx` |
| Vì sao chữ cuộn | `TEXT_LADDER_MODES`, `NudgeCutTextsWhole`, `ApplyTextMode`, `BodyMaxLines`/`HeadlineMaxLines`/`AdvertiserMaxLines` |
| Cây view của ô theo một template | `RectAdViewFactory.BuildContent` (nhánh template), `BuildIdentityAndText`, `BuildHeadlineAndActionStack`, `BuildTextStack`, `AddBodyAndOptional`, `CreateIconFiller` |
| Badge, dải badge, bo góc | `AddBadgeOverlays`, `ConfigureBadgeOverlays`, `BadgeEdgeInsetPx`, `ConfigureInsetContent`, `CornerInsetForRadius`, `PaddingForTier`, `RectAdPresentation.ConfigureBackground` |
| Dấu AdChoices của SDK nằm đâu / thụt thế nào / chữ né nó ra sao | Android `RectAdViewFactory.InsetSdkOverlay` + `ReportSdkLayerShape` + `DodgeAdChoicesMark`; iOS `insetSDKAdChoicesInNativeAdView:` (gọi từ `RANRectAdPresentation.ran_layoutBoundContent`); cây view thật: Android `uiautomator dump --windows` (README Android §9), iOS dump tạm trên Mac (README iOS §7) |
| Khi nào ô hiện, hình học có ổn không | `RectAdPresentation.Show`, `ActivateNextLayoutPlan`, `ObserveFinalAssetGeometry`, `RejectActivePlanAndTryNext`, `GeometrySignature` |
| Ô xoay/không xoay, nhả ad khi ẩn | `RectAdSlot.ReleaseHeldAds`, `IsActiveEntryConsumed`, `TrySwapActiveEntry`, `HandleRefresh`, `CurrentVisibleDurationMs`, `HandleForegroundRecheck` |
| Ô kẹt/trống | `HandleWatchdog`, `RemoveExpiredMaterializingEntry`, `RemoveExpiredActiveEntry`, `HandleEntryExpiry`, `ScheduleLayoutRetry` |
| Pool load/retry/hết hạn, lấy/trả/bỏ ad, sức chứa | `NativeAdPool.StartLoad`, `Take`, `GiveBack`, `RequestFor`, `DropUnfitAd`, `BackoffDelayMs`, `HandleCacheExpiry`, `Capacity` |
| Ô với pool | `RectAd.TakeCachedAd`, `ReturnCachedAd`, `RequestLoadFor`, `OnPoolChanged`, `CouldUse`, `WantedCount`; `RectAdSlot.SizeKey`, `ReturnEntry` |
| Overlay head/face | `OverlayAd.Refill`, `SizeKey`, `DropUnrenderableAd`, `AddCachedAd`, `PrepareFace`, `PreparePresentation`, `PrepareFullScreenContent`, `MediaSignature` |
| Overlay show/complete | `OverlayAd.Show`, `CompletePresentation`, `NotifyCompleted`, `NotifyCurrentState`, `OnActivityPresentationCompleted` |
| Full-screen không mở / mở lại | `OverlayAdActivity.StartSession`, `VerifyInitialAttach`, `ScheduleSessionRestore`, `RestoreSessionIfPossible`, `RetryOrFailRestore`, `Session` |
| Overlay chọn side/ticker/hero/scrim | `OverlayAdContentView.Build` (đầu hàm), `ShouldUseSideMedia`, `ConfigureResponsiveCollapsibleContent`, `ApplyScrimLayout` |
| Thẻ full-screen sai cỡ, mất body/icon, thanh app, không xếp lại khi cửa sổ đổi cỡ | Android `BuildCard`, `FitCard`, `GiveWay`, `RefitCard`, `BuildAppBar`, `SyncAppBar`; iOS `ran_arrangeCard`, `ran_fitCardInSize:safeArea:`, `ran_layoutCard`, `ran_buildAppBar`, `ran_syncAppBar`; Editor `EditorAd.Card.cs` |
| Media overlay không to ra / đè control | `EnableControlAvoidance`, `RecomputeMediaControlAvoidance`, `TrimChromeForStarvedMedia`, `MeasureLowerContent`, log `avoidance` |
| Rail side-media | `ConfigureResponsiveSideRail`, `SideRailPadding`, `ApplyRailContentScale`, `GrowTextLines`, `GrowRailIcon`, `RailFits` |
| Chữ overlay co/cuộn | `SettleContentBeforeLayout`, `SettleTextFitting`, `ApplyResponsiveContentScale`, `MarqueeWhenTooLong` |
| Close/timer/redirect | `ArmCloseFromPress`, `ForgetClosePress`, `RunCloseFromRedirect`, `CommitAdClick`, `StartCountdown`, `OnPresented`, `OnPaused` |
| Chiều cao panel half | `ResolveHalfScreenPanelHeight`, `OverlayAd.ResolvePanelHeight` |
| Trượt vào/ra | `OverlayAdPresentation.SetArrival`, `onStart`, `OnWindowShown`, `Dismiss(slideOut)`, `CloseByPlayer`; `OverlayAdContentView.SetTouchLocked` |
| Window/thứ tự/system UI | `OverlayAdPresentation.ConfigureWindow*`, `RectAdPresentation.ConfigureHostWindow`, `OverlayAdActivity.ConfigureSystemBars` |
| Sự kiện về C# | `AdEventDispatcher.Post`, `Notify*` ở hai unit, `*Proxy.cs` (tên method phải khớp Java) |
| C# ready/show/complete | `OverlayAd.Show`, `TryTakeShow`, `OnStateChanged`, `RectAd.Show`, `HandleDisplayed` |
| Editor vẽ gì | `EditorAd.Show`, `CreateBadges`, `PlaceAdChoices`, `ApplyRoundedCorners`, `EditorSortingOrder`, `AdvanceSlide` |

## A11. Tích hợp

Pack chỉ lo nạp và hiển thị ad. Mọi chính sách của game — gói bỏ quảng cáo,
tần suất, cờ remote config, chế độ test — nằm ở phía game, trước khi gọi pack.

**Tạo placement.** Mỗi placement là một instance sống trọn đời app, tạo một
lần lúc khởi động, sau khi Google Mobile Ads đã khởi tạo:

| Placement thường gặp | Type | Ghi chú |
|---|---|---|
| App open, interstitial, end card | `FullScreenAd` | `Close` quyết nút đóng và đếm ngược |
| Collapsible (nửa dưới màn) | `HalfScreenAd` | `HeightRatio` là phần màn hình bị che |
| Ô trong danh sách, MRec | `RectAd` | một instance mỗi ô, cỡ đặt bằng `SetSize` |

- `Format` là tên placement do game tự đặt; nó đi kèm mọi `OnAdPaid` để game
  ghi doanh thu theo placement.
- Placement nào bày ad thứ hai ngay khi ad đầu đóng (chuỗi ad) nên để
  `CacheSize` 2; màn hiện nhiều ô cùng lúc thì `CacheSize` của ô bằng số ô.
- Overlay: gọi `Load()` một lần sau khi tạo; pack tự nạp tiếp và tự retry
  (B2). Ô tự nạp từ lúc tạo — tạo sớm để ad kịp ấm trước khi ô hiện. Các
  placement cùng ad unit id và cùng chế độ tắt tiếng dùng chung một pool (B2).
- `Show()` khi `IsReady()`; kết quả về qua `OnAdDisplayed` /
  `OnAdDisplayFailed` / `OnAdHidden`.

**Luồng callback.** Mọi event của pack đến trên thread native, **không** phải
thread Unity (B1). Việc gì đụng tới Unity trong handler phải tự đưa về main
thread (`UniTask.Post`, `SynchronizationContext`…). Giá trị nào cần đọc trong
một callback bắn trên thread lạ thì ghi ngay trong callback của SDK hoặc tính
sẵn từ trước — đừng để nó được ghi qua một lệnh post sang main thread, vì lúc
đọc lệnh đó có thể chưa chạy.

**Thứ đứng sau ad nửa màn hình** (B3). Lớp nào phải khít mép panel (vd. một
màn che trơn giữ chỗ khi chuỗi ad đổi ad) lấy `HalfScreenAd.PanelHeight`
nguyên đơn vị nền tảng, đừng tự tính. Trên Android lớp đó phải là window
`TYPE_APPLICATION_SUB_PANEL` **add trước** `Show` của ad, cùng một phía thread
gọi (để hai lệnh post giữ thứ tự), và từ đó chỉ ẩn/hiện; trên iOS đặt bậc
`1000` theo bảng.

**Trượt** (A3). `Show(slideIn: true, ...)`: `OnAdDisplayed` bắn khi trượt
xong, nên thứ gì muốn đổi theo lúc ad *thật sự* lên màn thì chờ event đó.

**Ô từ uGUI.**
- Mỗi chỗ là một `RectAd`: `SetSize(sizePx, roundCornerPx)` khi đã đo được ô
  (gọi lại khi ô đổi cỡ), rồi `SetPosition(positionPx)` và
  `Show(onDisplayed)`; `Hide()` khi ô rời màn hình hay màn hình chứa nó đóng,
  `Dispose()` khi chỗ đó không còn.
- Đo ô sau `Canvas.ForceUpdateCanvases()`. Với canvas Screen Space Overlay,
  tọa độ world của `RectTransform` chính là pixel màn hình: nhân các góc của
  rect (đã trừ padding) với `localToWorldMatrix`. Pack nhận tọa độ **gốc
  trên-trái**, nên `y = Screen.height − top`.
- Bán kính bo góc tính theo đơn vị canvas thì đổi sang px bằng hệ số của chính
  rect: `scale = (topRight.x − topLeft.x) / bề rộng rect đã trừ padding`
  (= `lossyScale.x`), `roundCornerPx = round(radius × scale)`.
- Chỉ show khi ô nằm **trọn** trong khung nhìn; ô bị cắt thì hide.
- Pack không vẽ gì khi chưa có ad — ô trống tới lúc fill — nên giữ một
  placeholder của game bên dưới ô.

**Editor.** Canvas của game phải nằm dưới số nhỏ nhất của
`NativeAdEditorLayers` (README Editor §3).

---

# Phần B — Luật

## B1. Sự kiện và luồng

- **Pack không marshal callback về thread Unity.** `BaseAd.DispatchFromNative`
  chỉ gác release rồi gọi thẳng, đúng cách SDK AdMob làm: listener nào cần
  thread Unity thì tự nói. Hệ quả cho bên gọi: trong handler **không được**
  đụng API Unity đòi main thread — `PlayerPrefs`, `FindAnyObjectByType`, thao
  tác GameObject đều ném; `UnityEngine.Object == null` thì không. Bên gọi tự
  đưa về thread Unity khi cần (A11). Callback vẫn tới khi Unity đang dừng dưới
  ad full — B4.
- Android: mọi `Notify*` qua `HandlerThread` `RiseOnAdMobNativeEvents`
  (`AdEventDispatcher`), **không bao giờ main thread** — lý do ANR ở README
  Android. Tham số chụp local **trước** khi post; một queue giữ thứ tự. iOS:
  callback về từ main queue qua trampoline `MonoPInvokeCallback`, chạy được cả
  khi Unity đang pause; Editor: đồng bộ trên thread Unity.
- **Readiness do native công bố, và chỉ native** (0cf5db67). `OnStateChanged`
  áp đồng bộ dưới lock; `cachedCount` (0 hoặc 1, là head) đi kèm
  `OnShowCompleted` (thông báo tách riêng đo được trễ 1.5 frame).
- `Show` bị từ chối → `OnAdDisplayFailed` mã −1 ngay. `showId` echo để
  completion chỉ giải quyết đúng show đã đăng ký.
- `OnDisplayed → Load()`: prefetch **khi ad lên màn hình** (e31b462b).
- Handler người dùng gọi qua `InvokeSafely` (log, nuốt): một handler ném
  không chặn handler khác, nhưng phần còn lại *của chính handler đó* thì mất
  (B4).

## B2. Cache: bao nhiêu, nạp khi nào, hết hạn khi nào

Ad ấm nằm trong **pool**, một pool cho mỗi cặp ad unit id + chế độ tắt tiếng
(`NativeAdPool` / `RANNativeAdPool`), dùng chung cho ô hình chữ nhật,
collapsible và full-screen trên cùng unit:

- **Theo ad unit id**: request gửi tới một unit, nên doanh thu, báo cáo và
  mediation của unit tính cho ad đó. Mọi format native đều nhận cùng một loại
  creative Native advanced, chỉ khác giao diện pack dựng.
- **Theo chế độ tắt tiếng**: video tắt tiếng hay không là tùy chọn lúc
  request. Đổi sau khi tải chỉ được khi bật custom controls, mà custom
  controls chỉ có với reservation của Ad Manager
  ([Android `VideoController.mute`](https://developers.google.com/android/reference/com/google/android/gms/ads/VideoController),
  [iOS `GADVideoController.muted`](https://developers.google.com/admob/ios/api/reference/Classes/GADVideoController)).
  Format dừng game (full-screen) tải có tiếng, format cạnh game (ô hình chữ
  nhật, collapsible) tải tắt tiếng.
- **Bao nhiêu ad**: số lớn nhất trong các `CacheSize` của các format trong
  pool (`Capacity`), tối thiểu 1, không trần. Không cộng: các format chung
  pool ít khi cùng hiện, và mỗi ad bị lấy đều được tải bù, nên chỗ thấy pool
  trống lần lượt có ad. `CacheSize = 0` nghĩa là 1. Placement nào bày ad thứ
  hai ngay khi ad đầu đóng (chuỗi ad) nên để `CacheSize` **2**.
- **Lấy và trả**: format lấy ad cũ nhất có thể vừa chỗ của nó. Ad chưa từng
  hiện mà format không dựng được (không vừa ô, không vừa panel) về lại pool,
  đánh dấu không vừa **size key** của chỗ đó (cỡ ô tính bằng pixel;
  panel overlay có key riêng), format khác vẫn lấy được. Ad đang dựng dở của
  ô bị ẩn và head chưa hiện của overlay bị `Release` cũng về pool. Ad
  trả về giữ thời điểm tải cũ, nên vẫn hết hạn đúng giờ.
- **Pool đầy mà không ad nào vừa chỗ đang cần**: bỏ một ad không format nào
  trong pool dựng được; không có thì bỏ ad cũ nhất không vừa chỗ đó; rồi tải.
- Overlay giữ riêng **một** ad (head) và chỉ head được dựng sẵn giao diện
  (`MediaSignature`: asset về muộn làm chữ ký đổi thì dựng lại, ad67dcb4).
- **Một ad ấm không bao giờ bị hủy để nạp ad khác** (4753cfb3), trừ khi pool
  đầy ad không vừa như trên: ghế trống là lý do để load, bất kể ai đang trên
  màn hình.
- **Paid event bám identity ad, không bám load generation** (1a1dcf5d);
  format gắn paid event lúc lấy ad.

| | Pool | Format |
|---|---|---|
| Ai gọi nạp | format vào pool (khi có Activity); load thành công tự nối tới khi đầy; mỗi lần lấy ad; `RequestFor` từ format không tìm được ad vừa | overlay: `Load()`, `Show` rút head, `OnDisplayed` gọi `Load()`; ô: **không có `Load()` công khai**, ô đang hiện mà hết hàng gọi `RequestLoadFor` |
| Load hỏng | no-fill backoff từ lần hụt đầu: `BackoffDelayMs` 1s × 2^n (n = số lần hụt liên tiếp, trần 5): 2s, 4s, … 32s | layout fail: 2 lượt ngay, sau 20 lần chưa từng render → 5 phút/lần (ô và panel overlay, mỗi cái một chuỗi) |

**Bên gọi đừng chồng thêm retry** — nhân đôi request lúc no-fill.

### Hết hạn: 1 giờ

Docs AdMob, *Request ads*
([Android](https://developers.google.com/admob/android/native/start),
[iOS](https://developers.google.com/admob/ios/native/start)): "Since ads
expire after an hour, you should clear this cache and reload with new ads
every hour." → `MAX_CACHED_AD_AGE_MS = 3_600_000` /
`kRANNativeAdPoolMaxCachedAdAge = 3600`. Pool quét theo ad già nhất và mỗi lần
có format lấy ad; head của overlay **quét trước mỗi `Show`**, entry của ô
**khi trở lại foreground**: timer đứng khi máy ngủ, còn tuổi ad đo bằng đồng
hồ đếm cả lúc ngủ (`SystemClock.elapsedRealtime` / `RANNativeAdPoolNow`,
`CLOCK_MONOTONIC`; `systemUptime` của iOS không đếm lúc ngủ). Ad hết hạn
**đang trên màn hình** cũng bị thay, bản thay thế cố ý bỏ dwell — đừng
"khôi phục" check đó. Không có ad ấm → ô trống tới khi load về
(impression là của SDK). **4 giờ** là của `AppOpenAd` thật, không áp cho
pack.

## B3. Xếp lớp: ai đứng trên ai

    ô hình chữ nhật  →  half-screen  →  full-screen

Pack chỉ vẽ ba tầng của mình, nhưng package khác chen vào giữa (màn che, banner
của SDK khác). Không ai gọi ai: thứ tự là **bảng chung** ở
[README gốc](../README.md#thứ-tự-lớp), mỗi package tự đặt lớp mình theo bảng.

- **iOS**: mọi lớp là subview trực tiếp của view Unity. Mỗi view đặt
  `layer.name = "riseon.layer.<bậc>"` rồi chèn ngay dưới subview đầu tiên (từ
  dưới lên) có bậc **cao hơn** (`RANInsertSubviewAtTier`, `RANLayerOrder.h`).
  Ô hình chữ nhật `-1000`, half ad `2000`; view không đánh dấu là `0`. Thứ tự
  đến không quan trọng. Không dùng `zPosition`: UIKit hit-test theo thứ tự
  mảng `subviews`.
- **Android**: window xếp theo type rồi theo thứ tự add; không có API đổi
  z-order (vụ type 1003: README Android). Half ad là `SUB_PANEL`; thứ muốn
  nằm dưới nó phải add **trước** — hợp đồng của bên điều phối, pack không
  kiểm.
- Ad full-screen luôn trên cùng mà không ai phải sắp (Activity / present).
- **Editor**: sorting order theo cột Editor của bảng (README Editor).

## B4. Pause

| | Android | iOS | Editor |
|---|---|---|---|
| Ad full-screen | có (Activity) | có (`UnityPause(1)` khi present, `UnityPause(0)` khi dismiss) | **không** |
| Ad half, ô hình chữ nhật | không | không | không |

Ad full cư xử như mọi SDK quảng cáo. `UnityPause` là **cờ, không đếm**: ad
đóng là đặt `0`, kể cả khi thứ khác (một màn che, một SDK khác) cũng đang muốn
game dừng. Ai cần giữ pause xuyên qua ad thì tự canh và đặt lại sau khi ad
đóng; pack không biết tới họ. Chỉ presentation nào đã pause mới được resume
(`_askedForPause`): ad half đóng không đụng cờ. Editor không đổi
`Time.timeScale`, để không bao giờ chạy lại một game mà thứ khác đang giữ
dừng.

Khi Unity đang dừng dưới ad full, **C# không chạy trên player loop** nhưng
callback của pack vẫn tới (thread native, B1). Thứ gì xếp lịch qua player
loop (`Update`, coroutine, `UniTask.Post`) chỉ chạy sau khi ad đóng.

## B5. RedirectOnClose: đóng ad đúng lúc redirect chiếm màn hình

`CloseSettings.RedirectOnClose` biến nút close thành một cú click thật vào ad.
Mốc đóng **không** phải "SDK báo click" (ad biến mất trước khi trình duyệt tới
→ game trần) mà là **cửa sổ ad mất foreground**.

    IDLE
     │ chạm nút close
     ▼
    ARMED ──────────── hết CLOSE_REPORT_TIMEOUT (1s) ──► KHÔNG đóng
     │ SDK báo click                                     (chạm không trúng asset,
     ▼                                                    coi như chưa bấm)
    REDIRECT_PENDING
     │ mất foreground ──► đóng   ← redirect đã chiếm màn
     └ hết REDIRECT_TIMEOUT (3s) ──► đóng

Nút không được thành bẫy: **lần hụt thứ 3 liên tiếp** vẫn chờ hết 1s rồi
đóng trần. Bấm lại lúc chờ → claim mới, tính vào lưới; bấm lúc
`REDIRECT_PENDING` → bỏ qua. Deadline lưới là lời hứa: click sau có thể hủy
nó, không đẩy lùi được. Cú chạm **thả xuyên** xuống `NativeAdView` (SDK đếm
touch, `performClick()` vô nghĩa). Chỉ chạm **bắt đầu trên nút close** mới vũ
trang; CTA thật thì ad còn nguyên khi quay lại. Countdown đóng băng khi window
mất visibility.

| | Android | iOS |
|---|---|---|
| lấy click | thả xuyên touch | thả xuyên từ `hitTest:` |
| mất foreground | `onWindowFocusChanged(false)` (Activity) **và** `onWindowVisibilityChanged` (window half không focusable) | `onPaused` từ resign-active |

## B6. Layout: tháp ưu tiên và luật chữ

Mục tiêu: **luôn ra layout hợp lệ với mọi rect/ratio được yêu cầu.** Tháp ưu
tiên (chủ dự án): (1) nhiều element nhất → (2) bố cục đẹp → (3) phóng to
element quan trọng.

- **Chữ: trọn hoặc một dòng cuộn, không bao giờ cắt.** Thứ tự nhượng bộ:
  **nguyên vẹn > cỡ chữ > cuộn.** Ô hình chữ nhật: `TEXT_LADDER_MODES/SCALES` —
  wrapped 1.0/0.85/0.7 → cuộn dòng phụ 0.8/0.65 → wrapped 0.55 → cuộn phụ
  0.5 → cuộn cả headline 0.8/0.65/0.5; nấc cuộn luôn nhỏ hơn cỡ wrapped đã
  thất bại; `NudgeCutTextsWhole` trước mỗi nấc. Overlay rail: trần scale theo
  **bề rộng** rail (160dp→0, 400dp→1), thêm dòng (headline ≤4, body ≤6), hạ
  scale 3 nấc 0.34, rồi mới marquee (5a97fb4a); `MARQUEE_TEXT_SHRINK` 0.8.
- **Dòng cuộn iOS** (`RANAdTextLabel`) chạy bằng animation `transform`. Lượt
  layout cùng cỡ không khởi động lại vòng cuộn (`ran_layoutContentIfResized`),
  và mọi lần dừng đều gỡ animation trước khi trả `transform` về identity
  (`ran_stopMarquee`): animation UIKit cộng dồn, đặt identity dưới animation
  đang chạy làm chữ trôi từ ngoài mép phải vào, và ô layout lại liên tục thì
  chữ không bao giờ quay về khung (đo trên simulator: 6/15 ô mất headline hoặc
  body ngay khi bắt đầu cuộn).
- **Điểm của ô** (`EvaluatePlan`): di chuyển 1 000 000/px · trống 100/px
  (`WhitespacePx`: dải thừa dưới nội dung, cộng dải trống hai bên ảnh trên
  hẹp hơn cột, quy ra số hàng px theo bề ngang ô) ·
  lệch tier 1 000 · mỗi chữ cuộn 3 000 · media phải 500 · scrim 6 000 · diện
  tích media nhìn thấy −50/% (`VisibleMediaPercent`; scrim tính 0 vì ảnh nằm
  dưới lớp phủ) · cỡ headline đúng như sẽ vẽ −400/sp (`HeadlineSp`; iOS
  `headlineSizeForPlan`, tính /pt) · mỗi element −10 000. Cuộn là giá giữ một
  element, không bao giờ thắng element bị bỏ. Cả thang headline (6–17sp, tối
  đa 4 400) cộng media phủ kín ô (5 000) vẫn dưới một element, nên cỡ chỉ
  chọn giữa các layout hiện cùng element, không bao giờ đổi lấy việc bỏ
  element.
- **Tier là thang cỡ chữ, không phải cỡ ô** (77167823). Scorer được leo ROOMY
  cho ô 96dp → **mọi spacing tier phát ra bị chặn theo cạnh ngắn ô**
  (`CellSpacingCapPx` = clamp(shortSide×0.02, 1dp, 5dp); CTA padding ≤
  shortSide×0.035; scrim pad = clamp(shortSide×0.015, 1dp, 4dp)).
- **Asset rỗng không chiếm chỗ** (1e4c37cb); creative thiếu asset → **ẩn,
  nhường chỗ**, không loại ad. Validator chỉ chặn vì chính sách: chữ cắt, kể
  cả dòng cuộn thấp hơn một dòng chữ (Android đo chiều cao layout của mọi
  `TextView` như vậy); asset bị container cha cắt (Android
  `IsCutByContainer`; view iOS không cắt con vì `clipsToBounds` mặc định tắt,
  nên iOS chỉ kiểm theo khung ô); media dưới sàn; đè nhau.
- **Cột chữ cạnh media** (MEDIA_LEFT/RIGHT ảnh) cao theo nội dung, tối thiểu
  bằng media (`ConfigureInsetContent` đặt `setMinimumHeight`, iOS
  `ran_minimumSize`): spacer vẫn dồn CTA xuống chân ảnh khi chữ ngắn, và chữ
  dài làm hàng cao lên để lượt đo chiều cao thấy đúng. Trên iOS, mức tối thiểu
  của `RANLinearLayoutView` được tính vào phần weight chia, như LinearLayout.
- **Đo con của `RANLinearLayoutView` như LinearLayout**: từ con mang weight
  đầu tiên trở đi (kể cả nó), mỗi con được đo với cả chiều dài cha, không trừ
  phần đã dùng; lượt chia weight mới chốt cỡ. Trừ đi thì con sau bị ép nhỏ hơn cỡ đo
  tự nhiên (đo trên simulator: dòng advertiser cuộn còn 1,7pt).
- **Sàn 72dp** (`MIN_ICON_ROW_TEXT_WIDTH_DP`): headline cạnh icon phải còn
  ≥72dp chữ, không thì icon đứng riêng, chữ full width. **Một neo mỗi khối**
  (66803d95): icon đứng riêng thì icon và chữ cùng căn giữa, kể cả ở cột chữ
  cạnh media; icon cạnh thì lề trái. iOS căn giữa `RANAdTextLabel` qua
  `textAlignment` của nó (không phải `UILabel`), Android căn headline trong
  khung `FrameLayout` của nó.
- **Icon ngồi hàng nút lấy chiều cao nút** (`min(iconTier, ctaHeight)`).
- Box lấy cỡ từ ô/ad, chữ từ box: CTA = clamp(shortSide×0.18, 24dp, 36dp),
  rộng tối thiểu min(48dp, nửa ô) (`CTA_MIN_WIDTH_DP`, iOS `kRANCtaMinWidth`)
  cho vừa ngón tay, badge = clamp(shortSide×0.10, 15px, 18dp), reserve
  AdChoices 15dp (B12); tỉ lệ chữ 0.45 / 0.55. Ngưỡng viết dp; vài sàn px có
  chủ ý (15, 256).
- Cả rect click được; latch chống double-click nhả theo visibility (ô hình chữ
  nhật, half) / focus (full).
- **Layout pass**: không bao giờ measure-thăm-dò rồi `requestLayout` từ giữa
  một layout pass mà không có khóa idempotent — Android vứt `requestLayout`
  giữa pass và GMA lay cột ở cỡ thăm dò (ad67dcb4, tìm bằng `dumpsys activity
  top`). Bản sửa đầu (`post()` từ `onSizeChanged`) sau đó bị thay (loạt 21/8):
  nó tự là cú nhảy sau frame đầu trên máy báo `heightPixels` có/không cutout
  khác nhau. Hiện tại kế hoạch né lập **trong `onMeasure`** theo kích thước
  thật, chỉ khi `avoidancePlanDirty` hoặc panel đổi cỡ (`plannedPanelWidth/
  Height`), `planningInMeasure` chặn `requestLayout` thừa. Sửa vùng này thì
  giữ tính idempotent, nếu không vòng measure→mutate→layout chạy mãi.

## B7. Media: sàn chính sách và template

- **Asset chính luôn qua `MediaView`/`GADMediaView`.** `setImageView()` là
  AdMob từ chối request với mã 3. Ảnh fallback là `ImageView` **con** của
  MediaView.
- **120dp chỉ cho creative có video**; ảnh: thang 56/88/120dp, sàn **48dp**
  (`MediaPolicyFloorPx`). Video không vừa → layout fail, ad bị bỏ và ô (ô hình
  chữ nhật) hoặc overlay tải ad khác ngay. Không video nhỏ, không ảnh tĩnh
  trong MediaView, không scrim, không bỏ MediaView: trang chính sách native
  advanced (support.google.com/admob/answer/6329638) đòi ad video có MediaView
  từ 120×120dp, và validator báo "MediaView not used for main image or video
  asset" khi thiếu. Ô dưới 120dp không bao giờ hiện video; mỗi lần bỏ tốn một
  request (C3).
- **Không cắm view vào TRONG MediaView** (38704204): con của nó thuộc SDK,
  không sống qua bind. Ambient là anh em đứng sau.
- **Nền đen MediaView là chủ đích** (87652fd1); band letterbox bằng màu panel,
  đen chỉ cho video. Thẻ full-screen thì khác: nền `#F1F3F4` cho cả video (A6).
- **Media trái**; `MEDIA_RIGHT` phạt 500, không thắng hòa.
- **Scrim là phương án cuối** (penalty 6 000): ảnh FIT + ambient crop-phủ dim
  55% sau, **một** veil `#B3000000` phủ toàn ô, khối chữ neo đáy. Ambient chỉ
  cho scrim (229a361a). `IsAllowedScrimOverlay` cho chữ trong khối scrim đè
  media.
- **Video không bao giờ làm nền** (cd5fa5e0).
- **Không hardcode ratio fallback**: không báo → nguyên dải trống lớn nhất đã
  qua sàn; có báo → box đúng cỡ, nở tới giới hạn thật, slack chia đôi
  (3e046158 → 8cc5a787). `DEFAULT_MEDIA_ASPECT_RATIO` chỉ để thăm dò.
- Band MEDIA_TOP của ô nuốt height thừa (min = plan size để validator đo
  không đổi).
- Overlay side-media: aspect < 0.85 **nghiêm ngặt** (a73d1309), panel ≤
  1.3×W, share ≤ 0.56, cột media cao đúng aspect, rail ≥ 120dp.

## B8. Layout side-media: biến thể đẹp không bao giờ được chật hơn bản gốc

Creative dọc + panel đủ thấp → media cột trái, mọi thứ vào rail phải —
**MEDIA_LEFT thuần**. Media để thừa height → CTA (và có thể body) rơi xuống
dưới — **MEDIA_LEFT có element ở dưới**. Biến thể thứ hai sinh ra để làm đẹp
biến thể thứ nhất, nên:

> Ad nào bày vừa ở bản thuần thì phải bày vừa ở bản có element ở dưới.

Có element ở dưới thì media bỏ tràn mép trái, bắt đầu cùng mép với mọi element
— mép đó **cắt từ ngân sách cũ, không cộng thêm**:

```
x = (khe media ↔ rail) + (padding phải panel) = 2 × SideRailPadding(railOuterPlain)
share = x / 3   (px nguyên, Android)
dư 1 → khe media↔rail;  dư 2 → mép trái + mép phải (cặp cân)
lòng rail = W − media − x   (cả hai bản, đúng từng pixel)
```

Nên `ConfigureResponsiveSideRail` nhận cùng đầu vào → cùng kết quả;
`railScaleCap` đọc bề rộng cột **bản thuần**. Cạm bẫy đã dính: trong
`sideRow` media width cố định, rail mang weight — padding-left cho `sideRow`
mà không cắt từ ngân sách là trừ thẳng vào rail. Android
`OverlayAdContentView` nhánh `if (sideMediaLayout)` (`sidePaddingBudget`,
`sideRowLeftInsetPx`, `railGap`, `sideRowRightPadding`); iOS
`RANOverlayAdContentView.mm` nhánh `if (_sideMediaLayout)`. Mọi thứ đo từ mép
media cộng mép trái đó; badge Ad trong side-media **né media**, bám mép phải
media, control trái đứng cạnh (38704204). Log `padding <trái>/<khe>/<phải>
of <x>`.

## B9. Padding ngang: một hằng số, ba nền tảng

`5dp`: hai mép panel, margin element dưới, **trần** padding rail (thật =
`max(2, min(5, railOuterWidth×3%))`). Ba mép của side-media có element dưới
**không** lấy hằng này (B8).

| Nơi | Hằng số |
|---|---|
| Android | `OverlayAdContentView.HORIZONTAL_PADDING_DP` |
| iOS | `kRANHorizontalPadding` |
| Editor | `EditorAd.CONTENT_HORIZONTAL_PADDING_DP` |

Ô hình chữ nhật: **padding 0** — chỉ còn thụt góc bo (B11) và dải badge khi
phần tử trên cùng là chữ.

## B10. Overlay: sân media và né control

Một câu (fc370297): **media mọc từ stack lên, theo aspect (có báo; không báo =
nguyên sân), trong sân cố định: hai bên không vượt lề 8dp sync (không bleed —
user đòi lại lề); trần = mép trên panel; vươn vào dải control thì hai cột
close/timer là tường. Nở tới tường đầu tiên; slack chỉ khi bị chặn ngang, chia
đôi căn giữa.**

- Bốn ứng viên (dbd1408b): len khe hàng control trên / dưới hàng control trên /
  len khe giữa hai control bám mép ngay dưới badge / dưới control mép. Chọn
  media to nhất; control **di chuyển theo**; badge được đè, close/timer phải
  né; không ứng viên nào chứa sàn → đè như cũ. Chỉ half chạy (cùng
  maximizer); thẻ full-screen không né control: control ở góc màn hình, ngoài
  thẻ (A6).
- **Chỉ tường nhìn thấy** (7a7c527c); ứng viên control-mép bắt đầu `top = 0`
  (322ff153).
- **Cả hai vị trí close/timer là vật cản** dù nút nào đang hiện (b74df31d);
  **không buffer**. Khoảng trống là [trái..phải] thật, căn giữa trong khoảng
  (1bfcb915).
- **Thang co chrome chạy cho half** (b37ad3be; thẻ full-screen bỏ qua); bộ sàn gọn là mặc
  định của stacked-avoid (03f9427b) nhưng **không xóa padding ngăn hàng**
  (ac0b32eb); icon đã bị tiêu thì settle không nở lại.
- Margin media tính từ mép padding cột → trừ `paddingLeft` (ac0b32eb).
- Half: **một resolver chiều cao** (`ResolveHalfScreenPanelHeight`), công bố
  ra ngoài qua `PanelHeight` để lớp phía sau dùng đúng số đó; panel đo chiều
  cao **nhận được** (2720018e).
- Panel thấp **tôn trọng ratio**: media → icon-hero → ticker (< 120dp) → nới
  tới 48dp khi ticker cũng không vừa. Stacked half né dải control trước, co
  CTA 44→36, icon 36→28, padding; chỉ đè media khi thật sự không vừa.
- Full-screen không có trạm unrenderable. Control box 30dp, close vẽ hai nét.
- Ad **đang hiện** mà sai: đọc cây view thật trên máy (cách lấy: README
  Android §9) và log `avoidance` (panel/box/top/interval/slack) trước khi
  đoán (f6192ed4).

## B11. Bo góc ô hình chữ nhật

**Bán kính đi theo rect qua `SetSize(sizePx, roundCornerPx)`** (ô ấm từ lúc
tạo, đo sau), không có trong Settings. Kẹp ở nửa cạnh ngắn. Hình học:
`d = ceil(r × (1 − 1/√2))` (`CornerInsetForRadius` / `kRANCornerInsetRatio` /
`CORNER_INSET_RATIO`).

- Layout thường: **nội dung thụt vào chạm khít góc bo** — cột ngoài tiêu khoản
  thụt làm padding; engine đo media theo `ContentWidth/Height`.
- Scrim: **ảnh nền phủ hết ô và bị cung cắt** (`clipToOutline` /
  `clipsToBounds`), cột ngoài không thụt, khối chữ tự lùi.
- **Thụt là SÀN, không cộng**: `scrimPad = max(padding ô, cornerInset)`; badge
  `edgeInset = max(CONTENT_EDGE_INSET_PX, cornerInset)`.
- Badge trên root, tự mang thụt, mọi template kể cả scrim.
- Màu trong suốt vẫn clip.
- Bên gọi đổi bán kính từ đơn vị canvas sang px: A11.

## B12. Badge "Ad" và AdChoices

- `ATTRIBUTION_TEXT` **phải là `"Ad"`** ở cả ba bên. Rule rename từng cắn
  literal thành `"NativeAd"` (5c6afa3b) → máy thật mất fill. **Rename không
  đụng string literal; audit chuỗi sau rename.**
- Badge/control đè media và icon, **không đè chữ** (`IsAllowedBadgeOverlay`);
  badge là góc, không phải dải.
- **Dấu AdChoices nhìn thấy là của SDK, không phải của ta.** SDK vẽ nó trong
  một lớp riêng gắn vào `NativeAdView`, ghim vào góc lớp đó theo
  `ADCHOICES_TOP_RIGHT`. `reserve` của ta chỉ là ô giữ chỗ trong suốt để
  layout và validator biết góc đó đã có chủ — **không điều khiển được vị trí
  dấu**.
- **Đăng ký `AdChoicesView` qua `setAdChoicesView` KHÔNG dời được dấu** trên
  Android — đã thử và đã sai: cây view trên máy (2026-09-21) cho thấy SDK
  nhận view đó (gắn listener, view thành bấm được) nhưng vẫn vẽ dấu ở lớp
  của nó, sát góc thô; ô của ta trống. Đừng thử lại cách này.
- Cách đang dùng (Android, ô hình chữ nhật): **padding cho `NativeAdView`**
  bằng đúng khoảng thụt của badge (`BadgeEdgeInsetPx`), để lớp của SDK co vào
  và dấu theo vào; nội dung của ta bù lại bằng margin âm và
  `setClipToPadding(false)` nên phủ kín ô y như cũ (`InsetSdkOverlay`). Chi
  tiết và bằng chứng bytecode: README Android §7.
- Cách đang dùng (iOS, ô hình chữ nhật): SDK ghim khung AdChoices của nó
  (`GADNativeAdAttributionView`) vào **mép** `GADNativeAdView` bằng hai
  constraint `top == top`, `right == right` — padding hay margin không với
  tới. Nên **sửa thẳng hai constraint đó** thành ±khoảng thụt badge
  (`insetSdkAdChoicesInNativeAdView:`), gọi ở mọi lượt layout; khung giữ
  nguyên cỡ, chỉ góc dời vào. Vòng layout của pack bỏ qua view do Auto Layout
  quản (chỉ view của SDK mới là loại đó), không thì nó kéo khung ra phủ cả ô.
  Chi tiết: README iOS §3. Gán `adChoicesView` (cách ghi trong header) đã cân
  nhắc và bỏ: dấu hiệu trên máy giống kiểu thất bại của Android, và khung 9pt
  của ta nhỏ hơn dấu.
- **Cả hai fix dựa vào layout nội bộ của SDK**, nên cả hai tự kiểm và ghi log
  mỗi lần chạy app một lần: thành công thì in khoảng thụt + phiên bản GMA;
  cấu trúc SDK khác đi thì **không ép gì** (dấu về góc như trước) và in ra
  thấy gì thay vào — log chứa `Rect ad AdChoices`, tag `RectAd`.
  Nâng SDK xong hãy tìm dòng đó đầu tiên.
- **Reserve của ô = cỡ dấu thật**: SDK vẽ dấu cố định 15dp (đo trên Redmi:
  41×41px ở mật độ 2.75, cách mép 8px), nên `adChoicesReserve` là ô vuông
  `AD_CHOICES_MARK_DP` (iOS `kRANAdChoicesMarkSize`, 15pt) ở đúng góc dấu dời
  vào. Validator đo ô này, nên chữ dưới dấu bị bắt. iOS đo trên simulator
  với unit thật ở test mode: dấu 45×45px ở scale 3 (15pt), cách mép trên và
  mép phải 3pt, trùng ô reserve `{132, 3, 15, 15}` của ô rộng 150pt; giống
  nhau ở cả 15 ô.
- **Chữ né dấu theo bề ngang** (`DodgeAdChoicesMark`, iOS
  `dodgeAdChoicesMarkInProbe:`): sau khi cấu hình probe, layout EXACT một lượt;
  headline/body/advertiser/rating/CTA nào giao dấu thì thêm margin phải vừa
  đủ lùi khỏi mép trái dấu + 2dp, layout lại (tối đa 3 lượt, vì chữ hẹp đi có
  thể xuống dòng đẩy chữ khác vào dấu). Lượng né lưu trong plan
  (`*MarkDodge`) và áp lại y hệt khi dựng cây thật (`ApplyMarkDodges` trong
  `FinishContentRoot`).
- Overlay chưa sửa (C3). Editor giả định glyph AdChoices 15dp.

## B13. Ô hình chữ nhật: giữ ad, xoay vòng, dwell, watchdog

**Ô chỉ lấy ad từ pool lúc đang hiện.** Ẩn (hay `Configure`) thì nhả: ad
chưa hiện về pool cho chỗ khác, ad đã hiện đủ `MIN_DWELL_MS` 4s bị hủy, ad đã
hiện chưa đủ 4s ở lại cho lần hiện sau của chính ô đó. Lần hiện sau không có
ad giữ lại thì lấy ad mới từ pool.

- **Liếc không phải lượt** (95236f74): ad phải đứng đủ 4s mới bị thay;
  `MIN_SWAP_INTERVAL_MS` 3s giãn đổi.
- `DWELL_REFRESH_INTERVAL_MS` 30s: ô hiện liên tục thì đổi sang ad của pool
  (nếu có ad vừa) sau mỗi 30s nhìn thấy.
- **Foreground là resume, không phải rotation** (499d8e60): chỉ present ô
  đang hiện mà trống; đọc tuổi ad bằng elapsed real time ở đây.
- Activity nền: window không vẽ → chờ foreground, không coi là lỗi.
- Watchdog 1s: materializing kẹt (`MATERIALIZING_ENTRY_TIMEOUT_MS` 20s, lưới
  cuối); active không bao giờ visible (`ACTIVE_ENTRY_VISIBILITY_TIMEOUT_MS` 3s,
  chỉ khi có Show đang chờ).
- Hẹn giờ hết hạn arm **tại nơi entry ra đời**.
- Show khi chưa sẵn sàng → re-arm delay hiện tại, không tính hỏng; ô hỏng
  display liên tục đi qua backoff.

## B14. Đặt tên và phong cách code

- **Container mang họ, type mang vai trò** (fadbc996):
  `RiseOn.Ads.AdMob.Native` / `com.riseon.ads.admobnative` / `RAN` /
  `RiseOnAdsAdMobNative.androidlib`; type `BaseAd`,
  `RectAd(+Slot/Listener/Presentation/ViewFactory/LayoutEngine/LayoutValidator)`,
  `OverlayAd(+Activity/ContentView/Presentation)`, `FullScreenAd`,
  `HalfScreenAd`. Tag logcat `BaseAd`/`NativeAdPool`/`RectAd`/`OverlayAd`. Đổi
  tên một nơi = đổi cả ba, kể cả JNI string, manifest, tên class trong proxy.
- **`AndroidJavaProxy` dispatch theo tên**: tên method C# = tên method
  interface Java (`OnPresentationFailed` của `RectAdListener`): lệch một chữ
  là lời gọi rơi lặng, mọi lỗi hiển thị của ô mất theo.
- Java: method **PascalCase**, hằng UPPER_SNAKE, phẩy đầu dòng, comment nói
  **vì sao**. Obj-C++: `ran_`, `kRAN*`. C#: K&R; so sánh hằng bằng `is`/`is not`;
  `null` giữ `==`; một type một file; namespace = tên assembly.
- Comment tiếng Anh; README tiếng Việt.
- Mọi quyết định layout **log số**; mắt và code mâu thuẫn nhiều vòng → gắn log.

---

# Phần C — Vận hành

## C1. Quy trình làm việc và cổng kiểm tra

- Pack được phát triển trong một **project Unity sandbox** trỏ tới repo này
  bằng `file:` trong `Packages/manifest.json`; build và chạy thử trên máy qua
  project đó, hoặc qua một game đang dùng pack. **Chủ dự án tự build và
  chạy.** AI không build, không inject input (`adb shell input`), **không
  click vào nội dung quảng cáo**.
- **Mỗi thay đổi quan sát được → build → nhìn → mới đi tiếp.**
- Lỗi hiển nhiên → sửa ngay và báo. Quyết định thiết kế → trình bày, chờ chốt.
  "Sao không chạy?" → chỉ chẩn đoán. Chỉ đụng file/hàm được giao. Đọc lại file
  ngay trước khi ghi (chủ dự án sửa và commit song song).
- **Cổng bắt buộc trước khi nói "xong"** (hai build Android đã vỡ vì javac).
  Chúng chỉ kiểm **biên dịch**, không kiểm hành vi — không có test nào ở đây.
  - **Java**: javac của OpenJDK Unity + `android.jar` (android-36) +
    `classes.jar` của `play-services-ads-api` và `play-services-ads` trong
    `~/.gradle/caches`. Công thức đầy đủ: README Android §8.
  - **C#**: dùng compiler của chính Unity, đọc tham số từ file rsp mà Unity
    để lại trong **project host** (sandbox, hoặc game đang dùng pack) — nên
    reference và define giống hệt lúc Unity biên dịch.
    Đường dẫn, dưới `<Unity>/Editor/Data` của bản Unity project host đang mở:
    `NetCoreRuntime/dotnet.exe` chạy `DotNetSdkRoslyn/csc.dll`.
    Rsp nằm ở `<project host>/Library/Bee/artifacts/*.dag/<Tên assembly>.rsp`
    — lấy bản mới nhất theo thời gian sửa; **project host phải được mở trong
    Unity ít nhất một lần** thì mới có. Chạy: `dotnet.exe csc.dll -nologo
    @<rsp đã sửa>`, rồi lọc dòng chứa `: error `.
    Thứ tự: `RiseOn.Ads.AdMob.Native` trước, rồi `.Android`, `.iOS`, `.Editor` (ba
    assembly này tham chiếu core). Muốn biết một thay đổi chữ ký có làm vỡ
    code dùng pack không thì dựng thêm assembly của project host.
    Những chỗ phải sửa trong rsp trước khi chạy:
    1. `-out:` trỏ sang thư mục tạm của mình; bỏ hẳn dòng `-refout:`.
    2. Khi dựng assembly dùng pack: mọi `-r:` trỏ tới dll RiseOn phải **trỏ
       sang bản pack vừa dựng**, không thì đổi chữ ký hàm trong pack mà bên
       dùng vẫn "PASS" vì đang tham chiếu dll cũ Unity để lại.
    3. Bỏ dòng file nguồn đã bị xóa (rsp chỉ được ghi lại khi Unity biên dịch,
       nên một commit vừa pull về xóa file là rsp thành cũ).
    4. Thêm file `.cs` mới thuộc assembly đó mà rsp chưa liệt kê (với
       `Assembly-CSharp`: trừ file trong thư mục `Editor` và trong cây của một
       asmdef khác).
    Mẹo khi chủ dự án đang sửa dở một file: cho phép thay tạm đường dẫn file đó
    bằng bản đã commit (`git show HEAD:<path>`) để phần còn lại vẫn kiểm được.
  - **iOS**: **không có compiler trên Windows** — chỉ kiểm được ba thứ: ngoặc
    `{}()[]` cân sau khi bỏ comment, chuỗi và ký tự; chữ ký ABI
    (`RANRectAdCreate`, `RANRectAdConfigure`) khớp giữa
    `RANNativeAdBridge.h`, `.mm` và `NativeAdBridge.cs`; và **file `.mm` nào
    dùng `@try`/`@catch`/`@throw`/`@finally` thì `.meta` của nó phải có
    `CompileFlags: -fobjc-exceptions`** (bỏ comment và chuỗi trước khi tìm).
    Muốn biết code iOS có biên dịch được không vẫn phải build trên Mac.
  - **Script gate không nằm trong repo** (chủ dự án chốt 2026-09-21): phiên
    sau đọc công thức trên rồi dựng lại. Chúng hardcode đường dẫn máy và chỉ
    phục vụ việc phát triển, không đáng nằm trong sản phẩm.
- Sau sửa Java: kiểm build đã chứa code mới (grep symbol trong `classes/` của
  `unityLibrary/RiseOnAdsAdMobNative.androidlib` trong project host, README Android §8).

## C2. Sổ sự cố

| Khi | Triệu chứng | Nguyên nhân gốc | Luật rút ra |
|---|---|---|---|
| 2026-08-15 | Ô 96×120dp máy thật mất fill | Rule rename cắn literal, badge "NativeAd" (5c6afa3b) | Rename không đụng literal (B12) |
| 2026-08-15 | EditorAd không gắn, NRE | asm editor-only không AddComponent được (93bc80be) | `defineConstraints UNITY_EDITOR` |
| 2026-08-15 | Hai build Android vỡ javac | forward reference / definite assignment; backslash bị nuốt (b295eb56, 212266f0) | Cổng javac; script qua Write |
| 2026-08-15 | Full-screen "delay" | bind creative trong `onCreate`, không phải Activity; Dialog 33cf6634 revert | Activity + content dựng sẵn (d64fdc88) |
| 2026-08-16 | Collapsible miss mỗi lần thứ hai | load chỉ khi đóng; native chặn load khi show (e31b462b) | Prefetch khi displayed |
| 2026-08-16 | Prefetch vô ích | load lúc dismiss hủy ad vừa nạp (4753cfb3) | Ad ấm không bị hủy |
| 2026-08-16 | Cache có ad mà "chưa có" | C# xóa cờ ready đè native (0cf5db67) | Readiness chỉ native |
| 2026-08-16 | Doanh thu ad đang xem mất | paid event gác generation (1a1dcf5d) | Bám identity |
| 2026-08-16 | Layout thỉnh thoảng vỡ (cột 421px, media 0) | recompute giữa layout pass (ad67dcb4) | Idempotent trong `onMeasure` (B6) |
| 2026-08-16 | Ambient không hiện | view cắm trong MediaView bị nuốt (38704204) | Không cắm con vào MediaView |
| 2026-08-16 | Padding scrim to ở ô 96dp | ô chạy ROOMY, spacing theo tier (77167823) | Spacing chặn theo ô |
| 2026-08-16 | Media full-screen không to | pipeline co chrome sau `if (!fullscreen)` (b37ad3be) | Chạy cho cả hai format |
| 2026-08-16 | Media "chưa chạm gì" mà không scale | tường vô hình; `top = badgeHeight` (7a7c527c, 322ff153) | Chỉ tường nhìn thấy |
| 2026-08-16 | Ad vuông lọt rail | cổng 1.05 (a73d1309) | 0.85 nghiêm ngặt |
| 2026-08-16 | Mọi ad thành khung đen | nguyên dải cho cả ad có ratio (3e046158→8cc5a787) | Nguyên dải chỉ ca không báo |
| 2026-08-16 | Headline trống chiếm chỗ | (1e4c37cb) | Asset rỗng GONE |
| 2026-08-16 | Focus lại là xoay ad | foreground present ô đã có ad (499d8e60) | Resume ≠ rotation |
| 2026-08-19 | Dải đen trên ad half máy cutout | window phía sau đo `decorView`, ad đo `displayMetrics` | Một resolver, công bố qua `PanelHeight` |
| 2026-08-19 | Collapsible mảng đen trống | type 1003 (số lớn không vẽ trên) | 1002 + hợp đồng add-trước |
| 2026-08-21 | Panel half bị đẩy lên nav bar | window tôn trọng bar (Samsung 3 nút) | Lay out dưới bar, sticky |
| 2026-08-21 | Full-screen nhảy một bước sau frame đầu | `post()` từ `onSizeChanged` với `heightPixels` đoán | Lập kế hoạch trong `onMeasure` |
| 2026-08-30 | 5 cụm ANR "Input dispatching timed out" | main parked ở GC suspend IL2CPP khi Unity pause (9d6667bc) | `AdEventDispatcher` |
| 2026-08-30 | Game đen sau khi quay lại từ ad | BAL vứt `startActivity` end card (b970cd3d) | `VerifyInitialAttach` → restore |
| 2026-08-30 | Crash Android 8.0 orientation (7 user) | translucent + `screenOrientation` manifest (dfef7999) | Xin ở `onCreate`, bỏ API 26 |
| 2026-09 | Thụt góc bo tính hai lần | `scrimPad + cornerInset` | Sàn, không cộng |
| 2026-09-19 | AdChoices bị cung bo cắt | SDK ghim dấu vào góc thô của lớp riêng nó; reserve của ta không điều khiển được | (xem dòng dưới) |
| 2026-09-21 | Fix "đăng ký `AdChoicesView`" không có tác dụng | Giả định SDK vẽ vào view đăng ký, chưa kiểm trên máy; cây view cho thấy SDK vẫn vẽ ở lớp của nó | Kiểm bằng cây view thật trước khi báo xong; padding `NativeAdView` (B12) |
| 2026-09-21 | Build iOS vỡ: "cannot use '@try' with Objective-C exceptions disabled" | `@try` mới trong `RANRectAdViewFactory.mm`, file này không có `-fobjc-exceptions` (chỉ `RANRectAdPresentation.mm` có) | Không dùng `@try` ở file không có cờ; cổng iOS giờ kiểm (C1) |

## C3. Việc còn mở

- Scrim của ô: icon là hằng số tier, không nuốt phần dư như filler của
  `COMPACT_COLUMN`; dải ảnh ló dưới veil 70% đọc thành trống. Chưa chốt cho
  icon giãn và trần bao nhiêu.
- Body của ô: `BodyMaxLines` = **1** ở COMPACT với `COMPACT_ROW` và scrim →
  body dài luôn cuộn dù ô còn chỗ. Chưa chốt: nới 2 dòng / nấc co CTA / đổi
  trọng số body-trọn vs dòng phụ.
- AdChoices iOS của ô: cơ chế **đã chạy trên iPhone** với test ad (2026-09-21,
  GMA 13.9.0): log `In-feed AdChoices inset applied (GMA 13.9.0): SDK
  container inset by 3pt` (log nay ghi `Rect ad AdChoices…`), không có
  `RESET`. Dấu thật đã thấy trên simulator với unit thật ở test mode (cỡ và vị
  trí ở B12, không đè chữ), và trên iPhone 14 Pro với unit thật ở test mode
  (2026-11-18): góc phải trên, không đè chữ ở 12 ô chụp được, mới nhìn ở ảnh
  thu nhỏ; chưa rõ dấu có nằm trong `GADNativeAdAttributionView` không.
- Creative video ở ô không chứa nổi MediaView 120 (B7): app bỏ ad và tải ad
  khác, nên ô nhỏ chỉ hiện được ad ảnh; mỗi creative video bị bỏ là một
  request và một lần match không thành impression. Số đo liên quan:
  - Unit thật ở test mode trên iPhone (iPhone 14 Pro, iOS 26.6.2, GMA iOS
    13.13.0, 2026-11-18): 15/15 ô nhận creative bật video (SDK ghi
    "video-enabled").
  - Unit native video test của Google (`2521693316`) trên cùng iPhone fill 1
    ô rồi "No ad to show" 14/15; unit native ảnh test (`3986624511`) fill
    15/15.
  Chưa làm: đặt "Media type" của các ad unit in-feed trên AdMob thành chỉ ảnh,
  để AdMob không gửi video cho ô nhỏ (đổi cài đặt tài khoản, chờ chủ dự án
  đồng ý).
- Overlay (full/half) vẫn dùng ô giữ chỗ AdChoices ở góc vuông của
  `NativeAdView`. Thẻ full-screen bo 20dp, nhưng bo góc chỉ cắt cột nội dung
  (Android `setClipToOutline` của thẻ, iOS `_contentColumn.clipsToBounds`),
  không cắt lớp của SDK: dấu AdChoices thật nằm ở góc vuông, có thể lấn ra
  ngoài đường bo. Chưa đo trên máy.
- Google cảnh báo MediaView dưới 120 ở mọi creative, kể cả ảnh, trái với luật
  B7 "120dp chỉ cho video":
  - Android, validator ở test mode: "MediaView is too small for video … if
    your media view shows video" (Redmi, unit thật ở test mode: 12/12 ô có
    media ảnh 58–120dp).
  - SDK iOS 13.9.0: "media views slots that have a width or height smaller
    than 120x120 will be demonetized in the future".
  Chưa quyết, đang giữ sàn ảnh 48dp.
- Obj-C++ đã build sạch trên Mac (2026-09-21, README iOS §7), nhưng hành vi
  iOS mới được nhìn một phần (ô in-feed với test ad).
- "Not all asset views lie inside the native ad view" của validator iOS: 4
  lần với unit native ảnh test của Google (2 ở ô 1000×160px, 2 ở ô
  450×260px), 0 lần trong 15 ô với unit thật ở test mode trên cùng iPhone
  (2026-11-18); chưa rõ khác nhau ở đâu.
- Comment đầu `RANNativeAdBridge.h` về `MobileAdsEventExecutor` đã cũ.

## C4. Trạng thái bàn giao

Cập nhật mục này ở cuối mỗi phiên làm việc; ngày là của lần cập nhật.

**2026-11-26** (monorepo `Ads`, thư mục `AdMob.Native`)

- Full-screen thành thẻ (A6, A7): ba `FullScreenLayout`, thanh app của app open,
  Activity theo hướng của game (`SCREEN_ORIENTATION_BEHIND`). Nhìn trên emulator
  Android 16 (`sdk_gphone64_x86_64`, GMA Android 25.3.0) với unit thật của Get In The
  Bowl ở test mode, creative thật (ảnh, video 16:9, video dọc 9:16):
  - cỡ 720×1280@320, 1080×2400@420, 1440×3200@560, 1600×2560@320, dọc và ngang;
  - đổi cỡ màn hình khi ad đang hiện (`wm size`): thẻ xếp lại (`RefitCard`),
    không crash.
- Sửa từ creative thật: video hay ảnh tối trên panel tối làm thẻ mất mép trên → viền
  1dp vẽ đè lên media; dải đen hai bên video dọc cắt thẻ làm đôi → dải thừa màu
  `#F1F3F4` cho cả video (A6).
- Unit thật ở test mode gần như chỉ trả một creative cho mỗi format rồi "No fill",
  nên không lặp được để lấy nhiều mẫu.
- iOS: code thẻ đã viết song song Android, chưa build trên Mac, chưa nhìn trên máy.
- Việc kế tiếp: build iOS trên Mac và nhìn thẻ trên iPhone, iPad; đo dấu AdChoices ở
  góc bo của thẻ (C3).

**2026-11-18, chiều** (monorepo `Ads`, thư mục `AdMob.Native`, chưa commit)

- iPhone 14 Pro (iOS 26.6.2, GMA iOS 13.13.0) đã đăng ký test device, unit
  native thật, 15 ô 1000/700/450 × 120–330px: 15/15 hiện, creative ghi "Test
  mode", không có gợi ý `testDeviceIdentifiers`, không có click. Dấu
  AdChoices thật hiện ở góc phải trên; trong 12 ô chụp được (3 ô chụp lúc ad
  đã đóng), ảnh thu nhỏ không thấy dấu đè chữ.
- Sửa iOS: `RANAdTextLabel`, `RANNativeAdStarRatingView` và nút CTA của
  ô hình chữ nhật lẫn overlay không nhận chạm (A9). Trước sửa SDK log "User
  interactions must be disabled on the asset view…" 86 lần trong 15 ô, sau
  sửa 0 lần. Sau sửa, cùng iPhone, unit native ảnh test của Google
  (`3986624511`): format `NativeInterstitial` hiện, log không có cảnh báo
  "User interactions must be disabled…" nào và không có click (log của
  `NativeInterstitial` kéo dài 26 giây sau khi `RANOverlayAdViewController`
  hiện). Nút CTA của ô không còn hiệu ứng nhấn.
- Validator iOS báo "1 implementation issue" ở 12 ô chụp được khi còn ad; ở
  ô 1000×200px đó là "MediaView not used for main image or video asset" (C3).
- Xong trong việc kế tiếp của khối sáng: chạy 15 ô với unit thật trên iPhone
  đã đăng ký.
- Việc kế tiếp:
  - chủ dự án đồng ý đặt Media type chỉ ảnh cho ad unit in-feed (C3);
  - xem dấu AdChoices ở ảnh cỡ thật (đến giờ mới nhìn ở ảnh thu nhỏ);
  - tìm vì sao "Not all asset views…" có với unit ảnh test của Google mà
    không có với unit thật (C3).

**2026-11-18, sáng** (monorepo `Ads`, thư mục `AdMob.Native`, chưa commit)

- Đo trên Redmi Note 11 (Android 13, cỡ chữ 1.25) với unit thật ở test mode,
  15 ô từ 164×44dp tới 364×120dp: 15/15 hiện, 0/15 dấu AdChoices đè chữ
  (trước khi né: 12/15), cột chữ cạnh media không còn cắt CTA (ô 364×73dp).
- Mới trên cả hai nền tảng: icon không có ảnh coi như không có icon (A5);
  điểm theo cỡ headline thật và diện tích media nhìn thấy, bỏ thưởng tier
  (B6); reserve AdChoices bằng dấu thật 15dp và chữ né dấu (B12); cột chữ cạnh
  media cao theo nội dung (B6). Android thêm `IsCutByContainer`.
- Simulator iOS (iPhone 17, iOS 26.5, cỡ chữ large) với unit thật, cùng 15 ô:
  15/15 hiện đủ element, dấu AdChoices thật 15pt trùng ô reserve và không
  đè chữ (B12). Sửa thêm trên iOS: dòng cuộn biến mất khi bắt đầu cuộn (B6),
  con sau con có weight bị đo thiếu chỗ (B6), validator bỏ qua chiều cao
  dòng cuộn (B6).
- iPhone thật (iPhone 14 Pro, iOS 26.6.2) với unit native ảnh test của
  Google, cùng 15 ô: 15/15 hiện đủ element. Ad từ unit test của Google trên
  iPhone không có dấu AdChoices ở ô nào (khác simulator với unit thật ở test
  mode, B12), nên việc né dấu chưa kiểm được trên máy thật. Sửa thêm trên cả
  hai nền tảng: ô MEDIA_LEFT 450×330px có icon đứng riêng, icon, headline,
  body nằm lề trái, chỉ rating căn giữa; giờ cả bốn căn giữa (B6, "Một neo
  mỗi khối").
- Redmi chạy lại sau các sửa trên: 15/15 hiện, 0/15 dấu AdChoices đè chữ.
- Test device ID iOS (iPhone 14 Pro, iOS 26.6.2, GMA iOS 13.13.0): SDK chỉ in
  gợi ý `testDeviceIdentifiers` khi app được phép theo dõi (ATT là Allow). Từ
  chối hay chưa hỏi thì IDFA toàn số 0 và không có gợi ý. Cho phép bằng cách
  cài lại app để popup ATT hiện lại rồi chọn Allow, hoặc bật trong Settings
  của app (nút **Allow tracking in Settings** của `AdsDebugger`). Gợi ý hiện
  trong `devicectl --console`. iPhone đã đăng ký là test device trên AdMob.
- Trên cùng iPhone, với unit native ảnh test của Google (`3986624511`),
  validator của SDK iOS báo "Not all asset views lie inside the native ad
  view" 2 lần ở ô 1000×160px và 2 lần ở ô 450×260px. Log không ghi template
  của hai ô này.
- Việc kế tiếp:
  - chạy 15 ô với unit thật trên iPhone đã đăng ký, để kiểm dấu AdChoices và
    việc né dấu (B12) trên máy thật;
  - chạy lại hai ô 1000×160px và 450×260px, ghi template vào log, rồi mở "See
    issues" của validator để biết asset nào nằm ngoài native ad view.

**2026-11-10** (monorepo `Ads`, thư mục `AdMob.Native`, chưa commit)

- Pack chuyển từ repo `NativeAdMob` (1.0.3) vào monorepo, đổi tên toàn bộ.
  Màn che chuyển sang package `com.riseon.ads`; pack này
  không còn biết tới nó ngoài bảng thứ tự lớp.
- Mới, **chưa chạy trên máy nào**: trượt vào/ra của half trên Android
  (`ViewPropertyAnimator` trên content view) và iOS (`UIView` animation + view
  chắn chạm `RANTouchShieldView`); `PanelHeight`; bậc lớp iOS; ad full iOS tự
  `UnityPause`. Java qua javac, C# qua csc của Unity; Obj-C++ chỉ qua cổng cân
  ngoặc + ABI, **chưa build trên Mac**.
- Việc kế tiếp: build Android và iOS, xem trượt, xem hai window SUB_PANEL
  (màn che ẩn được add trước) bằng `dumpsys window windows`.

**2026-09-21** (repo `NativeAdMob`, nhánh `main`)

- Pack vừa được tách khỏi repo game thành repo riêng — lịch sử giữ nguyên, mã
  commit mới — và có `package.json` (`8a4d04b2`). Tag **`v1.0.0`** là commit
  ngay sau đó, sửa README cho pack đứng riêng: bỏ mọi phần riêng của game,
  thêm hướng dẫn tích hợp chung (A11), đổi mã commit sang repo này.
- `386c942a`: fix AdChoices in-feed Android (padding `NativeAdView`). **Chưa
  thử trên máy** — build rồi đọc cây view: dấu "Ad Choices Icon" phải thụt
  đúng bằng badge "Ad".
- `4324a629`, `3334eeea`: fix AdChoices in-feed iOS, log tự kiểm cho cả hai
  nền tảng, rồi bỏ `@try` gây vỡ build Mac (C2). Bản còn `@try` đã chạy trên
  iPhone nhờ cờ tạm và log `applied … 3pt`; **bản đã bỏ `@try` chưa build lại
  trên Mac.**
- Việc kế tiếp: build lại iOS trên Mac; thử Android trên máy; kiểm AdChoices
  iOS khi có ad unit thật; chờ chủ dự án chốt icon scrim và body 2 dòng (C3),
  hộp AdChoices 45px lấn headline (C3), cảnh báo MediaView < 120×120 của SDK
  iOS (C3).
