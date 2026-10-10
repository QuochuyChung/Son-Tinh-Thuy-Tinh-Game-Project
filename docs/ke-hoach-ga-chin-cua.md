# Kế hoạch: Gà Chín Cựa — bọn cướp Ninja trên `Map_SonTinh` (09/10)

Trạng thái (09/10): **đã làm xong, đã chạy thử trong Play** (nhánh `feat/ninja`, chưa commit). Kết quả và cách dùng: `docs/progress.md` mục 9.20.

Bạn đã chốt (09/10): không đụng skill Sơn Tinh; 2 Ninja có thanh máu nhỏ trên đầu; Ninja đỡ được đòn thường (chuột trái, sau này J), không đỡ được skill U / I / O.
Làm theo mặc định (bạn chưa trả lời, đổi được): bọn cướp áo đen ("mang về cho chủ nhân"), vòng đá tảng chỗ dừng 2, thủ lĩnh đứng ngoài không đánh được, bắt gà ~20–40 s, cutscene không có nút bỏ qua riêng (Esc của khung thoại vẫn bỏ qua được phần còn lại). Khiên: chưa trả lời, đang để tay trái trống.

## 0. Đã kiểm tra đồ bạn chuẩn bị (`Characters/Ninja-NPC_Character`)

| File | Nội dung |
|---|---|
| `Ninja_Character_NPC/…_generate.fbx` + `…_texture_fbx/` | Ninja "The Shadow Nomad": mesh xám 10572 đỉnh, mesh màu 10571 đỉnh (lệch 1 đỉnh → chép weight theo đỉnh gần nhất như Hùng Vương) |
| 11 file animation | Đều là **Mixamo, đã rig sẵn** (49 xương `mixamorig:`), 30 fps, gắn trên mesh xám |
| `Ninja-Sword_Skin/` | Thanh kiếm, mesh rời, không xương → gắn vào xương tay phải bằng code |

Độ dài clip (khung 30 fps): Draw 16 · Great Sword Impact 21 · Idle 77 · Impact 30 · Attack 74 · Death 70 · Kick 37 · Run 22 · Slash 51 · Strafe 21 · Two-Hand Combo 99.

Lưu ý: các clip "Sword And Shield" có tay trái ở tư thế cầm khiên nhưng không có model khiên ❓ (để tay trống, hay thêm một cái khiên/bao kiếm?).

## 1. Cốt truyện + cutscene 3D (có lời thoại)

Địa điểm: **vòng đá tảng ở chỗ dừng thứ 2 trên map Sơn Tinh (z = 175)**, đúng chỗ đặt Gà Chín Cựa hiện nay ❓. Pickup cũ "đi vào là nhận" sẽ bị tắt (giống ngựa).

Kích hoạt: Sơn Tinh đi tới gần vòng đá (~25 m) → cutscene (Timeline + Cinemachine + khung thoại sẵn có, giống cảnh phán xử). Người chơi bấm Space/E để qua câu; có nút bỏ qua ❓.

| # | Cảnh (máy quay) | Diễn | Thoại (nháp, bạn sửa thoải mái) |
|---|---|---|---|
| 1 | Toàn cảnh vòng đá | Gà đang mổ thóc (clip **Eat**) | *(dẫn truyện)* "Giữa vòng đá cổ, Gà Chín Cựa đang mổ thóc…" |
| 2 | Từ trên cây nhìn xuống | 3 Ninja nhảy vào, đứng vây (Idle) | Thủ lĩnh: "Gà chín cựa đây rồi! Bắt lấy, mang về cho chủ nhân." |
| 3 | Cận thủ lĩnh | Thủ lĩnh túm gà, gà vùng vẫy (clip **Fly** khi bị ôm) | Ninja 1: "Dễ như trở bàn tay!" |
| 4 | Sau lưng Sơn Tinh | Sơn Tinh bước vào | Sơn Tinh: "Dừng tay! Gà chín cựa là sính lễ dâng Vua Hùng. Bỏ nó xuống!" |
| 5 | Hai Ninja | quay lại, nhìn Sơn Tinh | Ninja 2: "Một mình ngươi mà đòi cản bọn ta sao?" |
| 6 | Thủ lĩnh lùi ra mép vòng đá, ôm gà | | Thủ lĩnh: "Hai đứa bay, xử nó! Ta giữ con gà." |
| 7 | Hai Ninja **rút kiếm** (clip Draw) | | Sơn Tinh: "Vậy thì để núi rừng phân xử!" |
| → | Trả điều khiển, vào trận | | |

