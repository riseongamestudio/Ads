# RiseOn Ads

Quảng cáo tự khởi động khi mở game theo thiết lập trong Project Settings: mediation
khởi động khi luồng consent cho phép quảng cáo, danh sách ưu tiên theo định dạng
(tại chỗ hoặc từ Remote Config), ad full-screen dưới màn che native, chuỗi
collapsible. Lõi là lớp static `AdsSdk`, như API của một SDK mediation; luật của game
(remove-ads, giãn cách ad full-screen và collapsible, end card, ad khi quay lại game)
đặt trên trang settings, lớp manager duy nhất của game áp chúng, pack tự tìm
và chạy lớp đó.

Package `com.riseon.ads`, namespace `RiseOn.Ads`.

## Yêu cầu

| Phụ thuộc | Cách có |
|---|---|
| Unity 6000.0+ | |
| `com.riseon.consent` | Tự cài theo `package.json`; luồng UMP + ATT mà mediation chờ |
| `com.riseon.utils`, `com.riseon.serializables` | Tự cài theo `package.json` |
| `com.cysharp.unitask` | Tự cài theo `package.json` (OpenUPM, scope `com.cysharp`) |
| `com.unity.ugui` | Tự cài theo `package.json`; màn che trong Editor vẽ bằng uGUI |
| Odin Inspector | Cài tay từ Asset Store |
| Ít nhất một adapter | `com.riseon.ads.admob`, `com.riseon.ads.max`, `com.riseon.ads.levelplay` |
| `com.riseon.firebase.remoteconfig` (tùy chọn) | Cài thì mỗi format trong mục Formats có dòng chọn key Remote Config cho danh sách mediation, và mỗi luật số hay bật/tắt của format (trừ Included In Remove Ads) cùng On Launch, After Away của mục On Return To Game có ô chọn key |

## Cài đặt

Thêm registry RiseOn (`https://upm.riseongamestudio.com`) với scope `com.riseon`, và
OpenUPM với scope `com.google`, `com.cysharp`, rồi thêm
`"com.riseon.ads": "1.0.0"` và adapter cần dùng. Bản local:
`"com.riseon.ads": "file:<thư mục repo Ads>/Core"`.

## Tổng quan

- **`AdsSdk`**: lớp static, lõi của pack. Trước scene đầu, pack đọc settings và tạo
  manager; rồi chốt danh sách ưu tiên của lần mở này, chờ luồng consent và khởi động
  cùng lúc mọi mediation có tên trong các danh sách; SDK nào khởi động chậm hay treo
  không giữ chân SDK khác. `AdsSdk` hiện đúng cái được gọi, không có luật riêng:
  `Show`, `Chain()`, `ShowBanner()`... Sự kiện: `Paid` (doanh thu từng impression),
  `Displayed` (ad full-screen lên màn hình), `CollapsibleDisplayed` (ad collapsible
  trượt vào hay thay chỗ), `Loaded`, `LoadFailed`, `PlayerLeft`, `PlayerReturned`
  (người chơi rời game và quay lại; ad full-screen của pack chiếm màn hình, hay lúc
  cửa sổ `AdsDebugger` đang mở, thì không tính). Chỉ gọi trên thread Unity; mọi sự
  kiện cũng đến trên thread đó.
- **`IsInitialized`, `WaitUntilInitializedAsync()`**: xong khi consent xong và mọi
  mediation đã khởi động hoặc lỗi. Consent chưa cho quảng cáo thì xong ngay sau
  consent, và mediation tự khởi động sau, khi người chơi đổi lựa chọn cho phép (ví dụ
  trong form privacy). `WaitUntilInitializedAsync()` gọi lúc nào cũng được, kể cả
  trước scene đầu. Một SDK treo giữ nó (và `IsInitialized`) chờ mãi.
- **`AdsManagerBase`**: mỗi project có tối đa một lớp `partial` kế thừa nó; game gọi
  mọi quảng cáo qua lớp này, mỗi format full-screen một hàm `Show…Async` riêng. Nó bọc
  toàn bộ `AdsSdk` và áp các luật hay gặp, đặt trong mục Formats và mục On Return To
  Game của trang settings. Mỗi luật là một member `virtual`; lớp của game thay bằng
  luật của mình mà vẫn đọc được giá trị trên trang (xem "Tự viết luật"). Game không
  có lớp này thì gọi thẳng `AdsSdk`.
- **Mediation**: một SDK quảng cáo, là class C# thuần cài các interface định dạng nó
  phục vụ (`IAdMediationBanner`, `IAdMediationFullScreen`,
  `IAdMediationNativeCollapsible`...). Mọi class mediation trong project tự có một mục
  trong danh sách Mediations của trang.
