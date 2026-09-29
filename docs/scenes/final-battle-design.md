# Thiết kế trận đánh cuối — Sơn Tinh vs Thủy Tinh

> Tài liệu chi tiết cho scene/arena trận cuối, bám khung đã chốt trong [game-overview](../game-overview.md).
> Trạng thái: **rev 2.3** (29/09) — simplify theo feedback: **bỏ layer phản ứng nguyên tố (aura/Ướt/Bùn/R1–R6)** → **1 trục môi trường Đất↔Nước**. Rev 2.1: **cắt adds + đê**, ult Sơn Tinh → **Hùm Voi Báo** (bỏ Giáp), bảng env lợi/hại tường minh (§3.3). Rev 2.2: **giữ HP** (player + boss), **bỏ thanh nộ** — phase chuyển khi boss **hết 1 đoạn HP rồi hồi đầy máu**. Rev 2.3: **bối cảnh & layout arena** (đồng bằng ngập lũ, adaptation "vua cho phép quyết đấu", 2 phe **chỉ di chuyển trong arena** — §13) + **chốt render HDRP** (migrate từ URP, §13.5). Không còn mục [?] mở.
> Ngày: 29/09/2026 (rev 2.3)

---

## 1. Nguồn tham chiếu & bài rút ra

### 1.1 Truyền thuyết gốc

**Lĩnh Nam chích quái / bản Văn 6 (Huỳnh Lý)** — dùng làm timeline chuẩn:
- Thủy Tinh "hô mưa gọi gió, dông bão đùng đùng, dâng nước sông cuồn cuộn" → nước ngập lúa, đồng, nhà, cửa.
- Sơn Tinh "bốc từng quả đồi, di từng dãy núi, dựng thành lũy đất ngăn nước lũ".
- **"Nước dâng lên cao bao nhiêu, Sơn Tinh lại làm cho đồi, núi mọc cao lên bấy nhiêu"** → cốt lõi cơ chế: trục môi trường 2 chiều, nước↔đất luân phiên dâng (§3).
- Đánh nhau **ròng rã mấy tháng** → **Thủy Tinh cạn kiệt sức lực, rút quân** → lý do boss "cạn kiệt" khi hết đoạn HP cuối (§6).
- Hằng năm lặp lại → dân **đắp đê** → motif đê **đã cắt khỏi gameplay** (29/09, §7) — giữ ở mức lore.

**Thơ Nguyễn Nhược Pháp (SGK Ngữ 9)** — mood board hình ảnh:
- Thi phép ở điện Hùng Vương: Thủy Tinh "Bắt quyết hô mây to nước cả, giậm chân rung khắp làng gần quanh" → "Áo ào mưa đổ xuống như thác, cây xiêu, cầu gãy… bò lợn và cột nhà trôi theo"; Sơn Tinh "Vung tay niệm chú: Núi từng dải, nhà lớn, đồi con lổm cổm bò chạy mưa".
- Tướng mạo: Sơn Tinh **một mắt ở trán**, cưỡi **bạch hổ**; Thủy Tinh **râu quăn xanh**, cưỡi **lưng rồng**.
- Trận chính: "Niệm chú, **đất nảy vù lên cao** / Hoa tay thần vẫy **hùm, voi, báo**… đạp long đất nổi, gầm, xáo xáo" — Sơn Tinh; Thủy Tinh: "Mây đen hùm hụ bay ma một, **sấm ran, sét giăng** nỗi lo xanh" + hải quân "cá voi quác mồm, cá mập quẫy đuôi, càng cua giơ như mác, tôm kềnh chạy quắp đuôi".
- Kết: "**Thuỷ Tinh năm năm dâng nước bể**" → chu kỳ lũ = cảm hứng nhịp phase (mỗi lần đổi phase = 1 đợt lũ đổi chiều).

**Việt điện u linh tập / Giao Châu ký**: 2 người vốn **bạn thân**, sau mới thành thù → gợi ý beat cutscene mở trận (optional).

### 1.2 Black Myth Wukong — combat DNA

- Stamina chung cho đánh/né; **perfect dodge** (after-image, né đúng timing → thưởng) là skill ceiling.
- **Focus/Phép** tích từ đòn trúng + né hoàn hảo → tiêu cho chiêu mạnh; spell có **mana + cooldown**.
- Boss: telegraph rõ từng đòn, luôn có **recovery window** để punish; phase đổi theo tiến trình trận (ở đây: **hết 1 đoạn HP → hồi đầy**, rev 2.2); phase cuối spam + tăng tốc.

### 1.3 Tiền lệ nguyên tố → rút gọn

- Genshin/Divinity: aura + trigger kết hợp (ướt, bùn…) — bản rev 1 đã làm theo kiểu này.
- **Rev 2 bỏ toàn bộ**: quá cầu kì cho scope hiện tại. Giữ lại bài học duy nhất: cơ chế phải **hiển nhiên ngay** → trục môi trường nước/dất hiện ngay trên màn hình (mực nước, khối đá) + buff tốc độ thấy được bằng mắt, không cần tooltip giải thích phản ứng.

---

## 2. Design pillars

