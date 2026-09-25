# Tiến độ dự án — Sơn Tinh Thủy Tinh

*Cập nhật: 25/09/2026. Thiết kế tổng thể xem [game-overview.md](game-overview.md).*

## 1. Tổng quan theo Phase

| Phase | Trạng thái | Ghi chú |
|---|---|---|
| 0 — Setup | ✅ Xong | Unity 6000.3.24f1, URP, git + GitHub, Unity MCP, Cinemachine 3.1.7, Input System |
| 1 — Chốt thiết kế | ✅ Xong phần chính | Combat, nhánh sính lễ, arena đã chốt trong GDD |
| 2 — Asset pipeline | 🟡 Đang làm | 2 nhân vật + Đinh Ba xong; còn animation combat |
| 3 — Lập trình core | 🟡 Đang làm | Mốc 1 (di chuyển, camera, stamina, dodge) xong |
| 4 → 9 | ⬜ Chưa bắt đầu | |

## 2. Asset đã xong

### Nhân vật

| | Sơn Tinh | Thủy Tinh |
|---|---|---|
| Prefab dùng khi chơi | `Assets/Prefabs/Player/Player_SonTinh.prefab` | `Assets/Prefabs/Player/Player_ThuyTinh.prefab` |
| Model (chỉ phần hình) | `Assets/Prefabs/Characters/SonTinh.prefab` | `Assets/Prefabs/Characters/ThuyTinh.prefab` |
| Chiều cao | 1.9m tới đỉnh đầu (vương miện nhô thêm) | 1.9m tới đỉnh đầu |
| Vũ khí | Tay không (võ + VFX đất đá khi đánh) | Đinh Ba 2m, cầm 2 tay |
| Animation hiện có | Idle / Walk / Run (tay không) | Great Sword Idle / Walk / Run (cầm vũ khí 2 tay) |
| Tốc độ đi / chạy | 2.01 / 6.66 m/s | 1.13 / 4.80 m/s |

- Thủy Tinh chạy chậm hơn Sơn Tinh ~27% vì animation cầm vũ khí nặng có bước chân ngắn hơn. Tốc độ code phải khớp tốc độ bước chân của animation, nếu không chân sẽ bị trượt. Bù lại, Đinh Ba cho tầm với xa hơn. Nếu muốn 2 nhân vật chạy nhanh bằng nhau thì chỉnh lại được.
- File gốc (chưa import) nằm trong `ArtSource/` (Concept, Meshy, Mixamo), không nằm trong `Assets/`.

### Vũ khí

- **Đinh Ba**: `Assets/Prefabs/Weapons/DinhBa.prefab`, gắn vào xương `mixamorig:RightHand` của Thủy Tinh (đã nằm sẵn trong prefab Player_ThuyTinh).

## 3. Code đã xong (`Assets/Scripts/`)

| File | Chức năng |
|---|---|
| `Core/StateMachine.cs` | Khung state machine dùng chung cho player và boss (sau này) |
| `Combat/Stamina.cs` | Stamina, hồi sau 0.8s, hết thì kiệt sức 1.5s (đúng GDD) |
| `Player/PlayerController.cs` | Di chuyển theo hướng camera, gia tốc, xoay người, trọng lực, đẩy tham số blend cho Animator |
| `Player/PlayerInputReader.cs` | Đọc input + nhớ lệnh bấm sớm 0.2s (input buffer) |
| `Player/DodgeSettings.cs` | Thông số dodge: 4m, 0.6s, i-frame 0.05–0.4s, tốn 25 stamina |
| `Player/States/PlayerLocomotionState.cs` | State đứng / đi / chạy |
| `Player/States/PlayerDodgeState.cs` | State né có i-frame |
| `CameraSystem/ThirdPersonCameraInput.cs` | Xoay camera góc thứ 3 bằng chuột / tay cầm (Cinemachine) |
| `DevTools/DebugHUD.cs` | Hiện state, tốc độ, stamina, i-frame góc trên trái (chỉ để test) |

**Phím** (`Assets/Input/PlayerControls.inputactions`): WASD / cần trái để di chuyển, chuột / cần phải để xoay camera, Space / B để dodge. LMB/RMB (đánh nhẹ / nặng) và Q / chuột giữa (lock-on) đã khai báo sẵn nhưng chưa có code.

## 4. Cách test

1. Mở scene `Assets/Scenes/Sandbox_Combat.unity` (sàn caro, mỗi ô 1m).
2. Bấm Play, **click vào cửa sổ Game** (không click thì Unity không nhận phím).
3. WASD chạy, chuột xoay camera, Space né, Esc nhả chuột.

Scene hiện đang đặt **Thủy Tinh**. Muốn test Sơn Tinh thì thay object `Player_ThuyTinh` bằng prefab `Player_SonTinh`, rồi gán lại 3 chỗ trỏ tới nhân vật: `PlayerFollowCamera` → Tracking Target (kéo `CameraTarget` của nhân vật vào), `PlayerFollowCamera` → ThirdPersonCameraInput → Input, và `DebugHUD` → Player. Quên gán chỗ nào là camera không xoay hoặc HUD không hiện.