- **`AdFullScreenFormat`**: các format full-screen, cùng giá trị với `AdFormat`; hàm
  chỉ phục vụ ad full-screen (`AdChain.Then`, `AdsSdk.Show`, `AdsSdk.IsReady`, sự kiện
  `Displayed`, các hàm full-screen của manager) nhận kiểu này, nên không truyền nhầm
  banner được; collapsible có hàm riêng (`AdsSdk.IsCollapsibleReady`...).
  `ToFormat()` đổi sang `AdFormat`; với một `AdFormat`, `IsFullScreen()` kiểm tra nó
  có full-screen không, còn `ToFullScreen()` đổi sang `AdFullScreenFormat?`, `null`
  khi không full-screen.
- **Danh sách ưu tiên**: mỗi định dạng có một danh sách id mediation, tốt nhất
  trước, chung cho Android và iOS, hoặc tách riêng từng nền tảng khi mỗi nền tảng
  dùng mediation khác. Danh sách trống là tắt định dạng đó. Thứ tự chốt trước khi
  mediation khởi động và giữ suốt lần mở; mỗi mediation chỉ load những định dạng có
  tên nó. Mediation không có tên trong danh sách nào thì không khởi động.
- **Thiết lập định dạng**: cách định dạng hiện (vị trí và kiểu banner, vị trí MRec,
  màu nền và nút đóng của native...) và luật manager áp cho nó (giãn cách, end card,
  remove-ads...) đặt một lần trên trang, chung cho cả hai nền tảng; mediation nào
  phục vụ định dạng đó cũng làm theo cách hiện.
- **Key Remote Config**: định dạng nào chọn key Remote Config ở dòng **Mediations
  Remote Key** của nó (xem dưới) thì đọc danh sách từ key đó, thay cho danh sách
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
  phải cuối. `ShowCollapsibleAsync` của manager dựng chuỗi theo plan trên trang.
- **`NativeRectHandle`**: mỗi chỗ trong game hiện ô quảng cáo native gọi một lần
  `CreateNativeMRec(sizePx, roundCornerPx)` (một khối native MRec, như chỗ MRec trên
  một màn) hay `CreateNativeInFeed(sizePx, roundCornerPx)` (một dòng trong danh sách,
  một ô trong lưới), được một ô có cỡ đó; mỗi format có unit id và **Cache Size**
  riêng. Game đặt chỗ ô bằng `SetPosition(positionPx)` (pixel, tính từ góc trên trái
  màn hình), bật bằng `Show(onDisplayed)`, tắt bằng `Hide()`, bỏ ô bằng `Dispose()`.
  Game chỉ bật và tắt: ô bật lúc chưa có ad thì hiện ngay khi có ad, ô đang hiện tự
  đổi sang ad mới. `onDisplayed` đến khi ad đầu tiên của lần bật đó lên màn hình. Ô
  tắt không giữ ad chưa hiện nào: ad đó về lại kho cho ô khác dùng. Kho giữ sẵn số ad
  đặt ở ô **Cache Size** của format trên trang (kho chung hay riêng giữa các format
  tùy mediation, xem README của nó), nên màn nào hiện nhiều ô cùng lúc thì tăng số
  đó. Định dạng tắt, chưa mediation nào phục vụ định dạng đó khởi động xong, hay
  mediation thiếu unit id thì handle rỗng: mọi lời gọi không làm gì và `HasCell` là
  `false`, nên game chỉ chừa chỗ cho ô quảng cáo khi `HasCell` là `true`.
- **`AdsDebugger`**: cửa sổ debug cho bản build test, game mở bằng
  `AdsDebugger.Show()` từ menu cheat. Cửa sổ hiện advertising ID của máy kèm nút
  copy, để đăng ký máy làm test device trên AdMob và LevelPlay (MAX bật test ads
  trong Mediation Debugger), và một nút mở
  công cụ debug của mỗi mediation đang chạy (Ad Inspector, Mediation Debugger, Test
  Suite).
- **Trong Editor**: Play mode không gọi SDK cho banner, MRec và ad full-screen; pack
  vẽ ad giả ghi tên mediation sẽ phục vụ nó (xem dưới). Native vẫn dùng bản xem
  trước của mediation.

Chi tiết từng khái niệm trong README của thư mục:
[Chain](Runtime/Chain/README.md),
[Cover](Runtime/Cover/README.md) (thứ tự lớp, pause),
[Debugger](Runtime/Debugger/README.md),
[Mediations](Runtime/Mediations/README.md).

## Cài đặt của project

