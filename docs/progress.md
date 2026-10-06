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

**Luồng game chạy được hiện tại:** Menu chính → Prologue (kể chuyện Vua Hùng kén rể) → Chọn nhân vật → **chọn Sơn Tinh thì vào `Map_SonTinh`** (map rừng, nhặt 3 sính lễ rồi đi tới cổng cuối map; xem mục 9.7), **chọn Thủy Tinh thì vào `Map_ThuyTinh`** (map đầm nước, xem mục 9.8). Cuối mỗi đường dốc lên một cổng đình, qua cổng là **`Map_HungVuong`** (cao nguyên cung điện Vua Hùng, ở giữa bản đồ thế giới; cung điện đang là chỗ giữ chỗ, xem mục 9.10), qua cổng cung điện là **cutscene phán xử `Cutscene_PhanXu`** (mục 9.12), rồi mới qua Sandbox_Combat (chỗ giữ tạm cho trận đánh cuối, có thanh máu/stamina thật + bấm Esc để tạm dừng). Cả 3 map có **vòng tròn bản đồ góc phải dưới**, bấm vào (hoặc phím M) để xem bản đồ chi tiết có mũi tên "bạn đang ở đây" (mục 9.9).

## 2. Asset đã xong

### Nhân vật

| | Sơn Tinh | Thủy Tinh |
|---|---|---|
| Prefab dùng khi chơi | `Assets/Prefabs/Player/Player_SonTinh_v2.prefab` (thiết kế mới, mão + áo choàng lông vũ; bản cũ `Player_SonTinh.prefab` giữ làm dự phòng, không còn dùng) | `Assets/Prefabs/Player/Player_ThuyTinh.prefab` |
| Model (chỉ phần hình) | `Assets/Prefabs/Characters/SonTinh_v2.prefab` | `Assets/Prefabs/Characters/ThuyTinh.prefab` |
| Chiều cao | 1.9m tới đỉnh đầu (mão lông vũ nhô thêm, tổng ~2.14m) | 1.9m tới đỉnh đầu |
| Vũ khí | Tay không (võ + VFX đất đá khi đánh) | Đại kiếm (Great Sword), cầm 2 tay |
| Animation hiện có | Idle / Walk / Run (tay không, bộ mới trong `Assets/Animations/SonTinh_v2/`, override `AOC_SonTinh_v2`) | Great Sword Idle / Walk / Run (cầm vũ khí 2 tay) |
| Tốc độ đi / chạy | 2.12 / 6.86 m/s | 1.13 / 4.80 m/s |

- Thủy Tinh chạy chậm hơn Sơn Tinh ~27% vì animation cầm vũ khí nặng có bước chân ngắn hơn. Tốc độ code phải khớp tốc độ bước chân của animation, nếu không chân sẽ bị trượt. Bù lại, Đại kiếm cho vùng chém rộng và lực đánh mạnh hơn. Nếu muốn 2 nhân vật chạy nhanh bằng nhau thì chỉnh lại được.
- File gốc (chưa import) nằm trong `ArtSource/` (Concept, Meshy, Mixamo), không nằm trong `Assets/`.
- **Sơn Tinh v2 (đổi thiết kế 01/10)** đã vào game qua `Assets/Data/Characters/Character_SonTinh.asset`. Cách dựng: xem [tools/README.md](../tools/README.md) (rig "ma-nơ-canh" rồi chép trọng số sang mesh đầy đủ). Lưu ý khi import vào Unity: (1) mỗi file animation phải dùng **avatar riêng** (`Create From This Model`), không dùng `Copy From Other Avatar` được vì cây xương của model có thêm node `Armature`; Humanoid vẫn tự chuyển clip sang model; (2) **vị trí Y của `Model` phải để 0**: lúc chạy animation Humanoid, chân luôn đứng trên mặt phẳng gốc của model, bù thêm độ cao theo tư thế gốc sẽ làm nhân vật lơ lửng; (3) material `M_SonTinh_v2` bật **Render Face: Both** vì áo choàng là tấm đơn; (4) tốc độ đi/chạy đo bằng cách cho Animator chạy 3 chu kỳ với root motion rồi chia quãng đường cho thời gian (cách này khớp lại giá trị cũ của Sơn Tinh 0.1%).

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

**Phím** (`Assets/Input/PlayerControls.inputactions`): WASD / cần trái để di chuyển, **giữ Shift (hoặc nhấn cần trái) để chạy nhanh (sprint)**, chuột / cần phải để xoay camera, **Ctrl trái / B để né (dodge)**, **Space / A để nhảy** (Space khi đang sprint = nhảy chạy), **C / nút vai phải để trượt (chỉ khi đang sprint)**, xem mục 9.11. LMB/RMB (đánh nhẹ / nặng) và Q / chuột giữa (lock-on) đã khai báo sẵn nhưng chưa có code.

**Chữ tiếng Việt**: dùng TextMeshPro với font Roboto (`Assets/Art/Fonts/`, đã đặt làm font mặc định). Atlas đã nướng sẵn đủ chữ có dấu nên file không bị đổi mỗi lần Play. Font này **không có ký tự mũi tên ← →** và dấu ✓.

## 4. Cách test

**Chơi cả luồng game:** mở `Assets/Scenes/MainMenu.unity` → Play → click vào cửa sổ Game → Bắt đầu → Space để đọc tiếp (Esc bỏ qua) → chọn nhân vật → vào Sandbox. Trong lúc chơi bấm Esc để tạm dừng / thoát ra menu chính.

**Chỉ test combat / di chuyển:**

1. Mở scene `Assets/Scenes/Sandbox_Combat.unity` (sàn caro, mỗi ô 1m).
2. Bấm Play, **click vào cửa sổ Game** (không click thì Unity không nhận phím).
3. WASD chạy, giữ Shift để sprint (nhả ra là về chạy thường), chuột xoay camera, **Space nhảy, Shift+Space nhảy chạy, Shift+C trượt, Ctrl trái né**, Esc mở menu tạm dừng (nhả chuột), K tự trừ máu, **L bị đánh gục ngay, R đứng dậy** (hai phím L/R chỉ có trong editor).
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
2. **Trước khi đưa lên Mixamo**: dùng Blender tách material khỏi chính file có texture bằng script [`tools/strip_material.py`](../tools/strip_material.py) (lệnh chạy nằm ở đầu file script). **Không** tắt texture rồi bấm "Tạo ra" lại trên Meshy, và **không** tạo thêm bản "không mặc đồ" riêng từ 1 ảnh 2D khác: mỗi lần Meshy tạo ra là 1 mesh khác hẳn (khác topology, khác UV), nên bản đã rig trên Mixamo không dùng chung được texture với bản có đồ. Nhân vật nhiều mảnh rời (lông vũ, áo choàng, giày/ống chân rời) mà Mixamo báo lỗi hoặc rig chân bị tật: dùng quy trình "ma-nơ-canh" trong [`tools/README.md`](../tools/README.md) (rig một thân sạch rồi chép trọng số sang mesh đầy đủ).
3. **Mixamo**: FBX for Unity. Lần đầu rig nhân vật chọn With Skin. Animation thêm cho nhân vật đã có thì With Skin hay Without Skin đều dùng được.
4. **Unity**: set Rig = Humanoid. Clip thêm vào thì dùng lại Avatar của file idle gốc (Copy From Other Avatar; riêng Sơn Tinh v2 mỗi file dùng avatar riêng, xem mục 2 và 9). Bật Loop Time. Gộp metallic + roughness thành 1 ảnh cho URP.
5. **Đo chiều cao** bằng xương `HeadTop_End`, không dùng khung bao của Renderer (khung bao rộng hơn mesh ~15%).
6. **Gắn vũ khí vào tay**: tạo prefab vũ khí riêng (gốc đặt ở tay cầm, scale đều 3 trục), rồi gắn vào xương tay **trong prefab nhân vật**, không gắn thẳng trong scene. Phải kiểm tra trong Play mode thật (giả lập tư thế trong Edit mode cho kết quả sai). Vũ khí là con của xương tay nên phải đặt `localScale = 1 / scale của model`, nếu không sẽ bị phóng to theo model.
7. **Thiết kế dễ rig (áp dụng khi vẽ ảnh gốc)**: Mixamo tự rig, không mô phỏng vải, phần áo choàng/váy bị gắn vào xương gần nhất nên dài thì sẽ bị kéo theo chân lúc chạy (căng, rách, xuyên). Nên: áo choàng **ngắn tới eo/hông** (tối đa giữa đùi), không chạm đất; buông thẳng sau lưng, không dính vào khoảng giữa tay và thân (A-pose), không nối 2 chân với nhau. **Trước khi mất công làm tiếp**: upload bản đã tách material lên Mixamo, bấm xem thử animation Walk/Run ngay trên web Mixamo (xem trước được mà chưa cần tải) để coi áo choàng có bị méo không. Méo nặng thì tách áo choàng ra thành mảnh riêng trong Blender (gắn cứng vào xương lưng hoặc dùng Cloth của Unity) hoặc bỏ. Tương tự, **tấm vải/tạp dề dài rủ giữa 2 chân** sẽ bị 2 chân kéo căng ra 2 bên khi chạy, nên làm ngắn (tới giữa đùi) hoặc tách thành 2 mảnh nhỏ ở 2 bên hông. Prompt mẫu (tiếng Anh) để chỉnh ảnh gốc theo hướng này: *"Keep this exact character (face, headdress, armor, belt, patterns). Change only: cape ends at hip level and hugs the back without flaring sideways; front apron ends at mid-thigh or is split into two small side panels, nothing hanging between the legs. Full body, front view, A-pose with arms about 35 degrees away from the body, legs slightly apart, plain background."*

## 7. Vấn đề đã biết

- Dodge đang mượn tạm animation Run (nhìn như lướt nhanh), cần tải animation lăn/né thật.
- **Áo choàng Sơn Tinh v2**: lúc đầu bám cứng theo lưng nên tay vung ra sau xuyên qua (đo trong Blender: tay lọt sau tấm áo tới ~16% chiều cao người). Đẩy áo ra sau hoặc cho mép áo bám theo tay chỉ giảm số đỉnh xuyên (98 → ~40), không hết. **Đã chuyển sang Cloth của Unity** (áo bay phấp phới khi chạy, va chạm với tay/thân/chân), chi tiết và việc còn lại ở mục 9. **Chưa đo lại có còn xuyên tay không**, cần coi bằng mắt khi chơi.
- Thủy Tinh khi đứng/đi cầm 2 tay: tay trái đặt trên cán nhưng lệch vài cm (chưa làm IK 2 tay). Đã đo: lúc chạy kiếm không cạ vào vai/tóc; lúc đứng/đi chuôi kiếm chạm nhẹ vạt áo dưới 1cm.
- Mesh kiếm rất nặng: **239.000 tam giác** (gấp ~30 lần cả nhân vật). Nên giảm còn ~4.000–8.000 (tạo lại trên Meshy theo công thức vũ khí ở mục 6, hoặc decimate bằng Blender), giữ nguyên tỷ lệ để khỏi phải canh lại tay cầm.
- File phím mẫu `Assets/InputSystem_Actions.inputactions` của template vẫn đang được đặt làm "project-wide actions" trong Project Settings. Game không dùng tới, vô hại, có thể gỡ khi dọn project.
- Vài cảnh báo **"The referenced script (Unknown) on this Behaviour is missing!"** (khoảng 7–19 dòng) hiện lúc vào/ra Play: **vô hại**, nguồn là `Assets/Settings/DefaultVolumeProfile.asset` (profile mặc định của template URP, có 2 volume component thử nghiệm `CopyPasteTestComponent2` và `VolumeComponentSupportedEverywhere` mà script chỉ có trong gói test của URP). Đã quét mọi scene/prefab của project (trừ các gói Asset Store): không có script nào bị thiếu. Muốn hết cảnh báo thì mở profile đó trong Inspector, gỡ 2 component bị "Missing" (chưa đụng vào vì là file của template).
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

### 8.1b Bộ chiêu của Thủy Tinh (đại kiếm + phép nước) — danh sách cần tải trên Mixamo (03/10)

Thiết kế từ đầu đã là **Thủy Tinh cầm Đại kiếm 2 tay** (Sơn Tinh tay không), kiếm đã có (`Sword.prefab`, mục "Vũ khí"); việc còn lại là gắn vào `Player_ThuyTinh_v2` (xương `mixamorig:RightHand`), giảm mesh kiếm (239.000 tam giác) và có animation cầm kiếm. Bộ chiêu đề xuất (ưu tiên: **A bắt buộc**, **B nên có**, **C tuỳ chọn**). Tên clip trên Mixamo có thể lệch chút, ưu tiên bộ **"Great Sword"** vì cùng kiểu cầm với Idle/Walk/Run đã dùng:

