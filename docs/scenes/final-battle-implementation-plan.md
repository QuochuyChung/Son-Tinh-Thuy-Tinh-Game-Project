# Kế hoạch implement — Trận đánh cuối (toàn bộ, chia commit)

>衍生 từ [final-battle-design](final-battle-design.md) (rev 2.3). Mục tiêu: **toàn bộ** nội dung trận cuối (combat + env + UI + audio + tune) chia thành các commit có nghĩa, mỗi commit review/test được.
> Trạng thái: **plan đã chốt 29/09** (thứ tự M0→M6, C3/C4 tách, C16 defer, asset ghi yêu cầu chuẩn bị) — **chưa implement**. Không commit khi user chưa yêu cầu.

---

## 0. Nguyên tắc commit

- 1 commit = 1 ý có thể review độc lập; conventional commits (`docs:`, `feat(combat):`, `feat(env):`, `chore(render):`, `fix:`).
- ~~Milestone có **check** (test/playtest) → không sang milestone sau khi check fail.~~ **Bỏ playtest check (07/10)**: không chạy matrix T1–T8 — user tự chơi, gặp lỗi → báo lại để fix.
- Commit code chỉ chạy sau khi user approve plan + thứ tự.
- `docs/progress.md` **không đụng** (ngoài mọi commit).

**Thứ tự thực hiện (đã chốt 29/09): chạy đúng thứ tự tài liệu M0 → M1 → M2 → M3 → M4 → M5 → M6**, tuần tự không song song — mọi phụ thuộc (C18–C21 chờ C8 + M1) tự thỏa mãn khi chạy tuần tự.

---

## 1. M0 — Docs (chưa chạy)

| # | Commit | Nội dung | Check |
|---|---|---|---|
| C1 | `docs: rev 2.3 — bối cảnh arena, boundary 2 phe, HDRP` | `docs/scenes/final-battle-design.md` (mới, rev 2.3) + `docs/game-overview.md` (+2 bullet line 75–76) | link 2 chiều OK, grep rev 2.3 |
| C2 | `docs: kế hoạch implement theo commit` | file này (tách riêng để C1 thuần rev 2.3) | — |

*(Muốn gộp C1+C2 thành 1 commit cũng được — nói lúc chạy.)*

---

## 2. M1 — HDRP migrate

| # | Commit | Nội dung | Check |
|---|---|---|---|
| C3 | `chore(render): migrate URP → HDRP` | chuyển pipeline asset + import HDRP package, conversion các material auto-convert được (Sandbox_Combat, DinhBa, character mats) | mở mọi scene không material hồng; play được nhánh sính lễ như cũ |
| C4 | `fix(render): fixup material HDRP còn lại` **(tách riêng — đã chốt 29/09)** | fix tay material/prefab không auto-convert, camera + light defaults của HDRP | screenshot 2 scene chính |

- **Risk**: HDRP khác URP về light/post — settings cũ (URP volume) phải thay bằng HDRP Volume; character skin/material cần tinh chỉnh.
- Env HDRP (water, fog, storm sky) **không nằm ở đây** — sang M4 (C21).

---

## 3. M2 — P0 combat, đấu được end-to-end (§11 P0-1…P0-5)

