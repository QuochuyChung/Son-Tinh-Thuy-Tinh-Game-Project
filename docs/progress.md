# Tiến độ dự án — Sơn Tinh Thủy Tinh

*Cập nhật: 28/09/2026. Thiết kế tổng thể xem [game-overview.md](game-overview.md).*

## 1. Tổng quan theo Phase

| Phase | Trạng thái | Ghi chú |
|---|---|---|
| 0 — Setup | ✅ Xong | Unity 6000.3.24f1, URP, git + GitHub, Unity MCP, Cinemachine 3.1.7, Input System |
| 1 — Chốt thiết kế | ✅ Xong phần chính | Combat, nhánh sính lễ, arena đã chốt trong GDD |
| 2 — Asset pipeline | 🟡 Đang làm | 2 nhân vật + kiếm của Thủy Tinh xong; còn animation combat |
| 3 — Lập trình core | 🟡 Đang làm | Mốc 1 (di chuyển, camera, stamina, dodge) xong. Đã thêm: máu, chọn nhân vật, hội thoại + prologue, sính lễ, chuyển cảnh |
| 4 → 9 | ⬜ Chưa bắt đầu | |

**Luồng game chạy được hiện tại:** Menu chính → Prologue (kể chuyện Vua Hùng kén rể) → Chọn nhân vật → Sandbox_Combat (tạm thay cho map sính lễ, nhặt đủ 3 sính lễ test, có thanh máu/stamina thật + bấm Esc để tạm dừng).

## 2. Asset đã xong

### Nhân vật

| | Sơn Tinh | Thủy Tinh |
|---|---|---|
| Prefab dùng khi chơi | `Assets/Prefabs/Player/Player_SonTinh.prefab` | `Assets/Prefabs/Player/Player_ThuyTinh.prefab` |
| Model (chỉ phần hình) | `Assets/Prefabs/Characters/SonTinh.prefab` | `Assets/Prefabs/Characters/ThuyTinh.prefab` |
| Chiều cao | 1.9m tới đỉnh đầu (vương miện nhô thêm) | 1.9m tới đỉnh đầu |
| Vũ khí | Tay không (võ + VFX đất đá khi đánh) | Đại kiếm (Great Sword), cầm 2 tay |
| Animation hiện có | Idle / Walk / Run (tay không) | Great Sword Idle / Walk / Run (cầm vũ khí 2 tay) |
| Tốc độ đi / chạy | 2.01 / 6.66 m/s | 1.13 / 4.80 m/s |

- Thủy Tinh chạy chậm hơn Sơn Tinh ~27% vì animation cầm vũ khí nặng có bước chân ngắn hơn. Tốc độ code phải khớp tốc độ bước chân của animation, nếu không chân sẽ bị trượt. Bù lại, Đại kiếm cho vùng chém rộng và lực đánh mạnh hơn. Nếu muốn 2 nhân vật chạy nhanh bằng nhau thì chỉnh lại được.
- File gốc (chưa import) nằm trong `ArtSource/` (Concept, Meshy, Mixamo), không nằm trong `Assets/`.

### Vũ khí