| Ưu tiên | Chiêu | Phím | Hiệu ứng (Claude làm bằng hệ hạt, không cần asset ngoài) | Clip cần tìm |
|---|---|---|---|---|
| A | Chém nhẹ, combo 3 đòn | Chuột trái | vệt kiếm xanh nước + giọt nước bắn khi trúng | 3 clip chém khác nhau trong bộ Great Sword (Slash / Attack) |
| A | Chém nặng (chậm, đẩy lùi) | Chuột phải | sóng nước vỡ hình quạt trước mặt | Great Sword High Spin Attack, hoặc Jump Attack |
| A | Lăn né | Ctrl trái | bụi nước | Roll / Forward Roll (hiện đang mượn clip Run) |
| A | Trúng đòn nhẹ / loạng choạng | (tự động) | giọt nước + rung nhẹ | Great Sword Impact / Hit Reaction / Stagger |
| B | **Gọi gió, gió tới** | Q | xoáy gió quanh kiếm, hút kẻ địch lại rồi hất tung | Great Sword Casting (hoặc Magic Spell) |
| B | **Hô mưa, mưa về** | E | vùng mưa 6 giây trên đầu, làm chậm kẻ địch, gợn nước dưới đất | giơ kiếm / tay lên trời (Casting) |
| B | **Sóng nước nghe lệnh** | R | bức sóng nước cao ~2 m chạy thẳng 10 m, hất kẻ địch | Great Sword Slide Attack hoặc Slash mạnh |
| C | Đỡ đòn | giữ chuột giữa | màng nước quanh người | Great Sword Blocking |
| C | Dâng nước (chiêu cuối cho trận boss) | cả 3 chiêu B đầy | nước dâng ngập arena | dùng lại Casting |

Tải theo Thủy Tinh v2 (FBX for Unity, 30 fps, Without Skin, **bật "In Place" cho đòn đánh / phép / lăn** vì quãng đi do code quyết định). Ba tên "Gọi gió / Hô mưa / Sóng nước" lấy từ dòng chiêu đặc trưng đã có ở màn chọn nhân vật (`CharacterDefinition.signatureMoves`). Cầm kiếm thì bộ Idle / Walk / Run cũng phải là bản Great Sword (bản v1 dùng thử trên v2 được nhờ Humanoid, tải lại cho v2 thì khớp tay cầm hơn).

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
- **Danh sách 8 cảnh + prompt cho Gemini/ChatGPT + cách đưa vào game: [`docs/prologue_art_prompts.md`](prologue_art_prompts.md)** (02/10, người dùng đã có ảnh Hùng Vương và Mị Nương, đang đi tạo cả bộ).
- **8 tranh Prologue đã vào game (02/10)**: file gốc `.webp` nằm ở `ArtSource/Story/` (Unity không đọc được webp), bản dùng trong game là JPG 1920×1080 (nâng từ 1672×941, gần đúng 16:9) ở `Assets/Art/Story/` (`01_van_lang` … `08_cuoc_dua`, import dạng Sprite). Gán vào `Dialogue_Prologue.asset`: dòng 1→01, 2→02, 4→03, 6→04, 7→05, 8→06, 10→07, 11→08 (đếm từ 1; các dòng 3, 5, 9, 12 giữ ảnh dòng trước). Hiển thị **phủ kín toàn màn hình ở mọi tỉ lệ cửa sổ** (không viền đen): `Illustration` có `AspectRatioFitter` kiểu Envelope Parent, `DialogueRunner` tự đặt tỉ lệ theo từng sprite; cửa sổ không phải 16:9 thì cắt bớt mép ảnh (đã chụp thử 16:9, 21:9, 4:3). Khung hội thoại, ô tên người nói và chấm "tiếp tục" **bo góc** (sprite 9-slice `Assets/Art/UI/Dialogue/ui_round_corner.png`, bán kính 30 / 16 / 9 px). Đã xem trong Play: dòng 1, 4, 7–12 (6 trong 8 ảnh); ảnh 02 (Mị Nương, dòng 2) và 04 (Sơn Tinh, dòng 6) gán đúng theo log nhưng chưa chụp hiển thị. Chưa làm: chuyển ảnh mờ dần (hiện cắt thẳng).

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

### 8.10 Làm lại giao diện chọn nhân vật cho giống ảnh tham khảo (**ĐÃ LÀM 02/10**, xem mục 9.1 "Màn chọn nhân vật mới"; phần dưới là ghi chú ý tưởng ban đầu)

Ảnh tham khảo: [`docs/reference/ui_character_select_ref.webp`](reference/ui_character_select_ref.webp) (màn chọn nhân vật của một game Naruto, **chỉ để tham khảo bố cục, không dùng asset của họ**). Chỉ làm sau khi xong việc sửa tay Sơn Tinh (mục 9.2). Màn hiện tại: `Assets/Scenes/CharacterSelect.unity`, code ở `Assets/Scripts/UI/CharacterSelect/`.

Ảnh có gì và áp vào game này thế nào (đây là cách hiểu ban đầu, chỉnh lại khi bắt đầu làm):

- **Chia đôi màn hình, mỗi bên 1 nhân vật 3D cỡ lớn** (nhân vật chiếm gần hết chiều cao khung, bán thân/gần toàn thân, ánh sáng nền vàng). Game này chỉ có **2 nhân vật chọn được**, nên 2 hình 3D chính là Sơn Tinh và Thủy Tinh đứng 2 bên, không cần lưới danh sách nhiều ô như ảnh gốc.
- **Tên nhân vật cỡ rất lớn, kiểu chữ cọ/thư pháp, đặt chồng lên người**, kèm 1 dòng phụ nhỏ bên dưới (ảnh gốc: tên chế độ; ở đây dùng danh hiệu "Thần núi Tản Viên" / "Thần nước miền biển cả" đã có sẵn).
- **Nhãn người chơi** ("P1" / "CPU") bằng chữ lớn, màu khác nhau, đặt cạnh tên. Áp vào game: bên được chọn là người chơi, bên còn lại là đối thủ cuối (đúng thiết kế: nhân vật mình chọn đánh với nhân vật còn lại).
- **3 dòng thanh nằm ngang phía dưới** (icon + tên kỹ năng/đòn). Áp vào: liệt kê 3 đòn đặc trưng của mỗi nhân vật (đòn nhẹ, đòn nặng, né/kỹ năng riêng), cần chốt sau khi có animation combat (mục 8.1).
- **Hàng ô nhỏ phía trên** (3 ô, ô đầu sáng). Chưa cần; có thể bỏ hoặc dùng cho 3 sính lễ.
- **Thanh gợi ý phím ở đáy màn hình** (phím + nhãn: Confirm / Return...). Dễ làm, nên có.
- **Ô đang chọn có viền cam nổi bật, bên không chọn bị tối đi.** Màn hiện tại đã có làm tối + xoay đi; cần thêm khung/viền sáng rõ hơn.

Chưa chốt: dùng 3D thật (2 model đứng cạnh nhau như màn hiện tại, camera gần hơn) hay vẽ ảnh 2D bán thân từ ChatGPT/Gemini như ảnh gốc. 3D thật tiện hơn vì không cần thêm asset.

## 9. Bàn giao session (01/10): làm tiếp từ đây

Mục này viết để mở session mới (hoặc người khác) đọc xong là làm tiếp được, không cần hỏi lại.

### 9.1 Đang ở đâu