1. **Bám GDD, có amend**: combat core 3 nút (light/heavy/dodge), stamina chung, lock-on 1 target, hit reaction flinch/stagger, state machine dùng chung player + boss, arena tròn 25–30m. *Amend rev 2: boss phase theo **3 đoạn HP (hết hồi đầy)**; môi trường luân phiên thay vì 1 lần ở phase 2.*
2. **Mirror đối xứng**: Sơn Tinh và Thủy Tinh là 2 bộ skill **cùng frame/cùng cost/cùng range, chỉ khác hình học** → 2 matchup cân bằng theo thiết kế, boss AI dùng chính bộ definition của player.
3. **Nguyên tố kể chuyện**: mỗi cơ chế rút từ truyền thuyết — **đua nước–núi** (trục env), **lũ luân phiên** (mỗi lần đổi phase env đổi chiều), **cạn kiệt sức lực** (finisher), **hùm voi báo** (F Sơn Tinh) vs hải quân (F Thủy Tinh).
4. **Đọc được (readability)**: telegraph ≥0.5s; môi trường = **2 màu tương phản** (nước xanh ngập / đất nâu dâng) đổi bằng timeline 10–15s; **thanh HP boss chia 3 đoạn** luôn thấy.
5. **Scope có thứ tự**: **P0** = combat core + E/R + trục env + phase theo HP → đấu được end-to-end; **P1** = F (Đại Pháp) + perfect dodge + timeline môi trường + finisher + bão sấm; **adds + đê đã cắt 29/09** (không còn nội dung combat P2).

---

## 3. Core mechanic — trục Môi trường & HP boss

> Nguyên tắc rev 2: **1 thanh env duy nhất**, không aura, không reaction table. Chiến thắng = **hết 3 đoạn HP boss** (mỗi đoạn = 1 phase) trong khi giữ được lợi thế môi trường (tốc độ, chỗ đứng) và không bị hết giờ env.

### 3.1 Trục môi trường

- Một giá trị nguyên bậc **-3 … 0 … +3**:
  - **âm (xanh) = Nước dâng**, **dương (nâu) = Đất dâng**.
  - Người chơi **Sơn Tinh muốn đất dâng (+)**; **Thủy Tinh muốn nước dâng (-)** — phe mình dâng = mình lợi thế.
- Trận **luân phiên 2 môi trường liên tiếp**: suốt 1 phase người chơi kéo dần env về phía mình bằng chuỗi đòn; đến lúc đổi phase → env **lật về phía boss** → hiển thị xen kẽ **nước → đất → nước → đất** liên tục suốt trận.

### 3.2 Cách môi trường thay đổi (toàn bộ luật, 4 dòng)

| Sự kiện | Hiệu ứng |
|---|---|
| **Chuỗi đòn thường** (light/heavy) trúng boss liên tiếp — cứ **5 đòn** không bị đánh trúng | env **+1 bậc về phía người chơi** |
| Người chơi dùng **F (Đại Pháp)** | env **+2 bậc về phía người chơi** |
| Người chơi **bị boss trúng** | chuỗi đòn reset về 0 **+ env -1 bậc về phía boss** (tối đa xuống -3) |
| **Boss hết 1 đoạn HP** (đổi phase) | env **lật về phía boss**: bậc = bậc người chơi đang giữ (tối thiểu 2) → người chơi bắt đầu phase mới ở thế bất lợi + **bắt đầu đếm ngược giành lại env** (§3.4) |

- Skill E/R **không** nạp chuỗi env (chỉ light/heavy/F) → chống spam 1 skill cheese.
- Không còn aura Ướt/Thổ Giáp, không còn zone Bùn/Đảo, không còn R1–R6.

### 3.3 Bảng trạng thái môi trường — lợi/hại tường minh cho từng phe

Luật bất biến: **env dâng về phía phe nào thì phe đó lợi** (Sơn Tinh luôn muốn Đất **+**, Thủy Tinh luôn muốn Nước **−**) — áp dụng **đối xứng cho cả player lẫn boss** (boss cùng luật: env bất lợi player = env lợi boss):

| Trạng thái | Sơn Tinh (phe Đất) | Thủy Tinh (phe Nước) |
|---|---|---|
| **Đất dâng** (+1 → +3) | ✅ **Lợi**: nhanh **+5%/bậc**, hồi stamina **+10%/bậc** — sân nhà: đá vững không cản bước, dư sức bám giữ chuỗi đòn | ❌ **Bất lợi**: chậm **−5%/bậc** — lạc trên đất liền, rời khỏi nguyên tố của mình, mất nhịp; *chỉ phạt speed, không phạt thêm stamina* |
| **Nước dâng** (−1 → −3) | ❌ **Bất lợi**: chậm **−5%/bậc** — bị nước cản bước (ngập cạn → gối → eo), khó giữ chuỗi đòn; *chỉ phạt speed, không phạt thêm stamina* | ✅ **Lợi**: nhanh **+5%/bậc**, hồi stamina **+10%/bậc** — sân nhà: nước nâng đỡ từng bước, dư sức bám giữ chuỗi đòn |

- Tối đa **±15%** speed (3 bậc); hồi stamina tối đa **+30%** (3 bậc). **Không đổi dmg, không đổi CD** — hết bảng buff phức tạp.
- Geometry theo bậc (timeline 10–15s, reuse "money shot" GDD) — **sân khấu thị giác, không cộng phạt speed thêm** (tránh đếm 2 lần):
  - Nước 0→3: cạn chân → ngập gối → ngập eo (arena co dần về giữa).
  - Đất 0→3: bục đá trồi cao dần, nước rút về rìa (địa hình nhiều tầng nhẹ).
