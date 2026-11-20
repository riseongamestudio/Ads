# RiseOn Ads AdMob Native — Editor

Assembly `RiseOn.Ads.AdMob.Native.Platforms.Editor`: cùng bộ client như hai nền tảng thật,
nhưng vẽ bằng uGUI (`Preview/EditorAd`) để developer **nhìn thấy** ad trong
play mode. Luật chung ở
[tài liệu core](../../README.md); file này ghi những gì **riêng Editor**.

## 1. Preview là gương, không phải máy bay mô phỏng

- Client Editor là **tức thời**: `Show` dựng ad ngay, báo
  `OnLoadingCompleted(1, 1)` ("một ad ấm, chỗ cho một") rồi `OnDisplayed`.
  Không có độ trễ load, không dwell, không xoay vòng, không scheduler. Preview
  không mô phỏng lifecycle (chủ dự án chốt, 516759f6): nó để xem ad **trông ra
  sao**, không để tái tạo độ trễ.
- **Không port layout engine sang Editor** (chủ dự án chốt): preview chỉ vẽ
  vài layout đại diện — overlay xếp chồng (media dưới dải control, stack dồn
  đáy) và ô hình chữ nhật (scrim khi ước lượng band `48 + headline + CTA + gaps`
  không vừa, ngược lại cột media-top nuốt height thừa). Nó **không có**
  MEDIA_LEFT, ticker, icon-hero, thang marquee. Creative đứng xem trong Editor
  ra thẻ xếp chồng, trên máy ra rail — kiểm side-media phải build lên máy.
- Vì preview là gương, mọi hằng số nó dùng phải **khớp máy thật**: 5dp
  padding ngang, 30dp control, 72dp sàn icon-row, 48/120dp sàn media, ngưỡng
  tier 150/280dp, icon 24/36/48dp, gap 1/3/5dp, CTA `0.18×shortSide` kẹp
  24–36dp, badge `0.10×shortSide` kẹp 15px–18dp, scrim veil 70%, tỉ lệ thụt
  góc `1 − 1/√2`. Sửa số bên Java/Obj-C++ thì sửa `EditorAd` cùng lượt.

## 2. asmdef: runtime trong Editor, vắng mặt trong build

`RiseOn.Ads.AdMob.Native.Platforms.Editor.asmdef`: `includePlatforms: []` +
`defineConstraints: ["UNITY_EDITOR"]` — **không** phải `includePlatforms:
["Editor"]`. Unity **cấm** `AddComponent` một MonoBehaviour thuộc assembly
editor-only ("Can't add script behaviour ... because it is an editor script")
→ `EditorAd` không gắn được, `GetComponent` trả null, NRE (93bc80be). Với
define constraint, asm được phân loại runtime nên attach được trong play mode,
còn `UNITY_EDITOR` không tồn tại khi build player nên vẫn bị loại — đúng
pattern asm test của Unity Test Framework. Bootstrap gác `Application.isEditor`.

## 3. Sorting order: thang xếp lớp bằng canvas

Trên máy mỗi tầng là window/Activity/subview riêng; trong Editor mọi thứ là
Screen Space Overlay và sorting order là thứ tự duy nhất. Ba số của pack
(`EditorSortingOrder`) đọc từ `NativeAdEditorLayers`, field public mà bên hiện
ad đặt trước khi tạo ad, để các lớp của nó (màn che…) chen vào giữa; pack
không biết lớp nào khác:

| Tầng | Field | Mặc định |
|---|---|---|
| `FULL_SCREEN_AD` | `FullScreen` | `short.MaxValue` |
| `HALF_SCREEN_AD` | `HalfScreen` | `short.MaxValue - 2` |
| `RECT_AD` | `RectAd` | `short.MaxValue - 4` |

Mặc định giữ ad xem trước trên game vẽ từ 0 trở lên, ad toàn màn hình trên tất
cả. Mediation `com.riseon.ads.admob` đặt ba số theo **Editor Sorting Order**
trên trang Ads (README `com.riseon.ads`, mục Trong Editor). Canvas của game
phải nằm **dưới** số nhỏ nhất — việc đó thuộc về game, pack không đụng tới
canvas của game.