- **Sơn Tinh v2 (áo choàng + mũ lông vũ) đã vào game và đã được chơi thử**: chạy 6.86 m/s, chân chạm đất, texture đúng. File: prefab nhân vật `Assets/Prefabs/Characters/SonTinh_v2.prefab`, prefab người chơi `Assets/Prefabs/Player/Player_SonTinh_v2.prefab`, `Character_SonTinh.asset` đã trỏ sang prefab v2 (bản cũ `Player_SonTinh.prefab` giữ lại làm dự phòng, muốn quay lại chỉ cần gán lại `Player Prefab` trong asset).
- **Áo choàng chạy bằng Cloth của Unity** (trước đó bám cứng vào lưng nên tay vung ra sau xuyên qua). Đã kiểm tra: chụp nhìn ngang trong Play mode thấy áo bay ra sau lúc chạy, không xuyên qua thân.
- **Chưa kiểm tra**: (1) tay vung có còn xuyên áo không (chụp ngang không thấy được, phải nhìn bằng mắt lúc chơi), (2) độ bay đã vừa mắt chưa (thông số chưa tinh chỉnh), (3) hành vi lúc dừng đột ngột / quay 180 độ, (4) lúc dịch chuyển nhân vật (respawn, chuyển scene).
- **Nắm tay Sơn Tinh đã thay (01/10 tối)**: mesh gốc của Meshy làm bàn tay thành cục liền (ngón dính nhau) và rig no-finger không có xương ngón, nên 2 nắm tay cũ được thay bằng nắm đấm có 4 ngón cuộn + ngón cái (`tools/replace_fists.py` + `tools/fist_builder.py`), weight 100% xương bàn tay. Chỉ 287 đỉnh cũ bị loại, 4.227 đỉnh còn lại giữ nguyên vị trí và weights (đã kiểm tra), xương không đổi. Mesh trong Unity giờ 10.927 tam giác (trước 7.265). Áo choàng Cloth đã gán lại vùng cử động (454 đỉnh áo, đúng như cũ). **Bản trước khi thay** đã xoá khi dọn dự án 03/10 (mục 9.5), không còn bản sao. Còn lại: nắm tay đôi chỗ bị tua/váy đâm xuyên qua (do tư thế Idle đưa tay sát váy), chưa xử lý.
- **Thủy Tinh v2 đã vào game (01/10 đêm)**: thân (Meshy "Azure Warlord", rig Mixamo đủ 65 xương có ngón) + bộ váy/quần/giày xếp lớp (model Meshy riêng) ghép bằng `tools/fit_garment.py`. Prefab `Assets/Prefabs/Characters/ThuyTinh_v2.prefab` và `Assets/Prefabs/Player/Player_ThuyTinh_v2.prefab`, `Character_ThuyTinh.asset` đã trỏ sang bản mới (bản cũ `Player_ThuyTinh.prefab` giữ làm dự phòng). Animation Idle/Walk/Run từ `ArtSource/Mixamo/ThuyTinh_v2/`, đi 1.996 m/s, chạy 5.309 m/s (đã đo). Model cao ~1.9 m tính tới xương đỉnh đầu (scale 2.03). **Chưa có**: kiếm (các clip hiện tại là tay không, kiếm cũ chưa gắn lại; cần tải bộ Great Sword cho rig mới), animation combat, tà váy dài mới chỉ skin thường (đã thấy đung đưa theo bước chạy, đầu gối hơi đội lên váy; nếu chưa vừa mắt thì làm Cloth như áo choàng Sơn Tinh), bàn tay là bàn tay phẳng dính ngón nhưng có xương ngón nên sẽ gập khi nắm.
- **Sơn Tinh mới ("Jade Warrior" + váy/quần/giày + áo choàng lông vũ) đã vào game (02/10)**, thay hẳn bản Sơn Tinh v2 dị tật. Quy trình y hệt Thủy Tinh v2: thân rig Mixamo (33 bone: thân + chuỗi ngón trỏ mỗi tay) + `Feathered Warden Attire` (váy/quần/giày) + `Verdant Feather Mantle` (áo choàng) ghép bằng `tools/fit_garment.py ... name=SonTinh mantle=...`. Unity: `Assets/Art/Characters/SonTinh_v2/` (3 mesh Body/Garment/Mantle, 3 material), `Assets/Animations/SonTinh_v2/`, `AOC_SonTinh_v2`, `SonTinh_v2.prefab` + `Player_SonTinh_v2.prefab` (copy giá trị gameplay của prefab cũ, scale 2.052, đi 1.885 / chạy 5.365 m/s), `Character_SonTinh.asset` trỏ vào bản mới. **Bản cũ** (mesh 193 mảnh, nắm tay thay thế, Cloth áo choàng) đã xoá khỏi project khi dọn dự án 03/10 (mục 9.5). **Chưa có**: bàn tay vẫn là bàn tay phẳng và rig chỉ có ngón trỏ nên clip đấm không nắm được, animation combat. Phần sau lưng đầu (tóc) nhìn từ phía sau hơi đen bóng có chỗ thủng.
- **Sprint (02/10)**: giữ Shift (hoặc nhấn cần trái) + đang di chuyển thì chạy nhanh, nhả Shift là về chạy thường, chỉ giữ Shift mà không đi thì đứng yên. Blend tree dùng chung `AC_Humanoid_Base` giờ có Idle 0 / Walk 1 / Run 2 / **Sprint 3** (clip placeholder `Assets/Animations/Shared/Sprint.anim`, mỗi AOC ghi đè bằng clip riêng của nhân vật). Code: hành động `Sprint` trong `PlayerControls.inputactions`, `PlayerInputReader.SprintHeld`, `PlayerController.sprintSpeed` (Thủy Tinh 6.862, Sơn Tinh 6.948 m/s, đã đo; chạy thường 5.309 / 5.365). Thủy Tinh còn đổi Idle sang clip 60 khung mới (`thuy_tinh_v2_idle_3.fbx`); clip Idle 251 khung cũ vẫn nằm trong project, đổi lại bằng cách gán clip Idle trong `AOC_ThuyTinh_v2`.
- **Áo choàng Sơn Tinh bay (02/10)**: dùng **xương + spring**, không dùng Unity Cloth. Đã thử Cloth trước: mesh áo gồm ~190 chiếc lông rời nên lúc nghỉ chúng rủ thành dải mảnh (bật trọng lực) hoặc kẹt ở tư thế vừa bị gió thổi (tắt trọng lực), rất xấu. Cách làm cuối: `tools/fit_garment.py` thêm 15 xương `Cape_<cột>_<đốt>` (5 cột × 3 đốt) dưới Spine2 và skin mềm cho áo choàng; trong Unity script `Assets/Scripts/Characters/OutfitSpringBones.cs` (đổi tên từ `CapeSpringBones`, gắn trên `SonTinh_v2.prefab`) mô phỏng các chuỗi xương này bằng verlet: trễ theo chuyển động của nhân vật rồi được kéo về dáng gốc. Lúc nghỉ giữ đúng dáng lông vũ xếp lớp. Không có va chạm thật với thân, chỉ giới hạn không cho vung về phía người quá `Inward Limit`; tay vung vẫn có thể xuyên qua lông. (Bản đầu bay quá mạnh, đã chỉnh lại ở mục ngay dưới.)
- **Đung đưa nhẹ cho áo choàng + váy cả 2 nhân vật (02/10)**: yêu cầu là chỉ lắc nhẹ lúc di chuyển, đứng yên thì không lắc. `OutfitSpringBones` giờ có danh sách `Groups` (mỗi nhóm một tiền tố xương: `Cape_`, `Skirt_`) với `Frequency` (Hz, nút chính: cao = lắc nhỏ, nhanh, chặt; thấp = lờ đờ), `Damping Ratio` (1 = không dội, <1 = đung đưa vài nhịp), `Drag` (làm áo trôi ra sau khi chạy; 0 = chỉ phản ứng khi tăng/giảm tốc và nhịp bước), `Gravity`, `Max Angle` (trần cứng, độ lệch mỗi đốt), `Inward Limit`. Giá trị đang dùng: áo choàng 5 Hz / 0.7 / drag 5 / Max Angle 8°, váy 6 Hz / 0.7 / drag 4 / Max Angle 10° (chỉnh trên `SonTinh_v2.prefab` / `ThuyTinh_v2.prefab`, mục Outfit Spring Bones). Xương váy `Skirt_<cột>_<đốt>` (8 cột × 2 đốt dưới Hips, 16 xương/nhân vật) do `fit_garment.py` thêm (`skirtbones=1`, chỉ các mảnh váy xa thân; phần giày bị loại bằng `boottop`). Các lỗi đã sửa: (1) quay nhân vật tại chỗ (màn chọn nhân vật) **không** làm lắc — xoay theo nhân vật quanh vị trí cũ, chỉ dịch chuyển mới gây trễ (`Ignore Root Rotation`); (2) khung hình dài bị cắt ở 0.05 s (`Max Step`); (3) **áo choàng "run như máy rung" (người dùng báo 02/10)**: bản đầu kéo về dáng gốc theo *tỉ lệ mỗi khung hình*, nên độ cứng lò xo phụ thuộc tốc độ khung hình — ở ~350 fps (Play trong editor) tần số riêng là ~16 Hz và không bao giờ tắt, ở 60 fps chỉ ~7 Hz. Giờ là lò xo–giảm chấn thật theo giây (`Frequency`/`Damping Ratio`, chia bước nhỏ ≤1/90 s) và mô phỏng không chạy nhanh hơn ~80 Hz (gom các khung ngắn lại, vì giới hạn độ dài/góc áp mỗi bước nên chạy 350 Hz làm dập tắt gấp ~5 lần). Test bằng `PlayerController` thật + tay cầm ảo (Input System, `Gamepad` + `QueueStateEvent`, chạy được khi editor không focus): rung tần số cao của áo choàng khi chạy 1.1° → 0.06–0.3° (đứng yên 0.83° → 0.00°), biên độ gần như không đổi theo fps (chạy, áo choàng: 2.9° / 3.1° / 3.6° ở 350 / 144 / 60 fps). Số đo (góc lệch trung bình mỗi đốt, tối đa là trần): đứng yên 0°, quay tại chỗ 0°, khung giật 90 ms 0°, đi bộ ≤1.7°, chạy áo choàng ~3° váy 4–5°, sprint áo choàng ~4.3° váy 5–6.6°; dừng lại thì về dáng gốc sau ~2 s. Chưa có: va chạm thật với thân, tóc đung đưa.
- **HUD máu/năng lượng làm lại theo ảnh tham khảo (02/10)**: ảnh [`docs/reference/ui_hud_ref.webp`](reference/ui_hud_ref.webp) (HUD của một game Naruto, **chỉ tham khảo kiểu dáng, không dùng asset của họ**). Vẫn nằm góc dưới-trái như cũ (cùng lề 32 px). Bản 2 (người dùng muốn giống 1:1 và thanh dài hơn): đầu + vai nhân vật **tách nền** đè lên đầu thanh, phía sau là **chim lửa (phượng hoàng)** — bản 3: hình dáng lấy từ silhouette người dùng đưa (`ArtSource/Concept/hud_fire_bird_silhouette.png`, nguồn/giấy phép chưa rõ, ảnh cũng lưu ở `docs/reference/ui_hud_fire_bird_ref.png`), tô lại bằng màu lửa cũ (viền nâu đậm, viền trong vàng, thân cam đậm ở dưới sáng dần lên trên); builder đọc ảnh, tách mặt nạ, tính khoảng cách tới mép để tô viền, ra `Assets/Art/UI/HUD/hud_fire_bird_emblem.png` 1024². Hai bản trước (hoa cánh thẳng, rồi lửa móc câu vẽ bằng đường cong) đã bỏ. tên nhân vật chữ trắng nghiêng **viền đen dày** + bóng, rồi một khối vát chéo ~35° gồm thanh máu xanh lá (680 × 20, dài hơn bản 1 là 490) trên thanh năng lượng (stamina) xanh dương (680 × 14) có ô vàng nhỏ ở cuối, hai thanh xếp thẳng hàng theo đường chéo như ảnh, khung vàng 2 lớp, dải sáng sát mép trên. Không làm thanh thứ 3 / viên cam / phím H của ảnh vì game chưa có cơ chế tương ứng; tên dùng Roboto (font cọ kiểu "Naruto" phải tải font ngoài). Thanh máu có **vệt cam tụt chậm** sau mỗi cú trúng đòn (giữ 0.45 s rồi tụt 0.35/s) và đổi màu xanh → cam → đỏ khi máu giảm. Chân dung lấy từ model 3D trong game (render đầu–vai, nền trong suốt, mép dưới/hai bên mờ dần; `Assets/Art/UI/Portraits/Portrait_SonTinh|ThuyTinh.png`; render lại bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Render HUD portraits**, `Assets/Editor/HudPortraitRenderer.cs`), gán vào ô `Portrait` mới của `Character_*.asset`; `PlayerSpawner` truyền nhân vật cho `GameplayHUD.Bind(player, character)` nên HUD tự đổi tên + mặt theo nhân vật đã chọn. Không có sprite vẽ tay: khung/thanh là hình chữ nhật trắng + 2 component mới `UIGradient` (đổ màu dọc, tách quad theo từng key của Gradient) và `UIShear` (làm vát chéo, cắt phần fill vẫn chéo khi tụt); trong `Assets/Art/UI/HUD/` chỉ có 2 sprite sinh bằng code (ô trắng — **Image dạng Filled bắt buộc có sprite, nếu không fillAmount bị bỏ qua** (đã thấy khi dựng; thanh cũ cũng không có sprite nên nhiều khả năng fill của nó chưa bao giờ chạy, chưa kiểm chứng) —, chim lửa `hud_fire_bird_emblem.png` sinh từ ảnh silhouette) và material viền chữ `HudName_Outline.mat`. Bố cục dựng lại bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Rebuild Gameplay HUD in open scene** (`Assets/Editor/GameplayHudBuilder.cs`, mở `Sandbox_Combat` trước, sau đó lưu scene); muốn chỉnh kích thước/màu thì sửa các hằng số trong file đó rồi chạy lại. Đã kiểm tra trong Play: Thủy Tinh vào đúng chân dung + tên, trừ 38 và 40 máu thì thanh tụt đúng, vệt cam tụt đúng nhịp. Chưa làm: 3 thanh/ô phụ trong ảnh (thanh chiêu đặc biệt, viên cam hỗ trợ, ô kỹ năng), chỉ làm máu + năng lượng theo yêu cầu.
- **Màn chọn nhân vật mới theo ảnh Naruto (02/10)** (`docs/reference/ui_character_select_ref.webp`, chỉ lấy bố cục): chia đôi màn hình, **Sơn Tinh bên trái nền vàng đất, Thủy Tinh bên phải nền xanh nước**, mỗi bên là model 3D cận cảnh đầu–ngực (camera `(0,1.58,-3)` FOV 30, nhân vật đứng ở x = ±0.72, tắt sân khấu và bệ cũ), tên xếp 2 dòng chữ trắng nghiêng viền đen dày ("Sơn" / "Tinh" dịch sang phải), nhãn **P1** (xanh dương) cho bên đang chọn và **CPU** (xanh lá) cho bên còn lại (đúng thiết kế: nhân vật mình chọn đánh với người kia ở trận cuối), dòng danh hiệu trong ngoặc, 3 thanh kỹ năng (icon vàng + chữ; **chữ tạm lấy từ truyền thuyết**: "Sức mạnh của núi non / Núi mọc theo tay chỉ / Đất dâng thành bãi" và "Gọi gió, gió tới / Hô mưa, mưa về / Sóng nước nghe lệnh", lưu ở `CharacterDefinition.signatureMoves`, đổi thành chiêu thật khi có combat), thẻ chân dung có viền cam ở góc trên mỗi nửa + khung mô tả, thanh phím tắt dưới cùng ("< >" chọn, Enter xác nhận, Esc quay lại — **Esc/B giờ về MainMenu, trước đây chưa có**). Bên không chọn bị phủ tối + mờ chữ, xoay đi 30°. Điều khiển: ←/→ hoặc A/D hoặc cần analog hoặc rê chuột đổi bên, Enter/Space/Click/nút A xác nhận. Code: `CharacterSelectMenu` (viết lại), `CharacterSelectPanel` (mới, 1 panel/nửa màn), bố cục dựng bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Rebuild Character Select in open scene** (`Assets/Editor/CharacterSelectBuilder.cs`, mở scene `CharacterSelect` trước rồi lưu); nền (`cs_backdrop.png`: 2 nửa vàng/xanh, vòng tròn đồng tâm kiểu mặt trống đồng, vệt cọ) và 6 icon (`cs_glyph_*.png`) đều sinh bằng code, không dùng asset của game Naruto. Chưa làm: font chữ kiểu cọ (cần tải font ngoài), lưới nhiều nhân vật ở hai bên (chỉ có 2 nhân vật), hàng ô nhỏ "Strike Back" phía trên.
  - **Sửa lớp tối bị lệch (02/10, người dùng báo)**: bản đầu đặt mọi thứ theo pixel cố định của canvas 1920×1080 (neo góc dưới-trái), nên cửa sổ Game không phải 16:9 (vd 1328×936) thì lớp tối của nửa bên kia (`Dimmer`) bắt đầu lệch khỏi vạch chia của cảnh 3D (đo được ~78 px, lớp tối Sơn Tinh lấn sang nửa Thủy Tinh) và còn hở 1 dải ngang trên cùng. Giờ mỗi `Panel_*` neo theo **tỉ lệ màn hình** (0–0.5 và 0.5–1, cao hết màn hình) nên `Dimmer` luôn phủ đúng 1 nửa; vạch `Divider`, `Title`, `HintBar` neo giữa màn hình (thanh phím tắt cũng canh giữa đúng). Chữ/thanh kỹ năng nằm trong khung `Content` 960×1080 neo giữa-dưới của nửa đó, `CharacterSelectPanel.designWidth` tự thu nhỏ khung này khi nửa màn hình hẹp hơn 960 (cửa sổ hẹp hơn 16:9) để chữ không tràn sang nửa kia. Đã chụp kiểm ở 16:9, 1328×936 và 21:9. Còn lại: màn siêu rộng (21:9) nền `cs_backdrop.png` không phủ hết 2 bên (sprite chỉ rộng theo chiều cao) và mép trên của khung bị cắt; chưa xử lý vì game chạy 16:9.