- Bậc env chỉ đổi ở các mốc §3.2 → ít animation chuyển tiếp, dễ tune.
- Gốc kể chuyện: env = **sân nhà** — đất dâng = Sơn Tinh về núi (canon *"núi từng dải… bò chạy mưa"*), Thủy Tinh lạc trên cạn; nước dâng = Thủy Tinh về sông, Sơn Tinh bị nước nhấn.

### 3.4 HP của boss — 3 đoạn, hết đoạn hồi đầy (rev 2.2)

- **Giữ HP** (đã chốt 29/09 rev 2.2 — đảo quyết định "boss no HP" của rev 2): boss có **thanh HP chia sẵn 3 đoạn** (mỗi đoạn = 1 phase); đòn của bạn (light/heavy/E/R/F) trừ thẳng HP boss.
- **Hết 1 đoạn HP → chuyển phase** (P1→P2→P3): cinematic ngắn (như Đại Pháp scripted đầu P2/P3) + boss **hồi đầy HP** + env lật về phía boss (§3.2). **Không còn thanh nộ** — mọi số liệu nộ (light +3% / heavy +8% / F +15%, tick...) đã bỏ; nhịp phase do bạn tự quyết qua DPS (đánh nhanh → phase đến nhanh).
- Mỗi đoạn HP ≈ 40–60s với người chơi bình thường → 3 phase ≈ 2–4 phút **[tune]** (khớp T1).
- **Thắng**: hết **đoạn HP thứ 3** → boss **cạn kiệt** (canon: *sức Thủy Tinh cạn kiệt*) → quỳ một gối ~4s → prompt kết liễu → cinematic thắng. (Đoạn 3 hết máu **không** hồi tiếp — chết hẳn.)
- **Thua** (2 điều kiện):
  1. Hết **HP người chơi**.
  2. **Hết giờ giành lại môi trường**: sau mỗi lần env lật về phía boss (đầu P2, đầu P3), đếm ngược **60s [tune sau]** — phải kéo env về **≥ +1 về phía mình** trước khi hết giờ, nếu vẫn ≤0 khi hết giờ → **thua**. Đếm ngược chạy **1 lần mỗi phase**: dừng ngay khi giành lại được; env dao động lại sau đó **không** mở lại đồng hồ (HP người chơi là lưới an toàn song song).

---

## 4. Resource, input, gauge

### 4.1 Input (sửa combat spec GDD — chỉ áp trận cuối)

| Nút | Vai trò | Ghi chú |
|---|---|---|
| LMB | Light — combo 3 hit | đã chốt |
| RMB | Heavy — chậm, poise break | đã chốt |
| Space | Dodge — i-frame | đã chốt |
| Q / MMB | Lock-on (1 target) | đã chốt |
| **E** | **Kỹ nhanh (E1)** — CD **8s** | mới, chỉ trận cuối |
| **R** | **Kỹ dịch–đột biến (E2)** — CD **14s** | mới, chỉ trận cuối |
| **F** | **Đại Pháp (Ultimate)** — tốn full **Thần Lực** | mới, chỉ trận cuối |

Lý do tách 3 nút riêng (không gộp E tap/hold): input system dễ đọc, mỗi kỹ 1 animation state rõ ràng, không phải hard-code timing nhấn giữ. Nút chỉ được **enable trong scene boss** (SkillGate) để không nhiễu nhánh sính lễ.

### 4.2 Resource

- **HP người chơi**: giữ nguyên (đã chốt rev 2.2), thua khi hết.
- **Stamina** (giữ nguyên): light/heavy/dodge; hết → vulnerable 1–2s.
- **Thần Lực (0–100)**: nạp light trúng +3, heavy trúng +8, **perfect dodge +12**; không tự mất; F tiêu sạch. Nạp đầy ~2–3 lần perfect dodge + combo.
- **Boss**: **HP 3 đoạn** — hết 1 đoạn → phase mới + hồi đầy; đoạn cuối hết → finisher. Xem §3.4. (Đã **bỏ thanh nộ**, rev 2.2.)

### 4.3 Perfect dodge **[P1]**

- Trigger: dodge bắt đầu trong **≤0.3s** trước thời điểm hitbox của boss chạm → không nhận dmg (do i-frame vốn có) + **+12 Thần Lực** + after-image VFX + 0.25s hitstop nhẹ.
- Cách code rẻ nhất: hitbox của boss khi kích hoạt check `player.dodgeElapsed < 0.3` → fire event PerfectDodged (không cần hard-code timing phía player).

---

## 5. Danh sách skill (mirror 2 chiều)

> P0 = làm ngay (Mốc 2-3); P1 = polish trận cuối; P2 = optional.
> **Đối xứng tuyệt đối**: mọi dòng "Frame" giống hệt nhau giữa 2 bảng — chỉ khác hình học/VFX.
> Rev 2: skill **không còn gắn tag reaction** — công cụ thuần (che, lướt, hất) + mỗi phe 1 skill F đẩy env +2.

### 5.1 Core (đã chốt GDD, nhắc lại)