## 4. Không pause; chiều cao và trượt của panel half

- Preview **không đổi `Time.timeScale`**, kể cả ad full: trên máy ad full tự
  pause như mọi SDK, nhưng trong Editor thứ dừng game (nếu có) là của package
  khác, và một ad đóng không được chạy lại game mà thứ khác đang giữ dừng.
- Chiều cao panel half theo **đúng công thức máy**:
  `min(Screen.height, max(Screen.height × ratio, 48dp × uiScale))`, pixel
  `Screen` (`EditorAd.ResolveHalfScreenPanelHeight`, cũng là
  `HalfScreenAd.PanelHeight` trong Editor). Không còn nới theo nội dung như
  bản cũ — nới thì `PanelHeight` sai với panel thật.
- Trượt: `anchoredPosition.y` của panel, `Time.unscaledDeltaTime`, 0.25s,
  ease bình phương hai chiều (phần còn ngoài màn khi vào được `p` là
  `(1 − p)²`, khi ra được `1 − p` cũng vậy → quay đầu tại chỗ không giật).
  Trong lúc trượt `CanvasGroup.interactable = false`: graphic vẫn chặn
  raycast nên chạm bị nuốt; đồng hồ đứng yên tới khi vào xong; `onDisplayed`
  bắn lúc đó. Đóng kiểu `RedirectOnClose` luôn `Finish` ngay.

## 5. Bo góc và mask

- Panel mang cung bằng **sliced sprite** rasterize từ distance field
  (`RoundedRectSprite(radius)`, cache theo bán kính texel); `Mask` cắt con
  theo cung, `showMaskGraphic = false`; nền nhìn thấy chuyển sang con
  `Backdrop` vì Mask đọc alpha của chính graphic — màu trong suốt (câu trả lời
  thật trên máy) sẽ xoá luôn mask.
- **`pixelsPerUnitMultiplier = canvas.referencePixelsPerUnit`**: uGUI chia
  border của sliced sprite cho `sprite.pixelsPerUnit / canvas.referencePixelsPerUnit`
  trước khi vẽ; sprite rasterize vài texel/unit nên tỉ lệ đó thổi border lên
  ~25–100× và mask thành cục tròn không cạnh thẳng. Multiplier triệt đúng nửa
  canvas của tỉ lệ.
- Khoản thụt góc tính **theo cách máy tính** — `ceil` bằng px — rồi chia
  `uiScale` sang canvas; sàn (không cộng) cho scrim pad và badge như máy.

## 6. Quy ước vẽ

- Viền element là **inner stroke** (bốn thanh vẽ trong mép, thanh dọc rút
  2×độ dày để không đè góc) như `GradientDrawable` stroke của Android; Unity
  `Outline` là outer, chủ dự án bác (87652fd1). CTA `#2196F3` viền `#1565C0`
  như máy, phần còn lại đen 35%.
- Dấu close **vẽ bằng hai nét** (capsule distance field) đúng hình học máy
  (`0.44` cạnh ngắn, nét 2.5dp), không gõ U+2715 (mỗi OEM một font). Close
  20sp, countdown 18sp trong box 30dp — close cố ý to hơn.
- Chữ trong box lấy cỡ **từ box** (tỉ lệ cap-height, có sàn/trần) vì cùng một
  preview vẽ ô 96dp lẫn full màn.
- AdChoices: giả định glyph 15dp; badge Ad 24×18dp; cả hai thụt theo khoản
  thụt góc (ô hình chữ nhật) hoặc safe-inset (overlay). Trên máy dấu thật do
  SDK vẽ, hộp 15dp; Android dời nó vào bằng padding `NativeAdView` (README
  Android §7), iOS bằng cách sửa hai constraint ghim khung của SDK (README
  iOS).
- Màu nền ô hình chữ nhật tôn trọng **alpha 0** như máy; không thay bằng màu
  nhà.
- Ô hình chữ nhật trong Editor vẽ body bị cắt vẫn hiện (ưu tiên số lượng
  element), khác máy (máy không bao giờ cắt, sẽ marquee).