- **Sửa miệng Sơn Tinh (02/10, người dùng báo "miệng như bị khâu")**: lúc cận cảnh thấy miệng là một khối phẳng đỏ-nâu 40×10 px, đứt gãy ở mép rồi chuyển thẳng sang da (UV đảo Meshy). Đã vẽ lại khối đó thành đôi môi mềm (viền mờ dần về màu da, đường khép môi mờ) **ngay trong texture** `Assets/Art/Characters/SonTinh_v2/Textures/son_tinh_v2_body_basecolor.png` (vùng x 1044–1088, y 1130–1150); bản gốc trước khi sửa chính là `ArtSource/Meshy/SonTinh_v2/son_tinh_v2_basecolor.png` (đã kiểm tra trùng hash; bản sao trong `_old` đã xoá). Còn lỗi nhỏ khi zoom sát mà tôi chưa sửa: mũi có chấm đen ở lỗ mũi, một mảng sáng dọc trán (đường nối UV). Thủy Tinh chưa kiểm tra cận cảnh.
- **Đánh giá "có cần tạo lại model 3D Sơn Tinh không?" (02/10, người dùng hỏi sau khi báo miệng vẫn méo): KHÔNG cần tạo lại.** Kiểm tra bằng render trong preview scene ở đúng tỉ lệ game (model ×2.052, tư thế Idle, đèn tự đặt — chưa chụp trong Play với ánh sáng thật của màn chọn):
  - **Miệng**: nguyên nhân gốc là do **Meshy** (mặt chỉ chiếm vài chục pixel trong atlas 2048², môi là một khối phẳng, normal map nhiễu), không phải Mixamo hay rig. Đã giảm **`_BumpScale` của `M_SonTinh_v2_Body.mat` từ 1 → 0.25** (normal map nhiễu là thủ phạm chính của vẻ "khâu"; chỉ vật liệu Body, Thủy Tinh không đụng). Render ở khoảng cách màn chọn nhân vật (camera 3 m, FOV 30, phóng 2×) thấy miệng giờ là một đường khép môi bình thường; còn vài chấm tối nhỏ ở khóe môi, đầu mũi hơi lõm và mảng sáng ở trán, chỉ thấy khi zoom. Ở camera gameplay (4.5 m, FOV 50) miệng chỉ cỡ ~12 px nên không đọc được. Nếu vẫn chướng mắt: sửa tiếp bằng cách làm phẳng normal/smoothness ở vùng mặt (không cần model mới).
  - **Phần còn lại ổn**: 3 mesh Body 5.908 / Garment 8.164 / Mantle 5.885 tam giác (~20k), texture 2048² mỗi cái; mặt, da, vòng cổ, vòng tay, váy, mũ lông vũ đẹp; áo choàng nhìn từ sau (camera game) tốt; **bàn tay có ngón riêng + ngón cái, da có chi tiết** (mesh tay không có vấn đề). Chỉ còn nhược điểm nhỏ: tóc sau đầu là khối đen bóng, vài lỗ hở trên áo choàng khi nhìn gần.
  - **Vì sao không tạo lại**: phải làm lại cả chuỗi (rig Mixamo → ghép váy/áo choàng bằng `fit_garment.py` → bone spring → prefab/AOC → animation) mà mặt mới vẫn là may rủi (mặt Thủy Tinh lần đầu cũng méo, phải bấm "x4 variants" mới ra mặt tốt).
  - **Vấn đề thật cần sửa: rig tay.** Rig hiện có 64 bone = 25 thân + chuỗi **ngón trỏ** mỗi tay (`mixamorig:LeftHandIndex1..4`, đã kiểm tra trong prefab) + 31 bone áo/váy; **thiếu ngón cái, giữa, áp út, út**. Sơn Tinh đánh tay không (võ + phép đất) nên khi có clip đấm/võ, chỉ ngón trỏ gập còn 4 ngón kia đứng thẳng. Model không cần đổi, chỉ cần rig lại: **upload lại đúng `ArtSource/Meshy/SonTinh_v2/son_tinh_v2_notex.fbx` lên Mixamo, chọn skeleton chuẩn (65 bone, có ngón)**, tải lại Idle/Walk/Run/Sprint (With Skin), rồi báo Claude chạy lại `fit_garment.py` + import (Thủy Tinh v2 làm đúng như vậy, 65 bone). **Phải làm TRƯỚC khi tải animation combat** vì đổi rig là phải tải lại hết. Chưa rõ vì sao lần rig trước chỉ ra ngón trỏ (có thể do tùy chọn skeleton hoặc Mixamo chỉ nhận ra 1 ngón); nếu rig lại vẫn ra 33 bone thì phương án dự phòng (chưa thử) là thêm xương ngón bằng Blender rồi tạo pose nắm tay riêng trong Unity.
- **Chưa commit gì** của phần v2. Code hệ thống (Health, chọn nhân vật, dialogue, HUD...) đã nằm trong commit `01608bc`.

### 9.2 Việc làm tiếp theo — theo đúng thứ tự

1. **Chơi thử Sơn Tinh v2** trong `Sandbox_Combat` (mở scene, bấm Play, chọn Sơn Tinh nếu `Fallback Character` chưa phải Sơn Tinh). Nhìn: chạy thẳng, chạy vòng, đổi hướng gấp, đứng lại đột ngột; để ý tay có xuyên áo không, áo có bay quá ít/quá nhiều/giật không.
2. **Tinh chỉnh Cloth nếu cần** (mục 9.3, mỗi lần chỉnh xong chơi lại 1 lần). Chỉnh trực tiếp trên prefab `SonTinh_v2` ở component **Cloth** của object `Mesh_0` (không cần chạy script lại, trừ khi muốn đổi `maxFree`/`yTop`/`yFree` hoặc bán kính collider).
3. **(Cập nhật 02/10: đoạn này viết cho model Sơn Tinh cũ. Với Sơn Tinh mới hãy rig lại 65 bone trên Mixamo TRƯỚC rồi mới tải combat; xem mục 9.1, bullet "Đánh giá có cần tạo lại model".)** **Tải animation combat cho rig mới của Sơn Tinh** theo bảng mục 8.1 (dòng 1, 2, 5, 6, 7, 8). Phải tải từ **chính nhân vật no-finger đã upload trên Mixamo** (cùng bộ xương). Bỏ vào `ArtSource/Mixamo/SonTinh_v2/`, rồi báo Claude import. Khác nhân vật cũ: **mỗi file animation dùng avatar riêng** (không dùng Copy From Other Avatar được), xem ghi chú "Sơn Tinh v2" ở mục 2.
4. **Chỉ khi Sơn Tinh v2 đã ổn mới làm Thủy Tinh mới** (đã có thiết kế mới). Làm đúng pipeline "nhân vật phức tạp" trong [`tools/README.md`](../tools/README.md) nếu nhân vật có nhiều mảnh rời (lông vũ, áo choàng, giày rời); nếu thiết kế gọn thì pipeline đơn giản là đủ. **Trước khi làm tiếp bước nào, bấm xem thử Walk/Run ngay trên web Mixamo** (mục 6 bước 7). Ưu tiên thiết kế Thủy Tinh theo mục 6 bước 7 (áo choàng/tạp dề ngắn) để khỏi phải làm ma-nơ-canh + Cloth lần nữa.
5. **Commit**: nên có 1 commit cho Sơn Tinh v2. Gồm: `Assets/Animations/SonTinh_v2/`, `Assets/Animations/Shared/AOC_SonTinh_v2.*`, `Assets/Art/Characters/SonTinh_v2/`, 2 prefab v2, `Character_SonTinh.asset`, `tools/`, `docs/`, `ArtSource/Meshy/SonTinh_v2/`, `ArtSource/Mixamo/SonTinh_v2/`. **Không commit** `ArtSource/Meshy_AI__1001133221_texture_fbx.zip` (21 MB, đã giải nén rồi) và file jpg bảng thiết kế lẻ ở gốc `ArtSource/` (nên chuyển vào `ArtSource/Concept/`). Chạy `git status` trước khi add để chắc không dính file lạ.

### 9.3 Cloth của áo choàng — chi tiết để chỉnh

Cloth nằm trên object `Mesh_0` (cái có SkinnedMeshRenderer) trong `SonTinh_v2.prefab`. Thông số hiện tại:

| Thông số | Giá trị | Chỉnh để làm gì |
|---|---|---|
| Stretching Stiffness | 1 | Hạ xuống ~0.8 nếu áo thấy căng cứng |
| Bending Stiffness | 0.35 | Tăng → áo cứng, ít nhăn; giảm → mềm, dễ gấp |
| Damping | 0.3 | Tăng → áo ngừng đung đưa nhanh hơn; giảm → đung đưa lâu |
| World Velocity Scale | 0.6 | Tăng (tới 1) → áo bị gió thổi ra sau mạnh hơn khi nhân vật chạy |
| World Acceleration Scale | 0.6 | Tăng → áo giật/văng mạnh hơn khi đổi tốc độ, đổi hướng |
| Friction | 0.3 | Ma sát với collider |
| Use Gravity / Tethers | bật / bật | Tethers giữ đỉnh khỏi bị kéo quá xa gốc |
| Continuous Collision | 0.5 | Tăng nếu áo lọt qua collider khi chạy nhanh |
| Self Collision | tắt | Bật tốn CPU, chỉ bật nếu áo tự xuyên nhau |

**Vùng cho áo cử động** (Max Distance từng đỉnh, đặt bằng script): 454 đỉnh áo choàng (398 đỉnh tự do), từ vai (`yTop` = 0.52 m, cố định tuyệt đối) xuống gấu áo (`yFree` = 0.05 m, được lệch tối đa `maxFree` = 0.9 m), nội suy mượt (smoothstep). Muốn áo bay xa hơn thì tăng `maxFree`; muốn vai/lưng trên cố định hơn thì tăng `yTop`... rồi **chạy lại phần 2 của script** (xem dưới). Toàn bộ đỉnh không phải áo choàng có Max Distance = 0 (đi theo xương như thường, không mô phỏng).

**Collider** (đều là **trigger**, nằm dưới xương tương ứng, tên bắt đầu bằng `ClothCol_`): thân 3 viên (Hips→Spine1 r0.17 m, Spine1→Spine2 r0.15 m, Spine2→Neck r0.15 m), mỗi tay 2 viên (Arm→ForeArm r0.075 m, ForeArm→Hand r0.07 m), mỗi chân 2 viên (UpLeg→Leg r0.12 m, Leg→Foot r0.09 m) và 2 hình cầu ở 2 bàn tay (r0.09 m). Áo vẫn xuyên tay → **tăng bán kính 2 viên tay lên ~0.09–0.10 m** (hoặc tăng `Continuous Collision`). Áo hở hẳn khỏi thân thấy khoảng trống → giảm bán kính thân.

**Script dựng lại từ đầu**: [`tools/unity/SonTinhCapeCloth.cs.txt`](../tools/unity/SonTinhCapeCloth.cs.txt) (2 đoạn, đã chạy đúng; để đuôi `.txt` để Unity không biên dịch). Đoạn 1 thêm Cloth + collider, đoạn 2 gán vùng cử động. Chạy từng đoạn bằng `Unity_RunCommand` (Claude) hoặc dán thành Editor script tạm. **Dùng lại cho Thủy Tinh mới** thì đổi đường dẫn prefab, tên xương, và điều kiện nhận ra áo choàng (hiện: mảnh liền ≥ 300 đỉnh, cao > 1.4 m, rộng > 0.9 m).

**Bẫy khi làm Cloth trên model này** (đã mất công đo, đừng đoán lại):
- Unity **gộp các đỉnh trùng vị trí** (đường nối UV): mesh có 8078 đỉnh nhưng Cloth chỉ có 4514 đỉnh. Mảng `cloth.coefficients` / `cloth.vertices` theo thứ tự **riêng của Cloth**, không phải thứ tự đỉnh mesh, gán theo chỉ số mesh sẽ ra sai (áo cứng, thân lại bay).
- Toạ độ của `cloth.vertices` = vị trí world − `skinnedMeshRenderer.rootBone.position`. Muốn biết đỉnh Cloth nào là đỉnh mesh nào: so khớp theo vị trí (đã làm trong đoạn 2 của script).
- Collider của Cloth là trigger nên không cản `CharacterController`, nhưng **vẫn kích hoạt `OnTriggerEnter`** của các trigger khác (`GiftPickup`, `SceneTransitionTrigger`). Lúc code combat, các truy vấn va chạm (OverlapSphere/Box) mặc định sẽ trúng cả collider này: lọc bằng layer hoặc kiểm tra tên/tag.
- **Dịch chuyển nhân vật** (respawn, vào scene mới, spawn) mà không reset Cloth sẽ thấy áo bị kéo giật theo. Khi code dịch chuyển, gọi `GetComponentInChildren<Cloth>().ClearTransformMotion()` sau khi đặt vị trí mới.
- Cloth chỉ chạy khi renderer **được tính là hiển thị**, đã bật `updateWhenOffscreen = true` để áo không đứng im khi camera lệch.
- Cloth chỉ là component, không chạy trên GPU, nên tốn CPU. Với 1 áo 454 đỉnh thì nhẹ; đừng thêm Cloth cho từng mảnh nhỏ.