| # | Commit | Nội dung | Check |
|---|---|---|---|
| C5 | `feat(combat): SkillDefinition SO + input E/R/F + SkillGate + state Cast + cooldown` | SO (id/type/CD/dmg/prefab), InputActions map `Skill`, `SkillGate` chỉ bật trong boss scene, `StateMachine` thêm `Cast`, CD runtime; DebugHUD: bơm CD | ấn E/R/F trong scene test → state Cast chạy, ngoài boss scene → không nhận input |
| C6 | `feat(combat): Sơn Tinh E1/E2 — Thành Lũy + Đạp Núi` | tường ụ đá (chặn 1 heavy, sống 5s, vỡ sớm) + lướt AoE r4m knockdown; prefab/VFX placeholder | 2 skill trúng bot, tường chặn được |
| C7 | `feat(combat): Thủy Tinh E1/E2 mirror — Lũ Cuốn + Trượt Sóng` | 100% re-use code C5–C6, đổi geometry (wave quạt r6m hất; dash 150% + i-frame 0.2s + chém); boss AI dùng cùng definition | 2 phe ra skill, frame CD/range khớp bảng mirror §5 |
| C8 | `feat(env): EnvironmentDirector + ComboChainTracker` | env int −3..+3, rules §3.2 (5 đòn +1, F +2, bị trúng −1 + reset, phase flip), apply speed/stamina mods **cả 2 phe** (§3.3), event → UI/timeline; DebugHUD ±1 bậc | test 1 phe: chuỗi 5 đòn → env +1, bị trúng → −1, speed đổi thấy được |
| C9 | `feat(boss): BossHealth 3 đoạn + phase flip + win/lose flow` | HP 3 segment, hết đoạn → hồi đầy + event phase + env flip; đoạn 3 hết → exhaustion; timer 60s env (điều kiện thua #2); HP player (thua #1) | **end-to-end**: đấu P1→P2→P3→finisher placeholder trên sàn test; T3 (spam E không win dễ) |

**Gate M2**: trận đánh end-to-end chạy được với placeholder env/UI → mới sang M3.

---

## 4. M3 — P1 combat (§11 P1-1…P1-2, §7)

| # | Commit | Nội dung | Check |
|---|---|---|---|
| C10 | `feat(combat): UltMeter (Thần Lực) + PerfectDodgeDetector + after-image + hitstop` | nạp light +3 / heavy +8 / perfect dodge +12; boss hitbox check `dodgeElapsed<0.3` → event; ghost VFX re-use clip dodge; hitstop 0.25s | dodge đúng timing → +12 + after-image |
| C11 | `feat(combat): ult F Sơn Tinh — Hùm Voi Báo` | AoE r6m ~3×heavy, hất tung + choáng 1.5s, Đất +2 bậc; fallback 3 bóng thú VFX | full meter → F chạy, env +2, boss choáng |
| C12 | `feat(combat): ult F Thủy Tinh — Hà Bá Giận` | mirror 1:1 (sóng 360° r6m, Nước +2, mưa 8s) | frame khớp C11 (quy tắc cân bằng §8.1) |
| C13 | `feat(boss): BossPatternController` | pattern table theo phase, scripted F đúng đầu P2/P3, kiệt sức 2.5s, aggression config dùng chung 2 boss, P3 tăng tốc | boss ra pattern theo phase, F đúng 2 lần |
| C14 | `feat(boss): FinisherPrompt + cinematic thắng` | boss quỳ ~4s → prompt 1 nút → cinematic (canon cạn kiệt / rút quân) | kill đoạn 3 → prompt → cinematic |

**Gate M3**: T4 (perfect dodge không bắt buộc) + boss pacing đúng §6.

---

## 5. M4 — Arena environment (§13.6)

| # | Commit | Nội dung | Check |
|---|---|---|---|
| C15 | `feat(scene): Arena_Final blockout + boundary + spawn 2 đầu` | đĩa ⌀28m lòng chảo, bậc thang, placeholder điện, spawn đối xứng, trigger boundary r≈14m | **T7** đọc được ranh giới mọi góc; **T8** knockback ra rìa → chặn |
| C16 | `feat(scene): entry cutscene phán xử → arena` **⏸ defer (đã chốt 29/09 — làm sau M5, commit riêng)** | 2 phe đi từ 2 đầu, nối Hùng Vương | flow GDD phán xử → trận |
| C17 | `feat(env): NavMesh + NavMeshObstacle carve` | bake trên địa hình cao nhất, cylinder carve thu hẹp khi nước dâng | **T5** không stuck khi địa hình đổi |
| C18 | `feat(env): geometry Đất 0→3 — ring trồi + nước rút (timeline)` | 3 ring prefab, `PlayableDirector` điều khiển từ event C8 | DebugHUD ± bậc → geometry chạy 10–15s/bậc |
| C19 | `feat(env): geometry Nước 0→3 — plane nâng + bán kính co` | mirror C18, arena co về giữa | như C18, 2 chiều xen kẽ |
| C20 | `feat(env): backdrop + set dressing` | biển lũ tới chân trời, silhouette núi/sông, điện + bậc, mảnh vỡ trôi, prop trong sàn (bệ ấn, cột gãy, vũng nước, đá vụn) | art brief §13.2, prop không che 2 phe (pillar 4) |
| C21 | `feat(env): HDRP lighting/post 2 màu + fog + storm sky` | volume (bloom, grading xanh/nâu, AO, volumetric fog), directional + sky bão; water HDRP | 2 màu đọc được nước/đất ngay; không lỗi material |

---

## 6. M5 — UI + bão + audio + integration

| # | Commit | Nội dung | Check |
|---|---|---|---|
| C22 | `feat(ui): trục env 2 màu + HP bar 3 đoạn + timer 60s + cooldown E/R + prompt finisher` | §10.2 UI — env axis có vạch bậc, HP bar đoạn sáng = phase | đổi phase → UI cập nhật; timer chạy/dừng đúng §3.4 |
| C23 | `feat(env): bão sấm P3 + mưa + screen shake` | khe sấm telegraph 0.8s mỗi 4s, mưa dần P2 → liên tục P3, shake | **T5** phần bão; rain không CPU spike |
| C24 | `feat(audio): SFX + ult stinger + cue env lật` | §10.3 (crumbling, wave, thunder, muffled; stinger; cue đổi phase) | mỗi event có cue |
| C25 | `test: camera/lock-on readability pass` | Cinemachine check mọi góc, prop che tầm nhìn, fix phát hiện được | **T7** pass |

---

## 7. M6 — Tune (bỏ playtest)

| # | Commit | Nội dung | Check |
|---|---|---|---|
| C26 | `docs: bỏ playtest gate T1–T8` | bỏ yêu cầu playtest matrix §8 — user tự chơi, gặp lỗi → báo lại để fix; số `[tune]` giữ nguyên để tune tay sau | — |

---

## 8. Tooling & MCP — trả lời "cần gì ngoài Unity CLI"

### 8.1 Bảng tóm tắt

| Việc | Tool | Cần cài? |
|---|---|---|
| Unity build/test/import headless | **Unity CLI** (đã có: `-batchmode -executeMethod`) + editor scripts | Không — đủ rồi, là xương sống |
| Agent thao tác Unity scene trực tiếp | Unity MCP community (Unity MCP Bridge / mcp-unity) | **Optional** — nice-to-have, không chặn plan; marketplace chưa tra được (lỗi auth), cài sau qua `opencode.json` nếu muốn |
| Skill Unity làm reference | `unity-technologies/skills@unity-cli` (official, 6.3K), `@unity-package-management`, `@ui-ugui`; `rmyndharis/antigravity-skills@unity-developer` | **Nên** — `npx skills add unity-technologies/skills@unity-cli` |
| Game feel (hitstop/shake/juice) | `gamedev-skills/awesome-gamedev-agent-skills@game-feel` | Nên (M3/C23) |
| UI (HP bar 3 đoạn, env axis) | `gamedev-skills/awesome-gamedev-agent-skills@game-ui-ux` | Nên (C22) |
| **Chuẩn bị model/animation** | Yêu cầu asset theo từng task (xem §8.2) + checklist design §10.1 — **plan không khai công cụ/nguồn/tài khoản** | Chuẩn bị trước commit cần asset |
| **Spine** | **Không dùng** — Spine là tool 2D skeleton, game này 3D humanoid | — |
| Environment đẹp | HDRP built-in (water surface, volumetric fog, post volume, storm sky) + ProBuilder + free texture (Poly Haven/ambientCG); Blender headless nếu cần mesh custom → skills `roble3/cc-blender-skill@blender-*`, `vladmdgolam/agent-skills@blender-mcp` | Không bắt buộc — HDRP + free assets đủ cho scope |
| Git/PR | GitHub MCP (đã cài trong session) | Đã có |
| Web research | websearch/webfetch (đã có) | Đã có |

### 8.2 Chuẩn bị asset — chỉ ghi *cần gì*, không khai công cụ

*(Công cụ/tài khoản chuẩn bị do team tự lo, **không khai trong plan**.)*

1. **Không cần MCP mới để bắt đầu** — Unity CLI + editor scripts + code viết tay là đủ cho M0–M3.
2. Cài 3 skill: `unity-technologies/skills@unity-cli`, `gamedev-skills/awesome-gamedev-agent-skills@game-feel`, `@game-ui-ux`.
3. **Model/animation cần chuẩn bị theo từng commit** (chi tiết design §10.1):
   - **Trước M2 (C6/C7)**: prefab/VFX kỹ năng E1/E2 cả 2 phe (ụ đá, sóng, lướt) — placeholder primitive được; clip attack/cast dùng lại.
   - **Trước M3 (C10–C14)**: clip dodge thật (thay mượn Run), clip quỳ/kneel cho finisher (C14), VFX ult — placeholder3 bóng thú được.
   - **Trước M4 (C15–C21)**: prop arena (bệ ấn, cột gãy, vũng nước, đá vụn, điện, bậc), backdrop silhouette núi/sông, sky bão.
   - **Animation combat 2 nhân vật** (design §10.1): combo 3 hit, né/lăn, trúng đòn, loạng choạng, chết, ult, finisher — cần trước M2/M3.
4. Unity MCP (scene manipulation trực tiếp) — để sau, không phải điều kiện tiên quyết.

---

## 9. Đã chốt (29/09)

1. **Thứ tự**: chạy đúng thứ tự tài liệu **M0 → M1 → M2 → M3 → M4 → M5 → M6** (HDRP trước combat, không gợi ý đảo thứ tự nữa).
2. **C16 entry cutscene**: **defer** — làm sau M5, tách commit riêng.
3. **Asset/model/animation**: plan **chỉ ghi yêu cầu chuẩn bị theo từng task** (§8.2 + design §10.1); không khai công cụ/tài khoản trong plan.
4. **C3/C4**: **tách** riêng 2 commit (migrate / fixup material).

---

## 10. Tổng kết số commit

| Milestone | Commit | Tổng |
|---|---|---|
| M0 Docs | C1–C2 | 2 |
| M1 HDRP | C3–C4 | 2 |
| M2 P0 combat | C5–C9 | 5 |
| M3 P1 combat | C10–C14 | 5 |
| M4 Arena env | C15–C21 | 7 |
| M5 UI/bão/audio | C22–C25 | 4 |
| M6 Tune | C26 | 1 |
| **Tổng** | | **26** (C16 defer → chạy sau M5) |
