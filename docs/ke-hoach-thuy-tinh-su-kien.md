# Kế hoạch: sự kiện + cốt truyện nhánh Thủy Tinh, thiết kế lại `Map_ThuyTinh` (11/10)

**Trạng thái 11/10: đã làm xong** (xem progress.md 9.27). Thứ tự thực tế: ngư tặc &rarr; làng &rarr; hải đăng (ma nước, thắp đèn) &rarr; Vịnh Mập &rarr; đảo Sấu &rarr; đền chìm (Rùa) &rarr; cổng. Cổng ra và đảo Sấu giữ nguyên chỗ (không dời) nên đường đắp vẫn là đoạn đầu + đoạn cuối.

## 0. Vì sao phải làm

- Mập 9 Vây và Rùa 9 Chân hiện là "chạm vào là chuyển sang scene riêng" (`Battle_Map9Vay`, `Battle_Rua9Chan`): không có NPC, thoại, mở đầu, thua/thắng trong map. Bên Sơn Tinh đã bỏ kiểu đó (Voi, Ngựa, Gà đều là sự kiện ngay trong map).
- Làng Chài, Bến Thuyền, Hải đăng, Đảo Thủy Cung mới chỉ là cảnh, chưa có chuyện gì xảy ra ở đó.
- Sấu Chín Đuôi đã là sự kiện trong map (đảo, thoại, hồi sinh) nhưng nằm trên đường đắp cũ, lệch khỏi trục biển mới.

## 1. Chốt từ lần bàn này

| Quyết định | Chọn |
|---|---|
| Trục chính | **Đường thủy** (đường đắp cũ chỉ còn là đoạn mở đầu) |
| Phe địch | **Ngư tặc** (kẻ địch thường) + 3 linh thú nổi giận (boss) |
| Phạm vi | Đủ 5 sự kiện, làm lần lượt, thử từng cái |

## 2. Cốt truyện nhánh Thủy Tinh

Thủy Tinh là chúa vùng nước, nhận chỉ dụ của Vua Hùng đi lấy sính lễ. Trên vịnh, ba linh thú canh vùng nước (Mập 9 Vây, Sấu 9 Đuôi, Rùa 9 Chân) đang nổi giận: **bọn ngư tặc** chiếm Bến Thuyền, phá lưới, cướp ngọc, làm hải đăng tắt đèn, sương dày không ai ra khơi được. Linh thú không chịu nhường sính lễ cho kẻ chưa chứng tỏ mình bảo vệ được vùng nước.

Mạch: dẹp tặc → dân làng tin → thắp lại hải đăng cho sương tan → lần lượt thuần phục ba linh thú → cổng cung điện mở.

## 3. Năm sự kiện (theo thứ tự đi)

| # | Nơi | Chuyện | Kiểu làm (tái dùng) |
|---|---|---|---|
| 0 | Đầu đầm | Thoại mở: nước dâng bất thường, sương dày. Đi theo đường đắp tới kênh, qua bè gỗ. | hệ thoại |
| 1 | **Bến Thuyền** | Lão chài giao việc; 3–4 ngư tặc trên sàn bến + một tên cầm đầu; xích chặn đường ra cầu cho đến khi dẹp xong. | khuôn quest Ngựa: NPC + kẻ cướp + rào chắn (`HorseQuestBuilder.BuildMountainEvent`) |
| 2 | **Làng Chài** | Trưởng làng kể về ba linh thú, chỉ đường ra hải đăng. Chỗ nghỉ: lửa trại hồi máu, nhận mốc hồi sinh. | NPC + `RespawnCheckpoint` |
| 3 | **Hải đăng** | Đèn tắt, ma nước (kẻ địch thường) canh chân tháp. Dẹp xong, lên thắp đèn (E), sương tan → bản đồ lộ ra phần còn lại của vịnh. | khuôn `BellSpot` + giảm `fogDensity` trong game |
| 4 | **Vịnh Mập 9 Vây** | Vũng kín giữa vành karst; bóng vây lượn, lặn, trồi lên đớp. Có thanh máu, thoại mở, thua thì hồi sinh ở mép, thắng nhận quà. | `VoiChinNgaArenaEvent` làm khuôn → arena event dùng `ManyFinnedSharkBoss` |
| 5 | **Đảo Sấu 9 Đuôi** | Giữ nguyên trận cũ; thêm thoại mở và kết (đã là sự kiện trong map). | `SauChinDuoiBuilder` hiện có |
| 6 | **Đảo Thủy Cung** | Cuối đường: Rùa Chín Chân (cơ chế máu hai chiều như bản hiện tại). Thắng → đủ 3 sính lễ → cổng ra mở. | arena event dùng `NineLeggedTurtleBoss` |