**Bản sao lưu prefab chưa có Cloth** (nằm trong thư mục tạm của session cũ, có thể đã mất): chưa thấy cần, vì nếu cần bỏ Cloth chỉ cần xoá component Cloth và các object `ClothCol_*` trên prefab, áo sẽ quay về bám cứng vào lưng (do trọng số đã nằm trong FBX).

### 9.4 Mẹo làm việc với Unity qua MCP (rút từ session này)

- **Chụp pose nhân vật phải ở Play mode.** Trong Edit mode / preview scene, SkinnedMeshRenderer chỉ được skin lại 1 lần mỗi frame nên các ảnh chụp ra giống hệt nhau (hoặc trắng nếu dùng `BakeMesh`).
- **Humanoid đặt chân nhân vật đúng mặt phẳng gốc của model**, nên `Model` trong `Player_*` phải ở local (0,0,0). Đừng bù độ lệch của bind pose: từng làm vậy và nhân vật lơ lửng 1,07 m.
- **Đo tốc độ clip**: lấy quãng đường chân/hông đi trong 1 vòng lặp (Play mode) ÷ thời lượng clip, rồi gán `walkSpeed`/`runSpeed` của `PlayerController` bằng đúng số đó để chân không trượt. Với v2 số đã gán sẵn trong `Player_SonTinh_v2`: walk 2.116, run 6.856 (m/s). Có clip mới (combat, né) thì đo lại theo cách này.
- **Test bằng input thật**, không gọi `onClick.Invoke()` (lỗi thiếu `EventSystem` suýt lọt chính vì cách này).
- `Application.runInBackground = true` giúp editor không bị đơ khi không focus lúc test, nhưng nó **ghi vào `ProjectSettings`**: test xong nhớ trả về `false` (kiểm tra `git diff ProjectSettings/`).
- Lúc Unity đang compile, MCP báo "Unity not detected": chờ vài giây rồi gọi lại.
- Tên `Image` / `Navigation` / `Mesh` đụng namespace của project khi viết script ở namespace `SonTinhThuyTinh.*`: dùng alias (`UImage`, `UNav`) hoặc ghi rõ `UnityEngine.Mesh`.

### 9.5 Dọn dự án để push (03/10)

Commit chưa push lúc đó nặng ~299 MB (chủ yếu `ArtSource`), sau khi dọn còn ~151 MB các file mới thật sự phải upload.

- **Đã xoá**: 5 file zip Meshy trong `ArtSource/Meshy/*_v2` (so hash: mọi file trong zip đều có bản giải nén y hệt cạnh đó, ~106 MB), `ArtSource/_old`, `Assets/_Old`, và gói `Packages/com.codergamester.mcp-unity` (411 MB). Gói này là repo git lồng nhau không có `.gitmodules`, mà `manifest.json` lại trỏ `file:` vào nó, nên bạn cùng nhóm clone về sẽ lỗi gói. Client MCP đang dùng là Unity AI Assistant (`com.unity.ai.assistant`), không phải gói này.
- **Đã làm lại commit chưa push** bằng `git reset origin/main` rồi `git add -A`, vì chỉ xoá file mà commit cũ còn nằm đó thì lúc push các file nặng vẫn bị đẩy lên.
- **`.gitignore`** thêm `*.slnx`, `__pycache__/`, `*.pyc`; `son_tinh_thuy_tinh.slnx` được bỏ theo dõi.
- **Quy tắc từ giờ**: không commit thư mục `_old` hay file `.zip` tải từ Meshy (giải nén rồi đổi tên là đủ); không để gói có `.git` riêng trong `Packages/`.
- **Cố ý giữ dù chưa ai dùng**: `Assets/Art/sword` + `Sword.prefab` (còn phải gắn kiếm vào Thủy Tinh v2), `SceneTransitionTrigger.cs` (map sính lễ sẽ dùng), `Assets/InputSystem_Actions.inputactions` (đang đặt làm project-wide actions), mesh nguồn v2 trong `ArtSource/Meshy` (đầu vào của pipeline Blender/Mixamo).
- **Còn dọn thêm được** (đều đã nằm sẵn trên GitHub nên xoá không làm push nhẹ hơn, chỉ gọn thư mục): Thủy Tinh v1 (`Assets/Art/Characters/ThuyTinh`, `Assets/Animations/ThuyTinh`, `ThuyTinh.prefab`, `Player_ThuyTinh.prefab`, `AOC_ThuyTinh`), Sơn Tinh v1 (`Assets/Art/Characters/SonTinh`, `Assets/Animations/SonTinh`, `SonTinh.prefab`, `Player_SonTinh.prefab`), Đinh Ba (`Assets/Art/Weapons/DinhBa`, `DinhBa.prefab`), `Assets/Scenes/SampleScene.unity` (scene mẫu của template, chỉ nó còn dùng Sơn Tinh v1), `Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_idle.fbx` (idle 251 khung cũ) và `ProjectSettings/McpUnitySettings.json` (mồ côi từ gói đã xoá). Các mục này đã kiểm tra là không còn scene/prefab/script nào tham chiếu.

**Dọn lần 2 (05/10, trước khi push các map mới):**
- **Cảnh báo cho mục "Còn dọn thêm được" ngay trên: đừng xoá Sơn Tinh v1 (`Assets/Animations/SonTinh/son_tinh_idle|walk|run.fbx`)**: đó chính là **3 clip gốc của `AC_Humanoid_Base`** (cây blend Idle/Walk/Run), `AOC_SonTinh_v2` / `AOC_ThuyTinh_v2` đều override từ chúng nên xoá là vỡ cả 2 nhân vật. Cũng giữ **Thủy Tinh v1 bản Great Sword** (`thuy_tinh_*_greatsword.fbx`): sẽ dùng lại cho Thủy Tinh cầm kiếm (mục 8.1b). Mấy mục còn lại trong danh sách đó (Đinh Ba, SampleScene, TutorialInfo, Readme...) vẫn gỡ được như đã ghi.
- **Đã xoá**: 7 file ảnh trùng hệt `* - Copy.jpg` trong `ArtSource/Concept` (so từng byte), thư mục `Assets/Audio` rỗng (git không giữ thư mục rỗng, còn `Audio.meta` mồ côi sẽ làm Unity cảnh báo khi clone; thêm âm thanh thì Unity tự tạo lại), 2 texture không dùng của lâu đài (`hungvuong_palace_roughness/metallic.png`, bản gốc vẫn ở `ArtSource/Meshy/Palace_HungVuong`) và 5 vật liệu sinh tự động không còn ai tham chiếu (`Mat_PalaceStone/Plaster/Door`, `Mat_RoofTile_662E1F` của cung điện giữ chỗ cũ, `Mat_Water` của nước Simple dự phòng; builder tự tạo lại khi cần).
- **Đã sắp lại `ArtSource`** (chỉ di chuyển, không mất file): 8 clip Mixamo mới (trượt / nhảy / nhảy chạy / chết) từ thư mục lồng `Son-Tinh-vs-Thuy-Tinh_Animation/Son-Tinh-vs-Thuy-Tinh_Animation` sang `ArtSource/Mixamo/SonTinh_v2` và `ThuyTinh_v2` với **đúng tên như bản trong `Assets/Animations`** (giống quy ước cũ, đã so byte: giống hệt); 14 clip Great Sword tải cho Thủy Tinh từ `ArtSource/Meshy/ThuyTinh_v2` sang `ArtSource/Mixamo/ThuyTinh_v2/GreatSword` (giữ nguyên tên, kể cả 5 file đuôi `(1)`: Blocking, Idle, Jump, Run, Walk; **chưa so được file nào là bản đúng**, xem khi import rồi xoá bản thừa); ảnh lẻ ở gốc `ArtSource` vào `ArtSource/Concept`.
- **`.gitignore`** thêm `/ArtSource/Meshy/*.zip` (file zip tải từ Meshy, 24 MB cho lâu đài, luôn để file giải nén rồi bỏ zip).
- **Cố ý để trùng giữa `ArtSource` và `Assets`** (quy ước của dự án: `ArtSource` là nguồn, `Assets` là bản đã import): lâu đài (~24 MB), 8 clip mới (~6 MB). Nếu muốn push nhẹ hơn thì có thể bỏ bản `ArtSource/Meshy/Palace_HungVuong` khỏi git (file zip tải lại được từ Meshy) hoặc thêm vào `.gitignore`.
- Ba file scene map mới nặng 9–15 MB mỗi file (hàng nghìn cây đá là prefab instance) + 3 dữ liệu địa hình 3,5 MB: lớn nhất trong lần push này nhưng dưới ngưỡng cảnh báo 50 MB của GitHub.

### 9.6 Gói Asset Store dùng cho map sính lễ (03/10) — KHÔNG commit, mỗi người tự tải

Repo là **public** và giấy phép Asset Store không cho đưa nguyên asset lên repo công khai, nên các thư mục dưới đây nằm trong `.gitignore`. Ai clone về thì tự tải (đều miễn phí) rồi import đúng chỗ, nếu không thì cảnh dùng chúng sẽ thiếu hình/shader:

