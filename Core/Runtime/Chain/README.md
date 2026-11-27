# Chain

Những điều không thấy ngay khi đọc từng file.

## Vì sao có luật thread

Màn che full-screen dừng hẳn engine Unity (cả âm thanh) để game không chạy dưới ad
và không bị gì ngoài pack chen lên trên. Hệ quả: giữa hai ad của chuỗi, player loop
của Unity không chạy, nên callback "ad đã đóng" của bước trước phải đến trên thread
của SDK, và bước sau phải show được từ chính thread đó.

Vì vậy mediation khai hai điều cho mỗi định dạng:

| | Nghĩa | Ai cần |
|---|---|---|
| `CallsBackOffLoop` | Listener được gọi thẳng trên thread của SDK, không chờ player loop | Mọi bước có bước sau |
| `ShowsFromAnyThread` | `IsReady` và `Show` chạy được trên thread bất kỳ | Mọi bước không phải bước đầu |

| Mediation | Định dạng | `CallsBackOffLoop` | `ShowsFromAnyThread` |
|---|---|---|---|
| AdMob | Định dạng của Google (trừ rewarded trên iOS) | có | không |
| AdMob | Rewarded, rewarded interstitial trên iOS | không | không |
| AdMob | Native (`com.riseon.ads.admob.native`) | có | có |
| MAX | tất cả | có | không |
| LevelPlay | Interstitial, rewarded trên Android, iOS | có | có |
| Ad giả trong Editor | tất cả | có | có |

Rewarded của AdMob trên iOS không có bước sau: từ GMA 11.1 plugin đưa sự kiện nhận
thưởng về thread Unity, nên dưới màn che nó chỉ đến sau khi chuỗi đã kết thúc.

API LevelPlay của Unity đưa mọi sự kiện qua player loop. Interstitial và rewarded
của LevelPlay vì vậy đi qua cầu nối riêng của pack tới API native của LevelPlay
(`RiseOnAdsLevelPlay.androidlib`, `ROLevelPlayFullScreenAd.m`): sự kiện đến theo đúng
thứ tự trên thread riêng của cầu nối (iOS: hàng đợi tuần tự), và cầu nối giữ sự kiện
đóng tối đa 1 giây chờ sự kiện nhận thưởng của network báo thưởng sau khi đóng.

Chuỗi tự bỏ những gì không chạy được: bước đầu không `CallsBackOffLoop` thì các
bước sau bị bỏ (có log); bước sau không đủ hai điều thì mediation đó bị bỏ qua và
chuỗi thử mediation kế trong danh sách ưu tiên. Ví dụ interstitial AdMob rồi end
card native chạy đủ; rewarded AdMob trên iOS thì chỉ hiện rewarded.

## Màn che

Bước đầu `CallsBackOffLoop` thì màn che bật trước bước đầu, kể cả chuỗi một bước
(`Show(format)`), và luôn gỡ sau bước cuối, kể cả khi bước nào đó lỗi hay ném
exception. Bước đầu không `CallsBackOffLoop` thì không có màn che: SDK tự dừng game
như thường.

Chuỗi chỉ có một ad App Open cũng không có màn che: ad đó không có nền, game lộ ra
quanh nó. Trong lúc nó hiện, banner, MRec và các ô native rời màn hình, rồi quay lại
như game đang yêu cầu khi nó đóng.

## Kết quả

`AdChainResult.Skip` là `NotReady` khi bước đầu chưa sẵn sàng: chuỗi không hiện
gì. Bước sau chưa sẵn sàng thì bị bỏ qua, chuỗi vẫn chạy tiếp. `Rewarded` đúng khi
bất kỳ bước rewarded nào trả thưởng.

Chuỗi lấy từ `AdsSdk.Chain()` bỏ bước ngay lúc thêm khi format của nó đang bị chặn
(`AdsSdk.SetSuppressed`). Không còn bước nào thì `Skip` là `Suppressed` nếu có bước bị
chặn, `NoStep` nếu chuỗi không có bước nào. Bước có thưởng bị chặn thì `Rewarded` vẫn
đúng: chặn lấy đi quảng cáo, không lấy đi phần thưởng.

## Cách đóng ad native

Mỗi bước native có `AdCloseSettings` riêng (đếm ngược, bên nút đóng, redirect), lấy
lúc gọi `Then`: `AdChain.Then(format, close)` và `AdCollapsibleChain.Then(close)` dùng
`close` truyền vào; `AdChain.Then(format)` và `AdCollapsibleChain.Then()` dùng
**Close** của format trong mục Formats của trang. Format không phải native bỏ qua
`close`.

## Collapsible

`AdCollapsibleChain` lấy mediation đầu tiên trong danh sách ưu tiên có ad sẵn sàng
và hiện cả chuỗi trên mediation đó. Ad đầu trượt vào; `onDisplayed` của bước đến
sau khi trượt xong. Ad cuối trượt ra khi người chơi đóng. Đóng kiểu redirect thì không trượt.
Màn che half-screen chỉ hiện dưới ad chưa phải cuối, để ad kế tiếp thay chỗ mà game
không lộ ra. `Run()` trả số ad đã hiện. `AdsSdk.HideCollapsible()` cắt chuỗi đang
chạy.