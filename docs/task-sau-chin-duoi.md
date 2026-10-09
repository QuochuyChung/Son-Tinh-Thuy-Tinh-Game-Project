# Sấu Chín Đuôi — boss của map Thủy Tinh (09/10)

Nhánh local `feat/sau-chin-duoi` (tách từ `main` sau PR #5), **chưa commit / chưa push**.

Model: `Characters/Sau-Night-Tails_Character` (Meshy "Spinedrake Crocodylus"), chép vào `ArtSource/Meshy/SauChinDuoi/`
(`sau_generate.fbx` xám, `sau_mesh.fbx` có màu + 4 texture). Một mesh liền, không có xương: tự rig như ngựa.

Các lựa chọn mặc định (người dùng chưa chọn, có thể đổi): 1 đòn cắn + 3 chiêu (quét đuôi có vòng đỏ cảnh báo, phun nước,
đập chín đuôi ở phase 2), phase 2 khi còn 50% máu, thanh máu boss, thắng thì nhận vật phẩm sính lễ (banner như ngựa).

## Việc cần làm

### 1. Rig + animation (Blender headless, `tools/rig_sau.py`)
- [x] Kiểm mô hình: 7987 đỉnh, mesh xám = mesh màu từng đỉnh; 1 mảnh chính 6902 đỉnh + 27 mảnh nhỏ (gai, móng); 9 đuôi xoè hình quạt từ một gốc sau hông
- [x] Khung xương 56 xương (Generic): thân, hàm, 4 chân bò, 9 đuôi × (2 xương animate + 2 xương lò xo `Tail_<k>_<0|1>`)
- [x] Weight trên mesh xám → chép 1:1 sang mesh màu; mỗi đỉnh của quạt đuôi chỉ theo đuôi của nó
- [x] 10 clip: Idle, Walk, Charge, Bite, TailSweep, WaterSpit, TailSlam, Hit, Roar, Death
- [x] Ảnh từng clip (`clip_<tên>.png`), ảnh weight, rest, `stretch.txt`
- [x] Xuất `ArtSource/Rig/SauChinDuoi/sau_skinned.fbx` + `sau_rig.blend`

### 2. Unity: model + prefab (`Tools ▸ Son Tinh Thuy Tinh ▸ Build Sau Chin Duoi (boss model)`)
- [x] Import Generic, 10 clip (Idle / Walk / Charge lặp), material URP/Lit
- [x] `AC_SauChinDuoi` (state theo tên clip, code gọi CrossFade), prefab `Assets/Prefabs/Characters/SauChinDuoi.prefab`
- [x] OutfitSpringBones cho 9 chuỗi `Tail_`, collider thân

### 3. Unity: boss trong `Map_ThuyTinh`
- [x] Đấu trường: cồn giữa hồ (chỗ dừng thứ 2, z = 175) mở rộng thành bãi ~16 m, nước sâu bao quanh, dựng bằng gói Asset Store sẵn có
- [x] AI: ngủ dưới nước → người chơi vào bãi thì gầm (thanh máu hiện) → đi / lao tới, cắn, quét đuôi (vòng đỏ), phun nước
- [x] Phase 2 (≤ 50% máu): gầm, nhanh hơn, thêm đập chín đuôi (vòng đỏ lớn + vòng dưới chân người chơi)
- [x] Thanh máu boss trên đỉnh màn hình (vạch 50%)
- [x] Thắng: banner "Bạn đã nhận được …", sính lễ 1/1, cổng ra mở; vào lại map thì boss không còn
- [x] Thua: hồi sinh ở mép bãi, boss hồi đầy máu

### 4. Sính lễ của map Thủy Tinh
- [x] Bỏ 3 vật phẩm Gà / Ngựa / Voi trên map Thủy Tinh, thay bằng **một** vật phẩm hợp với vực nước: **Minh châu đáy vực** (viên ngọc sáng Sấu Chín Đuôi giữ dưới đáy vực)
- [x] `Quest_SinhLe_ThuyTinh` (1 món) cho HUD + cổng ra của map Thủy Tinh; ghim bản đồ chỉ còn 1

### 5. Kiểm tra
- [x] Biên dịch 0 lỗi, dựng lại map bằng batch mode
- [x] Ảnh đấu trường + boss trong Unity
- [x] Chạy thử trong Play (vào bãi, đánh, phase 2, thắng / thua) — bằng `SauChinDuoiSmokeTest` trong Editor chạy nền, chưa bấm phím thật
- [x] Ghi `docs/progress.md` (mục 9.19) + `tools/README.md`

## Còn lại / cần người dùng quyết
- [ ] Bấm phím thật đánh thử (test tự động gọi `Health.TakeDamage` thay cho đòn chém của người chơi)
- [x] Cung điện (09/10, theo yêu cầu): thoại phán xử nói chung chung "sính lễ", không liệt kê; danh sách sính lễ trên màn hình theo nhân vật đã chọn (`CharacterDefinition.giftQuest`)
- [ ] Prologue vẫn để Vua Hùng đọc đủ "voi chín ngà, gà chín cựa, ngựa chín hồng mao" (chờ người dùng quyết)
- [ ] Âm thanh (gầm, cắn, nước), hiệu ứng nước khi Sấu trồi lên / đập đuôi, camera khoá mục tiêu
- [ ] Viên minh châu chưa có model (chỉ là tên trong danh sách + banner)