Dev mở bằng menu **RiseOn > Ads**, hoặc Edit > Project Settings > RiseOn > Ads. Trang
này lưu vào `Assets/RiseOn/Resources/RiseOnAdsSettings.asset`: toggle và lựa chọn lưu
ngay, ô chữ lưu khi gõ xong; Ctrl+Z hoàn tác như mọi chỗ khác của Unity.
Inspector của asset chỉ có nút mở trang này.

| Mục | Ý nghĩa |
|---|---|
| Display | **Cover**: màu màn che dưới ad full-screen và dưới collapsible giữa chuỗi. **Editor Sorting Order**: sorting order thấp nhất của các lớp ad trong Editor; các lớp xếp lên từ đó (xem dưới). Canvas của game phải thấp hơn số này |
| On Return To Game | Ad hiện khi người chơi quay lại game (từ màn hình chính hay app khác), và lúc mở game. **Format** chọn **Native App Open, else App Open** (native app open ad khi nó sẵn sàng, không thì app open ad), **App Open**, **Native App Open** hoặc **None** (không hiện); ad đó vẫn theo luật của format mình trong mục Formats (remove-ads, First After, Min Interval, end card). **From Launch**: ad hiện từ lần mở game thứ mấy trên máy, cả lúc mở lẫn lúc quay lại; 1 là từ lần đầu. **On Launch**: bật thì ad cũng hiện lúc mở game, đè lên scene đầu, khi ad load xong trước lúc game rời scene đó; ad load muộn hơn thì chờ lần quay lại sau. Game chỉ có một scene thì tắt, vì không có gì kết thúc lượt chờ. **After Away (s)**: người chơi phải ở ngoài game ít nhất bấy nhiêu giây thì lần quay lại mới hiện ad. On Launch và After Away có ô chọn key như các luật trong Formats. Lần hiện lúc mở có placement `launch`, lúc quay lại có `return` |
| Manager | Lớp `AdsManagerBase` của project: tên lớp trên thanh (chỉ đọc). Field riêng mà game khai trong lớp hiện bên trong; lớp không có field nào thì thanh không gập mở. Luật của manager nằm trong mục Formats và mục On Return To Game |
| Mediations | Các SDK quảng cáo. Mỗi class mediation trong project, cả adapter của pack lẫn class game tự viết, tự có một dòng gập mở; trang tự thêm khi class mới compile, không có bước thêm vào danh sách. Mediation không format nào dùng thì không khởi động. Alt+click mở hay gập cả nhánh. Bên trong, trên cùng là thiết lập của mediation (như App Key của LevelPlay) và của SDK, phần của SDK đọc và ghi thẳng vào file settings của chính SDK (xem README của từng mediation); dưới đó là mục con **Networks**, gập sẵn: các network mà SDK của mediation mediate, phiên bản adapter của từng network trong project, và cài, cập nhật, gỡ adapter theo đúng cách của SDK đó (LevelPlay chỉ có nút mở Network Manager của nó; xem mục Network trong README của từng adapter); dưới cùng là mục con **Formats** của mediation, mỗi format một mục con, unit id Android và iOS nằm ngay trên thanh của mục con; format có thêm thiết lập (như **Cache Size** của format native AdMob) thì gập mở được, và format không có thì đứng trước. Adapter của pack (AdMob, MAX, LevelPlay) có dòng dù chưa cài, như network trong mục **Networks**: cột giá trị ghi phiên bản trong project (chưa cài thì **Not installed**), kèm nút cuối dòng. **Install** thêm package của adapter qua Package Manager. Core cài từ thư mục local thì lấy từ thư mục cùng tên adapter cạnh `Core`; thiếu thư mục đó thì dòng không có **Install**, tooltip ghi đường dẫn thiếu. Core cài từ registry thì **Install** hỏi phiên bản, liệt kê các phiên bản registry có, mới nhất trước; danh sách chưa đọc xong (đang đọc, registry lỗi) thì **Install** cài thẳng bản mới nhất. Đã cài từ registry thì bấm vào phiên bản để chọn phiên bản khác, cũ hơn cũng được; có phiên bản mới hơn thì cột ghi `cũ → mới` kèm nút **Update**, và **Update All** trên thanh cập nhật mọi adapter cũ. Danh sách phiên bản đọc từ registry lần đầu mở trang trong mỗi phiên Editor; nút làm mới trên thanh đọc lại. **Uninstall** (khi manifest có tên adapter) hỏi lại một lần, kèm danh sách format đang dùng mediation, bỏ mediation khỏi danh sách và khỏi mọi format, rồi gỡ package. Unit id và thiết lập format của nó trong asset của pack mất theo; file settings của SDK trong `Assets` giữ nguyên, và package SDK chỉ được gỡ theo khi manifest không ghi nó trực tiếp. Cài, cập nhật, gỡ lỗi (ví dụ manifest thiếu scoped registry của SDK, xem README của adapter) thì Console báo `Installing … failed: …` (hay `Updating`, `Uninstalling`). Từ lúc bấm tới khi Package Manager xong, đầu danh sách có dòng quay ghi việc đang chạy, như **Uninstalling LevelPlay**, và mọi nút cài, gỡ trên trang mờ đi, vì Package Manager resolve lại mọi package rồi Unity mới compile |
| Formats | Mỗi format một mục con gập mở, theo thứ tự Banner, MRec, Interstitial, Rewarded, Rewarded Interstitial, App Open, rồi các format native, gập sẵn. Trong mỗi mục, dòng **Mediations** là các mediation phục vụ format, thử từ trái sang phải; hàng hẹp thì ô xuống dòng như chữ, thử tiếp từ trên xuống dưới, mũi tên chỉ nằm giữa hai ô cùng dòng. Kéo một ô để đổi thứ tự, **×** trên ô bỏ mediation khỏi format, **+** thêm một mediation của trang mà format chưa có và phục vụ được format đó (format native chỉ AdMob; App Open có AdMob, MAX; Rewarded Interstitial chỉ AdMob); không mediation nào thì format ghi **Off** và tắt. Project có `com.riseon.firebase.remoteconfig` thì dưới đó có dòng **Mediations Remote Key** chọn một key kiểu chuỗi của pack đó cho danh sách mediation (khai key ở trang **RiseOn > Firebase > Remote Config**), **No Remote Key** là không dùng key. Một key dùng cho cả hai nền tảng; muốn mỗi nền tảng một giá trị thì đặt condition theo nền tảng trên console. Giá trị là id mediation cách nhau bằng dấu phẩy, tốt nhất trước, ví dụ `max,admob`; `off` tắt format; giá trị rỗng thì dùng danh sách của dòng **Mediations**. Id không có trong Mediations bị bỏ, kèm cảnh báo trong Console; id nào cũng bị bỏ thì format tắt, như `off`. Có ít nhất một format chọn key thì mediation khởi động sau khi Remote Config xong lần mở game, theo giá trị nó có lúc đó. Tiếp theo là ô **Included In Remove Ads**: tick thì gói remove-ads lấy format đi, format có thưởng được tick thì người chơi nhận thưởng không cần xem ad; và với mỗi format full-screen trừ Native End Card, ô **Use Native End Card**: bật thì các hàm `Show…Async` của manager, kể cả ad khi người chơi quay lại game, hiện tiếp một native end card trong cùng chuỗi, sau ad của format. Cuối mục là thiết lập của format. Phần cách hiện thì mediation nào phục vụ format cũng làm theo: Banner có **Position** (bốn góc, giữa trên, giữa dưới, chính giữa vùng an toàn) và **Adaptive** (bật thì rộng hết màn hình, cao theo banner adaptive của mạng; tắt thì 320×50); MRec có **Position**; Native Interstitial, Native App Open, Native End Card có **Background Color** (màu chỗ creative không phủ) và **Close** (số giây chờ, cạnh đặt nút đóng, chỗ đồng hồ, bấm đóng thì mở quảng cáo); Native Collapsible có **Height Ratio** (phần chiều cao màn hình panel chiếm, từ đáy) và **Background Color**; Native MRec, Native In-Feed có **Background Color** (trong suốt thì chỗ game chừa sẵn lộ ra). Phần luật thì manager áp (xem `AdsManagerBase`): Banner có **Auto Show** (bật thì manager hiện banner ngay khi game chạy và giữ suốt game, remove-ads chặn thì ẩn; game gọi `ShowBanner` thì chỉ có cảnh báo trong Console, gọi `HideBanner` thì banner vẫn ở đó và Console báo lỗi. Tắt thì game tự gọi `ShowBanner`, `HideBanner`); Interstitial, Rewarded Interstitial, App Open, Native Interstitial, Native App Open, Native End Card, Native Collapsible có **First After (s)** (số giây từ lúc mở game tới lần hiện đầu) và **Min Interval (s)** (số giây giữa hai lần hiện, tính từ lúc lần trước hiện), mỗi format đếm riêng; Interstitial và Native Interstitial có thêm **After Rewarded (s)** (số giây từ lần một ad Rewarded hiện tới lần hiện kế của format); Native Collapsible có **Plans**: mỗi plan một dòng gập mở, trên thanh là ô tên (để nhận ra plan trên trang và trong lỗi chặn build) và **Weight** (kèm ô key khi có `com.riseon.firebase.remoteconfig`), bên trong là **Close** của từng ad trong chuỗi, **+** trên thanh thêm một ad; mỗi lần `ShowCollapsibleAsync` bốc một plan, cơ hội của plan bằng weight của nó chia tổng weight. Project có `com.riseon.firebase.remoteconfig` thì mỗi luật số hay bật/tắt (cả Use Native End Card và Weight, trừ Included In Remove Ads) có ô chọn key ở cuối dòng: **No Remote Key** là dùng giá trị trên trang; chọn một key cùng kiểu (float cho số, bool cho bật/tắt) thì lúc chạy dùng giá trị của key, ô giá trị trên trang mờ đi; trước khi Remote Config xong là giá trị mặc định khai trên trang Remote Config. Số trên trang không nhỏ hơn 0. Thiết lập dùng chung cho cả hai nền tảng. Riêng dòng **Mediations** có nút tách ở cuối dòng, cho format mà mỗi nền tảng dùng mediation khác: bấm thì dòng thành hai dòng **Android** và **iOS**, iOS bắt đầu bằng danh sách đang có; dòng tên có ô **Editor uses** chọn nền tảng có danh sách mà Play mode trong Editor dùng, mặc định Android, và nút gộp lại, giữ danh sách Android |

