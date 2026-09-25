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

### Combat spec — đã chốt (Phase 1)

**Input — chỉ 3 nút, không tham để tránh bug khi retarget animation Mixamo:**
- **Đòn thường** (light attack): combo 3 hit, đòn cuối gây stagger nhẹ
- **Đòn mạnh** (heavy attack): chậm hơn, không nối combo với đòn thường, nhưng phá giáp (poise break) — dùng để ngắt đòn boss
- **Dodge/né**: có i-frame ngắn. Không làm parry (parry thật khó tune, dễ bug với animation không chuyên game action)

**Resource — Stamina:** dùng chung cho đòn đánh và dodge; hết stamina → "vulnerable" 1-2s. Rẻ để code (1 float + cooldown) nhưng tạo cảm giác risk/reward kiểu Soulslike/Wukong.

**Lock-on:** Cinemachine, chỉ 1 target (boss trận cuối) — không cần multi-target switching.

**Hit reaction:** 2 loại — flinch (hit nhẹ) và stagger (hit nặng/poise-break, mở window combo miễn phí).

**State machine dùng chung (player + boss):** Idle → Attack → Dodge → Hit → Stagger → Die.

**Boss (nhân vật đối lập) — 2-3 phase:**
- Phase 1 (100%→50% HP): 2-3 pattern cận chiến cơ bản, dễ đọc
- Phase 2 (50%→20% HP): thêm 1 đòn AoE/telegraph dài + cơ chế môi trường động kích hoạt (nước dâng dần nếu đấu Thủy Tinh, đá/địa hình dâng cao nếu đấu Sơn Tinh) — chủ yếu VFX + 1 timer, ít code nhưng là "money shot" hình ảnh
- Phase 3 (dưới 20%, optional nếu kịp thời gian): tăng tốc độ đòn hiện có, không thêm pattern mới

### Gameplay nhánh sính lễ — đã chốt (Phase 1)

Dạng **traversal + collect + mini combat**, map nhỏ/tuyến tính (không open-world), mang chất riêng theo nhân vật (Sơn Tinh = núi rừng/hang động, Thủy Tinh = sông/đầm nước).

3 điểm sính lễ trên đường đi, mỗi điểm 1 kiểu thử thách khác nhau:
1. **Traversal đơn giản** — leo/nhảy qua chướng ngại, không cần platforming khó
2. **Combat nhỏ** — 2-3 quái thường, tái dùng combat system ở trên (bỏ bớt combo) — vừa tiết kiệm code vừa là "mini rehearsal" giúp người chơi làm quen control trước khi vào boss
3. **Tương tác môi trường theo yếu tố nhân vật** — Thủy Tinh dùng "phép nước" dâng mực nước qua chướng ngại, Sơn Tinh dùng "phép đất" dựng cầu đá — tạo khác biệt rõ giữa 2 nhánh mà không cần 2 bộ code riêng

Về code: 2 map dùng chung 1 hệ thống pickup/quest tracker (ScriptableObject cho "sính lễ item"), chỉ khác asset/layout, không viết logic riêng cho từng nhân vật.

### Layout arena trận đánh cuối — đã chốt (Phase 1)

- Hình dạng: đấu trường tròn/bát giác, đường kính ~25-30m, lòng chảo trũng nhẹ kiểu "đàn tế" (colosseum-style) để camera lock-on luôn đọc rõ ranh giới sàn đấu
- Vào trận: player + boss bước vào từ cầu/bậc thang nối tiếp cutscene phán xử Hùng Vương, đứng 2 đầu đối diện
- Rìa đấu trường: cột đá gãy xen lẫn vũng nước cạn (motif trung lập, gợi "đất lẫn nước" — đúng tinh thần đàn tế phân xử), rìa ngoài cùng là vực/tường vô hình chặn ra ngoài map
- Phase 1 (100→50% HP): sàn đấu khô, rải rác đá vụn (chỉ là prop cản tầm nhìn nhẹ, không cần collision phức tạp)
- Phase 2 (50→20% HP, trigger môi trường động, đúng nguyên tố **của boss** — tức nhân vật đối lập người chơi đang đánh):
  - Đấu với **Thủy Tinh** → nước dâng dần từ rìa vào giữa trong ~10-15s, thu hẹp ~30% diện tích sàn đấu, buộc 2 bên dồn vào giữa
  - Đấu với **Sơn Tinh** → cột đá trồi lên từ nền tạo địa hình nhiều tầng, buộc phải né/leo giữa các bệ đá cao thấp
  - Chỉ cần 1 animation/VFX timeline + thu hẹp NavMesh — không cần logic phức tạp, đây là "money shot" hình ảnh chính
- Phase 3 (optional, <20%): không đổi địa hình thêm, chỉ tăng tốc đòn + rung màn hình/VFX bão

### Layout 2 map nhánh sính lễ — đã chốt (Phase 1)

Dùng chung đúng 3 sính lễ trong truyền thuyết gốc cho cả 2 nhân vật — **voi chín ngà, gà chín cựa, ngựa chín hồng mao** — chỉ khác cách lấy và bối cảnh theo nhân vật. Mỗi map là 1 đường tuyến tính, tổng chiều dài blockout ~150-200m, 3 điểm dừng theo đúng thứ tự độ khó tăng dần (traversal → combat nhỏ → tương tác nguyên tố).

**Nhánh Sơn Tinh (núi rừng/hang động):**
1. **Ngựa chín hồng mao** — traversal: đuổi theo/dụ ngựa hoang qua các gờ đá/cầu treo gãy trên sườn núi
2. **Gà chín cựa** — combat nhỏ: bãi đất trống trong rừng, 2-3 quái thú núi canh gác, gà nhốt trong miếu thờ nhỏ sau khi dọn quái
3. **Voi chín ngà** — tương tác nguyên tố ("phép đất"): hang động sập lối đi, dùng phép đất dựng cột đá thành cầu bậc thang xuống hang sâu, voi đứng trên bệ đá trong động

**Nhánh Thủy Tinh (sông/đầm nước):**
1. **Ngựa chín hồng mao** — traversal: băng qua đầm lầy ngập nước bằng khúc gỗ nổi/bệ đá nhấp nhô
2. **Gà chín cựa** — combat nhỏ: cồn đất nhỏ giữa sông, 2-3 thủy quái canh gác, gà nhốt trong miếu nổi
3. **Voi chín ngà** — tương tác nguyên tố ("phép nước"): đền thờ ngập một phần, dùng phép nước dâng mực nước để nổi lên tới bệ cao nơi voi đứng

Cả 2 map dùng chung 1 prefab hệ thống pickup/quest tracker (ScriptableObject sính lễ) đã chốt ở trên — chỉ khác asset/terrain/tên bối cảnh.

### ⚠️ Còn cần bàn (TODO)
- [ ] Chia task cụ thể cho từng thành viên nhóm (khi nhóm sẵn sàng)

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

**Tiến độ thực tế** (đã làm gì, cách test, việc tiếp theo): xem [progress.md](progress.md).

---
*File này tổng hợp lại toàn bộ hướng đã bàn, dùng để cả nhóm nắm overview trước khi bắt đầu chia việc.*