Thứ tự 4/5/6 có thể đổi nếu thấy độ khó không hợp (Sấu khó nhất, nên có thể nằm giữa).

## 4. Việc phải đổi trên map

1. **Cổng ra dời về Thủy Cung** (đường lên cung điện không còn cần trên đường đắp). Đường đắp sau z≈110 thành đoạn phụ hoặc bỏ.
2. **Dời đảo Sấu vào vịnh** (hiện ở z=175 trên đường đắp): đổi `Stop(1)` thành một điểm trong vịnh; sàn hồ sâu quanh đảo đi theo.
3. **Vịnh Mập** (vũng kín trong vành karst, bãi đứng cho người chơi + chỗ cá lặn) và **đảo canh hải đăng** (khoảng trống quanh tháp cho trận ma nước).
4. **Chỗ ngư tặc ở Bến Thuyền** (sàn rộng hơn, thêm xích chặn cầu và vài thuyền cướp neo cạnh).
5. **Đường đi**: Bến Thuyền → Làng Chài → Hải đăng → Vịnh Mập → Đảo Sấu → Đảo Thủy Cung nối liền bằng cầu ván, có mốc hồi sinh mỗi chặng.
6. Đảo Thủy Cung: đủ rộng cho trận Rùa (r≥15) và có cổng.
7. Cập nhật bản đồ thế giới (nhãn mới, vị trí cổng) và HUD map (3 ghim sính lễ mới).

## 5. Việc kỹ thuật

- Trạng thái quest tĩnh như `HorseQuest`: `SeaQuest` (NotStarted → Accepted → PiratesCleared → LampLit → …) giữ qua lần đổi scene.
- Chuyển Mập và Rùa vào map: gỡ `ManyFinnedSharkEncounterTrigger` / `NineLeggedTurtleEncounterTrigger`; sao chép sự kiện kiểu `VoiChinNgaArenaEvent` (đánh thức khi vào vùng, thoại mở, thanh máu, hồi sinh, thưởng). Hai scene `Battle_*` để lại, không dùng nữa.
- Kẻ địch thường (ngư tặc, ma nước): dùng lại AI kẻ trộm ngựa (`HorseThief`) với model/màu khác.
- Mỗi sự kiện có test tự chạy giống `ElephantEventSmokeTest`.
- Thoại: soạn bằng tiếng Việt, lấy giọng NPC từ `AudioManager.VoiceFor`.

## 6. Thứ tự làm đề xuất

1. Thiết kế lại map (mục 4) + cổng + mốc hồi sinh → chốt vị trí các sự kiện.
2. Sự kiện Mập 9 Vây (cái người dùng thấy kì nhất) làm trước, rồi Rùa.
3. Ngư tặc + Lão chài, rồi hải đăng.
4. Thoại mở/kết cho Sấu, nối tất cả bằng `SeaQuest`.
5. Cập nhật bản đồ thế giới + progress.md.

## 7. Câu hỏi còn mở

- Thủy Tinh dẹp tặc bằng gì: chỉ đánh gần như hiện tại, hay có thêm "phép nước" đẩy tặc ra khỏi cầu (đúng thiết kế ban đầu về tương tác nguyên tố)?
- Lão chài và Trưởng làng có dùng chung một nhân vật không?
- Mập và Rùa có cần model/animation mới không, hay dùng đúng bản của Huy?