Trang báo đỏ và build dừng khi:

| Trường hợp | Dev làm gì |
|---|---|
| Asset nằm ngoài thư mục Resources | Bấm **Move it** |
| Có hơn một lớp kế thừa `AdsManagerBase` | Giữ một lớp |
| Lớp manager không có constructor rỗng | Thêm constructor rỗng |
| Lớp manager nằm trong assembly chỉ cho Editor: Play mode chạy được, bản build không có | Chuyển lớp sang assembly runtime |
| Settings chưa giữ lớp manager, thường khi lớp ra đời lúc script đang lỗi compile | Bấm **Take it** |
| Lớp mà settings giữ (mediation, hoặc manager cũ) đã đổi tên hay bị xóa | Bấm **Clear it** |
| Hai mediation cùng id, thường do merge file settings | Bấm **Remove extras** |
| Key Remote Config của một format không còn là key kiểu chuỗi của Remote Config (bị xóa hay đổi kiểu); ô của nó ghi **Missing** | Chọn key khác hoặc **No Remote Key** |
| Key Remote Config của một luật trong mục Formats hay On Return To Game (First After, Min Interval, After Rewarded, Use Native End Card, Weight của plan, On Launch, After Away...) không còn là key cùng kiểu trong Remote Config (bị xóa hay đổi kiểu); ô của nó ghi **Missing** | Chọn key khác hoặc **No Remote Key** |

