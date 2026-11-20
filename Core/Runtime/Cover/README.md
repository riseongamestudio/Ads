# Màn che

Hai lớp màu trơn do nền tảng vẽ, nằm trên mọi thứ Unity vẽ: `FullScreenCover`
che cả màn và **dừng game**, `HalfScreenCover` làm nền dưới một ad nửa màn hình
trong lúc chuỗi ad đổi ad, **không** dừng gì. Cả hai `internal`, chỉ bộ điều
phối quảng cáo dùng. Mọi hàm gọi được từ thread bất kỳ.

## Mục lục

- [API](#api)
- [Luật phải trả cho cái pause](#luật-phải-trả-cho-cái-pause)
- [Thứ tự lớp](#thứ-tự-lớp)
- [Android](#android)
- [iOS](#ios)
- [Editor](#editor)
- [Chưa kiểm trên máy](#chưa-kiểm-trên-máy)

## API

| Hàm | Làm gì |
|---|---|
| `FullScreenCover.Show(color)` | Che cả màn, dừng game. Gọi lại khi đang che thì sơn lại màu và giữ lại pause |
| `FullScreenCover.Hide()` | Gỡ, game chạy lại |
| `HalfScreenCover.Prepare(color, panelHeight)` | Thêm vào **ẩn**, đúng chỗ của nó trong thang lớp. Đã có thì sơn lại và đổi cao tại chỗ |
| `HalfScreenCover.Reveal()` / `Conceal()` | Chỉ hiện/ẩn, không bao giờ thêm lại |
| `HalfScreenCover.Remove()` | Gỡ hẳn |

- `panelHeight` là `PanelHeight` của chính ad nửa màn hình, truyền nguyên: pixel
  trên Android, point trên iOS, pixel `Screen` trong Editor. `≤ 0` là cả màn.
  Đừng tự tính: một màn che từng đo `decorView.getHeight()` trong khi ad đo
  `displayMetrics.heightPixels` — giống nhau trên giả lập, khác trên máy có
  cutout/gesture bar, thành dải đen phía trên ad.
- Màn che half chỉ nuốt chạm trên vùng panel; dải phía trên thuộc về game.
- Ad toàn màn hình (của SDK nào cũng vậy) luôn mở **trên** màn che full.

## Luật phải trả cho cái pause

Game dừng thì **C# không chạy trên player loop**, mà `Hide()` là một lời gọi C#:

> Thứ gỡ màn che full **không được** xếp lịch qua player loop của Unity.

Lời gọi thì thread nào cũng được (JNI/DllImport post sang main native, còn sống
khi Unity dừng). Cái chết là **chỗ xếp lịch**: `Update`, coroutine,
`UniTask.Post`, callback của SDK đã marshal về main thread Unity. Đường sống là
callback bắn trên thread của chính SDK quảng cáo (vd. AppLovin MAX với
`InvokeEventsOnUnityMainThread = false`; trên iOS đó là một `NSOperationQueue`
nền — iOS không được miễn).

- **Mọi** đường kết thúc một lượt show phải gỡ, kể cả hiển thị thất bại.
- Mọi thứ đứng trước `Hide` trong callback đó phải không ném và không đụng API
  Unity (`PlayerPrefs`, `FindAnyObjectByType`, GameObject…). Điều kiện cần tra
  thì tính trước trên thread Unity rồi truyền vào dạng bool. Một game từng đọc
  `PlayerPrefs` ngay trong callback đó: callback ném trước khi tới `Hide`, màn
  che không bao giờ hạ.
- Đã trả giá: `Hide` qua callback marshal về main → Activity màn che là
  `mResumedActivity`, `UnityPlayerActivity` STOPPED, log Unity im — màn đen
  vĩnh viễn, back bị nuốt có chủ ý, chỉ force-stop thoát.
- Giữ màn che full tới khi ad phía sau **đi hẳn**: gỡ là game chạy lại.

## Thứ tự lớp

Màn che chen giữa các lớp của package ad. Không package nào gọi package nào;
mỗi bên tự đặt lớp của mình theo bảng chung:

| Lớp | iOS: bậc trong `layer.name` | Editor: `AdsEditorLayers` | Android |
|---|---|---|---|
| Ad toàn màn hình | không có bậc: controller được present | `FullScreenAd` | Activity |
| Màn che toàn màn hình | `3000` | `FullScreenCover` | Activity |
| Ad nửa màn hình | `2000` | `HalfScreenAd` | window `TYPE_APPLICATION_SUB_PANEL` |
| Màn che nửa màn hình | `1000` | `HalfScreenCover` | window `TYPE_APPLICATION_SUB_PANEL`, **add trước** ad |
| View không đánh dấu (vd. banner của SDK khác) | `0` | `OverGame` | |
| Ô in-feed, native MRec | `-1000` | `NativeRect` | window `TYPE_APPLICATION_PANEL` |

Trong Editor các lớp là sorting order của canvas Screen Space Overlay, tính từ
**Editor Sorting Order** trên trang settings (`OverGame`) lên tới `FullScreenAd`
(+5). Ô in-feed và native MRec trong Editor nằm trên banner (+1), vì ad giả không có
view của game để chen xuống dưới.

## Android

`RiseOnAds.androidlib`, package Java `com.riseon.ads`: `FullScreenCover`,
`FullScreenCoverActivity`, `HalfScreenCover`, `BaseCover`. C# gọi qua
`CoverPlatformAndroid` (`AndroidJavaClass.CallStatic`).

- **Màn che full là Activity**: chỉ Activity trên cùng mới làm hệ thống pause
  Unity bên dưới, đúng như ad Activity. Không dòng nào gọi `UnityPlayer.pause()`.
  - Theme `RiseOnFullScreenCoverTheme` **đục**: `Theme.Translucent` bảo
    WindowManager rằng window có thể không phủ hết, nên Activity dưới được giữ
    VISIBLE và mọi khoảnh khắc window chưa vẽ xong, compositor lấp bằng frame
    cuối của Unity — game lộ qua màn che. `windowBackground` đen,
    `windowAnimationStyle @null`.
  - **Không `android:noHistory`**: cờ đó finish Activity ngay khi nó stop, mà
    ad Activity mở lên trên làm nó stop — màn che chết đúng lúc đang che vết nối.
  - `singleTop`: đang lên thì sơn lại, không xếp cái thứ hai. Back bị nuốt có
    chủ ý (`onBackPressed` + callback predictive back).
- **Màn che half là window** (Dialog) `TYPE_APPLICATION_SUB_PANEL`,
  `Gravity.BOTTOM`, `FLAG_NOT_FOCUSABLE`, cao đúng `panelHeight` — **short
  window**, dải trên không nằm trong input region.
  - Android xếp window theo type rồi theo **thứ tự add**; không có API chèn.
    Ad half cũng là `SUB_PANEL`, nên màn che nằm dưới ad **chỉ vì được add
    trước**. `Prepare` add rồi ẩn ngay trong cùng một message (frame đầu vẽ
    sau cả hai, không ai thấy); `Reveal`/`Conceal` đặt `VISIBLE`/`GONE` cho
    decor view **trực tiếp** — không qua `Dialog.show()`, vì trên window chưa
    add (vd. Activity đã chết) nó sẽ add lại, tức nhảy lên đỉnh. Đổi cao dùng
    `Window.setLayout`, cập nhật tại chỗ.
  - `Prepare` phải tới **trước** `Show` của ad, và **cùng một phía** UI
    thread: hai lệnh đều post lên main looper thì giữ đúng thứ tự gọi; một
    lệnh chạy ngay (gọi từ UI thread) còn lệnh kia post thì không.
- Callback/C#: mọi lệnh hop main bằng `BaseCover.RunOnMainThread` (Handler main
  looper, không phải `Activity.runOnUiThread` — cái đó không có gì để post khi
  Activity null).

## iOS

`Native/ROAdsCover.mm`, C# gọi qua `CoverPlatformIOS` (`DllImport`). Mọi hàm C
`dispatch_async` sang main queue.

- Cả hai màn che là **subview** của view Unity, chèn theo bậc
  (`ROAdsInsertSubviewAtTier`: đặt `layer.name = "riseon.layer.<bậc>"`, chèn
  ngay dưới subview đầu tiên từ dưới lên có bậc cao hơn, không có thì
  `addSubview:`). Không dùng `zPosition`: UIKit hit-test theo thứ tự mảng
  `subviews`. Thứ tự đến không quan trọng.
  - Không phải `UIWindow`: `windowLevel` xếp tuyệt đối trên **mọi thứ**, kể cả
    ad mà SDK mediation present.
  - Không phải present: một VC chỉ present được một thứ; màn che chiếm slot
    đúng lúc SDK cần nó để mở ad.
- Màn che half là container phủ cả host, `hitTest:` trả "không phải tôi" ở mọi
  chỗ ngoài panel; panel cao `panelHeight` (point, giữ phần lẻ — làm tròn lệch
  tới ⅓pt trên màn 3x). `Prepare` thêm với `hidden = YES`.
- **Pause**: màn che full gọi `UnityPause(1)` và **giữ** nó. `UnityPause` là cờ,
  không đếm, và mọi ad toàn màn (SDK nào cũng vậy, kể cả native ad của RiseOn)
  pause khi mở và **resume khi đóng** — cú resume rơi giữa lúc màn che còn
  đứng. Không có sự kiện nào báo, nên `ROAdsPauseGuard` là một `CADisplayLink`
  (common run-loop modes) chỉ sống khi màn che full đứng: mỗi frame đọc
  `UnityIsPaused()`, thấy bị xoá thì đặt lại, log một lần mỗi lượt. Có thể lọt
  **một frame** game chạy sau khi ad đóng, sau màn che đục. `Hide` khi không có
  màn che thì không đụng cờ (tránh chạy lại game mà một ad đang giữ dừng).

## Editor

`CoverPlatformEditor` + `EditorCover` (canvas Screen Space Overlay riêng,
`DontDestroyOnLoad`).

- Sorting order theo cột Editor của bảng. Ad full-screen trong Editor là ad giả
  của pack, nằm ở `FullScreenAd`, trên màn che như Activity của ad trên Activity
  màn che.
- Ẩn = `Canvas.enabled = false` (giữ chỗ). Area là `Image` có `raycastTarget` kể cả
  khi trong suốt, như window trên máy.
- Màn che không dừng game. Pause trong Editor thuộc về lượt full-screen: chuỗi bắt
  đầu hiện ad đầu thì pack gọi `PauseInEditor(true)` của manager, ad cuối đóng thì
  `PauseInEditor(false)`; game không có manager thì pack dùng cách dừng mặc định
  (xem README của pack, mục `AdsManagerBase`).
- Lệnh từ thread khác được post về thread Unity qua `SynchronizationContext`
  bắt ở `BeforeSceneLoad` (`EditorThread`).
- **Không tái tạo hiểm hoạ của máy**: Editor ở time scale 0 vẫn chạy mọi
  `Update`; một `Hide` không bao giờ tới trên máy vẫn tới ở đây.
- asmdef `RiseOn.Ads.Platforms.Editor` là `defineConstraints: ["UNITY_EDITOR"]`
  với `includePlatforms: []`, **không** `includePlatforms: ["Editor"]`: Unity
  cấm `AddComponent` một MonoBehaviour của assembly editor-only.

## Chưa kiểm trên máy

- Android: window `Prepare` rồi ẩn (decor `GONE`) có thật sự không nhận chạm,
  và có giữ đúng chỗ dưới ad qua nhiều lượt `Reveal`/`Conceal` không — đọc bằng
  `adb shell dumpsys window windows`.
- iOS: chưa build (Windows không biên dịch được Obj-C++).