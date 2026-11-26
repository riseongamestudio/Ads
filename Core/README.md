# RiseOn.Ads

Quảng cáo tự khởi động khi mở game theo thiết lập trong Project Settings: mediation
khởi động khi luồng consent cho phép quảng cáo, danh sách ưu tiên theo định dạng
(tại chỗ hoặc từ Remote Config), ad full-screen dưới màn che native, chuỗi
collapsible. Lõi là lớp static `AdsSdk`, như API của một SDK mediation, không có luật
riêng; luật của game (remove-ads, giãn cách, end card, ad khi quay lại game) nằm
trong lớp manager duy nhất của game, field của nó chỉnh trên trang settings. Pack có
sample `AdsManager` chứa các luật hay gặp.

Package `com.riseon.ads`, namespace `RiseOn.Ads`.

## Mục lục

- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Tổng quan](#tổng-quan)
- [`AdsSdk`](#adssdk)
- [Cài đặt của project](#cài-đặt-của-project)
- [Lớp manager](#lớp-manager)
- [Sample `AdsManager`](#sample-adsmanager)
- [Trong Editor](#trong-editor)
- [Hướng dẫn nhanh](#hướng-dẫn-nhanh)
- [Consent của SDK](#consent-của-sdk)
- [Lịch sử thay đổi](#lịch-sử-thay-đổi)
- [Giấy phép](#giấy-phép)

## Yêu cầu

| Phụ thuộc | Cách có | Dùng cho |
|---|---|---|
| Unity 6000.3 | | Bản đang dùng để phát triển |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Các mục của trang cài đặt |
| `com.riseon.consent` 1.0.2 | Tự cài theo `package.json` | Luồng UMP và ATT mà mediation chờ |
| `com.riseon.utils` 1.0.18 | Tự cài theo `package.json` | Các mục của trang cài đặt |
| `com.riseon.serializables` 1.0.5 | Tự cài theo `package.json` | Thiết lập của từng format, danh sách mediation |
| `com.cysharp.unitask` 2.5.11 | Tự cài theo `package.json`, từ OpenUPM | Các hàm `async` |
| `com.unity.ugui` 2.0.0 | Tự cài theo `package.json` | Màn che và ad mẫu trong Editor, cửa sổ `AdsDebugger` |
| Ít nhất một adapter | `com.riseon.ads.admob`, `com.riseon.ads.max` hoặc `com.riseon.ads.levelplay` | Mediation chạy quảng cáo |
| `com.riseon.firebase.remoteconfig` (tùy chọn) | Cài tay | Mở các ô chọn key Remote Config: ô cuối dòng **Mediations** của mỗi format trong mục Formats, và ô cuối dòng của mỗi field `AdRemoteSetting` của lớp manager (giãn cách, end card... của sample). Không có pack thì các ô đó mờ, ghi **No Remote Config** |

"Tự cài" là Package Manager tự lấy theo version từ Unity Registry và các registry khai
trong manifest, cả khi cài pack bằng git URL. Odin không có trên UPM nên phải cài vào
project trước. Thiếu Odin thì project báo một lỗi từ `RiseOn.Ads.Requirements`.

## Cài đặt

**Registry RiseOn** (khuyên dùng): thêm registry RiseOn với scope `com.riseon`, và
OpenUPM với scope `com.cysharp`, `com.google` (UniTask, External Dependency Manager
của `com.riseon.consent`), vào `Packages/manifest.json`, rồi thêm package và adapter
cần dùng:

```json
{
  "scopedRegistries": [
    {
      "name": "RiseOn",
      "url": "https://upm.riseongamestudio.com",
      "scopes": ["com.riseon"]
    },
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.cysharp", "com.google"]
    }
  ],
  "dependencies": {
    "com.riseon.ads": "2.0.0"
  }
}
```

Mỗi adapter cần thêm registry của SDK nó dùng, xem README của adapter.

**Git URL**: phụ thuộc vẫn lấy theo version từ registry, nên manifest cần OpenUPM như ví
dụ trên; `com.riseon.utils`, `com.riseon.consent` và `com.riseon.serializables` lấy từ
registry RiseOn, hoặc cài bằng git URL trước theo thứ tự này. Rồi *Package Manager → + →
Add package from git URL*:

```
https://github.com/riseongamestudio/Ads.git?path=/Core
```

**Thư mục local**: `"com.riseon.ads": "file:D:/path/to/Ads/Core"`.

## Tổng quan

- **`AdsSdk`**: lớp static, lõi của pack, như API của một SDK mediation. Trước scene
  đầu, pack đọc settings và tạo manager; rồi chốt danh sách ưu tiên của lần mở này,
  chờ luồng consent và khởi động cùng lúc mọi mediation có tên trong các danh sách;
  SDK nào khởi động chậm hay treo không giữ chân SDK khác. `AdsSdk` hiện đúng cái
  được gọi, không có luật riêng (giãn cách, remove-ads, end card... là việc của game,
  xem "Lớp manager"). Chỉ gọi trên thread Unity; mọi sự kiện cũng đến trên thread
  đó.
- **`IsInitialized`, `WaitUntilInitializedAsync()`**: xong khi consent xong và mọi
  mediation đã khởi động hoặc lỗi. Consent chưa cho quảng cáo thì xong ngay sau
  consent, và mediation tự khởi động sau, khi người chơi đổi lựa chọn cho phép (ví dụ
  trong form privacy). `WaitUntilInitializedAsync()` gọi lúc nào cũng được, kể cả
  trước scene đầu. Một SDK treo giữ nó (và `IsInitialized`) chờ mãi.
- **Lớp manager**: mỗi project có tối đa một lớp `partial` cài `IAdsManager`, chỗ
  của luật quảng cáo của game. Field của lớp hiện trong mục Manager của trang; pack
  tạo instance trước scene đầu và chỉ gọi `PauseInEditor` của nó. Pack có sample
  `AdsManager` chứa các luật phần lớn game của studio chạy (xem "Sample
  `AdsManager`"); game chép về rồi sửa.
- **Công cụ cho luật của game**: `AdPacing` (giãn cách một format, field của
  manager), `AdCollapsiblePlan` và `AdCollapsiblePlan.Pick` (cách chạy collapsible,
  bốc theo weight), `AdsSdk.SetSuppressed` (chặn format, như khi người chơi mua
  remove-ads), `AdsSdk.Launches` (lần mở game thứ mấy trên máy),
  `AdsSdk.SecondsSinceDisplay` (số giây từ lần format hiện gần nhất),
  `AdRemoteSetting<T>` (giá trị trên trang, thay được bằng key Remote Config).
- **Mediation**: một SDK quảng cáo, là class C# thuần cài các interface định dạng nó
  phục vụ (`IAdMediationBanner`, `IAdMediationFullScreen`,
  `IAdMediationNativeCollapsible`...). Mọi class mediation trong project tự có một mục
  trong danh sách Mediations của trang.
- **`AdFullScreenFormat`**: các format full-screen, cùng giá trị với `AdFormat`; hàm
  chỉ phục vụ ad full-screen (`AdChain.Then`, `AdsSdk.Show`, `AdsSdk.IsReady`, sự kiện
  `Displayed`) nhận kiểu này, nên không truyền nhầm banner được; collapsible có hàm
  riêng (`AdsSdk.IsCollapsibleReady`...). `ToFormat()` đổi sang `AdFormat`; với một
  `AdFormat`, `IsFullScreen()` kiểm tra nó có full-screen không, còn `ToFullScreen()`
  đổi sang `AdFullScreenFormat?`, `null` khi không full-screen.
- **Danh sách ưu tiên**: mỗi định dạng có một danh sách id mediation, tốt nhất
  trước, chung cho Android và iOS, hoặc tách riêng từng nền tảng khi mỗi nền tảng
  dùng mediation khác. Danh sách trống là tắt định dạng đó. Thứ tự chốt trước khi
  mediation khởi động và giữ suốt lần mở; mỗi mediation chỉ load những định dạng có
  tên nó. Mediation không có tên trong danh sách nào thì không khởi động. Ad
  full-screen thử lần lượt và lấy mediation đầu tiên có ad sẵn; banner, MRec và ô
  native lấy mediation đầu tiên đã khởi động, nên mediation không có unit id của một
  nền tảng thì không đứng trong danh sách của nền tảng đó.
- **Thiết lập định dạng**: cách định dạng hiện (vị trí và kiểu banner, vị trí MRec,
  màu nền và nút đóng của native...) đặt một lần trên trang, chung cho cả hai nền
  tảng; mediation nào phục vụ định dạng đó cũng làm theo.
- **Key Remote Config**: định dạng nào chọn key Remote Config ở ô cuối dòng
  **Mediations** của nó (xem dưới) thì đọc danh sách từ key đó, thay cho danh sách
  trên trang, trước khi mediation khởi động.
- **Consent**: pack [`com.riseon.consent`](https://github.com/riseongamestudio/Consent)
  tự chạy luồng UMP + ATT khi mở game. Pack trao `ConsentState` cho từng mediation
  trước khi khởi động, và trao lại khi người chơi đổi lựa chọn trong form privacy.
- **`AdChain`**: một hay nhiều ad full-screen nối nhau (interstitial rồi end card...).
  Màn che native full-screen nằm dưới mọi ad của chuỗi, màu chỉnh trên trang: chỗ ad
  không phủ (lề tai thỏ...) và khoảng giữa hai ad đều là màu đó, game không lộ ra, và
  game dừng tới khi ad cuối đóng. Riêng ad mà SDK báo đóng qua player loop của Unity
  (rewarded của AdMob trên iOS) thì không có màn che và không có bước sau (xem
  [Chain](Runtime/Chain/README.md)).
- **`AdCollapsibleChain`**: các native collapsible nối nhau; ad đầu trượt vào, ad
  cuối trượt ra, giữa các ad không trượt. Màn che half-screen chỉ hiện dưới ad chưa
  phải cuối. `AdsSdk.ShowCollapsibleAsync(plan)` dựng chuỗi theo các bước của một
  plan.
- **`NativeRectHandle`**: mỗi chỗ trong game hiện ô quảng cáo native gọi một lần
  `AdsSdk.CreateNativeMRec(sizePx, roundCornerPx)` (một khối native MRec, như chỗ MRec
  trên một màn) hay `AdsSdk.CreateNativeInFeed(sizePx, roundCornerPx)` (một dòng
  trong danh sách, một ô trong lưới), được một ô có cỡ đó; mỗi format có unit id và
  **Cache Size** riêng. Game đặt chỗ ô bằng `SetPosition(positionPx)` (pixel, tính từ
  góc trên trái màn hình), bật bằng `Show(onDisplayed)`, tắt bằng `Hide()`, bỏ ô bằng
  `Dispose()`. Game chỉ bật và tắt: ô bật lúc chưa có ad thì hiện ngay khi có ad, ô
  đang hiện tự đổi sang ad mới. `onDisplayed` đến khi ad đầu tiên của lần bật đó lên
  màn hình. Ô tắt không giữ ad chưa hiện nào: ad đó về lại kho cho ô khác dùng. Kho
  giữ sẵn số ad đặt ở ô **Cache Size** của format trên trang (kho chung hay riêng
  giữa các format tùy mediation, xem README của nó), nên màn nào hiện nhiều ô cùng
  lúc thì tăng số đó. Tạo ô trước khi mediation phục vụ format khởi động xong thì ô
  nhận khối khi có, giữ chỗ và trạng thái bật mà game đã đặt. Định dạng tắt hay
  mediation thiếu unit id thì handle rỗng: mọi lời gọi không làm gì và `HasCell` là
  `false`, nên game chỉ chừa chỗ cho ô quảng cáo khi `HasCell` là `true`.
- **`AdsDebugger`**: cửa sổ debug cho bản build test, game mở bằng
  `AdsDebugger.Show()` từ menu cheat. Cửa sổ hiện advertising ID của máy kèm nút
  copy, để đăng ký máy làm test device trên AdMob và LevelPlay (MAX bật test ads
  trong Mediation Debugger), và một nút mở công cụ debug của mỗi mediation đang chạy
  (Ad Inspector, Mediation Debugger, Test Suite).
- **Trong Editor**: Play mode không gọi SDK cho banner, MRec và ad full-screen; pack
  vẽ ad giả ghi tên mediation sẽ phục vụ nó (xem dưới). Native vẫn dùng bản xem
  trước của mediation.

Chi tiết từng khái niệm trong README của thư mục:
[Chain](Runtime/Chain/README.md),
[Cover](Runtime/Cover/README.md) (thứ tự lớp, pause),
[Debugger](Runtime/Debugger/README.md),
[Mediations](Runtime/Mediations/README.md).

## `AdsSdk`

| Lời gọi | Làm gì |
|---|---|
| `ShowInterstitialAsync(placement)`, `ShowAppOpenAsync`, `ShowNativeInterstitialAsync`, `ShowNativeAppOpenAsync`, `ShowNativeEndCardAsync` | Hiện một ad của format; trả `true` khi ad đã hiện |
| `ShowRewardedAsync(placement)`, `ShowRewardedInterstitialAsync` | Như trên, trả `true` khi người chơi nhận thưởng |
| `Show(format, placement, onDisplayed)` | Một ad full-screen của format chỉ biết lúc chạy; trả `AdChainResult` |
| `IsReady(format)`, `IsCollapsibleReady` | Một mediation đã khởi động trong danh sách của format có ad sẵn, và format không bị chặn |
| `Chain()`, `CollapsibleChain()` | Chuỗi ad full-screen, chuỗi collapsible (xem [Chain](Runtime/Chain/README.md)) |
| `ShowCollapsibleAsync(plan, onDisplayed)` | Các bước của plan nối nhau; plan `null` (như khi `AdCollapsiblePlan.Pick` không bốc được plan nào) thì không hiện. `onDisplayed` đến một lần, khi ad đầu hiện; trả số ad đã hiện |
| `HideCollapsible()` | Trượt collapsible đang hiện ra và kết thúc chuỗi của nó |
| `ShowBanner()`, `HideBanner()`, `ShowMRec()`, `HideMRec()` | Banner, MRec ở chỗ chọn trong mục Formats; đã hiện thì ở đó tới khi gọi `Hide…`. Gọi lúc chưa mediation nào khởi động xong (màn đầu game) thì hiện khi mediation đầu tiên phục vụ format khởi động xong; mediation đứng trước nó trong danh sách khởi động xong sau thì chuyển sang mediation đó |
| `CreateNativeMRec(sizePx, roundCornerPx)`, `CreateNativeInFeed(sizePx, roundCornerPx)` | Ô native, xem `NativeRectHandle` ở trên |
| `SetSuppressed(format, on)`, `IsSuppressed(format)` | Chặn hay thả một format. Format full-screen bị chặn thì bước của nó bị bỏ khỏi mọi chuỗi, format có thưởng thì coi như người chơi đã nhận thưởng; banner, MRec, collapsible và ô native rời màn hình, và hiện lại những gì game đã yêu cầu khi thả. Không lưu: game đặt lại từ save mỗi lần mở, trước ad của scene đầu; gọi được trước khi pack chạy |
| `Serves(format)` | Danh sách ưu tiên của format có ít nhất một mediation trên nền tảng này (danh sách trên trang, rồi danh sách từ key Remote Config sau khi Remote Config xong); mediation đã khởi động chưa thì không tính. Cho lựa chọn của game, như ẩn nút của format không mediation nào phục vụ |
| `Launches` | Lần mở game này trên máy, 1 là lần đầu; pack tự đếm |
| `SecondsSinceDisplay(format)` | Số giây từ lần format hiện gần nhất, từ mọi chuỗi; với Native Collapsible là từ ad đầu của chuỗi gần nhất; `null` khi chưa hiện lần nào |
| `IsShowingFullScreen` | Đang có ad hay chuỗi full-screen, hoặc vừa xong chưa tới một giây (lần resume nó gây ra không phải của người chơi) |

| Sự kiện | Khi nào |
|---|---|
| `Paid` | Doanh thu từng impression, từ mọi mediation và format, kèm `Placement`. Doanh thu đã tự gửi tới analytics khi project cài `com.riseon.analytics` và **Ad Revenue from Ads** trên trang Analytics bật (mặc định) |
| `Displayed` | Một ad full-screen lên màn hình, từ mọi chuỗi, kèm placement truyền vào lần show |
| `CollapsibleDisplayed` | Một ad collapsible trượt vào hay thay chỗ, từ mọi chuỗi |
| `Loaded`, `LoadFailed` | Một mediation (theo id) load xong hay lỗi một ad của format |
| `PlayerLeft`, `PlayerReturned` | Người chơi rời game và quay lại, kèm số giây ở ngoài; ad full-screen của pack chiếm màn hình, hay lúc cửa sổ `AdsDebugger` đang mở, thì không tính. `PlayerReturned` chỉ đến sau `IsInitialized` |

Placement là chuỗi game đặt cho chỗ hiện ad (như `revive`, `level_end`); nó đi tới
sự kiện `Displayed`, tới `AdImpression.Placement` của `Paid`, và tới SDK nhận
placement khi show (MAX).

## Cài đặt của project

Dev mở bằng menu **RiseOn > Ads**, hoặc Edit > Project Settings > RiseOn > Ads. Trang
này lưu vào `Assets/RiseOn/Resources/RiseOnAdsSettings.asset`: toggle và lựa chọn lưu
ngay, ô chữ lưu khi gõ xong; Ctrl+Z hoàn tác như mọi chỗ khác của Unity.
Inspector của asset chỉ có nút mở trang này.

| Mục | Ý nghĩa |
|---|---|
| Display | **Cover**: màu màn che dưới ad full-screen và dưới collapsible giữa chuỗi. **Editor Sorting Order**: sorting order thấp nhất của các lớp ad trong Editor; các lớp xếp lên từ đó (xem dưới). Canvas của game phải thấp hơn số này |
| Manager | Lớp manager của project: tên lớp trên thanh (chỉ đọc). Field của lớp hiện bên trong; lớp không có field nào thì thanh không gập mở |
| Mediations | Các SDK quảng cáo. Mỗi class mediation trong project, cả adapter của pack lẫn class game tự viết, tự có một dòng gập mở; trang tự thêm khi class mới compile, không có bước thêm vào danh sách. Mediation không format nào dùng thì không khởi động. Alt+click mở hay gập cả nhánh. Bên trong, trên cùng là thiết lập của mediation (như App Key của LevelPlay) và của SDK, phần của SDK đọc và ghi thẳng vào file settings của chính SDK (xem README của từng mediation); dưới đó là mục con **Networks**, gập sẵn: các network mà SDK của mediation mediate, phiên bản adapter của từng network trong project, và cài, cập nhật, gỡ adapter theo đúng cách của SDK đó (LevelPlay chỉ có nút mở Network Manager của nó; xem mục Network trong README của từng adapter); dưới cùng là mục con **Formats** của mediation, mỗi format một mục con, unit id Android và iOS nằm ngay trên thanh của mục con; format có thêm thiết lập (như **Cache Size** của format native AdMob) thì gập mở được, và format không có thì đứng trước. Adapter của pack (AdMob, MAX, LevelPlay) có dòng dù chưa cài, như network trong mục **Networks**: cột giá trị ghi phiên bản trong project (chưa cài thì **Not installed**), kèm nút cuối dòng. **Install** thêm package của adapter qua Package Manager. Core cài từ thư mục local thì lấy từ thư mục cùng tên adapter cạnh `Core`; thiếu thư mục đó thì dòng không có **Install**, tooltip ghi đường dẫn thiếu. Core cài từ registry thì **Install** hỏi phiên bản, liệt kê các phiên bản registry có, mới nhất trước; danh sách chưa đọc xong (đang đọc, registry lỗi) thì **Install** cài thẳng bản mới nhất. Đã cài từ registry thì bấm vào phiên bản để chọn phiên bản khác, cũ hơn cũng được; có phiên bản mới hơn thì cột ghi `cũ → mới` kèm nút **Update**, và **Update All** trên thanh cập nhật mọi adapter cũ. Danh sách phiên bản đọc từ registry lần đầu mở trang trong mỗi phiên Editor; nút làm mới trên thanh đọc lại. **Uninstall** (khi manifest có tên adapter) hỏi lại một lần, kèm danh sách format đang dùng mediation, bỏ mediation khỏi danh sách và khỏi mọi format, rồi gỡ package. Unit id và thiết lập format của nó trong asset của pack mất theo; file settings của SDK trong `Assets` giữ nguyên, và package SDK chỉ được gỡ theo khi manifest không ghi nó trực tiếp. Cài, cập nhật, gỡ lỗi (ví dụ manifest thiếu scoped registry của SDK, xem README của adapter) thì Console báo `Installing … failed: …` (hay `Updating`, `Uninstalling`). Từ lúc bấm tới khi Package Manager xong, đầu danh sách có dòng quay ghi việc đang chạy, như **Uninstalling LevelPlay**, và mọi nút cài, gỡ trên trang mờ đi, vì Package Manager resolve lại mọi package rồi Unity mới compile |
| Formats | Mỗi format một mục con gập mở, theo thứ tự Banner, MRec, Interstitial, Rewarded, Rewarded Interstitial, App Open, rồi các format native, gập sẵn. Trong mỗi mục, dòng **Mediations** là các mediation phục vụ format, thử từ trái sang phải; hàng hẹp thì ô xuống dòng như chữ, thử tiếp từ trên xuống dưới, mũi tên chỉ nằm giữa hai ô cùng dòng. Kéo một ô để đổi thứ tự, **×** trên ô bỏ mediation khỏi format, **+** thêm một mediation của trang mà format chưa có và phục vụ được format đó (format native chỉ AdMob; App Open có AdMob, MAX; Rewarded Interstitial chỉ AdMob); không mediation nào thì format ghi **Off** và tắt. Nút ở cuối dòng **Mediations** tách danh sách cho format mà mỗi nền tảng dùng mediation khác: bấm thì dòng thành hai dòng **Android** và **iOS**, iOS bắt đầu bằng danh sách đang có; nút ở cuối dòng tên gộp lại, giữ danh sách Android. Ô ngay trước nút ở cuối dòng **Mediations** (khi đã tách là dòng tên) chọn một key kiểu chuỗi của `com.riseon.firebase.remoteconfig` cho danh sách mediation (khai key ở trang **RiseOn > Firebase > Remote Config**), **No Remote Key** là không dùng key; project không có pack đó thì ô mờ, ghi **No Remote Config**. Một key dùng cho cả hai nền tảng; muốn mỗi nền tảng một giá trị thì đặt condition theo nền tảng trên console. Giá trị là id mediation cách nhau bằng dấu phẩy, tốt nhất trước, ví dụ `max,admob`; `off` tắt format; giá trị rỗng thì dùng danh sách của dòng **Mediations**. Id không có trong Mediations bị bỏ, kèm cảnh báo trong Console; id nào cũng bị bỏ thì format tắt, như `off`. Có ít nhất một format chọn key thì mediation khởi động sau khi Remote Config xong lần mở game, theo giá trị nó có lúc đó. Cuối mục là cách hiện của format, mediation nào phục vụ format cũng làm theo, chung cho cả hai nền tảng: Banner có **Position** (bốn góc, giữa trên, giữa dưới, chính giữa vùng an toàn) và **Adaptive** (bật thì rộng hết màn hình, cao theo banner adaptive của mạng; tắt thì 320×50); MRec có **Position**; Native Interstitial, Native App Open, Native End Card có **Background Color** (màu chỗ creative không phủ) và **Close** (số giây chờ, cạnh đặt nút đóng, chỗ đồng hồ, bấm đóng thì mở quảng cáo); Native Collapsible có **Height Ratio** (phần chiều cao màn hình panel chiếm, từ đáy) và **Background Color**; Native MRec, Native In-Feed có **Background Color** (trong suốt thì chỗ game chừa sẵn lộ ra). |

Trang báo đỏ và build dừng khi:

| Trường hợp | Dev làm gì |
|---|---|
| Asset nằm ngoài thư mục Resources | Bấm **Move it** |
| Có hơn một lớp cài `IAdsManager` | Giữ một lớp |
| Lớp manager không có constructor rỗng | Thêm constructor rỗng |
| Lớp manager nằm trong assembly chỉ cho Editor: Play mode chạy được, bản build không có | Chuyển lớp sang assembly runtime |
| Settings chưa giữ lớp manager, thường khi lớp ra đời lúc script đang lỗi compile | Bấm **Take it** |
| Lớp mà settings giữ (mediation, hoặc manager cũ) đã đổi tên hay bị xóa | Bấm **Clear it** |
| Hai mediation cùng id, thường do merge file settings | Bấm **Remove extras** |
| Key Remote Config của một format không còn là key kiểu chuỗi của Remote Config (bị xóa hay đổi kiểu); ô của nó ghi **Missing** | Chọn key khác hoặc **No Remote Key** |
| Key Remote Config của một field `AdRemoteSetting` trong lớp manager (First After, Min Interval của `AdPacing`, Weight của plan...) không còn là key cùng kiểu trong Remote Config (bị xóa hay đổi kiểu); ô của nó ghi **Missing** | Chọn key khác hoặc **No Remote Key** |

Không có lớp cài `IAdsManager` thì trang báo vàng, kèm nút **New script**: pack hỏi
chỗ lưu và viết khung lớp. Muốn có sẵn các luật hay gặp thì import sample
`AdsManager` thay cho bước này.

### Unit test của mạng

Mạng nào công bố unit test (hiện có AdMob) thì mỗi ô unit id của nó có nút **Test**
liền bên phải, riêng cho Android và cho iOS. Nút bật thì nền tảng đó lấy quảng cáo
test từ unit test của mạng cho đúng format; id đã điền vẫn nằm nguyên trong ô và dùng
lại khi tắt nút. MAX và LevelPlay không công bố unit test: dùng unit thật và đăng ký
máy làm test device (xem [Debugger](Runtime/Debugger/README.md)).

Còn ô nào bật Test thì trang báo vàng và lúc build Console in cảnh báo, kèm tên từng
ô và nền tảng. Bản build đó không có doanh thu từ các ô này, nên tắt nút trước khi
build bản store. Mediation tự viết khai unit test bằng `[AdUnitTest(android, ios)]`
trên field `AdUnitId`.

## Lớp manager

Luật quảng cáo của game (khi nào hiện, giãn cách, remove-ads, end card...) nằm trong
lớp manager của game, gọi `AdsSdk` bên dưới. Lớp là `partial`, cài `IAdsManager`,
không generic, không lồng trong lớp khác; mỗi project có tối đa một lớp như vậy.

Source generator của pack sinh thêm vào lớp lúc compile, không ra file:

- `sealed`, để không lớp nào kế thừa nó thành manager thứ hai.
- `Ins`: instance pack đang chạy. Đọc trước scene đầu, hoặc khi pack không chạy được
  manager nào (Console có lý do), thì ném `InvalidOperationException`.
- `[Serializable]`, nếu lớp chưa tự gắn.
- Lệnh đăng ký với pack, để IL2CPP không cắt lớp.

Compile báo lỗi `RISEONADS001` khi lớp không `partial`, generic hoặc lồng trong lớp
khác, và `RISEONADS003` khi có hơn một lớp cài `IAdsManager`.

`IAdsManager` có một member, `protected internal`, nên lớp cài nó tường minh
(`void IAdsManager.PauseInEditor(bool paused)`) và chỉ pack gọi được:

| Member | Khi nào |
|---|---|
| `PauseInEditor(paused)` | Trong Editor không SDK nào dừng Unity dưới ad full-screen, nên pack gọi hàm này: `true` khi chuỗi bắt đầu hiện ad đầu, `false` khi ad cuối đóng; game không có manager thì pack dùng `EditorGamePause.Apply`. `EditorGamePause.Apply` đưa `Time.timeScale` về 0 và dừng âm thanh, rồi trả lại như cũ; game viết khác khi với nó "dừng" là việc khác (ví dụ game tự đếm giờ bằng thời gian thật) |

Field của lớp hiện trong mục Manager của trang và lưu trong asset của pack. Field kiểu
`AdRemoteSetting<float>` hay `AdRemoteSetting<bool>`, kể cả bên trong `AdPacing` và
`AdCollapsiblePlan`, có ô chọn key Remote Config ở cuối dòng. Project không có
`com.riseon.firebase.remoteconfig` thì ô đó mờ, ghi **No Remote Config**, và lúc chạy
`Value` là giá trị trên trang. Có pack đó thì **No Remote Key** là dùng giá trị trên trang;
chọn một key cùng kiểu (float cho số, bool cho bật/tắt) thì lúc chạy
`Value` là giá trị của key, ô giá trị trên trang mờ đi; trước khi Remote Config xong
là giá trị mặc định khai trên trang Remote Config.

| Công cụ | Làm gì |
|---|---|
| `AdPacing` | Field giãn cách một format, trang vẽ mỗi `AdPacing` một mục gập mở mang nhãn của nó (tên field, hay tên key khi là giá trị của một `EnumMap`): **First After (s)** (số giây từ lúc mở game tới lần hiện đầu), **Min Interval (s)** (số giây từ lần hiện gần nhất), **After Rewarded (s)** (số giây từ lần một ad Rewarded hiện), mỗi ô có key Remote Config. `Allows(format)` đúng khi đã qua cả ba; nó đếm từ `AdsSdk.SecondsSinceDisplay`, không giữ trạng thái riêng, nên hai format dùng chung một `AdPacing` thì hỏi nó cho cả hai |
| `AdCollapsiblePlan` | Một cách chạy collapsible: tên, **Weight** (có key Remote Config) và **Close** của từng ad trong chuỗi. Gắn `[SectionList]` lên mảng plan thì trang vẽ mỗi plan một dòng gập mở, ô tên và Weight trên thanh. `AdCollapsiblePlan.Pick(plans)` bốc một plan có bước, cơ hội của plan bằng weight chia tổng weight; không plan nào có bước và weight lớn hơn 0 thì trả `null` |
| `AdsSdk.SetSuppressed(format, on)` | Chặn format, như mọi format gói remove-ads lấy đi (xem `AdsSdk`) |
| `AdsSdk.Launches`, `AdsSdk.SecondsSinceDisplay(format)` | Lần mở game thứ mấy, số giây từ lần format hiện gần nhất |
| `EditorGamePause.Apply(paused)` | Cách dừng game mặc định trong Editor, cho `PauseInEditor` |

## Sample `AdsManager`

Pack có sample `AdsManager`: các luật phần lớn game của studio chạy, trên `AdsSdk`.
Import bằng *Package Manager → RiseOn Ads → Samples → Ads Manager → Import*, rồi
chuyển file vào chỗ của game và sửa những gì game cần; file là của game, pack không
cập nhật nó. Lớp là `partial`, nên phần riêng của game (đọc save, log analytics...)
nằm được ở file khác mà không đụng file sample.

Field của sample trên trang và giá trị mặc định, theo thứ tự trên trang: luật áp cho nhiều
format trước, áp cho càng nhiều format càng lên trên (Remove Ads, Pacing); rồi mục riêng của
từng format, mang tên format đó, theo thứ tự của `AdFormat` (Banner; App Open cho App Open và
Native App Open, ad khi quay lại game; Collapsible Plans; End Card):

| Field | Mặc định | Nguồn |
|---|---|---|
| Remove Ads | Mỗi định dạng một ô tick; tick mọi định dạng trừ Rewarded, Rewarded Interstitial | Gói remove-ads thường chỉ bỏ quảng cáo người chơi không tự chọn xem; format có thưởng bị chặn thì người chơi nhận thưởng không cần ad |
| Pacing | Một mục cho mỗi giá trị của `PaceableFormat`: mọi format full-screen trừ Rewarded, thêm Native Collapsible, theo thứ tự của `AdFormat` | Rewarded do người chơi tự chọn xem nên không giãn cách |
| Pacing: Interstitial, Native Interstitial | First After 45, Min Interval 25 | Giá trị trên Remote Config của phần lớn game studio (Get In The Bowl, Bounce Adventure, Fish Sort) |
| Pacing: Native Collapsible | 0, 15 | Như trên |
| Pacing: Rewarded Interstitial, App Open, Native App Open, Native End Card | 0, 0 | Không game nào của studio giãn cách các format này; 0 là không giãn cách |
| Pacing: After Rewarded | 0 | Phần lớn game của studio không chờ thêm sau rewarded; Shikaku Cat chờ 45, OrganiCat 90 |
| Banner: Auto Show | Bật | Banner của các game studio hiện từ đầu game; game cần ẩn banner ở vài màn thì tắt ô này và tự gọi `ShowBanner`, `HideBanner` |
| App Open: Format | Native App Open, else App Open | Get In The Bowl, Bounce Adventure, Fish Sort, Shikaku Cat bật native app open ad trên Remote Config |
| App Open: From Launch | 1 | Mọi game của studio trừ OrganiCat hiện app open ad từ lần mở đầu |
| App Open: On Launch | Bật | Phần lớn game của studio hiện app open ad trên scene chờ lúc mở game; Google hướng dẫn như vậy cho cold start |
| App Open: After Away (s) | 0 | Mọi game của studio hiện app open ad mỗi lần người chơi quay lại |
| Collapsible Plans | **Plain** (weight 0.6: một ad, nút đóng sau 3 giây ở cạnh ngẫu nhiên), **Chained** (weight 0.3: hai ad, đóng bên phải rồi bên trái), **Redirect** (weight 0.1: một ad, bấm đóng thì mở quảng cáo) | Ba plan và tỉ lệ trên Remote Config của Get In The Bowl, Bounce Adventure, Fish Sort, Shikaku Cat |
| End Card: After Interstitial, After Rewarded | Bật | Phần lớn game của studio hiện end card sau interstitial và rewarded |
| End Card: After Native Interstitial | Tắt | Như trên |

**App Open › Format** chọn **Native App Open, else App Open** (native app
open ad khi nó sẵn sàng, không thì app open ad), **App Open**, **Native App Open**
hoặc **None** (không hiện). **On Launch** bật thì ad cũng hiện lúc mở game, đè lên
scene đầu, khi ad load xong trước lúc game rời scene đó; ad load muộn hơn thì chờ lần
quay lại sau. Game chỉ có một scene thì tắt, vì không có gì kết thúc lượt chờ. Lần
hiện lúc mở có placement `launch`, lúc quay lại có `return`.

End card nối sau ad khác theo giãn cách của chính nó: chưa tới thì bước end card bị
bỏ, ad trước vẫn hiện. Ad trước không hiện (bị chặn, chưa qua giãn cách) thì end
card cũng không hiện.

| Lời gọi | Làm gì |
|---|---|
| `ShowInterstitialAsync(placement)`, `ShowNativeInterstitialAsync` | Hiện khi format không bị chặn và giãn cách của nó cho phép; thêm end card theo mục End Card; trả `true` khi đã hiện |
| `CanShowInterstitial`, `CanShowNativeInterstitial` | Giãn cách cho phép và có ad sẵn |
| `ShowRewardedAsync(placement)`, `ShowRewardedInterstitialAsync` | Như trên, trả `true` khi người chơi nhận thưởng; format bị chặn thì thưởng luôn, không hiện ad |
| `CanShowRewarded`, `CanShowRewardedInterstitial` | Có ad sẵn (Rewarded Interstitial thì giãn cách cũng phải cho phép), hoặc format bị chặn, khi đó thưởng không cần ad. Dùng để bật nút xem quảng cáo |
| `ShowAsync(format, placement)`, `Allows(format)` | Đường chung của các hàm trên, cho format chỉ biết lúc chạy; `Allows` là giãn cách của format |
| `SetAdsRemoved(removed)` | Chặn hay thả các format của mục Remove Ads. Gọi lúc mở game từ save, và sau khi mua hay hoàn tiền gói remove-ads |
| `ReturnAdBlocked` | Đặt `true` khi game tự đưa người chơi ra ngoài (bảng mua hàng, share, trang store), vì lần quay lại đó không phải lượt mở mới |
| `ReturnFormat`, `ShowReturnAdAsync(placement)` | Format ad khi quay lại sẽ hiện lúc này (`null` khi chưa tới From Launch hay chưa có ad), và hiện nó, cho game tự hiện ad đó, ví dụ trên màn loading của mình |
| `ShowCollapsibleAsync(onDisplayed)` | Không hiện khi giãn cách của Native Collapsible chưa cho phép; không thì bốc một plan theo weight và hiện các bước của nó nối nhau. Trả số ad đã hiện |
| `CanShowCollapsible` | Giãn cách cho phép và có ad sẵn |
| `ShowBanner()`, `HideBanner()` | Như của `AdsSdk` |

Project mẫu `PackageSandbox` dùng sample này, thêm một file `partial` riêng của game:
remove-ads đọc từ save, switch không quảng cáo cho tester, log analytics.

## Trong Editor

Play mode không gọi SDK cho banner, MRec và ad full-screen. Danh sách ưu tiên nào
tách nền tảng thì Editor dùng danh sách của build target đang chọn (iOS, hay Android
cho mọi target khác); unit id thì dùng của Android, ô Android trống thì của iOS.
Mediation có tên trong các danh sách ưu tiên coi như khởi động không cần SDK và có ad
sẵn cho các định dạng đó; mediation có cả native thì ad giả có sau khi SDK của nó
khởi động xong cho native, và không có nếu lần khởi động đó lỗi. Pack vẽ ad giả ghi
tên mediation đang phục vụ: ad full-screen kín màn hình có nút đóng ở góc phải trên
(rewarded đếm ngược 5 giây rồi mới đóng được, đóng là nhận thưởng), banner 320×50
point (Adaptive thì rộng hết vùng an toàn), MRec 300×250 point, mỗi cái ở chỗ chọn
trong mục Formats; hai cái cùng chỗ thì đè lên nhau, như trên máy. Kích thước tính
như trên điện thoại rộng 360 point. Native vẫn dùng bản xem trước của mediation, nên
mediation phục vụ native vẫn khởi động SDK cho các định dạng native.

Mọi lớp là canvas Screen Space Overlay, xếp bằng sorting order từ **Editor Sorting
Order** lên:

| Lớp | Sorting order |
|---|---|
| Banner, MRec, view không đánh dấu | Editor Sorting Order |
| Native in-feed, native MRec | +1 |
| Màn che half-screen | +2 |
| Collapsible | +3 |
| Màn che full-screen | +4 |
| Ad full-screen | +5 |

Mediation tự vẽ bản xem trước lấy các số này từ `AdsEditorLayers`.

## Hướng dẫn nhanh

Game không dùng sample: một lớp manager tự viết luật của mình, ví dụ interstitial chỉ
từ level 3, giãn cách chỉnh trên trang:

```csharp
using Cysharp.Threading.Tasks;
using RiseOn.Ads;
using Sirenix.OdinInspector;
using UnityEngine;

public partial class AdsManager : IAdsManager {
    [SerializeField, LabelText("Interstitial Pacing")]
    private AdPacing interstitialPacing = new(45, 25);

    public async UniTask<bool> ShowInterstitialAsync(string placement) {
        if (Save.Level < 3 || !interstitialPacing.Allows(AdFullScreenFormat.Interstitial)) return false;

        return await AdsSdk.ShowInterstitialAsync(placement);
    }

    void IAdsManager.PauseInEditor(bool paused) => EditorGamePause.Apply(paused);
}

// Lúc mở game, từ save: gói remove-ads chặn interstitial và banner
AdsSdk.SetSuppressed(AdFormat.Interstitial, Save.AdsRemoved);
AdsSdk.SetSuppressed(AdFormat.Banner, Save.AdsRemoved);

// Ở chỗ khác của game
var shown    = await AdsManager.Ins.ShowInterstitialAsync("level_end");
var rewarded = await AdsSdk.ShowRewardedAsync("revive");
```

## Consent của SDK

App id AdMob, câu xin tracking (ATT) và việc tắt consent flow riêng của từng SDK
do pack `com.riseon.consent` quản: trang **RiseOn > Consent** liệt kê chỗ sai kèm
nút sửa, và build dừng khi còn sai. Xem README của pack đó, mục "Khi build".

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).