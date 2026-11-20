# RiseOn Ads AdMob Native — iOS

Bản hiện thực iOS của bộ luật chung ở [tài liệu core](../../README.md): Obj-C++
trong `Bridge/Native/` (prefix `RAN`), C# gọi qua `DllImport("__Internal")`
trong `Bridge/NativeAdBridge.cs`, cùng bộ client `RectAdClient` /
`OverlayAdClient` như hai nền tảng kia. Ba nền tảng ngang
hàng — cùng máy trạng thái, cùng layout engine, cùng ngưỡng chính sách, cùng
format dòng log — nên khi sửa một luật thì sửa cả ba, và bảng đối chiếu dưới
đây chỉ để tìm nhanh file tương ứng bên Java khi cần đối chiếu từng dòng.
File này ghi những gì **riêng iOS**.

## 1. Bảng đối chiếu file với bản Java

| Obj-C++ (`Bridge/Native/`) | Java (`../Android/Bridge/RiseOnAdsAdMobNative.androidlib`) |
|---|---|
| `RANNativeAdBridge.h/.mm` | (bề mặt `AndroidJavaObject` + các `*Proxy.cs`) |
| `RANBaseAd.h/.mm` | `BaseAd.java` (+ giao thức `NativeAdPresentation`) |
| `RANNativeAdStarRatingView.h/.mm` | `NativeAdStarRatingView.java` |
| `RANMeasureLayout.h/.mm` | (mô hình measure của View/LinearLayout/FrameLayout) |
| `RANAdTextLabel.h/.mm` | (TextView + `ApplyTextMode` + marquee) |
| `RANNativeAdPool.h/.mm` | `NativeAdPool.java` |
| `RANRectAd.h/.mm` | `RectAd.java` |
| `RANRectAdSlot.h/.mm` | `RectAdSlot.java` |
| `RANRectAdPresentation.h/.mm` | `RectAdPresentation.java` |
| `RANRectAdLayoutEngine.h/.mm` | `RectAdLayoutEngine.java` |
| `RANRectAdLayoutValidator.h/.mm` | `RectAdLayoutValidator.java` |
| `RANRectAdViewFactory.h/.mm` | `RectAdViewFactory.java` |
| `RANRectAdLayoutTypes.h` | (các struct/enum plan dùng chung) |
| `RANOverlayAd.h/.mm` | `OverlayAd.java` |
| `RANOverlayAdContentView.h/.mm` | `OverlayAdContentView.java` |
| `RANOverlayAdPresentation.h/.mm` | `OverlayAdActivity.java` + `OverlayAdPresentation.java` |
| `RANLayerOrder.h` | (window type + thứ tự add, README Android §4) |

## 2. Quy ước riêng iOS

- **Đơn vị**: Unity đưa px màn hình qua bridge; iOS chia cho
  `UIScreen.nativeScale` thành point **một lần duy nhất tại biên**
  (`RANNativeAdBridge.mm`, `RANPointsFromPixels`). Nội bộ mọi thứ tính bằng
  point — 1pt đứng đúng chỗ 1dp của luật chung (`Dp(120)` → `120pt`); các sàn
  viết bằng px trong luật chung (`kRANMinAttributionSizePx` 15) đi qua
  `ran_ptFromPx:`. Dòng log vẫn in cả px lẫn "dp" với `density = nativeScale`
  để đọc chung một cách với Android. Point chia hết nên iOS không có "pixel
  thừa" để phát trong ngân sách side-media (tài liệu core B8).
- **Thread**: lệnh từ Unity hop về `dispatch_get_main_queue()`
  (`+runOnMainThread:`). Callback về C# rơi trên thread SDK chọn; core C#
  không marshal (tài liệu core B1). Bảng callback
  (`RANNativeAdListenerCallbacks`) được đọc trên thread SDK và ghi từ thread
  Unity → swap dưới lock để không ai thấy nửa bản.
- **Measure**: layout engine chung được viết theo mô hình measure-spec
  (UNSPECIFIED / AT_MOST / EXACTLY, weight, wrap/match); `RANMeasureLayout`
  là hiện thực của mô hình đó trên UIKit (`RANLinearLayoutView`,
  `RANFrameLayoutView`, `ran_layoutWidth/Height/Gravity/Margins/Padding`) —
  kể cả quy tắc "weight + height 0 trong UNSPECIFIED đọc thành WRAP_CONTENT"
  mà icon filler phụ thuộc. Sửa engine thì sửa cả `RANRectAdLayoutEngine.mm`
  và `RectAdLayoutEngine.java`; đừng "tối ưu" model đo cho giống UIKit hơn.