- **Kiếm của Thủy Tinh**: `Assets/Prefabs/Weapons/Sword.prefab` (mesh + texture trong `Assets/Art/sword/`, material `Mat_Sword`). Dài 1.5m, scale đều 3 trục đúng tỷ lệ ảnh concept `ArtSource/Concept/kiem_cua_thuy_tinh.jpg`. Gốc prefab đặt ở tay cầm (20% chiều dài tính từ chuôi), mũi kiếm hướng +Y.
- Kiếm gắn vào xương `mixamorig:RightHand` **bên trong prefab** `Player_ThuyTinh` (không gắn trong scene), nên dùng prefab ở scene nào cũng có kiếm. Trục kiếm đo từ vị trí 2 bàn tay trong animation Great Sword Idle, cạnh lưỡi cùng hướng đốt ngón tay.
- Đinh Ba cũ (`Assets/Prefabs/Weapons/DinhBa.prefab`) đã gỡ khỏi Thủy Tinh, file vẫn giữ lại nhưng không còn dùng.

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
| `DevTools/DebugHUD.cs` | Hiện state, tốc độ, máu, stamina, i-frame góc trên trái (chỉ để test). Bấm **K** để tự trừ 10 máu |
| `Combat/Health.cs`, `Combat/DamageInfo.cs` | Máu: `TakeDamage(DamageInfo)`, `Heal`, `Revive`, sự kiện `Changed` / `Damaged` / `Died`. Đang né (i-frame) thì không mất máu. `DamageInfo.IsHeavy` để sau này phân biệt flinch / stagger |
| `Characters/CharacterDefinition.cs`, `CharacterRoster.cs`, `CharacterId.cs` | Dữ liệu nhân vật (tên, danh hiệu, mô tả, prefab, **scene nhánh sính lễ riêng**). Asset trong `Assets/Data/Characters/` |
| `Player/PlayerSpawner.cs` | Đặt vào scene gameplay: tự spawn nhân vật đã chọn và tự gắn camera + DebugHUD. Chạy thẳng scene (không qua màn chọn) thì dùng `Fallback Character` |
| `UI/CharacterSelect/*` | Màn chọn nhân vật: 2 nhân vật đứng trên sân khấu có đèn rọi, chọn bằng phím mũi tên / tay cầm / chuột |
| `Dialogue/DialogueRunner.cs`, `DialogueSequence.cs`, `DialogueLine.cs` | Hệ thống hội thoại: chữ hiện dần, Space / Enter / click / nút A để tiếp, Esc / Start để bỏ qua. Mỗi câu có thể kèm 1 ảnh minh hoạ toàn màn hình |
| `Dialogue/PrologueDirector.cs` | Mở đầu game: tiêu đề → kể chuyện (`Assets/Data/Dialogue/Dialogue_Prologue.asset`) → sang màn chọn nhân vật |
| `Quest/GiftItem.cs`, `GiftQuest.cs`, `GiftTracker.cs` | Sính lễ: mỗi món là 1 asset (`Assets/Data/Gifts/`), `Quest_SinhLe` liệt kê 3 món cần mang về. Tiến độ giữ nguyên khi chuyển scene |
| `Quest/GiftPickup.cs` | Vùng trigger: nhân vật đi vào là nhận sính lễ. Có sự kiện `Collected` để mở cổng / bật việc tiếp theo |
| `Quest/GiftTrackerHUD.cs` | Danh sách sính lễ góc trên phải + thông báo "Đã có ..." / "Đã đủ sính lễ!" |
| `Flow/SceneLoader.cs` | Chuyển scene có mờ đen: `SceneLoader.Load("TenScene")`. Không cần đặt gì vào scene |
| `Flow/SceneTransitionTrigger.cs` | Lối ra của map: đi vào là sang scene khác, có thể khoá tới khi đủ sính lễ |
| `Flow/GameSession.cs`, `SceneNames.cs` | Nhân vật đã chọn + tên các scene |
| `UI/GameplayHUD.cs` | Thanh máu (đổi màu xanh→vàng→đỏ) + thanh stamina thật, thay cho DebugHUD chỉ để test |
| `UI/PauseMenu.cs` | Bấm Esc để tạm dừng: dừng thời gian, mở khoá chuột, hiện menu Tiếp tục / Về màn hình chính / Thoát |
| `UI/MainMenu/MainMenuController.cs` | Màn hình chính: Bắt đầu (vào Prologue) / Thoát |

**Phím** (`Assets/Input/PlayerControls.inputactions`): WASD / cần trái để di chuyển, chuột / cần phải để xoay camera, Space / B để dodge. LMB/RMB (đánh nhẹ / nặng) và Q / chuột giữa (lock-on) đã khai báo sẵn nhưng chưa có code.

**Chữ tiếng Việt**: dùng TextMeshPro với font Roboto (`Assets/Art/Fonts/`, đã đặt làm font mặc định). Atlas đã nướng sẵn đủ chữ có dấu nên file không bị đổi mỗi lần Play. Font này **không có ký tự mũi tên ← →** và dấu ✓.

## 4. Cách test