| Skill | Sơn Tinh | Thủy Tinh | Frame |
|---|---|---|---|
| Light ×3 | quyền + gạch đá | chém đại kiếm + tóe nước | hit3 stagger nhẹ; **nạp chuỗi env** |
| Heavy | ground-pound đấm đất | chém ngang sóng ngang | chậm, không nối combo, poise break; **nạp chuỗi env (≈2 đòn)** |
| Dodge | lướt đá | lướt sóng | i-frame, 25 stamina |

### 5.2 Sơn Tinh — Đất

| Nút | Tên | Mô tả | Ưu tiên |
|---|---|---|---|
| E | **Thành Lũy** | Dựng tường/ụ đá trước mặt, **chặn được 1 đòn heavy** của boss, tồn tại 5s (hoặc vỡ sớm). Cast 0.6s có telegraph (tay đập nền). Công cụ che/né thuần. | **P0** |
| R | **Đạp Núi** | Lướt nhanh tới lock-on, giáng xuống đất, gợn địa chấn AoE r4m, knockdown nhẹ. | **P0** |
| F | **Hùm Voi Báo** (Ultimate) | Hô **hùm, voi, báo** lao ra (canon: *"Hoa tay thần vẫy hùm, voi, báo"*): AoE **r6m** quanh mình (~3× heavy, hất tung + **choáng boss 1.5s** = cửa sổ free-hit) + **Đất dâng +2 bậc** (§3.2). *Thay "Dời Núi + Giáp 3s" (chốt 29/09 — rev 2.1). Fallback asset: nếu không kịp làm summon AI → 3 bóng thú VFX (skin), frame giữ nguyên.* | P1 |

### 5.3 Thủy Tinh — Nước

| Nút | Tên | Mô tả | Ưu tiên |
|---|---|---|---|
| E | **Lũ Cuốn** | Vung đại kiếm → cung sóng quạt trước mặt r6m, hất tung + đẩy lùi mục tiêu & projectile. | **P0** |
| R | **Trượt Sóng** | Lướt sóng vượt hơn dodge 50% khoảng cách, i-frame 0.2s, kết thúc chém ngang. | **P0** |
| F | **Hà Bá Giận** (Ultimate) | **Nước dâng +2 bậc** (§3.2) + sóng thần 360° **r6m** (~3× heavy, hất tung + **choáng boss 1.5s**) + **mưa bão 8s** (giảm sáng nhẹ). | P1 |

### 5.4 Ghi chú đối xứng

- E1 vs E1: **cùng CD 8s, cùng cast ~0.6s** — một bên là tường che, một bên là wave hất → cùng dạng "đòn mở trận", khác hình học.
- E2 vs E2: **cùng CD 14s** — cả 2 đều gap-closer.
- F vs F: cùng giá 100 Thần Lực, cùng dmg 3× heavy, cùng **+2 bậc env**, cùng **r6m + hất tung + choáng 1.5s** → stat 1:1; chỉ khác "hương": Sơn Tinh = bầy thú (VFX hùm voi báo), Thủy Tinh = mưa bão 8s.
- **Boss AI** dùng đúng bộ definition này (SkillDefinition ScriptableObject), pattern table theo phase ở §6 — code skill viết 1 lần, chạy cả 2 phía.
- Passive cũ (Thổ Giám hồi stamina / Thủy Thế hồi CD) **đã gộp** vào bảng env §3.3 — không còn slot Passive riêng.

---

## 6. Phase design (theo HP boss — 3 đoạn)

| Phase | Kích hoạt | Nội dung |
|---|---|---|
| **P1** | mở trận, env **0** (trung lập) | 2–3 pattern cận chiến cơ bản, dễ đọc (theo GDD). Người chơi build chuỗi → env nghiêng về phía mình (đang ở thế **lợi thế**). Hết **đoạn HP 1** → |
| **P2** | boss hết đoạn HP 1 (hồi đầy) | **Env lật về phía boss** (≥2 bậc bất lợi — đang ở thế **bất lợi**) + **đếm ngược 60s giành lại env** (§3.4). Boss cast **Đại Pháp scripted** (telegraph 1.5s → cinematic, đúng motif "hô mưa gọi gió"/"bốc đồi") → **kiệt sức 2.5s** (punish window). Thêm 1 pattern AoE. Hết **đoạn HP 2** → |
| **P3** | boss hết đoạn HP 2 (hồi đầy) | Env lật **lần nữa** + đếm ngược giành lại env chạy lại. Boss max tốc độ (nhân env bất lợi), tăng tốc đòn có sẵn + rung màn hình/VFX **bão** (GDD), mưa liên tục (nếu boss Thủy Tinh). Hết **đoạn HP 3** → |
| **Finisher** | boss hết **đoạn HP 3** (không hồi nữa) | Boss **cạn kiệt** quỳ một gối ~4s → prompt kết liễu (1 nút) → cinematic thắng (canon: phe thua hết sức, rút quân / mất Mị Nương) |

- Timeline môi trường chạy lại ở **mỗi lần đổi phase** (P2: nước/lũ dâng hoặc cột đá trồi — đúng nguyên tố **của boss**, theo GDD; P3: không đổi geometry thêm, chỉ bão VFX) → env thật sự **luân phiên liên tiếp** suốt trận.
- **Boss scripted beats**: Đại Pháp boss cast đúng 2 lần (đầu P2, đầu P3); giữa 2 lần AI chỉ dùng core + E1/E2. Giữ boss "đạo diễn" được, tránh AI ult ngẫu nhiên phá pacing.
- Aggression config (giãn cách 2 đòn, % chance ra E1/E2) **dùng chung 1 bộ cho 2 boss** — khác biệt chỉ đến từ hình học skill; theo phase (P3 tăng tốc).