- Method riêng tư prefix `ran_`, hằng `kRO*`; tham số nhiều dòng dấu phẩy đầu
  dòng như hai bên kia.
- **`@try`/`@catch`/`@throw`/`@finally` chỉ biên dịch được trong file có cờ
  `-fobjc-exceptions`**, và Unity truyền cờ theo từng file từ `CompileFlags`
  trong `.meta`. Hiện chỉ `RANRectAdPresentation.mm` có cờ này (nó bắt
  exception do factory ném khi bind). File khác cần xử lý lỗi thì kiểm điều
  kiện trước thay vì `@try`; nếu thật sự cần `@try` thì thêm cờ vào `.meta`
  của file đó. `[NSException raise:…]` là lời gọi method nên không cần cờ.
  Một `@try` thiếu cờ đã làm vỡ lần build Mac 2026-09-21; cổng iOS giờ kiểm
  điều này.

## 3. Khác biệt SDK phải nhớ

- `GADAdValue.value` là **đơn vị tiền tệ**, không phải micros (không chia
  1e6, nếu không doanh thu co 10^6 lần).
- No-fill là `GADErrorNoFill = 1` (Android là 3); C# chỉ phân biệt 0/khác 0.
- Asset view **không nhận chạm**: `RANAdTextLabel`, `RANNativeAdStarRatingView`
  và nút CTA đặt `userInteractionEnabled = NO`, để chạm rơi xuống
  `GADNativeAdView` và SDK báo click. Android thì CTA là `Button` nhận chạm như
  thường. Asset view nào nhận chạm thì SDK iOS tự tắt và log "User interactions
  must be disabled on the asset view to enable click handling".
- Paid-event source lấy từ
  `responseInfo.loadedAdNetworkResponseInfo.adNetworkClassName`.
- `GADNativeAd` **không có** `performClickOnAssetWithKey:` (đó là của
  `GADCustomNativeAd`) → cú chạm close của RedirectOnClose được thả xuyên
  xuống `GADNativeAdView` từ `hitTest:` — hook duy nhất chạy lúc touch-down và
  được phép từ chối target. Mất foreground đọc từ
  `UIApplicationWillResignActive` (`onPaused` của presentation); countdown
  giữ thời gian còn lại qua resign-active.