**Chơi cả luồng game:** mở `Assets/Scenes/MainMenu.unity` → Play → click vào cửa sổ Game → Bắt đầu → Space để đọc tiếp (Esc bỏ qua) → chọn nhân vật → vào Sandbox. Trong lúc chơi bấm Esc để tạm dừng / thoát ra menu chính.

**Chỉ test combat / di chuyển:**

1. Mở scene `Assets/Scenes/Sandbox_Combat.unity` (sàn caro, mỗi ô 1m).
2. Bấm Play, **click vào cửa sổ Game** (không click thì Unity không nhận phím).
3. WASD chạy, chuột xoay camera, Space né, Esc mở menu tạm dừng (nhả chuột), K tự trừ máu.
4. 3 khối vàng phát sáng là sính lễ test (ngựa / gà / voi), chạy vào là nhặt.

Nhân vật không còn đặt sẵn trong scene nữa mà do object `PlayerSpawn` tạo ra khi Play. **Đổi nhân vật để test**: chọn `PlayerSpawn` → đổi `Fallback Character` (Sơn Tinh / Thủy Tinh), không cần gán lại camera hay HUD.

**Làm scene gameplay mới** (map sính lễ, arena): copy 3 object `PlayerFollowCamera`, `Main Camera`, `PlayerSpawn` từ Sandbox sang, rồi kéo `PlayerFollowCamera` vào 2 ô trên `PlayerSpawner`. Muốn có thanh máu/stamina và menu tạm dừng thì copy thêm `PlayerHUD`, `PauseCanvas`, `PauseMenu` và **`EventSystem`** (thiếu EventSystem thì menu tạm dừng hiện lên nhưng bấm chuột không trúng nút gì cả). Thêm scene vào Build Settings và `Flow/SceneNames.cs`. Map sính lễ của từng nhân vật thì điền tên scene vào `Gift Branch Scene` của `Character_SonTinh` / `Character_ThuyTinh`.

## 5. Quyết định kỹ thuật đã chốt

- **Di chuyển bằng code, không dùng root motion**: dodge cần quãng đường/i-frame chính xác, lock-on cần đi ngang. Animation Mixamo là thư viện chung nên root motion không đem lại lợi thế gì. Có thể bật root motion riêng cho 1–2 đòn đặc biệt sau này.
- **Animator chỉ để "diễn"**: state machine C# quyết định mọi thứ và gọi `CrossFadeInFixedTime` theo tên state. Dùng chung 1 controller `Assets/Animations/Shared/AC_Humanoid_Base.controller`, mỗi nhân vật đổi clip qua Override Controller (`AOC_ThuyTinh`).
- **Blend tree theo tham số `LocomotionBlend`** (0 = Idle, 1 = Walk, 2 = Run). Mỗi prefab tự khai báo `walkSpeed` / `runSpeed` khớp animation của mình, nên 2 nhân vật có tốc độ khác nhau vẫn dùng chung controller mà không trượt chân.
- **Vũ khí bất đối xứng**: Sơn Tinh tay không, Thủy Tinh dùng Đại kiếm (Great Sword).
- **Mỗi nhân vật có bản sao bộ phím riêng** (`PlayerInputReader` tự nhân bản `PlayerControls` khi khởi tạo). Nếu dùng chung 1 file, xoá hoặc tắt 1 nhân vật (hồi sinh, chọn nhân vật, chuyển scene) sẽ tắt phím của tất cả.
- **Dữ liệu tách khỏi code bằng ScriptableObject** (`Assets/Data/`): lời thoại, sính lễ, thông tin nhân vật đều sửa được trong Inspector mà không cần đụng code. Thêm đoạn hội thoại mới: chuột phải → Create → Son Tinh Thuy Tinh → Dialogue Sequence.
- **Nhân vật ở màn chọn lấy thẳng từ prefab Player** (chỉ phần hình + Animator + vũ khí), nên đổi model / vũ khí trong prefab là màn chọn tự cập nhật theo.

## 6. Quy trình tạo asset (làm lại cho asset mới)