---

## 7. Độ khó & nhịp (P1)

> **Đã cắt 29/09**: *adds* và *hệ đê 3 gạch* — out of scope. Trận cuối là **1v1 thuần** (đúng DNA Wukong, lock-on 1 target của GDD không bị chia sẻ); motif "dân đắp đê" giữ ở mức lore (§1.1).

1. **Bão sấm** *(P1, chỉ P3)*: 1 khe sấm định vị lại mỗi 4s (circle telegraph 0.8s trước khi đánh) → đứng trong khe = dính dmg + choáng nhẹ; cả 2 phải di chuyển → giữ nhịp.
2. **Cửa sổ kiệt sức** *(P1)*: sau mỗi lần boss cast F (đầu P2/P3) → boss **kiệt sức 2.5s** (quỳ một gối, không ra đòn) → punish window free. Đúng canon *sức Thủy Tinh cạn kiệt* + đúng DNA Wukong (recovery window). Lần kiệt sức **cuối cùng** (lần 3) nâng cấp thành finisher §6.

---

## 8. Cân bằng & plan test

**Quy tắc cân bằng (áp khi tune):**
1. Frame lock: startup/recovery/CD/cost/range của cặp mirror **không được lệch** — chỉ dmg thực nhận được phép lệch ≤5%.
2. Swing env tối đa **±15% speed** — đủ thấy nhưng không khiến phe bất lợi "chậm như sên"; kiểm thực tế ở 3 bậc bằng stopwatch, không bằng cảm tính.
3. Boss aggression (giãn cách 2 đòn, % chance ra E1/E2) **dùng chung 1 config** → khác biệt chỉ từ hình học skill, không từ AI.
4. HP và env phải **cùng hướng logic**: spam E/R (không nạp chuỗi env) → boss vẫn mất máu → phase đổi sớm, trong khi env chưa dâng về phía bạn → bắt đầu phase mới ở thế bất lợi (tối thiểu -2 bậc) + đếm ngược 60s → trừng phạt lối chơi spam.

**Test matrix (playtest mỗi lần đổi số):**

| # | Setup | Check |
|---|---|---|
| T1 | Chơi Sơn Tinh vs boss Thủy Tinh (P1→Finisher) | fight 2–4 phút (3 đoạn HP), deathcount ≤3 cho người mới |
| T2 | Chơi Thủy Tinh vs boss Sơn Tinh (P1→Finisher) | cùng khoảng; nếu lệch >30% thời gian → tune dmg cặp mirror liên quan (mỗi đoạn HP) |
| T3 | Spam 1 skill duy nhất (E spam) | không được win dễ: env không dâng (chuỗi chỉ nạp từ light/heavy/F) nhưng HP boss vẫn mất → phase đổi sớm → bắt đầu phase mới ở thế bất lợi + timer 60s |
| T4 | Perfect dodge chỉ dùng dodge thường (không E/R/F) | vẫn clear được P1 → P0 không bắt buộc perfect dodge |
| T5 | Đứng im ở **thế bất lợi** 60s | hết giờ giành lại env → **thua** (timer §3.4); đứng im ở **thế lợi** → boss vẫn đánh trả, không thắng được vì boss không mất máu; boss không stuck pattern / không frame treo NavMesh khi địa hình đổi |
| T6 | Cố tình ăn đòn liên tiếp | chuỗi reset + env **-1/lần bị trúng**, floors tại -3; kéo env về ≥+1 trước 60s → sống; để hết giờ mà env vẫn ≤0 → thua |
| T7 | Lock-on + camera di quanh arena ở mọi góc | ranh giới sàn luôn đọc được, prop/bệ ấn không che 2 phe (lòng chảo đàn tế — pillar 4) |
| T8 | Đẩy player/boss về rìa arena (charge, knockback, flinch ở cạnh boundary) | cả 2 dừng trong tường vô hình, không trôi ra biển; **Thủy Tinh không lặn/đi ra ngoài arena** (§13.4) |

---

## 9. Phân định với nhánh sính lễ

**Chốt: nhánh sính lễ chỉ dùng skill cơ bản.** Cụ thể:

- Nhánh sính lễ giữ đúng GDD đã chốt: traversal đơn giản + combat nhỏ (combo light/heavy/dodge **bỏ bớt**) + **tương tác môi trường dạng trigger** ("phép nước" nâng mực nước / "phép đất" dựng cầu đá) — đây là **kích hoạt level trigger**, **không phải skill combat**, không cần state machine, không có hệ env.
- **Không đưa E / R / F, Thần Lực, perfect dodge, trục môi trường, phase theo HP vào nhánh.** Nội dung này **riêng của trận cuối** (SkillGate chỉ bật trong scene boss).
- Code vẫn tái dùng phần nền: `PlayerInputReader`, state machine, `PlayerController` — chỉ thêm state/input map mới có điều kiện bật/tắt. `SkillDefinition` SO là asset **riêng** cho trận cuối, không phải hệ chung với sính lễ.
- Lý do: giữ nhánh dễ, tránh scope creep; đồng thời trận cuối là "mở khoá đầy đủ bộ kỹ năng" → cảm giác lên đỉnh ở cuối game.