- **AdChoices của ô hình chữ nhật.** Đo trên iPhone (2026-09-21, GMA 13.9.0):
  - SDK tạo `GADNativeAdAttributionView` làm **con trực tiếp** của
    `GADNativeAdView`, ghim bằng Auto Layout vào **mép** của nó:
    `V:|-(0)-[GADNativeAdAttributionView]` và
    `GADNativeAdAttributionView.right == GADNativeAdView.right` — inset 0,
    `translatesAutoresizingMaskIntoConstraints = NO`. Cung bo do
    `RANRectAdPresentation` cắt (`clipsToBounds`, `cornerRadius`), nên dấu
    hiện trong view này sẽ bị cắt như Android trước đây.
  - SDK còn thêm `GADOverlayView` (ẩn) và một `UIView` trơn, cả hai phủ kín
    `GADNativeAdView` bằng constraint center/width/height.
  - Header SDK **hứa rõ**: gán `GADAdChoicesView` vào
    `GADNativeAdView.adChoicesView` trước `setNativeAd:` thì AdChoices "will
    render inside" view đó. Android không có câu hứa này và trên máy đã không
    làm vậy. Khi gán, SDK có nhận view (log: nó tắt `userInteractionEnabled`
    trên view đó như mọi asset view) nhưng **vẫn tạo** attribution view riêng;
    vì test ad không có AdChoices nên chưa biết nội dung thật đi vào đâu.
  - **Cách Android không chép sang được**: attribution view ghim vào mép
    `GADNativeAdView`, không phải con MATCH_PARENT tôn trọng padding. Thu nhỏ
    `GADNativeAdView` rồi cho nội dung tràn ra thì MediaView phủ cả ô của
    template scrim sẽ nằm ngoài nó — SDK coi đó là lỗi tích hợp ("Not all
    asset views lie inside the native ad view") — và chạm ở viền sẽ không tới
    được ad. Đừng làm.
  - **Cách đang dùng** — `RANRectAdViewFactory
    insetSDKAdChoicesInNativeAdView:logResult:`, gọi cuối mỗi
    `ran_layoutBoundContent` (không log) và một lần lúc layout ready (có log):
    tìm subview class `GADNativeAdAttributionView`, tìm hai constraint trên
    `GADNativeAdView` ghim nó `top == top`, `right/trailing == right/trailing`
    (hệ số 1, quan hệ bằng, viết xuôi hay ngược đều nhận), rồi đổi hằng số
    thành ±`ran_badgeEdgeInset` — đúng giá trị badge "Ad" dùng, nên hai dấu góc
    luôn thụt bằng nhau. Chỉ gán khi khác; khung giữ nguyên cỡ.
  - **Không ép gì khi SDK khác đi**: không thấy class, không thấy đủ hai
    constraint, hay hằng số không phải 0/giá trị mình đặt → để nguyên (dấu về
    góc như trước) và log một lần mỗi lần chạy app, dạng
    `RectAd: Rect ad AdChoices inset SKIPPED (GMA x.y.z): <thấy gì> - update
    insetSDKAdChoicesInNativeAdView: for this SDK`. Thành công: `... inset
    applied (GMA x.y.z): SDK container inset by 3pt`. SDK tự đặt lại hằng số
    về 0 sau khi mình sửa: log `RESET` rồi sửa lại. Phiên bản GMA đọc qua
    runtime (`valueForKey:@"versionNumber"`), không gọi hàm SDK theo tên —
    file này chỉ biên dịch trên Mac, đoán sai tên hàm là vỡ build.
  - **Vòng layout của pack bỏ qua view do Auto Layout quản**
    (`translatesAutoresizingMaskIntoConstraints == NO`): không bỏ qua thì
    `ran_layoutBoundContent` gán frame cả ô cho khung AdChoices của SDK, kéo
    nó ra phủ cả ô. Mọi view của pack đều đặt bằng frame (không chỗ nào tắt
    tamic), nên chỉ view của SDK bị bỏ qua.
  - Đã chạy trên iPhone 14 Pro với test ad (2026-09-21): log `In-feed
    AdChoices inset applied (GMA 13.9.0): SDK container inset by 3pt` (log nay
    ghi `Rect ad AdChoices…`), không có `RESET`. Chưa thấy dấu thật vì chưa có
    ad unit iOS thật.
- Callback của SDK khác (vd. MAX với `InvokeEventsOnUnityMainThread =
  false`) có thể bắn trên một `NSOperationQueue` nền, không phải main queue —
  iOS **không** được miễn luật "không đụng API Unity trong callback".

## 4. Xếp lớp bằng bậc trong `layer.name`

iOS không có Activity, không có window type; `UIWindow` chỉ có `windowLevel`,
và `zPosition` thì đổi thứ tự vẽ nhưng **không** đổi thứ tự nhận chạm
(`hitTest:` duyệt `subviews` từ cuối về đầu), nên hai thứ sẽ lệch nhau. Thứ
duy nhất vừa đúng khi vẽ vừa đúng khi chạm là **vị trí trong mảng
`subviews`** — cuối mảng là trên cùng.

Ô hình chữ nhật và ad half là **subview** của `rootVC.view` (chính là
`UnityView`; game nằm ở lớp *cha*, không phải anh em, nên subview thấp nhất
vẫn nằm trên game). Package khác cũng đặt lớp của họ vào đó (màn che,
banner…), và không package nào được biết view của package nào. Vị trí vì thế
quyết bằng **bậc** theo bảng ở [README gốc](../../../README.md#thứ-tự-lớp):

- Mỗi view đặt `layer.name = @"riseon.layer.<bậc>"`, rồi chèn ngay **dưới**
  subview đầu tiên (đếm từ dưới lên) có bậc **cao hơn**; không có thì
  `addSubview:`. View không đánh dấu là bậc `0`. Cùng bậc thì cái đến sau nằm
  trên. Hàm: `RANInsertSubviewAtTier` trong `RANLayerOrder.h` (inline, header
  riêng của pack — mỗi package tự viết bản của mình, không gọi chéo).
- Ô hình chữ nhật `kRANRectAdLayerTier` = `-1000` (dưới cả view không đánh
  dấu); ad half `kRANHalfScreenAdLayerTier` = `2000`.
- Thứ tự đến không quan trọng: lớp bậc thấp tới sau vẫn chui xuống dưới.
  Giới hạn: một view **không** theo quy ước mà `addSubview:` muộn sẽ nằm
  trên cả những lớp bậc dương đã có — quy ước chỉ giữ được giữa những ai tuân
  theo.

Ad full-screen là thứ duy nhất pack `present`, và present thẳng từ Unity root
VC — đúng cách GMA present ad toàn màn của họ. Một VC được present thì luôn vẽ
trên **toàn bộ** subview của VC present nó, nên nó không có bậc và tự đứng
trên mọi lớp kia.

### Trượt và chạm

Panel half trượt bằng `transform` của content view (`UIView` animation 0.25s,
ease-out vào, ease-in ra, `BeginFromCurrentState` để quay đầu giữa chừng);
frame không đổi. Trong lúc trượt **chạm phải bị nuốt**, và cả hai cách hiển
nhiên đều rò xuống game: UIKit bỏ view đang animate ra khỏi hit-test, và
`touchesBegan:` mặc định của `UIView` chuyển chạm lên responder chain — từ đây
là `UnityView`. Nên trong lúc trượt có một `RANTouchShieldView` trong suốt, đúng
khung nằm yên của panel, cùng bậc, ngay trên content view, với bốn phương thức
`touches…` để trống. `onShow` (displayed) và `onPresented` (đồng hồ) chạy khi
trượt vào xong; trượt ra xong mới `ran_dismissWithNotify:`.

## 5. Pause: `UnityPause` như mọi SDK

Không có Activity nên ad full tự gọi `UnityPause(1)` ngay trước khi present và
`UnityPause(0)` khi dismiss, chỉ khi chính presentation đó đã pause
(`_askedForPause`; ad half đóng không đụng cờ). `UnityPause` là **cờ**, không
phải bộ đếm: ad đóng là game chạy lại, kể cả khi thứ khác (màn che của package
khác, SDK khác) đang muốn game dừng. Pack không canh chuyện đó — ai cần giữ
pause xuyên qua ad thì tự canh (vd. một `CADisplayLink` đọc `UnityIsPaused()`
mỗi frame và đặt lại).

Callback của pack về C# qua trampoline từ main queue, vẫn chạy khi Unity đang
pause (tài liệu core B1, B4).

## 6. ABI với C#

- `RANNativeAdBridge.h` và `NativeAdBridge.cs` là **một hợp đồng hai bản
  chép**: cùng tên hàm, cùng thứ tự tham số, cùng ngữ nghĩa với bề mặt Java
  mà C# đã dùng — để tầng C# là một đường code với hai transport. Đổi chữ ký
  một bên là đổi cả ba nơi (`.h`, `.mm`, `.cs`); gate `balance.py` kiểm
  `RANRectAdCreate` / `RANRectAdConfigure` khớp giữa ba file.
- Callback về C# là **trampoline static** `[MonoPInvokeCallback]` (yêu cầu
  IL2CPP), tra client theo `instanceID` mà C# chọn và native echo lại; late
  callback không khớp generation thì rơi. `bool` marshal `[MarshalAs(I1)]`;
  chuỗi UTF-8 chỉ sống trong thời gian callback — copy nếu cần giữ.
- Handle: `Create` trả handle đã retain; `Release` vừa tháo ad vừa balance
  retain; không lệnh nào hợp lệ sau đó.
- Comment đầu `RANNativeAdBridge.h` còn nói "C# proxies marshal qua
  `MobileAdsEventExecutor`" — **đã cũ**: core C# không marshal (tài liệu core
  B1). Sửa dòng đó khi chạm file.