| Gói | Dùng cho | Cách dựng lại |
|---|---|---|
| [Fantasy Worlds: Forest FREE](https://assetstore.unity.com/packages/3d/environments/fantasy/fantasy-worlds-forest-free-stylized-forest-environment-open-worl-282610) (TriForge) | Cây, đá, cỏ cho map Sơn Tinh (rừng), và bờ sông cho map Thủy Tinh | Package Manager → My Assets → Download → Import (**bỏ chọn mọi mục dưới `ProjectSettings`** nếu có). Thư mục `Assets/TriForge Assets/` chỉ có 2 file `.unitypackage` bên trong, nội dung thật nằm trong đó: **import tiếp file `_URP Content - Fantasy Worlds - Old Forest DEMO.unitypackage`** (không phải bản BiRP). |
| [Simple stylized water](https://assetstore.unity.com/packages/vfx/shaders/simple-stylized-water-393128) (Houidisoft) | Nước biển/sông cho map Thủy Tinh | Import như trên vào `Assets/Houidisoft technology/`. Cảnh thử: `Simple water/Demo/Demo.unity` (nước có bọt bờ, độ sâu, chạy đúng trên Unity 6). |
| [Idyllic Fantasy Nature](https://assetstore.unity.com/packages/3d/environments/fantasy/idyllic-fantasy-nature-260042) | Cao nguyên cung điện `Map_HungVuong` (cây hoa đào/liễu/lá rộng, bụi, hoa, đá, trời, nước biển, lớp địa hình) | Package Manager → My Assets → Import vào `Assets/Idyllic Fantasy Nature/`. **Không import được khi Unity đang ở chế độ Play** (nút Import bị khoá): bấm Stop trước. Sau khi import **chạy menu Tools ▸ Son Tinh Thuy Tinh ▸ Fix Idyllic Fantasy Nature shaders for Unity 6** một lần (`Assets/Editor/IdyllicShaderFix.cs`), không thì cây/bụi/nước báo lỗi shader và hiện màu hồng. |

- Đã kiểm tra trong Unity 6000.3.24f1 + URP 17.3: 16 vật liệu và 4 shader của gói rừng đều chạy được, không bị hồng, 0 lỗi console. Cảnh thử: `Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Scenes/URP/fwOF_FreeDemo_OldForest.unity` (trong cảnh có bảng quảng cáo của hãng, bỏ đi khi dựng map thật).
- Bản miễn phí chỉ có ít model: 1 loại cây lớn (`P_fwOF_Tree_M_2`) + cây non, 1 đá + 1 tảng nhỏ, cỏ, bụi rừng. Muốn rừng rậm thì rải nhiều bản và đổi tỉ lệ/xoay, hoặc mua bản đầy đủ của hãng.
- **WaterWorks (GapperGames, miễn phí) dùng MỘT PHẦN** cho nước map Thủy Tinh. Gói viết cho Unity 2021 nên phần **`Water_Volume` (hiệu ứng nhìn từ dưới nước) gây lỗi biên dịch trên Unity 6** (`RenderTargetHandle` đã bị gỡ) và lỗi shader, làm cả project không Play được. Phần shader nước SSR (`Shaders/SSR_Water.shadergraph` + các subgraph + `WaterSSR.hlsl` + `Materials/`) thì **chạy bình thường** (0 lỗi, URP của project đã bật sẵn Depth Texture + Opaque Texture). Cách dùng: import gói WaterWorks rồi **bỏ chọn**: `Scripts/` (cả 2 file), `Resources/`, `Demo/`, `Water_Plane.prefab`, `Shaders/Volumetric_Water.shader`, `Shaders/Water_Volume.hlsl`; chỉ giữ `Materials/` và phần còn lại của `Shaders/`. Mất hiệu ứng "dưới nước" (nhìn từ trong nước ra) nhưng chưa cần. Thư mục `Assets/WaterWorks/` nằm trong `.gitignore` như các gói khác. Bản gốc đầy đủ vẫn còn ở `D:\PRU212\_asset_store_trials\` (không nằm trong project).
- Vẫn còn thiếu: đá/núi lớn và hang (chưa có gói miễn phí hợp), đền ngập nước cho voi chín ngà.

### 9.7 Map sính lễ Sơn Tinh `Map_SonTinh` (03/10) — bản khung (blockout) chạy được

Scene `Assets/Scenes/Map_SonTinh.unity`, dựng tự động bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Build Son Tinh Map** (`Assets/Editor/SonTinhMapBuilder.cs`). Chạy lại được bất cứ lúc nào: phần sinh tự động (nhóm `Generated`, địa hình) được dựng lại y hệt (seed cố định), đồ làm tay thì giữ nguyên. Muốn đổi độ dài đường, độ cong, số cây, vị trí 3 điểm dừng thì sửa hằng số đầu file rồi chạy lại. **Cần đã import các gói ở mục 9.6** (không có thì địa hình vẫn ra nhưng không có cây đá và phủ màu).

- **Cấu trúc**: bắt đầu từ bản sao của `Sandbox_Combat` (camera Cinemachine, `PlayerSpawn`, HUD máu/stamina, menu tạm dừng, `EventSystem`, 3 `GiftPickup`), bỏ sàn caro/trụ/DebugHUD. `PlayerSpawn.Fallback Character` = Sơn Tinh (chạy thẳng scene này cũng ra Sơn Tinh).
- **Địa hình**: `Assets/Art/Maps/SonTinh/Terrain_SonTinh.asset` (**220×370 m** từ bản mở rộng 03/10, trước đó 130×240; sinh bằng code, tô 4 lớp: cỏ, đất đường mòn và các bãi, đá ở chỗ dốc, rêu). Đường mòn uốn lượn dài ~295 m dọc trục +Z; **thung lũng rộng mỗi bên đường 30 m** rồi mới tới vách dốc (>45°) nhốt người chơi, hai đầu cũng đóng kín (đầu cuối đóng ngay sau cổng ra nên không đi vượt qua được). Ba **"vịnh"** bên hông, nơi vách lùi ra để có chỗ cho làng, vòng đá và lùm cây cổ: z≈45 bên trái, z≈130 bên phải, z≈212 bên trái (xem bullet "Chất Sơn Tinh"). Muốn đổi kích thước/vị trí vịnh thì sửa hằng số `Width`/`Length`/`PathEnd`/`ValleyHalfWidth`/`Bays` đầu `SonTinhMapBuilder.cs`.
- **Rừng**: ~280 cây lớn (có LOD), ~120 tảng đá, ~2.300 bụi/cỏ/đá nhỏ (nhóm `Generated`; scene nặng ~13 MB), sương mù, ánh sáng xanh lục ấm, gió của gói. Cây đá là prefab instance nên scene nhẹ.
- **3 điểm dừng** (theo đúng thứ tự mục 8.2): z≈85 **gò đá** có ngựa chín hồng mao trên đỉnh (traversal); z≈175 **bãi tròn ~12 m vây tảng đá**, đường vào và ra để hở (chỗ mini-combat gà chín cựa); z≈250 **bệ đá giữa hai trụ đá** (voi chín ngà, sau này cho tương tác "phép đất"). Mỗi điểm có 1 `GiftPickup` (vật giữ chỗ màu vàng + đèn điểm sáng để nhìn thấy từ xa) gán đúng 3 asset sính lễ.
- **Cổng ra** `Exit_To_Next` ở z≈295, `SceneTransitionTrigger` khoá tới khi đủ `Quest_SinhLe`. **Từ 03/10 (bản theo bản đồ thế giới)** 30 m cuối đường **dốc lên cao 7 m** (`ClimbStart/ClimbEnd/ClimbHeight` trong `SonTinhMapBuilder.cs`, độ dốc tối đa ~19°, có lát ván và đuốc) rồi tới **cổng đình** (2 trụ đỏ, xà ngang, mái ngói 2 tầng, cờ; `MapDecor.Gate`) kèm 2 trụ đá hai bên; qua cổng là scene **`Map_HungVuong`** (`SceneNames.PalaceMap`, mục 9.10).
- **Theo bản đồ thế giới (03/10)**: đường bên trái của bản đồ là map Sơn Tinh (mục 9.9). Trang trí thêm cho khớp: **vịnh z≈130 = "Khu rừng linh" (Mystic Grove)**: bệ đá giữa vòng 8 trụ đá có **cụm pha lê phát sáng xanh ngọc** (`MapDecor.Crystals`, có đèn), **hồ nước nhỏ** bên cạnh (đào lõm trong địa hình, `PondCentre`/`PondRadius`) và 3 đuốc; **vịnh z≈212 = "Rừng Rối" (Tangled Woods)**: cây cổ thụ + **trại: lửa trại (`MapDecor.Campfire`, hạt lửa + đèn nhấp nháy) và 3 lều vải (`MapDecor.Tent`)** + cờ; **lối ván gỗ** từ đường chính vào làng z≈45 và lối ván lên dốc cuối map (`MapDecor.PlankWalk`, chỉ là ván đặt trên mặt đất có trụ và dây, không phải cầu).
- **Nối vào luồng game**: `Character_SonTinh.asset` → `Gift Branch Scene` = `Map_SonTinh` (hằng số `SceneNames.SonTinhMap`), scene đã thêm vào Build Settings. Thủy Tinh vẫn vào Sandbox_Combat.
- **Chất Sơn Tinh (đồ trang trí, 03/10)**: bộ dựng chung `Assets/Editor/MapDecor.cs` (không cần asset ngoài: khối nguyên thủy, mesh tiện, hệ hạt), vật liệu và mesh sinh ra nằm ở `Assets/Art/Maps/Decor/`. Trong map: **núi Tản Viên mờ xa** (9 đỉnh núi nhọn xanh thẫm ngoài rìa bản đồ, dựa vào sương mù), **trống đồng** (mesh tiện theo dáng trống Đông Sơn, chân loe, eo thắt, mặt trống rộng có mặt trời nổi ở giữa) ở đầu đường và bên cạnh sính lễ voi trên bệ đá, **~16 đuốc** có lửa (hệ hạt) + đèn nhấp nháy (`Assets/Scripts/Environment/LightFlicker.cs`) dọc đường và quanh 3 điểm dừng, **cờ hiệu** đỏ/vàng đất ở đầu đường, trước gò và ở cổng ra, **2 thác nước** đổ xuống vách đá sau làng (z≈45) và sau lùm cây cổ (z≈212), mỗi thác rơi vào 1 vũng nước xanh ngọc (xem mục "Thác nước" ngay dưới), **3 nhà sàn** của người dân núi dọc đường (chỗ đất phẳng, rừng quanh nhà được dọn), **đom đóm** vàng xanh bay trên đường. **Ba vịnh bên hông (map mở rộng)**: *làng* (z≈45, bên trái: 2 nhà sàn quay mặt vào trống đồng + 4 đuốc), *vòng đá* (z≈130, bên phải: 8 trụ đá đứng quanh bệ có trống đồng, 4 đuốc), *lùm cây cổ* (z≈212, bên trái: 1 cây rất lớn giữa vòng 6 cờ + 2 đuốc). Muốn thêm/bớt thì sửa `BuildTheme` trong `SonTinhMapBuilder.cs` rồi chạy lại menu build.
- **Chưa có**: kẻ địch/đòn đánh cho đoạn gà chín cựa (chờ combat, mục 8.7), tương tác "phép đất" cho voi, thử thách traversal thật cho ngựa (hiện chỉ là leo gò), hang/núi đá lớn, mô hình ngựa/gà/voi thật (đang là khối vàng), âm thanh, tin nhắn "chưa đủ sính lễ" khi tới cổng sớm (cổng chỉ không làm gì). Mô hình cây trong bản miễn phí chỉ có 1 loại nên rừng hơi lặp, nhìn gần nhận ra. Đồ trang trí làm bằng khối đơn giản (nhà sàn, cờ, đuốc nhìn gần hơi thô), thay bằng model Meshy khi có thời gian.
- **Thác nước (03/10, làm bằng code, không dùng asset ngoài)**: `MapDecor.Waterfall` dựng 1 dải mesh bám theo mặt đất từ mép vách xuống chân vách (nên chảy được trên sườn dốc bất kỳ, tự tìm chỗ vách đủ cao ≥14 m bằng `MapDecor.FindSlope`), gắn shader riêng `Assets/Art/Maps/Decor/Waterfall.shader` (URP, trong suốt, vệt nước chạy xuống bằng noise, mỏng ở mép trên, bọt ở chân, có sương mù của scene), cộng 2 hệ hạt ở chân (sương trắng bốc lên + hạt nước bắn). Map Sơn Tinh có thêm **vũng nước dưới chân thác**: địa hình được đào lõm 0,9 m có bệ phẳng (`PlanPools`/`FallFoot`/`PadHeight` trong `SonTinhMapBuilder.cs`), mặt nước là đĩa dùng bản sao vật liệu "Simple stylized water" (`Assets/Art/Maps/SonTinh/Mat_Pool.mat`, cần gói Houidisoft). Thác **chưa có âm thanh**, không có va chạm (đi xuyên qua được), và chưa có logic gì (làm ướt, đẩy người chơi...).
- **Cách chạy thử**: mở `Map_SonTinh.unity` → Play → click vào cửa sổ Game (WASD đi, Shift chạy nhanh, chuột xoay camera, Space nhảy, Ctrl trái né, Esc tạm dừng). Hoặc chạy từ MainMenu rồi chọn Sơn Tinh.

### 9.8 Map sính lễ Thủy Tinh `Map_ThuyTinh` (03/10) — bản khung chạy được

Scene `Assets/Scenes/Map_ThuyTinh.unity`, dựng bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Build Thuy Tinh Map** (`Assets/Editor/ThuyTinhMapBuilder.cs`), cùng công thức với map Sơn Tinh (bản sao Sandbox_Combat + địa hình sinh bằng code + cây đá + ánh sáng + cổng ra), có dùng chung các hàm trong `SonTinhMapBuilder.cs` và `MapDecor.cs`. Cần các gói ở mục 9.6 (rừng + nước).

- **Địa hình** (`Assets/Art/Maps/ThuyTinh/Terrain_ThuyTinh.asset`, **220×370 m** sau bản mở rộng 03/10): con đường đắp cao hơn mặt nước ~0,75 m uốn lượn dài ~295 m giữa đầm; hai bên là **đầm nước nông rộng 30 m mỗi bên** có những cồn cỏ; vách đá dốc hai bên và hai đầu đóng kín như map Sơn Tinh. Ba **cồn nhỏ giữa đầm** (z≈40 trái, z≈135 phải, z≈215 trái) mang nhà sàn của dân sông nước (2 cồn) và cụm trụ đá (1 cồn), tới được bằng cách lội qua nước nông. Sửa hằng số `Width`/`Length`/`PathEnd`/`ChannelStart`/`Bays` đầu `ThuyTinhMapBuilder.cs` để đổi.
- **Nước**: mặt phẳng nước, không có collider. Mặc định dùng shader nước **WaterWorks** (phản chiếu + khúc xạ màn hình, bọt bờ): `Assets/Art/Maps/ThuyTinh/Mat_WaterWorks.mat` là bản sao của `SSR_Water.mat` đã chỉnh màu xanh lục lam, **giảm `Caustic Strength` từ 2 xuống 0,15** (mặc định làm cả mặt nước lấp lánh như hạt) và `Normal Strength` 0,06. Nếu không có gói WaterWorks thì tự rơi về nước "Simple stylized water" (`Mat_Water.mat`). Muốn ép dùng Simple: menu **Tools ▸ Son Tinh Thuy Tinh ▸ Build Thuy Tinh Map (Simple stylized water)**. Nước WaterWorks lấp lánh/thực hơn, Simple mịn/hoạt hình hơn hợp phong cách cây rừng hơn một chút; hai kiểu đều chạy trên Unity 6. Lần đầu mở scene shader có thể mất vài giây biên dịch, lúc đó mặt nước trống.
- **3 điểm dừng** (theo đúng mục 8.3): z≈62–82 **kênh sông cắt ngang đường**, qua bằng **bè khúc gỗ nổi** (~20 khúc gỗ xếp liền, có collider hộp để đi được), sính lễ ngựa ở bờ bên kia z≈92 giữa 2 trụ đá; z≈175 **cồn đất giữa hồ** có vòng tảng đá (mở 2 đầu cho đường), sính lễ gà ở giữa (chỗ mini-combat sau này); z≈252 **đền ngập nước**: bệ đá giữa 4 cột đá đứng trong nước nông (lội ngang gối) và 2 trụ đá cổng, sính lễ voi trên bệ. Cổng ra ở z≈295.
- Sườn kênh dốc ~14° nên rơi xuống nước vẫn leo lên bờ được (không kẹt). **Chưa có cơ chế chết đuối / lội nước chậm lại**: hiện đi xuyên qua nước như đi đường, bè chỉ là đường ưu tiên (chưa có thử thách thật).
- **Thác nước**: 2 thác đổ xuống vách đá vào đầm, 1 bên trái (z≈105) và 1 bên phải (z≈185), cùng kỹ thuật với map Sơn Tinh (xem mục 9.7 "Thác nước"): dải nước chảy bám sườn vách, shader riêng, sương và hạt nước bắn ở chân; nước đầm là mặt nước WaterWorks nên không cần đào vũng riêng.
- **Trang trí**: núi xa xanh lam, đuốc ở đầu đường, 2 đầu bè, quanh cồn và cổng ra, đom đóm xanh nhạt trên mặt nước. Sương mù xanh xám, ánh sáng lạnh hơn map Sơn Tinh.
- **Nối vào luồng game**: `Character_ThuyTinh.asset` → `Gift Branch Scene` = `Map_ThuyTinh` (hằng số `SceneNames.ThuyTinhMap`), đã thêm vào Build Settings. **Từ 03/10** cuối đường cũng dốc lên 7 m khỏi mặt nước (cùng hằng số `ClimbStart/End/Height` với map Sơn Tinh), có lối ván + đuốc, rồi tới cổng đình (cờ xanh lam) dẫn vào `Map_HungVuong`.
- **Theo bản đồ thế giới (03/10)**: đường bên phải của bản đồ là map Thủy Tinh. Thêm **3 cầu ván có collider** từ đường đắp sang 3 cồn nhỏ (`BuildBridges`, `MapDecor.PlankBridge`: ván + trụ + dây rào), và biến cồn z≈135 thành **"Hang Chìm" (Sunken Grotto)**: vòm đá (2 trụ + phiến ngang) phủ lên **cụm pha lê phát sáng xanh lam và tím** (có đèn), vài tảng đá xung quanh. Hai thác nước (z≈105, z≈185) giữ làm "Thác Mờ Sương" (Veiled Falls). Bãi cát chưa làm riêng (đất đường mòn thay).
- **Chưa kiểm khi chạy thật**: cả 2 map mới chỉ chụp trong editor và chạy được Play đến lúc nhân vật xuất hiện (vòng lặp game bị đứng vì cửa sổ Unity không hiện), chưa thử đi hết đường, nhặt đủ 3 sính lễ và qua cổng trong Play. Cần chạy thử bằng tay.
- **Chưa có**: bản đồ vẫn dùng chung kiểu cây "rừng" cho đầm (chưa có cây đầm lầy, sen, sậy riêng), kẻ địch cho cồn, tương tác "phép nước" cho voi, nhạc/âm thanh nước.

### 9.9 Bản đồ thế giới và vòng tròn bản đồ trên HUD (03/10)

**Tranh bản đồ**: ý tưởng bố cục lấy từ ảnh minh hoạ bạn đưa (`docs/reference/world_map.webp`, giữ trong git làm tranh gốc: đường trái = Sơn Tinh, đường phải = Thủy Tinh, giữa trên cùng là cao nguyên cung điện). **Bản dùng trong game không còn là ảnh vẽ đó nữa mà là ảnh chụp từ trên xuống chính 3 map thật** (`Assets/Art/UI/Map/world_map.jpg`, 1890×1537, ~600 KB) để hình khớp với những gì đang dựng: cột trái là map Sơn Tinh (rừng, đường mòn, nhà sàn, trại lều, hồ ở khu rừng linh), cột phải là map Thủy Tinh (kênh nước, đường đắp, cầu ván sang các cồn, bè gỗ), giữa trên cùng là cao nguyên cung điện (quảng trường trống đồng, đại lộ, cung điện giữ chỗ), nối bằng đường chấm vàng từ cổng cuối mỗi đường tới cổng tây/đông của cao nguyên; nền là biển, viền vàng. Tên địa danh là chữ tiếng Việt do HUD vẽ đè lên (`Cung điện Vua Hùng`, `Rừng Rối`, `Khu rừng linh`, `Làng sơn cước`, `Vịnh Đầm Lầy`, `Thác Mờ Sương`, `Hang Chìm`); vị trí chúng lấy từ đồ vật thật trong scene (lửa trại, cụm pha lê, nhà sàn, bè gỗ, thác nước).

**Cách hoạt động** (`Assets/Scripts/UI/Map/`, namespace `SonTinhThuyTinh.UI.Map`):
- `WorldMapData` (ScriptableObject, `Assets/Data/Map/WorldMap.asset`): tranh + danh sách địa danh (vị trí tính theo tỉ lệ từ góc trên-trái).
- `MapRoute` (đặt trong từng scene): vì tranh là ảnh chụp từ trên xuống nên chỉ cần **một phép dịch + co** từ toạ độ thế giới (x, z) ra điểm trên tranh (+x sang phải, +z lên trên, mũi tên quay đúng bằng hướng nhân vật); kèm vị trí 3 chấm sính lễ. Phép dịch của từng scene nằm trong `Assets/Editor/WorldMapLayout.cs` (3,5 điểm ảnh mỗi mét).
- `MapHUD` (trên canvas `MapCanvas`, sortingOrder 5): **vòng tròn 300 px ở góc phải dưới** hiện 1 mảnh tranh (zoom 0,2) quanh người chơi, mũi tên màu theo nhân vật (Sơn Tinh cam đỏ, Thủy Tinh xanh lam, cung điện vàng); **bấm vào vòng tròn hoặc phím M / nút Select tay cầm** mở bản đồ lớn (mũi tên nhấp nháy ở chỗ đang đứng, chấm sính lễ xám → vàng khi đã nhặt, tên địa danh tiếng Việt); đóng bằng M / Esc / nút Đông tay cầm / bấm chuột. Mở bản đồ **dừng game** (`Time.timeScale = 0`) và tắt `ThirdPersonCameraInput` để hiện chuột, giống menu tạm dừng; `PauseMenu` được vá để Esc đóng bản đồ không bật luôn menu tạm dừng.
- `PlayerSpawner` có thêm ô `Map Hud` (tự gán) và mảng `Spawn Points` (mỗi nhân vật 1 điểm xuất hiện riêng, dùng ở map cung điện).
- **Dựng bằng code**: `Assets/Editor/MapHudBuilder.cs` (`Build(title, màu, khung toạ độ, chấm sính lễ)`), được 3 map builder gọi (hàm `BuildMapHud`). **Tranh bản đồ do menu Tools ▸ Son Tinh Thuy Tinh ▸ Rebuild World Map Picture tự làm** (`Assets/Editor/WorldMapComposer.cs`): mở lần lượt 3 scene, chụp từ trên xuống bằng camera trực giao (tắt sương mù, tắt bóng đổ, bỏ núi xa và ô sính lễ, thay nước đầm bằng màu xanh phẳng, tăng sáng/màu cho đều), ghép + viền + chấm nối, ghi `world_map.jpg`, ghi danh sách địa danh vào `WorldMap.asset` rồi dựng lại giao diện bản đồ của cả 3 scene. **Sửa map nào thì chạy lại menu này** để tranh và mũi tên khớp. Cần Edit mode (không Play), và 3 scene đã dựng bằng các menu build của chúng.
- **Chưa kiểm khi chạy thật**: đã chụp bố cục UI trong editor (vòng tròn, bản đồ lớn, tên địa danh, mũi tên đặt thử ở một vật thật thì rơi đúng chỗ) nhưng chưa bấm thử trong Play (cần bạn thử: bấm vòng tròn, M, Esc, đi bộ xem mũi tên chạy đúng đường, tay cầm). Chưa có chấm "đã nhặt" cho bản đồ cung điện (không có chấm sính lễ ở đó). Bản đồ lớn hiện nhỏ hơn màn hình vì tranh khá vuông (tỉ lệ 1,23), có thể phóng to khung nếu thấy khó đọc.

### 9.10 Cao nguyên cung điện Vua Hùng `Map_HungVuong` (03/10) — cung điện đang là chỗ giữ chỗ

Scene `Assets/Scenes/Map_HungVuong.unity`, dựng bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Build Palace Map** (`Assets/Editor/PalaceMapBuilder.cs`). Đây là "map ở giữa" của bản đồ thế giới, dùng gói **Idyllic Fantasy Nature** (mục 9.6, phải import + chạy menu sửa shader trước, không có thì menu build báo lỗi và dừng). Bắt đầu từ bản sao `Sandbox_Combat` như các map khác (bỏ `GiftPickups`).

- **Địa hình** `Assets/Art/Maps/HungVuong/Terrain_HungVuong.asset` (300×300 m, sinh bằng code): cao nguyên tròn bán kính ~104 m ở độ cao +14 m, bờ vách đá dốc đổ thẳng xuống biển (đáy biển sâu −45 m để shader nước ra xanh thẫm, nước nông thì trong suốt nên nhìn thấy đáy). Tô 4 lớp: cỏ (bản sao `TL_Grass.terrainlayer` đã chỉnh bớt vàng), đất cho đường, đá cho vách, đá lát cho quảng trường + đại lộ + sân cung điện. Biển + 11 đỉnh núi xa xanh lam (kiểu vịnh đá vôi, cùng hàm `MapDecor.DistantMountains`). **Tường vô hình** 72 hộp dọc mép vách để không rơi khỏi cao nguyên.
- **Bố cục**: **cổng tây** (cờ đỏ, chỗ Sơn Tinh bước lên) và **cổng đông** (cờ xanh lam, chỗ Thủy Tinh bước lên) cách tâm 62 m, hai con đường đất uốn lượn gặp nhau ở **quảng trường lát đá bán kính 15 m có trống đồng lớn** ở chính giữa (4 đuốc + 4 cờ); từ quảng trường một **đại lộ lát đá** (cờ hai bên) chạy về phía bắc tới **cổng cung điện** ở z=36. `PlayerSpawner.Spawn Points`: Sơn Tinh xuất hiện ở trong cổng tây nhìn về tâm, Thủy Tinh ở trong cổng đông (chạy thẳng scene này không qua chọn nhân vật thì ra Sơn Tinh).
- **Cung điện (05/10): đã thay chỗ giữ chỗ bằng model Meshy "Crimson Phoenix Citadel"** do bạn đưa (`ArtSource/Meshy/Palace_HungVuong/`, giải nén từ file zip; bản dùng trong game ở `Assets/Art/Palace/`: `hungvuong_palace.fbx` 7.629 tam giác + 4 texture `_basecolor/_normal/_metallic/_roughness`, **chỉ dùng basecolor + normal**, metallic/roughness đặt hằng số 0,05 / 0,28 trong vật liệu `M_HungVuongPalace.mat`). Đó là một thành lũy có tường đá, bậc thềm lên cổng, lầu cổng mái cong, cờ phượng đỏ, trống đồng và cột cờ lông vũ. `PalaceModelSetup.cs` dựng vật liệu + prefab `Palace_HungVuong.prefab` (xoá prefab đi để dựng lại nếu đổi vật liệu); `PalaceMapBuilder.BuildModel` đặt model rộng **48 m** (sâu 48 m, cao 33 m) ở x∈[−24, 24], z∈[36, 84], mặt trước (bậc thềm) quay về phía nam nhìn ra quảng trường, đáy chạm sân phẳng +14 m (địa hình tự phẳng + lát đá theo khung này), thêm `MeshCollider` cho mesh nên tường đặc còn **lối cổng để trống đi xuyên qua được**, 2 trống đồng + 2 đuốc trước bậc thềm. **Cổng ra `Exit_To_Next`** nằm trong lối cổng cách mặt trước ~7,7 m. Không có file model thì builder tự quay về **bản giữ chỗ cũ** (nhóm `PalacePlaceholder`: tường 68×44 m, 4 tháp, cổng đình, chính điện 2 tầng mái, bảng chữ "chỗ giữ chỗ"). Muốn đổi kích thước thì sửa `ModelWidth`/`ModelFront` đầu `PalaceMapBuilder.cs` rồi chạy lại menu build và menu **Rebuild World Map Picture**.
- **Đã kiểm trong Play (05/10)**: cho nhân vật đi thẳng từ đại lộ vào cổng bằng `Assets/Scripts/DevTools/GateWalkTest.cs`: đi qua bậc thềm và lối cổng không bị kẹt (độ cao giữ ~14 m, tức bậc thềm thấp hơn bước chân 0,35 m của controller), chạm `Exit_To_Next` thì tải sang `Sandbox_Combat` đúng. **Chưa kiểm**: va chạm khi nhảy/trượt sát tường lâu đài, bóng đổ và hiệu năng với mesh collider trong Play lâu, nhân vật có đi lên được sân trong phía sau cổng không (model có sân/lầu bên trong, chưa mở thêm lối vào).
- **Cây cỏ**: ~170 cây (lá rộng xanh, hoa đào hồng, liễu gần mép biển), ~230 bụi, ~700 cây nhỏ/cỏ, ~500 hoa, ~160 mảng hoa, ~160 đá nhỏ, 36 tảng đá bên mép vách; chừa trống đường, quảng trường, đại lộ và sân cung điện. Gió dùng prefab `WindControl` của gói (nhớ giữ trong scene).
- **Cổng ra** `Exit_To_Next` ngay trong cổng cung điện (không đòi sính lễ), đang dẫn sang `Sandbox_Combat` làm chỗ giữ cho cutscene phán xét + trận đánh cuối (mục 8.8). `Map_HungVuong` đã vào Build Settings (trước `Sandbox_Combat`).
- **Bản đồ**: cao nguyên là phần giữa-trên của tranh thế giới (ảnh chụp từ trên xuống của chính scene này, mục 9.9); không có chấm sính lễ.
- **Chưa kiểm khi chạy thật** (chỉ chụp trong editor), chưa có: âm thanh, bướm/hạt lá của gói (`ButterflySpawnArea`, `Particles` — chưa thêm), hiệu ứng hậu kỳ của gói (camera game đang tắt post-processing nên không dùng), NPC Vua Hùng / Mị Nương (chờ model, mục 8.4), cầu thang/đường lên từ hai cổng (hiện bước thẳng vào cao nguyên từ cổng). Bảng chữ "chỗ giữ chỗ" chỉ còn ở bản giữ chỗ dự phòng.

### 9.11 Trượt, nhảy, nhảy chạy, bị đánh gục (03/10)

Animation Mixamo bạn thêm ở `ArtSource/Son-Tinh-vs-Thuy-Tinh_Animation/` (từ 05/10 đã sắp vào `ArtSource/Mixamo/SonTinh_v2` và `ThuyTinh_v2`) (8 file: mỗi nhân vật có Running Slide, Stay Jumping Up, Running Jump (Thủy Tinh tên "Jump"), Death) đã nhập vào `Assets/Animations/SonTinh_v2/` và `ThuyTinh_v2/` (`*_slide`, `*_jump_up`, `*_run_jump`, `*_death`), Humanoid, không loop, tên clip `Slide`/`JumpUp`/`RunJump`/`Death`. Bản gốc ở `ArtSource/Mixamo/` (không đụng).

**Phím** (`Assets/Input/PlayerControls.inputactions`; **Space trước kia là né nên né đã chuyển sang Ctrl trái**, tay cầm vẫn là B):
| Hành động | Bàn phím | Tay cầm | Điều kiện |
|---|---|---|---|
| Nhảy lên (jump up) | Space | A | đang đứng hoặc chạy thường; chùn gối ~0,3 s rồi bật cao ~0,9 m, giữ ~60% tốc độ |
| Nhảy chạy (running jump) | **Shift (đang chạy) + Space** | cần trái nhấn + A | bật ngay, cao ~1 m, **giữ nguyên đà chạy**, bay ~5 m |
| Trượt (slide) | **Shift (đang chạy) + C** | cần trái nhấn + vai phải | chỉ khi đang sprint; trượt ~6,5 m trong 1,53 s, ra khỏi trượt thì chạy tiếp |
| Bị đánh gục | (máu về 0) | | `Health.Died` → ngã xuống và nằm yên; trong editor L = gục ngay, R = đứng dậy |
"Đang chạy" = giữ Shift + đang có phím di chuyển + tốc độ ≥ 80% tốc độ chạy (`PlayerController.IsSprinting`). Bấm C khi không sprint thì bị bỏ.

**Code** (`Assets/Scripts/Player/`): thêm 3 state vào máy trạng thái của `PlayerController`: `PlayerJumpState` (dùng cho cả 2 kiểu nhảy, `running` true/false: vận tốc dọc từ độ cao muốn đạt theo gravity của controller, lái được trong không trung, tiếp đất khi `IsGrounded`), `PlayerSlideState` (lướt theo hướng đang nhìn, đường cong quãng đường đo từ chính clip), `PlayerDeathState` (phát clip chết một lần rồi đứng yên, vẫn chịu trọng lực). Số chỉnh được trên `PlayerController` (nhóm Jump, Slide) qua `JumpSettings.cs` / `SlideSettings.cs`: độ cao, thời gian chùn gối, lực lái trên không, quãng đường/thời gian/hồi chiêu của trượt. Số mặc định đã khớp độ dài clip (thời điểm bật và tiếp đất của clip nhảy, 1,53 s của clip trượt), đổi clip thì chỉnh lại. `PlayerInputReader` có thêm `ConsumeJump()` / `ConsumeSlide()` (nhớ phím bấm sớm 0,2 s như dodge) và `ClearBuffered()`.

**Animator**: `AC_Humanoid_Base` có thêm 4 state `Slide`/`JumpUp`/`RunJump`/`Death` (không có transition, code `CrossFade` theo tên giống `Dodge`; clip gốc của state là clip Sơn Tinh); `AOC_SonTinh_v2` và `AOC_ThuyTinh_v2` thay bằng clip của riêng từng nhân vật. Dựng/chạy lại bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Set Up Slide, Jump and Death Animations** (`Assets/Editor/ExtraMovesBuilder.cs`: cấu hình importer, thêm state, gán override). Thiết lập root: clip trượt và chết **giữ chuyển động lên xuống của người trong pose** (cơ thể thấp xuống / nằm), clip nhảy **không giữ** (độ cao nhảy do vật lý, không do clip, để khỏi bị cộng đôi); `Animator.applyRootMotion` vẫn tắt, quãng đường đi do code.

**Đã kiểm khi chạy thật** (Play, gõ phím ảo bằng `Assets/Scripts/DevTools/MovesSmokeTest.cs`, ghi vào `Temp/moves_smoke_test.txt` + ảnh `Temp/smoke_*.png`): trên Sơn Tinh (map Sơn Tinh) và Thủy Tinh (Sandbox): chạy → sprint → nhảy chạy (state `RunJump`, bay ~0,6 s, cao ~1 m, tiếp đất vẫn chạy tiếp) → trượt (`Slide`, trượt đủ 6,5 m trên nền phẳng; trên đường rừng có thể bị cây đá chặn) → nhảy lên (`JumpUp`, bật sau ~0,3 s, cao ~0,9 m) → bị đánh gục (`Death`, nằm sấp trên sàn, không chìm xuống đất) → đứng dậy (về `Locomotion`). Ảnh chụp cho thấy tư thế đúng ở cả 4 clip. **Chưa kiểm bằng tay**: cảm giác điều khiển, tay cầm, nhảy trên dốc/mép vách, va chạm khi trượt sát vật cản, camera khi nằm gục. Chưa có: hoạt cảnh ngã (falling) khi rơi khỏi mép, hoạt cảnh bị đánh trúng không chết (flinch/knockdown ngắn), màn hình thua / hồi sinh thật (hiện chỉ có phím R trong editor), tiếng bước/va chạm.
**Cách tự chạy bài thử**: mở scene gameplay, Play, thêm `MovesSmokeTest` vào một object rỗng, đợi ~25 s rồi đọc 2 file trên (bài thử tự thoát Play). Nếu Unity ở nền thì cần `Application.runInBackground = true` (bật tạm bằng script, nhớ tắt lại), nếu không game không chạy khi cửa sổ không được focus.

---
Bảng việc chi tiết + phân người: [Claude Doc](https://claude.ai/code/artifact/9b63c56a-f193-4db3-a892-4930ebe64584)

### Gà chín cựa (model thật, 05/10)
- **Nguồn**: `ArtSource/Meshy/Rooster/` (model Meshy "Embercrest Rooster" có texture) và `ArtSource/Blender/Rooster/Rooster_Rigged_Color.blend` (đã rig 19 xương gồm 2 đốt cánh mỗi bên, kèm 3 animation **Walk** 73 khung / **Eat** 83 khung / **Fly** 17 khung, 24 fps). Model cao ~0,7 m, gốc toạ độ ở chân.
- **Trong game**: `Assets/Art/Characters/Rooster/rooster.fbx` + `Textures/` (chỉ dùng basecolor + normal, như cung điện). Menu **Tools ▸ Son Tinh Thuy Tinh ▸ Rooster ▸ Setup Ga chin cua (prefab + 3 scenes)** (`Assets/Editor/RoosterGiftSetup.cs`) dựng vật liệu `M_Rooster`, `AC_Rooster` (mặc định phát **Fly**, đổi default state sang Eat/Walk nếu muốn), prefab `Assets/Prefabs/Gifts/Rooster_GaChinCua.prefab`, rồi thay khối vàng bên trong `Visual` của `Pickup_GaChinCua` ở `Map_SonTinh`, `Map_ThuyTinh`, `Sandbox_Combat` (chạy lại không sao, pickup đã thay thì bỏ qua). `GiftPickup` vẫn xoay/nhấp nhô `Visual` như cũ. Hai MapBuilder đã sửa để lúc dựng lại map, `Visual` có gà thì đặt ở độ cao 0,9 thay vì 1,4.

### 9.12 Cutscene Hùng Vương phán xử `Cutscene_PhanXu` (06/10)

Dựng bằng menu **Tools ▸ Son Tinh Thuy Tinh ▸ Build Judgement Cutscene** (`Assets/Editor/JudgementCutsceneBuilder.cs`, chạy lại là dựng lại từ đầu). Luồng: `Map_HungVuong` (cổng cung điện) → `Cutscene_PhanXu` → `Sandbox_Combat` (chỗ giữ cho arena, mục 8.8; đổi ở `nextScene` của `JudgementDirector`). Scene đã vào Build Settings ngay sau `Map_HungVuong`.

- **Bối cảnh**: bản sao `Map_HungVuong` (cao nguyên, cung điện, đại lộ, ánh sáng) bỏ hết đồ gameplay (camera theo người chơi, spawn, HUD, tạm dừng, bản đồ, cổng ra). Hùng Vương (prefab `HungVuong`, mục NPC) đứng chân bậc thềm cung điện (z = 33,6), hai chàng đứng trên đại lộ nhìn lên vua (Sơn Tinh bên trái, Thủy Tinh bên phải; lấy phần hình của prefab người chơi lúc chạy, như màn chọn nhân vật).
- **Intro**: Timeline `Assets/Data/Cutscene/TL_PhanXu_Intro.playable` với 1 Cinemachine track (máy quay hạ từ trên cao xuống đại lộ rồi vào cảnh toàn, ~7 s, bấm Space/Esc/click để bỏ qua).
- **Hội thoại**: khung chữ của Prologue (chuyển sang), 11 câu, 2 bản theo nhân vật đã chọn (`Assets/Data/Dialogue/Dialogue_PhanXu_SonTinh/ThuyTinh.asset`): hai chàng cùng đến với đủ voi chín ngà / gà chín cựa / ngựa chín hồng mao, vua khó xử, Mị Nương (tranh `02_mi_nuong` toàn màn hình, chỉ ở câu của cô: `DialogueRunner.keepIllustration` = tắt) xin nghe cha, vua phán hai người ra đàn tế đấu một trận, ai thắng rước Mị Nương. `JudgementCutsceneDirector` cắt camera theo từng câu (toàn cảnh, vua, vua góc thấp, cận người chơi, cận đối thủ, qua vai vua) và bật động tác của vua (Talk / Point / Nod / Idle). Hết thoại (hoặc Esc) thì tải scene tiếp.
- **Đã chạy thử trong Play (bản Sơn Tinh)**: intro, cả 11 câu đúng camera + tranh Mị Nương, hết thoại tải `Sandbox_Combat`. **Chưa chạy thử bản Thủy Tinh** (cùng code, chỉ khác file thoại). Mặt đất hiện ô caro vì gói Idyllic Fantasy Nature chưa có trên máy này (cây cỏ cũng báo Missing Prefab), giống `Map_HungVuong`.