Không có lớp kế thừa `AdsManagerBase` thì trang báo vàng, kèm nút **New script**:
pack hỏi chỗ lưu và viết khung lớp.

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

## `AdsManagerBase`

Luật trong mục Formats, On Return To Game và giá trị mặc định:

| Luật | Mặc định | Nguồn |
|---|---|---|
| Interstitial, Native Interstitial: First After (s), Min Interval (s) | 45, 25 | Giá trị trên Remote Config của phần lớn game studio (Get In The Bowl, Bounce Adventure, Fish Sort) |
| Native Collapsible: First After (s), Min Interval (s) | 0, 15 | Như trên |
| Interstitial, Native Interstitial: After Rewarded (s) | 0 | Phần lớn game của studio không chờ thêm sau rewarded; Shikaku Cat chờ 45, OrganiCat 90 |
| Rewarded Interstitial, App Open, Native App Open, Native End Card: First After (s), Min Interval (s) | 0, 0 | Không game nào của studio giãn cách các format này; 0 là không giãn cách |
| On Return To Game: Format | Native App Open, else App Open | Get In The Bowl, Bounce Adventure, Fish Sort, Shikaku Cat bật native app open ad trên Remote Config |
| On Return To Game: From Launch | 1 | Mọi game của studio trừ OrganiCat hiện app open ad từ lần mở đầu |
| On Return To Game: On Launch | Bật | Phần lớn game của studio hiện app open ad trên scene chờ lúc mở game; Google hướng dẫn như vậy cho cold start |
| On Return To Game: After Away (s) | 0 | Mọi game của studio hiện app open ad mỗi lần người chơi quay lại |
| Banner: Auto Show | Bật | Banner của các game studio hiện từ đầu game; game cần ẩn banner ở vài màn thì tắt ô này và tự gọi `ShowBanner`, `HideBanner` |
| Use Native End Card | Bật cho Interstitial, Rewarded | Phần lớn game của studio hiện end card sau interstitial và rewarded |
| Native Collapsible: Plans | **Plain** (weight 0.6: một ad, nút đóng sau 3 giây ở cạnh ngẫu nhiên), **Chained** (weight 0.3: hai ad, đóng bên phải rồi bên trái), **Redirect** (weight 0.1: một ad, bấm đóng thì mở quảng cáo) | Ba plan và tỉ lệ trên Remote Config của Get In The Bowl, Bounce Adventure, Fish Sort, Shikaku Cat |
| Included In Remove Ads | Tick mọi định dạng trừ Rewarded, Rewarded Interstitial | Gói remove-ads thường chỉ bỏ quảng cáo người chơi không tự chọn xem |

