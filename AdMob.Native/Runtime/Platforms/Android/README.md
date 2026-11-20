# RiseOn Ads AdMob Native — Android

Bridge Java của pack: `Bridge/RiseOnAdsAdMobNative.androidlib` (package
`com.riseon.ads.admobnative`), gọi từ C# qua `AndroidJavaObject` trong
`RectAdClient` / `OverlayAdClient`, nghe lại qua
`AndroidJavaProxy` trong `Bridge/*Proxy.cs`. Luật chung của pack ở
[tài liệu core](../../README.md); file này chỉ ghi những gì **riêng Android**.

## 1. androidlib

- `build.gradle`: `compileOnly "com.google.android.gms:play-services-ads:25.3.0"`
  — app đã có GMA qua plugin Unity, `compileOnly` để **không đóng gói trùng**.
  Đổi phiên bản GMA của host thì đổi dòng này theo. `minSdk 24`, `compileSdk
  34`, `sourceCompatibility 1.8` → **không dùng cú pháp sau Java 8** dù cổng
  javac local có thể chạy với `-source 11`.
- `AndroidManifest.xml` khai `OverlayAdActivity`; mỗi thuộc tính đều có lý do
  ghi ngay trong file — đọc comment trước khi đổi. Điểm hay bị "sửa cho gọn":
  - **Không có `android:screenOrientation` trên `OverlayAdActivity`.** Android
    8.0 (đúng API 26) ném "Only fullscreen opaque activities can request
    orientation" từ `super.onCreate` cho Activity translucent khai orientation
    cố định — crash trước khi code của ta chạy, 7 người dùng dính (dfef7999).
    Orientation xin ở runtime trong `onCreate`, **bỏ qua API 26** (ở đó
    Activity translucent theo orientation của Activity đục bên dưới — game
    portrait — nên vẫn portrait).

## 2. Luồng

- Mọi lệnh từ C# hop về main thread qua `BaseAd.RunOnMainThread` (Handler
  main looper, không phải `Activity.runOnUiThread`
  — cái đó không có gì để post khi Activity null và bản cũ chạy action trên
  thread gọi: thao tác window trên thread Unity là crash chờ sẵn).
- **Mọi `Notify*` đi qua `AdEventDispatcher.Post`** — một `HandlerThread`
  `RiseOnAdMobNativeEvents`. Lý do: listener là C# sau `AndroidJavaProxy`,
  gọi nó là bước vào IL2CPP; bước vào IL2CPP có thể **block chừng nào player
  còn pause** (handshake stop-the-world của GC không xong khi Unity ngủ dưới
  ad full, hay dưới bất cứ thứ gì khác làm nó dừng). Đo trên production
  2026-08: năm cụm ANR, mỗi cụm là ảnh
  chụp main thread parked ở GC suspend point trong `OnAdCompleted` /
  `OnStateChanged` ("Input dispatching timed out"). Không sửa được cái stall
  từ tầng này; sửa được **ai bị stall**: main thread không bao giờ được đứng ở
  cửa đó. Tham số chụp vào local **trước** khi post; một queue giữ thứ tự.
  **Luật cho code mới: không gọi listener trực tiếp từ bất kỳ thread nào —
  luôn `AdEventDispatcher.Post`.**
- Callback SDK (paid event, `onAdClicked`, load) rơi trên thread SDK; cấu trúc
  mutable của ô sống trên main → membership test dùng
  `Collections.newSetFromMap(new ConcurrentHashMap<>())` (`OwnsAd`), không
  walk deque.
- Mọi lệnh từ C# **mang `AndroidApplication.currentActivity`** (Unity giữ
  static, gọi được từ thread nào cũng rẻ). Activity đổi = window cũ chết:
  ô hình chữ nhật bỏ mọi thứ đã materialize, ad đang dựng về pool, chỉ ad
  thô sống sót.
- `AndroidJavaProxy` dispatch theo **tên method**: proxy C# phải đặt đúng tên
  method của interface Java (`RectAdListener`, `NativeAdLoadListener`,
  `NativeAdCompletedListener`). Sai tên là nuốt lặng, không có lỗi.

## 3. Ad full-screen là Activity — yêu cầu cứng

- **Không được đổi sang Dialog.** Bản Dialog (33cf6634) đã bị revert
  (53434fcd) theo yêu cầu chủ dự án. Điều tra delay cho thấy thứ phình ra
  không phải Activity switch mà là **bind creative trong `onCreate`** (video
  end card khởi tạo player). Fix giữ Activity (d64fdc88): `OverlayAdContentView`
  **dựng sẵn lúc LOAD**, truyền qua `Session` (kèm `CloseRelay` và
  `closeOnLeft` đã roll), `onCreate` chỉ attach; restore path dựng tươi.