---

## 10. Checklist asset

### 10.1 Animation (Mixamo, tải theo bộ nhân vật)

**P0 — cả 2 nhân vật:**
- [ ] 3× light attack (Sơn Tinh: martial arts không vũ khí / Thủy Tinh: Great Sword pack 3 hit)
- [ ] 1× heavy (ground slam / wide sweep)
- [ ] **Dodge roll thật** (thay vì mượn run như hiện tại)
- [ ] Hit flinch, stagger, death (đang plan Mốc 2)
- [ ] 1× **cast pose** cho E1 (arm thrust / casting)
- [ ] 1× **leap attack** cho E2 (leaping strike / dash slash kết thúc)

**P1:**
- [ ] 1× ult pose lớn (hô thú / big cast) — **Hùm Voi Báo** / **Hà Bá Giận**
- [ ] Boss **quỳ/kneel** cho cửa sổ kiệt sức + **finisher** (quỳ gối giữ chỗ → prompt kết liễu)
- [ ] After-image pose cho perfect dodge (dùng lại clip dodge + VFX, **không cần clip mới**)

### 10.2 Prefab / VFX
- [ ] Tường đất prefab (Sơn Tinh E1) + effect vỡ khi chặn xong 1 đòn
- [ ] Wave mesh cung quạt (Thủy Tinh E1)
- [ ] Gợn địa chấn vòng (Đạp Núi) + after-image ghost
- [ ] **Bầy thú VFX** cho Hùm Voi Báo: 3 bóng hùm/beo/voi lao AoE — fallback skin nếu không làm summon AI
- [ ] **Timeline môi trường 3 bậc × 2 chiều**: mực nước (0→3) / bục đá trồi + nước rút (0→3) — reuse money shot GDD, thêm bậc thay vì 1 lần
- [ ] **Scene env arena (§13)**: water plane nội/ngoài arena (HDRP water/Shader Graph), ring prefab ×3 (Đất), boundary trigger r≈14m, backdrop (biển lũ mở, silhouette núi/sông, điện phán xử + bậc thang, mảnh vỡ trôi), prop trong sàn (bệ ấn, cột đá gãy, vũng nước cạn, đá vụn)
- [ ] **HDRP profile arena**: volume post (bloom, color grading 2 màu xanh/nâu, AO, volumetric fog) + storm sky/lightning
- [ ] **UI**: trục env 2 màu (nước xanh / đất nâu, có vạch bậc), **thanh HP boss chia 3 đoạn** (đoạn sáng = phase hiện tại), prompt finisher, cooldown icons E/R
- [ ] Bão: particle mưa + khe sấm circle telegraph + screen shake

### 10.3 Audio
- [ ] SFX: crumbling stone, wave crash, thunder, muffled underwater (đứng lũ)
- [ ] Ult stinger mỗi phe; thunder cue cho bão P3; cue "rung chuyển" mỗi lần env lật (đổi phase)

---

## 11. Implementation notes

> Kế hoạch chia commit chi tiết (M0–M6, 26 commit, thứ tự đã chốt 29/09): **[final-battle-implementation-plan](final-battle-implementation-plan.md)**.

**Script mới (đề xuất, đặt theo cấu trúc `Assets/Scripts` hiện có):**

| Script | Phụ trách |
|---|---|
| `Combat/Skills/SkillDefinition.cs` | SO: id, type (E1/E2/Ult), cooldown, damage multiplier, prefab/VFX — **bỏ tags reaction** (không còn ApplyWet/Shatter…) |
| `Combat/Skills/SkillGate.cs` | enable map input E/R/F **chỉ trong boss scene** |
| `Combat/Skills/PerfectDodgeDetector.cs` | boss hitbox check `dodgeElapsed<0.3` → event |
| `Combat/Skills/UltMeter.cs` | Thần Lực 0–100, nạp từ onHit + perfect dodge |
| `Combat/Environment/EnvironmentDirector.cs` | env -3..+3 (int), rule §3.2, apply speed/stamina mods **cả 2 phe** (mirror), bắn event UI + trigger timeline geometry |
| `Combat/Environment/ComboChainTracker.cs` | đòn thường trúng liên tiếp (5 = +1 bậc, heavy ≈ 2), reset khi player bị trúng |
| `Combat/Boss/BossHealth.cs` | HP **3 đoạn**: hết 1 đoạn → hồi đầy + event phase + env flip; hết đoạn 3 → exhaustion (**thanh nộ đã bỏ**, rev 2.2) |
| `Combat/Boss/FinisherPrompt.cs` | boss quỳ + prompt 1 nút → cinematic kết |
| `Combat/AI/BossPatternController.cs` | pattern table theo phase, scripted F at P2/P3, aggression config dùng chung |

**Sửa code có sẵn:**
- `StateMachine`: thêm state **`Cast`** (giữa Attack và Dodge) — hoặc tái dùng `Attack` + `skillId` nếu muốn nhẹ (đề xuất thêm state riêng cho rõ).
- `PlayerInputReader` + InputActions: thêm map `Skill` (E/R/F) — chỉ đăng ký khi `SkillGate` bật.
- `DebugHUD`: thêm nút debug set env (±1 bậc), bơm Thần Lực, set HP boss % để test nhanh.