End card nối sau ad khác theo **First After**, **Min Interval** của chính nó: chưa
tới thì bước end card bị bỏ, ad trước vẫn hiện; lần end card hiện không tính vào giãn
cách của ad trước. Ad trước không hiện (remove-ads chặn, chưa qua giãn cách) thì end
card cũng không hiện.

| Lời gọi | Làm gì |
|---|---|
| `ShowInterstitialAsync(placement)`, `ShowAppOpenAsync`, `ShowNativeInterstitialAsync`, `ShowNativeAppOpenAsync`, `ShowNativeEndCardAsync` | Hiện khi không remove-ads và đã qua First After, Min Interval, After Rewarded của format; thêm end card khi format bật **Use Native End Card**; trả `true` khi đã hiện |
| `CanShowInterstitial`, `CanShowAppOpen`, `CanShowNativeInterstitial`, `CanShowNativeAppOpen`, `CanShowNativeEndCard` | Không remove-ads, đã qua First After, Min Interval, After Rewarded và có ad sẵn |
| `ShowRewardedAsync(placement)`, `ShowRewardedInterstitialAsync` | Như trên, trả `true` khi người chơi nhận thưởng; format tick **Included In Remove Ads** mà người chơi đã mua gói thì thưởng luôn, không hiện ad |
| `CanShowRewarded`, `CanShowRewardedInterstitial` | Đã qua First After, Min Interval của format (Rewarded không giãn cách) và có ad sẵn; hoặc remove-ads chặn format, khi đó thưởng không cần ad. Dùng để bật nút xem quảng cáo |
| `Chain()`, `CollapsibleChain()` | Như của `AdsSdk`, bỏ những bước remove-ads chặn và những bước chưa qua First After, Min Interval, After Rewarded của format đó; không tự thêm end card. `CollapsibleChain()` không theo First After, Min Interval và Plans của Native Collapsible, và lần hiện qua nó không tính vào Min Interval của Native Collapsible |
| `ShowCollapsibleAsync(onDisplayed)` | Collapsible theo luật của trang: không hiện khi remove-ads hay chưa qua **First After**, **Min Interval** (Min Interval tính từ ad đầu của lần hiện trước); không thì bốc một plan theo weight và hiện các bước của nó nối nhau. `onDisplayed` đến một lần, khi ad đầu hiện; trả số ad đã hiện. Không plan nào có bước và weight lớn hơn 0 thì không hiện, kèm cảnh báo trong Console |
| `CanShowCollapsible` | Không remove-ads, đã qua First After, Min Interval và có ad sẵn |
| `ShowBanner()`, `HideBanner()` | Banner, trừ khi remove-ads; đã hiện thì ở đó tới khi gọi `HideBanner`. Chỉ cho game tắt **Auto Show**: bật thì `ShowBanner` chỉ cảnh báo, `HideBanner` báo lỗi và banner vẫn ở đó. Gọi lúc chưa mediation nào khởi động xong (màn đầu game) thì banner hiện khi mediation đầu tiên phục vụ banner khởi động xong; mediation đứng trước nó trong danh sách khởi động xong sau thì banner chuyển sang mediation đó. `ShowMRec` chạy như vậy |
| `CreateNativeMRec(sizePx, roundCornerPx)`, `CreateNativeInFeed(sizePx, roundCornerPx)` | Khối native MRec, ô in-feed; ô game bật mà remove-ads chặn thì không hiện, và hiện lại khi hết chặn. Gọi trước `IsInitialized` thì ném `InvalidOperationException`: tạo ô sau `WaitUntilInitializedAsync()` |
| `RefreshAdsRemoved()` | Gọi khi `AdsRemoved` đổi (mua hoặc hoàn tiền gói remove-ads): ẩn banner, MRec, native MRec, collapsible, ô in-feed mà gói giờ chặn; hiện lại những gì game đã yêu cầu khi hết chặn |
| `ReturnAdBlocked` | Đặt `true` khi game tự đưa người chơi ra ngoài (bảng mua hàng, share, trang store), vì lần quay lại đó không phải lượt mở mới |