## 5. Quyết định kỹ thuật đã chốt

- **Di chuyển bằng code, không dùng root motion**: dodge cần quãng đường/i-frame chính xác, lock-on cần đi ngang. Animation Mixamo là thư viện chung nên root motion không đem lại lợi thế gì. Có thể bật root motion riêng cho 1–2 đòn đặc biệt sau này.
- **Animator chỉ để "diễn"**: state machine C# quyết định mọi thứ và gọi `CrossFadeInFixedTime` theo tên state. Dùng chung 1 controller `Assets/Animations/Shared/AC_Humanoid_Base.controller`, mỗi nhân vật đổi clip qua Override Controller (`AOC_ThuyTinh`).
- **Blend tree theo tham số `LocomotionBlend`** (0 = Idle, 1 = Walk, 2 = Run). Mỗi prefab tự khai báo `walkSpeed` / `runSpeed` khớp animation của mình, nên 2 nhân vật có tốc độ khác nhau vẫn dùng chung controller mà không trượt chân.
- **Vũ khí bất đối xứng**: Sơn Tinh tay không, Thủy Tinh cầm Đinh Ba.
- **Mỗi nhân vật có bản sao bộ phím riêng** (`PlayerInputReader` tự nhân bản `PlayerControls` khi khởi tạo). Nếu dùng chung 1 file, xoá hoặc tắt 1 nhân vật (hồi sinh, chọn nhân vật, chuyển scene) sẽ tắt phím của tất cả.

## 6. Quy trình tạo asset (làm lại cho asset mới)

1. **Meshy** (tài khoản Pro): model **T2**, **Smart Topology**, texture ON. Nhân vật ~8000 mặt, A-pose. Vũ khí/đồ vật ~4000 mặt, không cần pose, **không cần qua Mixamo**.
2. **Trước khi đưa lên Mixamo**: dùng Blender tách material khỏi chính file có texture (script `strip_material.py`). **Không** tắt texture rồi bấm "Tạo ra" lại trên Meshy, vì làm vậy ra một mesh khác hẳn và mất UV.
3. **Mixamo**: FBX for Unity. Lần đầu rig nhân vật chọn With Skin. Animation thêm cho nhân vật đã có thì With Skin hay Without Skin đều dùng được.
4. **Unity**: set Rig = Humanoid. Clip thêm vào thì dùng lại Avatar của file idle gốc (Copy From Other Avatar). Bật Loop Time. Gộp metallic + roughness thành 1 ảnh cho URP.
5. **Đo chiều cao** bằng xương `HeadTop_End`, không dùng khung bao của Renderer (khung bao rộng hơn mesh ~15%).
6. **Gắn vũ khí vào tay**: phải kiểm tra trong Play mode thật (giả lập tư thế trong Edit mode cho kết quả sai). Vũ khí là con của xương tay nên phải đặt `localScale = 1 / scale của model`, nếu không sẽ bị phóng to theo model.

## 7. Vấn đề đã biết

- Dodge đang mượn tạm animation Run (nhìn như lướt nhanh), cần tải animation lăn/né thật.
- Thủy Tinh khi chạy vác Đinh Ba qua vai, cán hơi cạ vào vai/tóc (cán dài hơn thanh kiếm gốc của animation).
- Thủy Tinh khi đứng/đi cầm 2 tay: tay trái đặt gần cán nhưng không khít 100% (chưa làm IK 2 tay).
- File phím mẫu `Assets/InputSystem_Actions.inputactions` của template vẫn đang được đặt làm "project-wide actions" trong Project Settings. Game không dùng tới, vô hại, có thể gỡ khi dọn project.
- **Cần chốt**: GDD ghi dùng HDRP nhưng project đang chạy **URP**. Đề xuất giữ URP (nhẹ, đủ đẹp cho phong cách stylized; đổi sang HDRP phải chuyển lại toàn bộ material).

## 8. Việc tiếp theo

1. **Tải animation combat trên Mixamo**:
   - Sơn Tinh (tay không): 3 đòn nhẹ nối combo + 1 đòn nặng, tìm "Martial Arts" / "Punch" / "Kick".
   - Thủy Tinh: lấy trong bộ **Great Sword** cho đồng bộ kiểu cầm (vũ khí đã canh theo kiểu cầm này), gồm 3 đòn chém + 1 đòn nặng.
   - Cả 2: né/lăn (Thủy Tinh ưu tiên lấy trong bộ Great Sword nếu có), trúng đòn nhẹ, loạng choạng, chết.
2. **Code mốc 2**: máu (Health), combo đòn nhẹ + đòn nặng, trúng đòn (flinch / stagger), chết, lock-on + đi ngang (strafe).
3. **Chọn nhân vật**: spawn Sơn Tinh hoặc Thủy Tinh theo lựa chọn, thay cho việc đổi tay trong scene như hiện tại.
