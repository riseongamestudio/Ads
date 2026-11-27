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
  vài layout đại diện — mặt full-screen (`EditorAd.Card.cs`: ba
  `FullScreenLayout`, interstitial và end card phủ cả màn hình, app open là thẻ,
  dọc/ngang, thanh app, cùng thứ tự nhường của `FitCard`;
  creative giả định 16:9), half-screen xếp chồng (media dưới dải control,
  stack dồn đáy) và ô hình chữ nhật (scrim khi ước lượng band
  `48 + headline + CTA + gaps` không vừa, ngược lại cột media-top nuốt height
  thừa). Nó **không có** MEDIA_LEFT, ticker, icon-hero, thang marquee.
  Half-screen: creative đứng xem trong Editor ra thẻ xếp chồng, trên máy ra rail — kiểm
  side-media phải build lên máy.
- Vì preview là gương, mọi hằng số nó dùng phải **khớp máy thật**: 5dp
  padding ngang, đĩa control 30dp (dải control half 42dp), 72dp sàn icon-row, 120dp sàn media, ngưỡng
  tier 150/280dp, icon 24/36/48dp, gap 1/3/5dp, CTA `0.18×shortSide` kẹp
  24–36dp, badge `0.10×shortSide` kẹp 15px–18dp, scrim veil 70%, tỉ lệ thụt
  góc `1 − 1/√2`; thẻ app open: bo 20dp, lề 16dp, rộng ≤440/760dp, cao ngang
  ≤420dp; phủ cả màn hình: chữ và CTA rộng ≤560dp; cả hai: padding 20dp, icon 56/88/40dp, CTA 52dp, ảnh ≤58% bề rộng (ngang) / ≤60%
  chiều cao (dọc), thanh app cao 48dp ôm nội dung, tên ≤40% bề rộng. Sửa số bên Java/Obj-C++ thì
  sửa `EditorAd` cùng lượt.

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
| `FullScreenAd` | `FullScreen` | `short.MaxValue` |
| `HalfScreenAd` | `HalfScreen` | `short.MaxValue - 2` |
| `RectAd` | `RectAd` | `short.MaxValue - 4` |

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
  `HalfScreenAd.PanelHeight` trong Editor). Panel không nới theo nội dung,
  để `PanelHeight` khớp panel thật.
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
  `Backdrop` vì Mask đọc alpha của chính graphic.
- **`pixelsPerUnitMultiplier = canvas.referencePixelsPerUnit`**: uGUI chia
  border của sliced sprite cho `sprite.pixelsPerUnit / canvas.referencePixelsPerUnit`
  trước khi vẽ; sprite rasterize vài texel/unit nên tỉ lệ đó thổi border lên
  ~25–100× và mask thành cục tròn không cạnh thẳng. Multiplier triệt đúng nửa
  canvas của tỉ lệ.
- Khoản thụt góc tính **theo cách máy tính** — `ceil` bằng px — rồi chia
  `uiScale` sang canvas; như máy, scrim pad cộng khoản thụt góc (chữ đo từ mép
  ảnh đã lùi khỏi cung bo), badge lấy khoản thụt làm sàn.

## 6. Quy ước vẽ

- Media không có viền, như trên máy: ảnh đã có mép của nó, vành tối chỉ làm
  bẩn mép trên nền sáng và không thấy trên nền tối.
- Bo góc bằng sprite 9-slice rasterize từ distance field (`RoundedRectSprite`,
  `Round`) như ô hình chữ nhật: CTA là pill màu `Theme.Primary` (sáng `#0A57D1`,
  tối `#1A73E8`) không viền, icon bo 22% cạnh (cắt bằng `Mask`), badge Ad bo 4dp,
  close và timer là đĩa. Trên nền tối (game tối đi quanh thẻ app open, bản mờ
  của end card ở màn dọc, theme tối) đĩa trắng mờ, dấu sáng; trên theme sáng
  đĩa đen mờ, dấu tối.
- Dấu close **vẽ bằng hai nét** (capsule distance field) đúng hình học máy
  (`0.34` cạnh ngắn, nét 3dp), không gõ U+2715 (mỗi OEM một font). Close
  20sp, countdown 15sp trong box 30dp — close cố ý to hơn.
- Chữ trong box lấy cỡ **từ box** (tỉ lệ cap-height, có sàn/trần) vì cùng một
  preview vẽ ô 96dp lẫn full màn.
- AdChoices: giả định glyph 15dp; badge Ad 26×18dp. Ô hình chữ nhật: cả hai
  thụt theo khoản thụt góc; overlay full-screen: sát hai góc trên của ảnh
  (`PlaceBadgesOnPicture`). Trên máy dấu thật do SDK vẽ, hộp 15dp; Android dời
  nó vào bằng padding `NativeAdView` (README Android §7), iOS vẽ nó trong
  `GADAdChoicesView` của pack (README iOS §3).
- Màu mặt, chữ và nút theo theme sáng tối của Editor, cùng bảng màu với máy;
  game không đặt màu nào.
- Ô hình chữ nhật trong Editor vẽ body bị cắt vẫn hiện (ưu tiên số lượng
  element), khác máy (máy không bao giờ cắt, sẽ marquee).