| Override | Khi nào |
|---|---|
| `AdsRemoved` | Luôn, khi game bán remove-ads: đọc từ save của game hoặc `IapSdk.Owns` |
| `IsRemoved(format)` | Game có nhiều gói (remove-ads, VIP...) chặn những định dạng khác nhau, hay switch không quảng cáo của tester (chặn mọi format, rewarded thưởng luôn) |
| `PacingAllows(format)`, `CollapsiblePacingAllows()` | Thay cách tính First After, Min Interval, After Rewarded bằng code của game: hàm đầu cho format full-screen (gốc luôn cho Rewarded qua), hàm sau cho `ShowCollapsibleAsync`. Lấy giá trị từ Remote Config thì chỉ cần chọn key trên trang |
| `EndCardFollows(format)` | Thay **Use Native End Card** bằng code của game, ví dụ chỉ từ level 3. Không bao giờ được hỏi cho Native End Card |
| `DrawCollapsiblePlan()` | Thay cách bốc plan của `ShowCollapsibleAsync` |
| `ReturnFormat`, `ShowsOnLaunch`, `ReturnAfterAwaySeconds` | Format (hay `null` cho không hiện; gốc tính cả From Launch, và với **Native App Open, else App Open** thì lấy native app open khi nó sẵn sàng (không remove-ads, qua giãn cách, đã load), không thì App Open), có hiện lúc mở game không, và số giây ở ngoài của mục On Return To Game, tính bằng code của game. Lấy từ Remote Config thì chỉ cần chọn key trên trang |
| `OnPlayerReturned(awaySeconds)` | Thay cách hiện ad khi quay lại. Chỉ được gọi sau khi `IsInitialized` |
| `AutoShowsBanner` | Thay **Auto Show** của Banner bằng code của game. Pack đọc một lần, ngay trước `OnAttached`; đổi sau đó không có tác dụng |
| `ShowLaunchAdAsync()` | Thay cách hiện ad lúc mở game, ví dụ game một scene chờ tới khi màn loading tắt. Pack gọi một lần, ngay sau `OnAttached` |
| `OnShowRequested(format, placement)` | Hàm `Show…Async` của một format full-screen (không gồm `ShowCollapsibleAsync`), `ShowAsync`, hay ad khi quay lại game vừa được gọi, trước khi luật quyết định có hiện không: log cơ hội hiện quảng cáo |
| `OnDisplayed(format, placement)`, `OnCollapsibleDisplayed()` | Log sự kiện của game khi một ad full-screen lên màn hình, khi một ad collapsible trượt vào hay thay chỗ (từ mọi chuỗi) |
| `OnAttached`, `OnPaid`, `OnLoaded`, `OnLoadFailed`, `OnPlayerLeft` | Nghe thêm. Doanh thu đã tự gửi tới analytics khi project cài `com.riseon.analytics` và **Ad Revenue from Ads** trên trang Analytics bật (mặc định) |
| `PauseInEditor(paused)` | Trong Editor không SDK nào dừng Unity dưới ad full-screen, nên pack gọi hàm này: `true` khi chuỗi bắt đầu hiện ad đầu, `false` khi ad cuối đóng; game không có manager thì pack dùng cách dừng mặc định. Mặc định đưa `Time.timeScale` về 0 và dừng âm thanh, rồi trả lại như cũ. Game override khi với nó "dừng" là việc khác (ví dụ game tự đếm giờ bằng thời gian thật) |

Mỗi hook `On…` trừ `OnAttached` và `OnShowRequested` có một event C# cùng tên không
có `On` (`Paid`, `Displayed`...), gọi sau hook, cho chỗ khác của game nghe.

### Tự viết luật

Override một luật không làm mất phần pack lo: lớp của game đọc được mọi giá trị trên
trang (đã thay bằng giá trị Remote Config ở chỗ chọn key), gọi lại luật gốc bằng
`base`, và dùng các hàm mà hàm `Show…Async` dùng:

| Member `protected` | Làm gì |
|---|---|
| `Formats` | Mục Formats: `RemoveAdsTakes(format)`, `EndCardFollows(format)`, `PacingOf(format)` (First After, Min Interval, After Rewarded của format full-screen; của Rewarded toàn 0), thiết lập từng format (`Formats.AppOpen.FirstSeconds`, `Formats.NativeCollapsible.Pacing`...) |
| `ReturnToGame` | Mục On Return To Game: `Formats` (các format ad có thể là, tốt nhất trước; rỗng khi **None**), `FromLaunch`, `OnLaunch`, `AfterAwaySeconds` |
| `Launches` | Lần mở game này trên máy, 1 là lần đầu; pack tự đếm |
| `SecondsSinceDisplay(format)`, `SecondsSinceCollapsible` | Số giây từ lần format full-screen hiện gần nhất (từ mọi chuỗi), từ ad đầu của lần `ShowCollapsibleAsync` gần nhất; `null` khi chưa hiện lần nào |
| `ShowAsync(format, placement)`, `IsReady(format)` | Đường chung của các hàm `Show…Async` và `CanShow…`, theo mọi luật đang áp, cho format chỉ biết lúc chạy |