**Thứ tự làm (gợi ý):**
1. P0-1: input map E/R + `SkillGate` + state Cast + CD.
2. P0-2: E1/E2 Sơn Tinh (tường, đạp núi) chạy được trên sàn test.
3. P0-3: mirror sang Thủy Tinh (lũ cuốn, trượt sóng) — 100% re-use code, đổi prefab/VFX.
4. P0-4: `EnvironmentDirector` + `ComboChainTracker` — trục env chạy, speed mods áp được (test đơn giản trước UI).
5. P0-5: `BossHealth` (3 đoạn + hồi đầy) + phase flip + win/lose flow (P1→P2→P3→finisher placeholder) — **đấu được end-to-end**.
6. P1-1: ult F (Hùm Voi Báo / Hà Bá Giận) + Thần Lực + perfect dodge.
7. P1-2: timeline geometry theo bậc + scripted boss F + kiệt sức + finisher cinematic + UI (env axis, HP bar 3 đoạn).
8. P2: bão sấm (P3) — adds/đê **đã cắt 29/09**.

---

## 12. Chốt & Open questions

**Đã chốt (29/09):**
1. **Điều kiện thắng (rev 2.2)**: hết **3 đoạn HP boss** — mỗi đoạn hết → phase mới + **hồi đầy HP**; đoạn cuối hết → finisher. ✅
2. **Ngưỡng chuỗi 5 đòn / buff ±5%/bậc (cap ±15%) / thời gian đếm ngược** — baseline tạm thời, **tune sau playtest**. ✅
3. **Bị boss trúng** → reset chuỗi **+ env -1 bậc**; thêm điều kiện thua: **hết giờ giành lại env** (60s mỗi phase, env vẫn ≤0 → thua) — mô tả ở §3.2/§3.4. ✅
4. **Bỏ hẳn thanh nộ (rev 2.2, 29/09)** — boss quay lại **HP 3 đoạn**; phase chuyển khi hết 1 đoạn (hồi đầy). Không còn fill-rate (light +3% / heavy +8% / F +15%) hay tick; nhịp phase do DPS của bạn quyết. ✅
5. **Adds + Đê — CẮT** (29/09): không spawn quái nhỏ, không trụ đê; trận cuối = **1v1 thuần** + bão sấm (§7). Motif "dân đắp đê" giữ ở mức lore (§1.1). ✅
6. **Ult Sơn Tinh → Hùm Voi Báo** (29/09): thay "Dời Núi + Giáp 3s" — hô hùm voi báo: AoE **r6m** (đồng bộ Thủy, trước đây lệch r5m) + hất tung + **choáng 1.5s** + Đất +2; F Thủy Tinh cũng thêm choáng 1.5s để frame 1:1. Fallback: không kịp summon AI → VFX 3 bóng thú, frame giữ nguyên. *(chi tiết "choáng 1.5s" — nếu không thích thì bỏ, AoE + env +2 vẫn đủ; nói lúc review)* ✅
7. **Bảng env lợi/hại tường minh** (§3.3): đất dâng = lợi Sơn / hại Thủy, nước dâng = lợi Thủy / hại Sơn; phe bất lợi chỉ phạt **speed −5%/bậc** (không phạt stamina — đúng bảng rev 2 gốc; muốn đối xứng cứng phạt cả stamina thì nói lúc tune). ✅
8. **HP — GIỮ, nộ — BỎ (rev 2.2, 29/09)**: bạn chốt *"không bỏ HP, bỏ nộ, giữ phase — boss hết máu → chuyển phase + hồi đầy máu"*. Áp dụng: HP người chơi giữ (lưới thua #1), **HP boss 3 đoạn có hồi đầy**, thanh nộ xoá khỏi §3.4/§4.2/§6/§11. ✅ *(mục [?] đóng)*
9. **Bối cảnh arena + giới hạn di chuyển + HDRP (rev 2.3, 29/09)**: **đồng bằng ngập lũ** (canon nước ngập đồng — không bịa); quyết đấu tại đàn tế = **adaptation "vua cho phép quyết đấu"** (truyền thuyết gốc chỉ có phán sính lễ); **player + boss clip trong arena** r≈14–15m, **Thủy Tinh không lặn ra ngoài** (không swim system); render **HDRP** (project đang URP → migrate). ✅

---

## 13. Bối cảnh & layout arena (environment)

> Chốt 29/09 (rev 2.3): **đồng bằng ngập lũ**, 2 phe **chỉ di chuyển trong arena**, render **HDRP**. Toàn bộ nội dung env/bối cảnh gộp tại đây — không tách file riêng.

### 13.1 Grounding truyền thuyết & adaptation

- **Canon** (Lĩnh Nam chích quái, bản Văn 6): Hùng Vương **chỉ phán xử sính lễ** (ai mang lễ trước → được cưới Mị Nương); **không có quyết đấu nào được vua cho phép** — Thủy Tinh đến trễ, tự dẫn quân *"dâng nước tiến đánh"*, hai bên đánh nhau **ròng rã mấy tháng** giữa cảnh *"nước ngập lúa, ngập đồng rồi ngập nhà"*.
- **ADAPTATION (đã chốt 29/09)**: sau phán xử, Thủy Tinh không phục kéo nước đến, **Hùng Vương cho phép quyết đấu 1v1 tại đàn tế** để bảo vệ dân khỏi lũ — hợp flow GDD *phán xử → trận đấu → ai thắng được Mị Nương* (game-overview §1). Ghi rõ đây là **adaptation của game**, không phải dị bản truyền thuyết.
- **Đồng bằng ngập lũ = canon, không bịa**: chính là bối cảnh nước ngập đồng trong truyện + cảm hứng thơ Nguyễn Nhược Pháp (*"cầu gãy… cột nhà trôi theo"*). Arena = **đàn tế tròn** giữa biển lũ — nơi phán xử thành chiến trường, trung tính "đất lẫn nước".

### 13.2 Quang cảnh (art brief)

- **Sàn đấu**: đàn tế đá tròn ⌀ **~28m** [tune], lòng chảo trũng nhẹ ~1–1.5m [tune] — đối xứng vô hướng, 2 phe không có góc nào là "góc nhà".
- **Lối vào**: bậc/cầu đá nối từ **điện phán xử** trên cao — player + boss đi từ 2 đầu đối diện, nối cutscene Hùng Vương (đã chốt GDD).
- **Xung quanh**: **biển lũ đồng bằng** bao 360° tới chân trời (nước nổi mảnh vỡ trôi: khúc gỗ, mái nhà — gợi *"cột nhà trôi theo"*); 1 bên là **dãy núi** thấp (phía Sơn Tinh), 1 bên là **sông lớn/biển khơi** (phía Thủy Tinh).
- **Bầu trời**: bão đen, sấm chớp định kỳ; mưa rơi dần từ P2, liên tục ở P3 (VFX §10.2).
- **Trong arena**: bệ ấn/đàn tế ở giữa; rìa **cột đá gãy + vũng nước cạn** (motif trung lập đã chốt GDD); đá vụn rải rác — prop che tầm nhìn nhẹ, **không collision phức tạp** (đã chốt).

### 13.3 Ba lớp scene

| Lớp | Nội dung | Tương tác |
|---|---|---|
| **Playable** | đĩa arena ⌀28m + geometry 2 trục (3 ring Đất / plane Nước) + bậc thang + props trong sàn | NavMesh, collision, timeline |
| **Boundary** | tường vô hình cylindrical r ≈ 14–15m [tune] quanh rìa | chặn player + boss |
| **Backdrop** | biển lũ tới chân trời, silhouette núi/sông, điện phán xử, mảnh vỡ trôi, bầu trời bão | non-interactive, chỉ render |

### 13.4 Giới hạn di chuyển — CHỐT

- **Player và boss đều chỉ trong arena** — **Thủy Tinh không lặn ra/vào**; nước sâu xung quanh = backdrop, **không swim system** (không có ở cả player lẫn AI).
- Out-of-bounds bằng mọi cách (nhảy ra rìa, knockback/charge ra rìa) → chặn **tường vô hình**; không kill-zone, không đếm giờ riêng (giếm 60s env §3.4 vẫn là điều kiện thua duy nhất về thời gian).
- **Trong arena**, các bậc depth khi nước dâng (cạn chân → gối → eo) **vẫn walkable**: speed chỉ đến từ % env (§3.3), không phạt thêm do độ sâu.
- NavMesh clip theo đĩa tròn, thu hẹp dần khi nước dâng (§13.6 bước 2–3) — nối tiếp check **T5** (không stuck) + **T8** (không trôi ra ngoài).

### 13.5 HDRP (chốt 29/09)

- **Render pipeline = HDRP** (đúng GDD — game-overview §2/§3) — project hiện đang URP → cần **migrate** trước khi dựng arena (task riêng, xem kế hoạch commit).
- Env cần từ HDRP: water (HDRP Water / Shader Graph), volumetric fog, directional light + storm sky, post-profile (bloom, color grading 2 màu xanh/nâu, AO).
- Fixup nằm trong cùng task migrate: material lỗi (màu hồng) ở scene/prefab sẵn có (Sandbox_Combat, DinhBa, character mats).

### 13.6 Build steps (Phase 4 — chỉ plan, chưa implement)

1. **Blockout** (`Arena_Final`): primitives/ProBuilder — đĩa ⌀28m lòng chảo, bậc thang, placeholder điện, spawn 2 đầu, trigger boundary; **test lock-on/Cinemachine đọc rõ ranh giới ngay từ đầu** (pillar 4).
2. **NavMesh + boundary**: bake trên trạng thái địa hình cao nhất; thu hẹp vùng đi được bằng `NavMeshObstacle` cylinder carve khi nước dâng; verify **T5/T8**.
3. **Geometry 2 trục** (timeline 10–15s/bậc, §3.3): **Đất 0→3** = ring prefab trồi + nước rút; **Nước 0→3** = plane nâng level + bán kính vùng ngập co về giữa. Điều khiển bởi `EnvironmentDirector` event → `PlayableDirector` (§11); khi chưa có code thì debug trigger qua DebugHUD.
4. **Set dressing + backdrop**: cột đá gãy/vũng nước/đá vụn/bệ ấn, điện + bậc, silhouette núi/sông (low-poly + fog), mảnh vỡ trôi, biển lũ mở.
5. **Integration + polish**: HDRP volume 2 màu, rain/bão P3, SFX (§10.3), Cinemachine check, tune các số [tune].

**Giá trị tune mở khi build**: ⌀ arena (28m), r boundary (14–15m), độ sâu lòng chảo, khoảng cách điện ↔ rìa.