- **Background activity launch (Android 10+)**: `startActivity` từ app đang ở
  nền **thành công như một lời gọi và bị hệ thống vứt lặng**. Kịch bản thật
  (b970cd3d): người chơi tap xuyên ad vào trình duyệt → interstitial tự đóng
  sau lưng trình duyệt → start end card đi vào hư không → session treo, sau
  lớp đen mà game dựng cho lúc chuyển ad, vĩnh viễn ("game đen sau khi quay
  lại"). Luật: **một session đã
  start thì hoặc attach, hoặc được restore khi resume, hoặc complete với lỗi —
  không bao giờ treo.** `StartSession` post `VerifyInitialAttach` sau
  `RESTORE_ATTACH_TIMEOUT_MS` (1200ms): chưa có Activity → `restorePending`,
  giao cho máy restore (host ở nền → watcher `onActivityResumed` mở lại end
  card khi người chơi quay về; host đang trước → vòng restore thử lại
  `MAX_RESTORE_START_ATTEMPTS` 4 lần cách 180ms, chờ focus tối đa 300×100ms,
  rồi complete lỗi). Chủ dự án chốt **mở lại end card khi quay về** (không
  bỏ qua).
- Back: hai cơ chế vì hai thời kỳ — `onBackPressed` (cũ) và callback no-op
  đăng ký cho predictive back (`enableOnBackInvokedCallback`).
- `excludeFromRecents`, `launchMode standard`, `configChanges` đầy đủ để
  xoay/resize không recreate.
- Pause: Activity trên cùng làm hệ thống pause Unity phía dưới, như mọi ad
  Activity của SDK khác. **Không dòng nào trong pack gọi
  `UnityPlayer.pause()`** (tài liệu core B4).

## 4. Window type và thang xếp lớp

| Tầng | Cơ chế | Type |
|---|---|---|
| ô hình chữ nhật | window trên Unity Activity (`RectAdPresentation`) | `TYPE_APPLICATION_PANEL` (1000) |
| half-screen ad | Dialog window (`OverlayAdPresentation`) | `TYPE_APPLICATION_SUB_PANEL` (1002) |
| full-screen ad | Activity | — |

Window không có index như subview — `WindowManager` không có API đổi z-order.
Chỉ hai đòn bẩy: **type**, và thứ tự `addView` trong cùng type. Đòn bẩy thứ
hai vô dụng vì muốn leo lên phải gỡ window ra thêm lại (flicker, mà cũng chỉ
tới được *đỉnh* của type chứ không chèn xuống dưới được). Window nào của
package khác muốn nằm **dưới** ad half (vd. một màn che) cũng phải là 1002 và
**add trước** khi ad `Show`; ẩn/hiện (decor `GONE`/`VISIBLE`) giữ chỗ, chỉ gỡ
rồi add lại mới nhảy lên đỉnh. Đó là hợp đồng của bên điều phối, pack không
biết và không kiểm.

### Đã thử tách type và đã sai — đừng thử lại

Ý tưởng: đẩy ad half lên `TYPE_APPLICATION_ATTACHED_DIALOG` (1003) để thang tự
đúng, dựa trên giả định "số type lớn hơn thì vẽ trên". **Giả định đó sai.** Đo
bằng `adb shell dumpsys window windows` (liệt kê từ trên xuống) trên bản build
đã chứa 1003:

    ty=APPLICATION_SUB_PANEL          <- window lẽ ra phải nằm dưới
    ty=APPLICATION_ATTACHED_DIALOG    <- ad half, bị chôn
    ty=APPLICATION_PANEL              <- ô hình chữ nhật
    ty=BASE_APPLICATION               <- Unity

Triệu chứng trên máy: collapsible ra một mảng đen nửa dưới màn hình, không có
gì trong đó. Sub-window type được ánh xạ sang layer bên trong
`WindowManagerService`, và ánh xạ đó **không** theo thứ tự số. Type duy nhất
xếp trên `SUB_PANEL` là `ABOVE_SUB_PANEL` (1005), mà nó `@hide` nên không gọi
tên được. Vậy 1002 là bậc cao nhất dùng được, phần còn lại do hợp đồng gọi hàm
gánh.

### Hợp đồng system UI

Mọi window của pack **lay out DƯỚI navigation bar và cutout, ẩn bar kiểu
sticky** — đúng hợp đồng bề mặt của game. Window ad half từng là thứ duy nhất
trên màn hình tôn trọng bar: vuốt bar ra khỏi auto-hide, quay từ Recents →
WindowManager đậu panel LÊN TRÊN bar, dải trắng chỗ nút, cả ad bị xô lên, tới
lần chạm sau bar ẩn lại thì panel rơi xuống. Đo trên Samsung ba nút,
2026-08-21. Ô hình chữ nhật đặt bằng px tuyệt đối game tính trên bề mặt full
màn; một window né bar là một ô không còn nằm chỗ game đặt.

### Chiều cao panel half

**Một** nơi quyết: `ResolveHalfScreenPanelHeight` trong
`OverlayAdContentView`, công bố ra C# qua `OverlayAd.ResolvePanelHeight` →
`HalfScreenAd.PanelHeight` (pixel). Window nào đứng sau panel phải dùng đúng
số đó: đo riêng bằng `decorView.getHeight()` bằng
`displayMetrics.heightPixels` trên giả lập 1080×1920 và **khác** trên máy có
cutout/gesture bar — cả hai window ở `Gravity.BOTTOM`, cái cao hơn thừa ra
thành dải đen trên ad. Window half là **short window**, không phải window
full với đỉnh trong suốt: dải trên không nằm trong input region nên game vẫn
nhận chạm.

### Trượt vào/ra

`OverlayAdPresentation` dịch **content view** (`translationY`), không dùng
window animation và không đổi cỡ window: window giữ đúng chiều cao panel suốt
lúc trượt, mép window cắt phần đã ra ngoài, và window phía sau (nếu có) luôn
khớp mép. `onStart` chạy **trước** khi window được add, nên vị trí xuất phát
(dưới mép) và khóa chạm (`OverlayAdContentView.SetTouchLocked`, nuốt mọi chạm
trong window) đặt ở đó — frame đầu không bao giờ thấy panel nằm yên. Animation
bắt đầu ở show message (`OnShowListener`, lúc window đã add); `withEndAction`
không chạy khi bị cancel, nên trượt ra giữa chừng trượt vào chỉ việc cancel
rồi chạy ngược từ chỗ đang đứng. `dismiss()` luôn cancel animation trước khi
release, để end action không bắn trên ad đã đi. Developer option "Animator
duration scale = 0" làm trượt kết thúc ngay — vẫn đúng thứ tự event.

## 5. RedirectOnClose: hook nào bắn ở đâu

- Cú chạm được **thả xuyên** xuống `NativeAdView` (touch listener trả `false`
  ở DOWN) — SDK thấy ngón tay thật trên view của họ, tự đếm, tự redirect.
  `performClick()` từng ngồi đó và không làm gì: nó bắn `OnClickListener`
  không có `MotionEvent`, SDK đếm touch chứ không đếm lời gọi.
- `onWindowFocusChanged(false)` chỉ phủ **đường Activity** (full-screen).
  Từng tin rằng "view nhận focus change dù nằm window nào" — **sai** cho đúng
  window quan trọng: `OverlayAdPresentation` đặt `FLAG_NOT_FOCUSABLE` cho
  window half, mà window không bao giờ nhận focus thì không bao giờ báo mất
  focus. Đo: cả phiên show half, hook đó bắn đúng một lần, cho một ad
  full-screen. Đường half do `onWindowVisibilityChanged` gánh: trình duyệt
  chiếm màn → host Activity stop → mọi window trên token đó invisible.
- Chọn hook view chứ không phải `Activity.onPause` vì collapsible sống trong
  Dialog trên Unity Activity, không có onPause riêng. Nhánh `OnPaused` vẫn
  giữ — thừa chứ không thay thế, phòng full-screen được bật `RedirectOnClose`.
- Countdown đóng băng khi window mất visibility (Dialog không có Activity
  pause riêng; visibility thì window nào cũng có).
- Latch chống double-click (`SingleClickNativeAdContainer`) nhả theo
  **visibility**, không theo focus, cùng lý do.

## 6. Presentation của ô hình chữ nhật

- Giữ tham chiếu `ViewTreeObserver` đã đăng ký: `getViewTreeObserver()` trả
  observer của window khi còn attach và một observer nổi **khác** sau khi
  detach; remove nhầm instance là no-op lặng → presentation, ad, view tree và
  Activity sống mãi trong callback list của window.
- Vòng quan sát hình học chỉ tiến trên **draw pass**, mà Android bỏ
  `invalidate()` trên view chưa VISIBLE → phải drive decor view của window
  host, không thì vòng lặp đứng tới khi thứ gì đó tình cờ vẽ lại.
- Chờ root sẵn sàng có giới hạn (`MAX_ROOT_WAIT_MS` 10s) và kết thúc như một
  dismissal bình thường, không được park ô (park là chặn mọi load sau).
  Layout cuối phải ổn định 3 pass, quan sát 80–500ms.
- Backdrop bo góc kiêm clip (`setOutlineProvider(BACKGROUND)` +
  `setClipToOutline(true)`); màu trong suốt vẫn clip.
- **Không measure-thăm-dò + `requestLayout` giữa layout pass** mà không có
  khóa idempotent: Android vứt `requestLayout` giữa pass và GMA lay cột ở cỡ
  thăm dò (ad67dcb4). Bản sửa `post()` từ `onSizeChanged` sau đó bị thay:
  overlay giờ lập kế hoạch né control **trong `onMeasure`** theo kích thước
  thật, guard bằng `plannedPanelWidth/Height` + `avoidancePlanDirty`, và
  `planningInMeasure` chặn `requestLayout` thừa — vì `displayMetrics.heightPixels`
  có OEM đã trừ cutout, có OEM chưa, nên kế hoạch đoán trước rồi sửa sau frame
  đầu là một cú nhảy nhìn thấy (tài liệu core B6).

## 7. AdChoices

**Dấu AdChoices là của SDK và nằm trong lớp của SDK.** Cây view của ô hình chữ
nhật đo trên máy (2026-09-21, Oppo CPH2121, ô 602×234px):

```
NativeAdView
├─ FrameLayout            nội dung của ta (ảnh nền, veil, chữ, nút, badge "Ad", reserve)
└─ FrameLayout (zza)      lớp của SDK, full-size
   └─ RelativeLayout
      └─ "Ad Choices Icon" 45×45px, ghim sát góc trên-phải của lớp này
```

Bytecode `play-services-ads-api` 25.3.0 xác nhận: `NativeAdView extends
FrameLayout`, **không** override `onMeasure`/`onLayout`/`setPadding`; lớp
`zza` được tạo trong `zze(Context)` là `FrameLayout` thường với
`LayoutParams(MATCH_PARENT, MATCH_PARENT)` không margin; override `addView`
và `bringChildToFront` chỉ để giữ `zza` luôn ở trên cùng.

**Đã thử và sai — đừng thử lại:** đăng ký `AdChoicesView` của ta qua
`setAdChoicesView` (trước `setNativeAd`). Bytecode chỉ cho thấy hàm đó đăng ký
view dưới asset "3011"; không có gì nói SDK sẽ vẽ dấu vào đó. Trên máy: SDK
nhận view (gắn listener, view thành bấm được) nhưng **vẫn vẽ dấu ở lớp `zza`,
sát góc thô**; view của ta trống, thành một ô vô hình bấm được. Đã gỡ.

**Cách đang dùng** — `RectAdViewFactory.InsetSdkOverlay`, gọi trong
`BuildNativeAdView` trước khi `addView` nội dung:
- `nativeAdView.setPadding(inset ×4)` với `inset = BadgeEdgeInsetPx()` — đúng
  giá trị badge "Ad" dùng, nên hai dấu góc luôn thụt bằng nhau. Lớp `zza`
  (MATCH_PARENT) co vào `inset`, dấu theo vào.
- Nội dung của ta: `LayoutParams` MATCH_PARENT với **margin âm = −inset** →
  FrameLayout đặt nó ở `padding + margin = 0`, đo với `padding + margin = 0`
  → phủ kín ô **đúng từng pixel như cũ**: cùng bounds, cùng layout, validator
  đo cùng số (nó đo tương đối `NativeAdView` bằng
  `offsetDescendantRectToMyCoords`).
- `setClipToPadding(false)`: không thì dải giữa padding và mép ô (mép ảnh
  nền scrim) bị cắt. Cung bo vẫn do outline của presentation cắt như cũ.
- `inset == 0` (ô vuông) → return sớm, không đổi gì.

Rủi ro duy nhất: cách này dựa vào việc `zza` là con MATCH_PARENT của
`NativeAdView`. Nên mỗi lần bind, `ReportSdkLayerShape` kiểm đúng điều đó
(trước khi nội dung của ta được thêm vào, lúc mọi con đều là của SDK) và log
một lần mỗi lần chạy app, tag `RectAd`:
- đúng: `Rect ad AdChoices: SDK layer inset by 7px (GMA 25.x.y)`;
- sai: `Rect ad AdChoices inset may not reach the mark (GMA ...): SDK layer
  <class> is <WxH margins=[...]>, not MATCH_PARENT without margins - update
  InsetSdkOverlay for this SDK` (hoặc "no SDK layer at bind").
Chỉ đọc, không đổi gì theo kết quả kiểm. Nâng GMA xong hãy tìm dòng này đầu
tiên, rồi kiểm lại bytecode `zze` và cây view.

Hộp dấu 45px (cố định 15dp) cao hơn dải trên cùng ta chừa cho badge (~31px)
nên lấn vào dòng headline — có từ trước fix; validator không thấy vì nó đo
reserve (23px) chứ không đo dấu thật. `OverlayAdContentView` vẫn dùng ô giữ
chỗ trơn (overlay không bo góc nên chưa lộ).

## 8. Cổng javac — bắt buộc sau mọi sửa Java

Hai lần build Android của chủ dự án vỡ ở javac (illegal forward reference +
definite assignment trong `RectAdSlot`; escape `\|` bị shell nuốt một
backslash). Công thức, chạy được không cần Android Studio:

```
javac  = <Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/javac.exe
cp     = <Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platforms/android-36/android.jar
       ; ~/.gradle/caches/**/jetified-play-services-ads-api-2*/jars/classes.jar   (bản mới nhất)
       ; ~/.gradle/caches/**/jetified-play-services-ads-2*/jars/classes.jar
javac -source 11 -target 11 -Xlint:-options -cp <cp> -d <out> <mọi .java trong androidlib>
```

Phiên bản trong gradle cache đổi theo host (25.4.0 từng bị evict) → glob,
đừng ghim. Gradle thật biên dịch với `1.8` nên code phải là Java 8. Sau khi
build, kiểm build đã chứa code mới:

```
grep -a "<symbol>" Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/RiseOnAdsAdMobNative.androidlib/build/intermediates/javac/release/compileReleaseJavaWithJavac/classes/com/riseon/ads/admobnative/*.class
```

## 9. Debug trên máy

- `adb` **không** có trong PATH của shell nền → đường dẫn tuyệt đối
  `%LOCALAPPDATA%/Android/platform-tools/adb`. Cắm nhiều thiết bị cùng lúc
  thì **luôn truyền `-s <serial>`**, thiếu là adb trả rỗng. Cùng một ô tính
  bằng px ra số dp khác nhau theo density của máy (máy thật thường 3.0, giả
  lập có thể 1.5), nên layout khác nhau giữa hai máy thường là do dp, không
  phải bug.
- Log: `logcat -v time -s OverlayAd:V RectAd:V NativeAdPool:V BaseAd:V`
  chạy **nền ra file** — buffer bị AppLovin spam đẩy trôi trong 1–2 phút nên
  `logcat -d` hay trượt. Dòng cần tìm: lý do loại của validator
  (`Rect ad validation failed: …`, như `registered assets overlap`,
  `... is clipped`), `Rect ad exhausted …` (hết ứng viên layout), dòng
  `Rect ad AdChoices` (§7), và việc của pool (`Loaded:`, `Took back an ad
  unfit for size …`).
- `adb shell dumpsys window windows` — thứ tự window từ trên xuống và khung
  (`mFrame`) của từng window; ô hình chữ nhật là window `ty=APPLICATION_PANEL`,
  ad half (và window của package khác nằm dưới nó) là
  `APPLICATION_SUB_PANEL`.
- **Cây view thật của ad đang hiện — ăn đứt screenshot:** `adb shell
  uiautomator dump --windows /data/local/tmp/ui.xml` rồi `adb pull`. Chỉ đọc,
  không chạm gì. Cho class, `bounds` px tuyệt đối, text, `content-desc` (dấu
  AdChoices của SDK có desc `Ad Choices Icon`) của **mọi window**. **Không
  dùng `dumpsys activity top` cho ô hình chữ nhật hay half-screen**: nó chỉ
  dump cây view của chính Activity, không có window con — và trên máy Oppo này
  còn không dump cả Activity của game. Nó chỉ dùng được cho ad full-screen (vì
  đó là Activity).
- Git Bash đổi đường dẫn dạng `/data/...` thành `C:/Program Files/Git/data/...`
  trước khi đưa cho adb → đặt `MSYS_NO_PATHCONV=1` khi gọi adb với đường dẫn
  phía máy.
- `dumpsys activity activities` cho `mResumedActivity` khi nghi màn đen.
- Không bao giờ `adb shell input` lên thiết bị; không bao giờ tap vào nội
  dung quảng cáo.