Field mà lớp manager khai hiện trong mục Manager của trang; field kiểu
`AdRemoteSetting<float>` hay `AdRemoteSetting<bool>` có ô chọn key Remote Config như
luật của trang. Ví dụ ad khi quay lại game giữa level chờ lâu hơn After Away của
trang, và interstitial chỉ có từ level 3, trên First After, Min Interval của trang:

```csharp
using RiseOn.Ads;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed partial class GameAds : AdsManagerBase {
    [SerializeField, LabelText("In Level After Away (s)"), Tooltip("After Away for a return in the middle of a level.")]
    private AdRemoteSetting<float> inLevelAfterAwaySeconds = new(600);

    protected override double ReturnAfterAwaySeconds => Level.IsPlaying ? inLevelAfterAwaySeconds.Value : base.ReturnAfterAwaySeconds;

    protected override bool PacingAllows(AdFullScreenFormat format) {
        if (format is AdFullScreenFormat.Interstitial && Save.Level < 3) return false;

        return base.PacingAllows(format);
    }
}
```

Project mẫu `PackageSandbox` có `AdsManager` của một game hoàn chỉnh: remove-ads đọc
từ save, switch không quảng cáo cho tester, log analytics; mọi luật khác, kể cả banner
suốt game, để trang lo.

## Trong Editor

Play mode không gọi SDK cho banner, MRec và ad full-screen. Danh sách ưu tiên nào
tách nền tảng thì Editor dùng danh sách của nền tảng chọn ở ô **Editor uses**; unit
id thì dùng của Android, ô Android trống thì của iOS. Mediation có tên trong các
danh sách ưu tiên coi như khởi động không cần SDK và có ad sẵn cho các định dạng đó;
mediation có cả native thì ad giả có sau khi SDK của nó khởi động xong cho native,
và không có nếu lần khởi động đó lỗi. Pack vẽ ad giả ghi tên mediation đang phục vụ:
ad full-screen kín màn hình có nút đóng ở góc phải trên (rewarded đếm ngược 5 giây
rồi mới đóng được, đóng là nhận thưởng), banner 320×50 point (Adaptive thì rộng hết
vùng an toàn), MRec 300×250 point, mỗi cái ở chỗ chọn trong mục Formats; hai cái
cùng chỗ thì đè lên nhau, như trên máy. Kích thước tính như trên điện thoại rộng 360
point. Native vẫn dùng bản xem trước của mediation, nên mediation phục vụ native vẫn
khởi động SDK cho các định dạng native.

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

Trên lớp manager, source generator của pack sinh thêm lúc compile, không ra file:

- `Ins`: instance pack đang chạy. Đọc trước scene đầu, hoặc khi pack không chạy được
  manager nào (Console có lý do), thì ném `InvalidOperationException`.
- `[Serializable]`, nếu lớp chưa tự gắn.
- Lệnh đăng ký với pack, để IL2CPP không cắt lớp.

Compile báo lỗi `RISEONADS001` khi lớp không `partial`, generic hoặc lồng trong lớp
khác, và `RISEONADS003` khi có hơn một lớp kế thừa `AdsManagerBase`.

```csharp
using Cysharp.Threading.Tasks;
using RiseOn.Ads;
using RiseOn.Analytics;

public sealed partial class GameAds : AdsManagerBase {
    protected override bool AdsRemoved => Save.AdsRemoved;

    public async UniTask ShowInterstitialThenEndCard(string placement) {
        if (!CanShowInterstitial) return;

        await Chain()
            .Then(AdFullScreenFormat.Interstitial, placement)
            .ThenIf(Save.Level >= 3, AdFullScreenFormat.NativeEndCard, placement)
            .Run();
    }

    // ad_displayed khai ở mục Events của trang Analytics.
    protected override void OnDisplayed(AdFullScreenFormat format, string placement) =>
        AnalyticsSdk.Log(AnalyticsEvent.ad_displayed, new("format", format.ToString()), new("placement", placement ?? "none"));
}

// Ở chỗ khác của game
var rewarded = await GameAds.Ins.ShowRewardedAsync("revive");
```

## Consent của SDK

App id AdMob, câu xin tracking (ATT) và việc tắt consent flow riêng của từng SDK
do pack `com.riseon.consent` quản: trang **RiseOn > Consent** liệt kê chỗ sai kèm
nút sửa, và build dừng khi còn sai. Xem README của pack đó, mục "Khi build".

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).