❓ Bọn Ninja là ai? Gợi ý: **tay sai của Thủy Tinh** cho thêm kịch tính (thủ lĩnh nói "…mang về cho Thủy Tinh đại vương"), hoặc chỉ là bọn cướp áo đen. Đặt tên thủ lĩnh không?

## 2. Trận đánh: 2 Ninja đánh, 1 Ninja ôm gà đứng ngoài

**AI Ninja (NPC, máy điều khiển)** — mỗi Ninja một máy trạng thái C#, Animator chỉ "diễn" (giống Sấu Chín Đuôi):

| Tình huống | Clip | Ghi chú |
|---|---|---|
| Bắt đầu trận | `Draw_A_Great_Sword_1` | Kiếm đeo sau lưng, giữa clip chuyển kiếm sang tay phải |
| Người chơi ở xa (> ~6 m) | `Run` | Dí theo |
| Gần, đang chờ đánh | `Strafe` | Đi vòng quanh người chơi, mặt luôn hướng vào người chơi |
| Người chơi quá sát (< ~1,3 m) | `Kick` | Đá văng người chơi ra, sát thương nhỏ |
| Đòn thường | `Attack` (đòn nhảy) | Đỡ được |
| Đòn thường mạnh hơn (gồng lên) | `Two-Hand-Sword-Combo` | Có báo trước (gồng lâu hơn), đỡ được |
| Đòn mạnh | `Slash` | Sát thương lớn nhất, có khoảng hở sau đòn để phản công |
| Bị đánh | `Sword_And_Shield_Impact` | Giật, **mất nhịp đánh** (bị ngắt đòn) → cho người chơi phản công |
| Đỡ đòn | `Great_Sword_Impact` | **Chỉ đỡ đòn đánh thường** của người chơi (không mất máu); **phép / skill vẫn mất máu** |
| Hết máu | `Death` | Nằm lại vài giây rồi mờ dần |
| Cutscene / đứng chờ | `Idle` | Cũng dùng cho thủ lĩnh đứng ôm gà |

- **Lượt đánh**: mỗi lúc chỉ 1 Ninja được ra đòn, Ninja kia Strafe chờ (kiểu game hành động thông thường, không bị kẹp 2 bên quá khó).
- **Đỡ đòn**: có tỉ lệ đỡ (ví dụ 30%, tăng khi người chơi spam đòn thường). Cần thêm cờ "đòn phép" vào `DamageInfo` để phân biệt đòn thường / phép của Sơn Tinh.
- **Số liệu khởi điểm** ❓: Ninja 120 máu; đòn thường 8, đòn gồng 12, Slash 18, Kick 5 (Sơn Tinh 100 máu). Chỉnh sau khi bạn chơi thử.
- Thanh máu nhỏ trên đầu mỗi Ninja ❓ (hay chỉ hiện số sát thương như hiện giờ).
- **Thua**: Sơn Tinh gục → hồi sinh ở mép vòng đá, 2 Ninja hồi đầy máu, đánh lại (không chiếu lại cutscene).

**Thủ lĩnh ôm gà**: đứng ở mép vòng đá, clip `Idle` + một tư thế ôm gà mình tự làm thêm trong Blender (tay vòng trước ngực; Mixamo không có clip ôm), gà gắn vào tay, vùng vẫy bằng clip **Fly**. Thủ lĩnh không đánh, không bị đánh (hoặc bị đánh thì né ra ❓).

Khi **cả 2 Ninja chết**: thủ lĩnh hét "Chết tiệt… rút!", **thả gà xuống**, chạy (`Run`) ra khỏi vòng đá theo đường núi rồi biến mất.

## 3. Bắt gà (bấm E)

- Gà thả xuống thì **chạy lung tung trong khu vòng đá** (không chạy vòng tròn): chọn điểm ngẫu nhiên trong vùng, đi tới (clip **Walk**), thỉnh thoảng dừng mổ (clip **Eat**), đổi hướng bất chợt.
- Người chơi tới gần (~4 m) thì gà **vỗ cánh phóng đi** một quãng ngắn theo hướng ngẫu nhiên tránh người chơi (clip **Fly**), không ra khỏi vòng đá.
- Gà **mệt dần**: sau vài lần phóng thì dừng mổ lâu hơn → dễ bắt; không bị kẹt cứng (ai cũng bắt được trong ~20–40 s) ❓.
- Đứng sát gà hiện "Nhấn E để bắt", bấm E → bắt được → banner **"Bạn đã nhận được Gà chín cựa"**, danh sách sính lễ +1 (dùng `GiftTracker` + hệ phím E sẵn có từ nhiệm vụ ngựa).