## 7. Kiểm chứng

**Build sạch lần đầu ngày 2026-09-21** trên máy Mac của chủ dự án: Xcode 26.6,
iPhoneOS SDK 26.5, Google-Mobile-Ads-SDK 13.9.0 (CocoaPods), Unity
6000.3.19f1, chạy trên iPhone 14 Pro (scale 3). 0 lỗi; một warning có sẵn
(`contentEdgeInsets` deprecated từ iOS 15, trong `RANRectAdViewFactory.mm`).
Ô in-feed đã hiện và layout đúng với test ad.

Máy Windows vẫn không compile được — cổng local chỉ kiểm ngoặc `{}()[]` cân
sau khi bỏ comment và chuỗi, chữ ký ABI khớp ba nơi, và cờ
`-fobjc-exceptions` có mặt ở file dùng `@try` (§2). Sửa code iOS xong thì vẫn
phải build trên Mac mới biết.

**Cách đọc cây view trên iPhone** (đã dùng ngày 2026-09-21): chèn tạm một
hàm dump đệ quy vào **bản copy** `RANRectAdViewFactory.mm` trong project Xcode
đã export (Unity copy plugin chứ không symlink — không đụng repo), gọi sau
`nativeAdView.nativeAd = _nativeAd;` bằng `dispatch_after` 3/8/15/30s, in class,
`frame`, rect trong window, `hidden`, `autoresizingMask`,
`translatesAutoresizingMaskIntoConstraints`, `clipsToBounds`, `cornerRadius`,
và **mọi constraint mỗi view đang giữ**; lọc console theo một tiền tố. Chụp
ảnh bằng `drawViewHierarchyInRect:` vào `tmp/` của app.

**Test ad trên iOS không mang AdChoices** (unit test `3986624511`: container
AdChoices của SDK luôn `hidden`, 0×0, rỗng). Mọi thứ liên quan AdChoices phải
kiểm bằng ad thật.