1. **Meshy** (tài khoản Pro): model **T2**, **Smart Topology**, texture ON. Nhân vật ~8000 mặt, A-pose. Vũ khí/đồ vật ~4000 mặt, không cần pose, **không cần qua Mixamo**.
2. **Trước khi đưa lên Mixamo**: dùng Blender tách material khỏi chính file có texture (script `strip_material.py`). **Không** tắt texture rồi bấm "Tạo ra" lại trên Meshy, vì làm vậy ra một mesh khác hẳn và mất UV.
3. **Mixamo**: FBX for Unity. Lần đầu rig nhân vật chọn With Skin. Animation thêm cho nhân vật đã có thì With Skin hay Without Skin đều dùng được.
4. **Unity**: set Rig = Humanoid. Clip thêm vào thì dùng lại Avatar của file idle gốc (Copy From Other Avatar). Bật Loop Time. Gộp metallic + roughness thành 1 ảnh cho URP.
5. **Đo chiều cao** bằng xương `HeadTop_End`, không dùng khung bao của Renderer (khung bao rộng hơn mesh ~15%).
6. **Gắn vũ khí vào tay**: tạo prefab vũ khí riêng (gốc đặt ở tay cầm, scale đều 3 trục), rồi gắn vào xương tay **trong prefab nhân vật**, không gắn thẳng trong scene. Phải kiểm tra trong Play mode thật (giả lập tư thế trong Edit mode cho kết quả sai). Vũ khí là con của xương tay nên phải đặt `localScale = 1 / scale của model`, nếu không sẽ bị phóng to theo model.

## 7. Vấn đề đã biết

- Dodge đang mượn tạm animation Run (nhìn như lướt nhanh), cần tải animation lăn/né thật.
- Thủy Tinh khi đứng/đi cầm 2 tay: tay trái đặt trên cán nhưng lệch vài cm (chưa làm IK 2 tay). Đã đo: lúc chạy kiếm không cạ vào vai/tóc; lúc đứng/đi chuôi kiếm chạm nhẹ vạt áo dưới 1cm.
- Mesh kiếm rất nặng: **239.000 tam giác** (gấp ~30 lần cả nhân vật). Nên giảm còn ~4.000–8.000 (tạo lại trên Meshy theo công thức vũ khí ở mục 6, hoặc decimate bằng Blender), giữ nguyên tỷ lệ để khỏi phải canh lại tay cầm.
- File phím mẫu `Assets/InputSystem_Actions.inputactions` của template vẫn đang được đặt làm "project-wide actions" trong Project Settings. Game không dùng tới, vô hại, có thể gỡ khi dọn project.
- **Cần chốt**: GDD ghi dùng HDRP nhưng project đang chạy **URP**. Đề xuất giữ URP (nhẹ, đủ đẹp cho phong cách stylized; đổi sang HDRP phải chuyển lại toàn bộ material).

## 8. Việc tiếp theo — chia theo người, làm xong hết là đủ game

Đã chia sao cho **không ai chờ ai** (trừ mục 8.1 nên làm trước vì mở khoá nhiều việc khác). Ai rảnh nhận việc nào thì ghi tên vào bảng Claude Doc (link cuối file).

### 8.1 Animation combat trên Mixamo — ƯU TIÊN SỐ 1, làm trước tiên

Đây là việc mở khoá nhiều nhất: xong việc này thì code Combo / Trúng đòn / Chết / Boss AI mới làm được. Tải theo đúng pipeline mục 6 (chọn Humanoid, dùng lại Avatar của file idle gốc). Mỗi dòng "Cả 2" nghĩa là **tải riêng cho từng nhân vật** (Mixamo retarget theo từng người), nên tổng cộng khoảng 12 file FBX:

