# Sơn Tinh - Thủy Tinh — Game Overview

> Đồ án môn PRU212 — game 3D action-adventure dựa trên truyền thuyết Sơn Tinh - Thủy Tinh.
> Yêu cầu: dùng C# (Unity), đồ họa chất lượng cao (định hướng như Black Myth Wukong ở trận đánh cuối).

## 1. Cốt truyện & cấu trúc gameplay

**Flow tổng thể:**

```
Character Select (Sơn Tinh / Thủy Tinh)
        ↓
Nhánh lấy sính lễ (riêng theo nhân vật đã chọn)
        ↓
Cutscene: Hùng Vương phán xử
        ↓
Trận đánh cuối (style Black Myth Wukong)
        ↓
Ending theo kết quả trận đấu
```

- Người chơi chọn 1 trong 2 nhân vật: **Sơn Tinh** hoặc **Thủy Tinh**
- Mỗi nhân vật có nhánh riêng để đi lấy sính lễ (map/thử thách khác nhau tùy nhân vật)
- Sau khi lấy đủ sính lễ, vào cutscene Hùng Vương phán xử — dẫn thẳng tới trận đánh cuối
- **Trận đánh cuối**: nhân vật người chơi chọn sẽ đấu với nhân vật đối lập (chọn Sơn Tinh → đánh Thủy Tinh, và ngược lại)
- **Kết thúc không cố định theo truyền thuyết gốc** — ai thắng trận đấu, người đó được Mị Nương. Có 2 đoạn epilogue khác nhau tùy kết quả, để cả 2 lựa chọn nhân vật đều công bằng với người chơi.

### Trận đánh cuối — chi tiết đã chốt
- Combat theo phong cách Black Myth Wukong: combo đòn thường/mạnh, dodge, lock-on target, hit reaction
- VFX tương phản 2 nguyên tố: đất/đá (Sơn Tinh) vs nước (Thủy Tinh) — điểm nhấn thị giác chính
- Đây là scene được đầu tư polish nhiều nhất (lighting, camera cinematic, VFX) thay vì dàn trải đều cho cả game
- Gợi ý (chưa chốt): cơ chế môi trường động theo đúng truyền thuyết — nước dâng dần / địa hình núi cao lên theo thời gian trận đấu, thay vì arena tĩnh

### ⚠️ Còn cần bàn kỹ trước khi code (TODO)
- [ ] Layout cụ thể của arena trận đánh cuối, số phase trong trận
- [ ] Cơ chế combat chi tiết: số loại đòn, có stamina không, có parry không
- [ ] Gameplay cụ thể của từng nhánh lấy sính lễ (đi tìm đồ? giải đố? chiến đấu nhỏ?)
- [ ] Chia task cụ thể cho từng thành viên nhóm

## 2. Định hướng nghệ thuật

- **Art style**: stylized (không chạy theo photorealistic) — giữ tông màu tương phản rõ giữa 2 phe (đỏ/đất cho Sơn Tinh, xanh/nước cho Thủy Tinh)
- **Concept art**: đã có phác thảo 2D ban đầu cho cả 2 nhân vật (cel-shaded, silhouette rõ ràng)
- **Pipeline tạo model 3D** (vì team không có background art/graphic design):
  1. Concept art 2D (tách riêng từng nhân vật, đủ góc nhìn nếu có thể)
  2. Generate mesh 3D bằng AI: Meshy AI hoặc Tencent Hunyuan3D
  3. Cleanup mesh trong Blender (retopology, giảm poly)
  4. Rig + animation qua Mixamo (auto-rig, thư viện animation có sẵn — không cần kỹ năng animation)
  5. Import vào Unity
- **Nâng chất lượng hình ảnh trong giới hạn thời gian**:
  - Unity **HDRP** (thay vì URP mặc định) cho lighting/hậu kỳ đẹp hơn
  - Post-processing: bloom, color grading, ambient occlusion, volumetric fog
  - Dùng asset pack chất lượng cao có sẵn (Unity Asset Store) thay vì tự làm từ đầu
  - Cinemachine cho camera cinematic
  - Dồn lực polish vào ít cảnh quan trọng (arena cuối + cutscene chính) thay vì dàn trải

## 3. Tech stack

| Thành phần | Công nghệ |
|---|---|
| Engine | Unity 6 (C#) |
| Render pipeline | HDRP |
| Model 3D | Meshy AI / Tencent Hunyuan3D (AI generate từ ảnh 2D) |
| Rigging & Animation | Mixamo |
| Camera | Cinemachine |
| AI-assisted dev | Claude Code + **Unity MCP** (cho phép AI thao tác trực tiếp trên Unity Editor đang chạy: tạo/sửa GameObject, xem console, trigger compile...) |
| Version control | Git (workflow chi tiết setup khi nhóm bắt đầu code cùng nhau) |

## 4. Roadmap theo Phase

| Phase | Nội dung |
|---|---|
| **0 — Setup nền tảng** | Init git + `.gitignore` Unity, tạo project + cài HDRP, dựng folder structure, **cài đặt Unity MCP** (Edit > Project Settings > AI > Unity MCP, config cho Claude Code) |
| **1 — Chốt thiết kế** | Hoàn thiện GDD, chốt chi tiết combat spec, chốt gameplay từng nhánh sính lễ |
| **2 — Asset pipeline** *(song song Phase 3)* | Concept art → Meshy/Hunyuan3D → Blender cleanup → Mixamo rig/animation → import Unity |
| **3 — Lập trình core** *(song song Phase 2)* | Character select, player controller, combat state machine, enemy AI, dialogue system, scene management |
| **4 — Dựng scene/level** | Scene lấy sính lễ theo nhân vật, cutscene Hùng Vương, arena trận cuối |
| **5 — Polish hình ảnh** | Lighting, post-processing, VFX nguyên tố, camera cinematic |
| **6 — UI/UX** | Menu chính, character select UI, HUD, pause menu |
| **7 — Âm thanh** | Nhạc nền, SFX combat |
| **8 — Testing & cân bằng** | Playtest, sửa bug, cân bằng độ khó |
| **9 — Build & nộp bài** | Build executable, đóng gói |
| **Song song** | DevOps: chia branch theo hệ thống (combat/dialogue/UI/asset), optional CI/CD khi nhóm vào code cùng lúc |

**Lưu ý thứ tự**: Phase 2 (asset) và Phase 3 (code) chạy song song để tiết kiệm thời gian — không cần chờ nhau. Phase 1 nên chốt trước hoặc song song Phase 0 vì ảnh hưởng trực tiếp đến cách viết code combat ở Phase 3.

---
*File này tổng hợp lại toàn bộ hướng đã bàn, dùng để cả nhóm nắm overview trước khi bắt đầu chia việc.*