→ **Dùng đủ cả 3 animation của gà**: Eat (cutscene + lúc chạy loanh quanh), Fly (vùng vẫy khi bị ôm + phóng đi khi bị đuổi), Walk (đi loanh quanh).

## 4. Lưu trạng thái

Như nhiệm vụ ngựa (tĩnh, giữ khi ra vào map, reset khi chơi mới): Chưa gặp → Đã xem cutscene → Đã thắng Ninja → Đã bắt gà. Rời map giữa trận thì vào lại bắt đầu từ trận đánh (không xem lại cutscene). Đã bắt gà thì vòng đá trống, không có Ninja.

## 5. Việc kỹ thuật (thứ tự làm)

1. **Ninja model**: chép vào `ArtSource/Meshy/Ninja` + `ArtSource/Mixamo/Ninja`; chép weight Mixamo từ mesh xám sang mesh màu (lệch 1 đỉnh); kiểm tra 11 clip trên mesh màu, chụp ảnh từng clip.
2. **Kiếm**: gắn vào `mixamorig:RightHand` (đo tư thế cầm từ clip), bao kiếm sau lưng (`Spine2`), đổi chỗ giữa clip Draw.
3. **Clip ôm gà** cho thủ lĩnh (Blender, khung xương Mixamo) + điểm gắn gà.
4. **Unity**: `NinjaBuilder` (Humanoid, `AC_Ninja`, prefab, Health, collider), `NinjaEnemy` (AI ở mục 2), `DamageInfo` thêm cờ phép, thanh máu Ninja.
5. **Gà**: dùng lại `rooster.fbx` (Walk / Eat / Fly), thêm `RoosterRunner` (chạy lung tung + bắt bằng E).
6. **Cutscene**: Timeline + Cinemachine + thoại, dựng bằng `RoosterQuestBuilder` vào `Map_SonTinh` (object gốc `RoosterQuest`, chạy lại là dựng lại; tắt `Pickup_GaChinCua` cũ).
7. **Chạy thử** trong Play (test tự động như Sấu Chín Đuôi + ảnh), ghi `docs/progress.md`.

## 6. Cần bạn chốt (❓)

1. Ninja là tay sai Thủy Tinh hay bọn cướp thường? Có tên thủ lĩnh không?
2. Địa điểm: vòng đá tảng (chỗ dừng 2, z = 175) được không?
3. Khiên: để tay trái trống hay thêm khiên?
4. Thủ lĩnh ôm gà có bị đánh được không (hay đứng ngoài, không đánh tới)?
5. Độ khó: máu / sát thương ở trên được không? Có thanh máu trên đầu Ninja không?
6. Bắt gà khó cỡ nào (khoảng 20–40 giây)?
7. Cutscene có cho bỏ qua không?

## 7. Checklist (09/10)

- [x] Ninja: weight Mixamo → mesh màu (`tools/skin_ninja.py`), 11 clip Mixamo + clip ôm gà tự làm (`tools/pose_ninja_hold.py`)
- [x] Kiếm: cầm tay phải (ổ cắm tính từ xương bàn tay), đeo sau lưng, rút ra bằng clip Draw
- [x] `Ninja_Enemy.prefab`: Health 120, thanh máu nhỏ trên đầu, AI `NinjaEnemy` (Run / Strafe 2 phía / Kick / Attack / Combo / Slash / Impact / Block / Death), mỗi lúc 1 Ninja đánh
- [x] Đỡ đòn: chỉ đòn thường (`DamageInfo.IsNormalAttack`, đặt ở `PlayerAttackState`), skill không đỡ được
- [x] Cutscene 3D 7 câu thoại, máy quay đổi theo từng câu, người chơi đứng yên trong lúc xem
- [x] Thua → hồi sinh ở mép vòng đá, Ninja đầy máu, đánh lại (không chiếu lại cutscene)
- [x] Thắng → thủ lĩnh hét, thả gà, chạy mất; gà chạy lung tung (Walk / Eat / Fly), E để bắt, banner + sính lễ +1
- [x] Lưu trạng thái (ra vào map giữ tiến độ), tắt `Pickup_GaChinCua` cũ
- [x] Chạy thử trong Play (test tự động `RoosterQuestSmokeTest`), 0 lỗi
- [ ] Bạn chơi thử bằng phím thật (đánh, đỡ, bắt gà) và chỉnh độ khó
- [ ] Khiên cho tay trái (chờ bạn chọn)
