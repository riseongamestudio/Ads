# Debugger

Cửa sổ debug của pack cho bản build test. Người đọc là dev gắn nó vào menu cheat
của game và tester dùng nó trên máy.

## Mở cửa sổ

| Hàm | Làm gì |
|---|---|
| `AdsDebugger.Show()` | Mở cửa sổ. Đang mở thì không làm gì |
| `AdsDebugger.Hide()` | Đóng cửa sổ (nút **Close** trong cửa sổ cũng gọi hàm này) |
| `AdsDebugger.IsShowing` | Cửa sổ đang mở |

- Chỉ gọi trên thread Unity.
- Pack không tự mở cửa sổ và không có nút nổi: chỉ `AdsDebugger.Show()` mở nó. Game
  gọi hàm này từ menu cheat của mình, nên bản store không có cách mở cửa sổ.

## Trong cửa sổ

- **Advertising ID** của máy: GAID trên Android, IDFA trên iOS, kèm nút **Copy**.
  Tester đăng ký ID này trên AdMob và LevelPlay để máy nhận quảng cáo test mà không
  cần đổi unit id hay build lại:
  - AdMob: dán vào Settings → Test devices trên web AdMob. Máy nhận quảng cáo test
    từ unit thật.
  - LevelPlay: dán vào Settings → Test devices, rồi chọn ad source cần thử và bấm
    **Test ads**. Máy chỉ nhận quảng cáo của mạng đó, và LevelPlay tự trả cấu hình
    về như cũ trong vòng một giờ
    ([Unity, Integration testing](https://docs.unity.com/en-us/grow/levelplay/sdk/unity/integration-testing)).

  MAX không cần ID này: tester bật test ads trong Mediation Debugger (nút bên dưới)
  ([AppLovin, Test Mode](https://support.applovin.com/en/max/unity/testing-networks/test-mode/)).
- Không đọc được ID thì cửa sổ ghi lý do thay cho ID:
  - Android: người dùng đã xóa advertising ID (Settings → Privacy → Ads), hoặc
    Google Play services lỗi (cửa sổ ghi lỗi nó trả về).
  - iOS: app chưa được phép theo dõi (ATT), nên IDFA toàn số 0. Dòng **Tracking
    (ATT)** ghi trạng thái. Trạng thái `Denied` có thêm nút mở Settings của app để
    bật lại. `Restricted` thì Screen Time hoặc quản lý thiết bị đang khóa quyền này,
    Settings của app không có công tắc để bật.
  - Editor và nền tảng khác: không có advertising ID.
- Mỗi mediation đang chạy có một nút mở công cụ debug của SDK đó:

  | Mediation | Công cụ |
  |---|---|
  | AdMob | Ad Inspector (chỉ mở trên máy đã đăng ký test) |
  | MAX | Mediation Debugger |
  | LevelPlay | Test Suite |

  Công cụ không mở được thì dòng chữ dưới nút ghi việc cần làm. Trên Android công
  cụ là Activity riêng, game bị pause; khi cửa sổ đang mở, pause đó không tính là
  người chơi rời game, nên quay về không bật app open ad. LevelPlay chỉ mở
  Test Suite khi được báo trước lúc khởi tạo: lần bấm đầu trên một máy chỉ lưu cờ
  vào `PlayerPrefs` (`RiseOn.Ads.LevelPlay.TestSuite`). Tester đóng hẳn app rồi mở
  lại, từ đó nút mở Test Suite. Cờ ở lại trên máy tới khi xóa dữ liệu app.

## Lớp và chạm

- Cửa sổ là một canvas uGUI `Screen Space - Overlay`, sorting order 32767, có lớp
  nền mờ phủ cả màn hình. Lớp nền nhận mọi chạm qua EventSystem, nên nút uGUI và
  raycaster của game nằm dưới không nhận được. Code của game đọc chạm thẳng từ
  `Input` hay Input System thì vẫn nhận, game tự bỏ qua khi `AdsDebugger.IsShowing`.
- Scene chưa có EventSystem thì cửa sổ tự tạo một cái (dùng
  `InputSystemUIInputModule` khi project bật Input System, không thì
  `StandaloneInputModule`) và xóa nó khi đóng.
- Banner, MRec và native là view của nền tảng, nằm **trên** mọi thứ Unity vẽ, kể
  cả cửa sổ này. Ẩn các ad đó trước khi mở cửa sổ: chạm vào chỗ ad đè lên là bấm
  vào ad, một click người chơi không định bấm.