| # | Animation | Nhân vật | Từ khoá tìm trên Mixamo |
|---|---|---|---|
| 1 | Combo đánh nhẹ x3 | Sơn Tinh (tay không) | "Martial Arts", "Punch Combo" |
| 2 | Đòn đánh nặng | Sơn Tinh (tay không) | "Heavy Punch", "Uppercut" |
| 3 | Combo chém x3 | Thủy Tinh (kiếm) | trong bộ **"Great Sword"** (bộ đã dùng cho Idle/Walk/Run) |
| 4 | Đòn chém nặng | Thủy Tinh (kiếm) | cũng trong bộ "Great Sword" |
| 5 | Né/lăn thật (đang mượn tạm anim Run) | Cả 2 | "Roll", "Dodge" |
| 6 | Trúng đòn nhẹ (flinch) | Cả 2 | "Hit Reaction" |
| 7 | Loạng choạng (stagger) | Cả 2 | "Stagger", "Impact" |
| 8 | Chết (death) | Cả 2 | "Death", "Dying" |

Tải xong đưa Claude import + rig là dùng được ngay.

### 8.2 Map sính lễ Sơn Tinh (núi/hang) — cần 1 người dựng trong Unity

- **Animation cần**: không bắt buộc — Sơn Tinh đã đủ Idle/Walk/Run/Dodge để chạy map này. Chỉ cần animation combo (#1, #2 ở trên) nếu muốn đoạn mini-combat (gà chín cựa) đánh nhau có cảm giác thật, không có cũng test traversal/nhặt đồ được bình thường.
- **Setup thủ công**:
  1. Tạo scene mới, copy nguyên cụm `PlayerFollowCamera` + `PlayerSpawn` + `PlayerHUD` + `PauseCanvas` + `PauseMenu` + `EventSystem` từ `Sandbox_Combat` sang (xem mục 4 "Làm scene gameplay mới" — thiếu `EventSystem` thì menu tạm dừng không bấm được).
  2. Đổi `Fallback Character` trên `PlayerSpawn` = Sơn Tinh.
  3. Dựng blockout núi/hang, tuyến tính ~150-200m, 3 điểm dừng theo đúng thứ tự: ngựa chín hồng mao (traversal) → gà chín cựa (mini-combat) → voi chín ngà (tương tác nguyên tố "phép đất").
  4. Đặt 3 `GiftPickup` tại 3 điểm, gán đúng asset trong `Assets/Data/Gifts/` (`Gift_NguaChinHongMao`, `Gift_GaChinCua`, `Gift_VoiChinNga`).
  5. Đặt `SceneTransitionTrigger` ở cuối map, gán `Required Quest` = `Assets/Data/Gifts/Quest_SinhLe.asset` (khoá tới khi đủ 3 sính lễ), `Scene Name` = tên scene cutscene phán xét (Claude sẽ báo tên khi dựng xong).
  6. Báo Claude tên scene để thêm vào Build Settings + gán vào `Gift Branch Scene` của `Character_SonTinh.asset`.

### 8.3 Map sính lễ Thủy Tinh (sông/đầm) — cần 1 người khác dựng

Y hệt mục 8.2, chỉ đổi: theme sông/đầm (Ngựa = băng đầm lầy bằng khúc gỗ nổi, Gà = cồn đất giữa sông, Voi = đền ngập nước dùng "phép nước"), `Fallback Character` = Thủy Tinh, gán vào `Gift Branch Scene` của `Character_ThuyTinh.asset`.

### 8.4 Tạo hình còn thiếu: Vua Hùng, Mị Nương

Hai người này **chưa qua bước tạo hình nào** (không có trong ArtSource). Họ không phải nhân vật chơi được, chỉ xuất hiện qua lời thoại ở Prologue và cutscene phán xét, nên chọn 1 trong 2 mức rồi báo Claude:

- **Mức tối thiểu (đủ dùng, đỡ tốn công)**: chỉ cần 1 ảnh 2D/người, thả vào ô `Illustration` của dòng thoại (xem mục 8.5) — không cần model 3D, không cần animation.
- **Mức đầy đủ**: model 3D đứng yên trong cutscene phán xét, qua đúng pipeline mục 6 (ảnh concept → Meshy → Mixamo) nhưng **không cần animation combat**, chỉ 1 pose đứng/ngồi ngai — tốn công ít hơn nhân vật chơi được nhiều.

### 8.5 Tranh minh hoạ cho Prologue / cutscene phán xét (không bắt buộc)

Hệ thống đã code xong 100%, không cần sửa gì thêm — `DialogueLine.illustration`: dòng thoại nào gán ảnh thì hiện full màn hình phía sau chữ, dòng không gán thì giữ nguyên ảnh của dòng trước. Muốn làm thì cần:

- Không cần đủ 1 ảnh/1 câu — tính theo khúc cảnh. Prologue 12 câu thì áng chừng **5-6 ảnh** là đủ (làng Văn Lang mở đầu, Mị Nương, Vua Hùng ban lệnh, Sơn Tinh xuất hiện, Thủy Tinh xuất hiện, Vua Hùng ra điều kiện sính lễ).
- Tỉ lệ khung hình gần **16:9** (kiểu 1920x1080) để không bị viền đen 2 bên.
- **Vua Hùng/Mị Nương phải giữ đúng 1 mặt/trang phục xuyên suốt** vì xuất hiện ở cả 2 cutscene — tạo 1 ảnh gốc trước rồi dùng chính ảnh đó làm **ảnh tham chiếu** cho các lần vẽ sau (ChatGPT/Gemini đều hỗ trợ "vẽ tiếp dựa theo ảnh này"), đừng tạo mỗi ảnh từ đầu không tham chiếu vì dễ lệch mặt.
- Không cần né chỗ đặt chữ khi vẽ — khung chữ đã có nền đen mờ đủ đậm (82%), ảnh phía sau sáng/rối cỡ nào chữ vẫn đọc được.
- Có ảnh thì gửi Claude, Claude import (đổi đúng loại Sprite) + gán vào từng dòng thoại, không cần làm tay trong Editor.
- *Lưu ý*: phần hiện ảnh này viết xong chưa test với ảnh thật (chỉ mới test phần chữ) — nên gửi thử 1 ảnh bất kỳ trước để Claude xác nhận hiển thị đúng, rồi mới đầu tư vẽ cả bộ.

### 8.6 Unity AI Generate (tạo asset bằng prompt ngay trong Editor) — đang bị khoá

Unity 6 có sẵn công cụ AI tạo skybox / texture địa hình / material / model 3D đơn giản (đá, bệ thờ, tượng voi-gà-ngựa...) / âm thanh / ảnh 2D / animation người từ prompt văn bản, ngay trong Editor. Đã thử nhưng **chưa dùng được** vì project chưa liên kết Unity Cloud:

- Ai rảnh vào **Edit → Project Settings → Services** → chọn Organization → **Create project ID** (hoặc link vào project có sẵn) để mở khoá.
- **Mỗi lần tạo tốn credit Unity AI** — liên kết xong coi số dư trước khi tạo nhiều.
- Liên kết xong báo Claude, Claude thử tạo mẫu (skybox, texture đất đá, model tượng voi chín ngà, âm thanh vung kiếm) để coi chất lượng trước khi dùng nhiều.
- Đây là hướng có thể thay thế việc tự đi tải Asset Store cho phần "dựng map muốn đẹp" (xem trao đổi trước) và có thể phụ giúp phần 8.4/8.5 (tạo hình, ảnh minh hoạ) nếu chất lượng ổn — nhưng vẫn cần thử mới biết, chưa chắc.

### 8.7 Code mốc 2 (Claude tự làm, chờ mục 8.1 xong)

Combo đánh nhẹ + đánh nặng, trúng đòn (flinch/stagger, nối vào `Health.Damaged`), chết (nối vào `Health.Died`), khung AI cho boss, lock-on + đi ngang (strafe).

### 8.8 Arena trận đánh cuối + Cutscene Hùng Vương phán xét (Claude tự làm)

Arena cần đủ animation ở mục 8.1 để đấu boss có cảm giác thật. Cutscene phán xét không cần animation, chỉ chờ quyết định mục 8.4.

### 8.9 Chờ, chưa làm được

2 đoạn epilogue: chờ tới khi Arena đấu được trọn vẹn mới viết kết phù hợp.

---
Bảng việc chi tiết + phân người: [Claude Doc](https://claude.ai/code/artifact/9b63c56a-f193-4db3-a892-4930ebe64584)
