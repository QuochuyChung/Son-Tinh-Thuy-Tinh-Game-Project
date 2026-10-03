# Tranh minh hoạ Prologue: danh sách cảnh + prompt

> **Trạng thái (02/10): cả 8 ảnh đã làm xong và đã vào game** (`Assets/Art/Story/`, gốc ở `ArtSource/Story/`). File này giữ lại để vẽ bổ sung hoặc làm cutscene phán xét và đoạn kết cho cùng phong cách.

Màn Prologue hiện chỉ có chữ trên nền đen: màn tiêu đề, rồi 12 câu thoại/dẫn truyện (`Assets/Data/Dialogue/Dialogue_Prologue.asset`).
Hệ thống hiện ảnh toàn màn hình phía sau khung chữ đã code xong (`DialogueLine.illustration`, xem `docs/progress.md` mục 8.5).
Dòng nào được gán ảnh thì hiện ảnh đó, dòng không gán thì giữ ảnh của dòng trước.

## Quy cách ảnh

- Tỉ lệ **16:9** (lý tưởng 1920×1080 trở lên). Ảnh khác tỉ lệ sẽ bị viền đen 2 bên (Image đang bật Preserve Aspect). Gửi tôi, tôi cắt giùm.
- Khung chữ nằm ở **30% dưới của màn hình** (cao 250 px, cách đáy 60 px, trên nền đen mờ 82%). Chủ thể đặt ở **2/3 phía trên**, phần dưới để thoáng (đất, nước, cỏ).
- Không có chữ, logo, khung viền trong ảnh.

## Cách làm cho 4 nhân vật giống nhau ở mọi ảnh

1. Mở **1 cuộc trò chuyện mới** trong Gemini hoặc ChatGPT, tải lên 4 ảnh tham chiếu **một lần ở đầu**:
   ảnh 1 = Sơn Tinh, ảnh 2 = Thủy Tinh, ảnh 3 = Hùng Vương, ảnh 4 = Mị Nương.
2. Dán khối **PHONG CÁCH + NHÂN VẬT** bên dưới, rồi lần lượt dán prompt từng cảnh **trong cùng cuộc trò chuyện** đó.
3. Ảnh đầu tiên ưng ý thì giữ lại làm mẫu phong cách: nhắn "các ảnh sau giữ đúng phong cách của ảnh này".
4. Mặt bị lệch thì nhắn: "giữ đúng khuôn mặt và trang phục của ảnh 3 (hoặc 1, 2, 4), vẽ lại".
5. Với Sơn Tinh và Thủy Tinh nên dùng ảnh của **model 3D hiện tại** làm ảnh 1 và 2
   (`Assets/Art/UI/Portraits/Portrait_SonTinh.png`, `Portrait_ThuyTinh.png`), không dùng `ArtSource/Concept/son_tinh.jpg` và `thuy_tinh.jpg` vì đó là thiết kế cũ.

### Khối PHONG CÁCH + NHÂN VẬT (dán 1 lần, ngay sau 4 ảnh tham chiếu)

```text
You are drawing a series of story illustrations for a 3D action game based on the Vietnamese legend of Sơn Tinh and Thủy Tinh. Every image must look like it belongs to the same set.

STYLE: cinematic 2D digital illustration, 16:9 landscape, stylized semi-realistic painterly look with clean cel-shaded shading and strong silhouettes, like a high quality game cutscene illustration (not photorealistic, not cartoonish). Rich but not oversaturated colors, dramatic lighting.

SETTING: ancient Vietnam, the Văn Lang kingdom of the Hùng Kings, Đông Sơn culture: bronze drums, thatched stilt houses, feather headdresses, hemp and silk garments, bronze ornaments, rice fields, tropical mountains and rivers. NOT Chinese and NOT Japanese: no pagodas, no Chinese dragon robes, no katana, no samurai.

CHARACTERS: use the four attached reference images. Image 1 = Sơn Tinh (lord of the mountains; palette earth brown, forest green, gold). Image 2 = Thủy Tinh (lord of the waters; palette deep blue, silver, white). Image 3 = Hùng Vương, the king. Image 4 = Mị Nương, the princess. Keep each character's face, hair, outfit and colors exactly as in their reference in every picture.

COMPOSITION RULES: no text, no letters, no watermark, no border. Keep the important subjects in the upper two thirds of the image; the bottom third should be calm (ground, water, grass) because a text box will cover it.

Reply "ready" and wait for the first scene.
```

## 8 cảnh (khớp với 12 dòng thoại)

| # | Tên file | Các dòng thoại | Nội dung |
|---|---|---|---|
| 1 | `01_van_lang` | dòng 1 | Toàn cảnh nước Văn Lang thái bình |
| 2 | `02_mi_nuong` | dòng 2–3 | Mị Nương, đến tuổi lấy chồng |
| 3 | `03_hung_vuong_ban_lenh` | dòng 4–5 | Hùng Vương ban lệnh kén rể |
| 4 | `04_son_tinh` | dòng 6 | Sơn Tinh xuất hiện |
| 5 | `05_thuy_tinh` | dòng 7 | Thủy Tinh xuất hiện |
| 6 | `06_doi_dau` | dòng 8–9 | Hai chàng đứng trước Hùng Vương, vua khó chọn |
| 7 | `07_sinh_le` | dòng 10 | Voi chín ngà, gà chín cựa, ngựa chín hồng mao |
| 8 | `08_cuoc_dua` | dòng 11–12 | Hai vị thần lên đường, cuộc đua bắt đầu |

Dòng 3, 5, 9, 12 không có ảnh riêng, tự giữ ảnh của dòng ngay trước.

### Prompt từng cảnh

**01_van_lang**
```text
Scene 1. Wide establishing shot of the Văn Lang kingdom at golden sunrise. A green rice plain, thatched stilt houses along a river, farmers working, a large bronze drum on a small hill, the royal hall of Phong Châu on a low hill in the middle distance, and the three-peaked Tản Viên mountain far in the background, soft morning mist. Peaceful, prosperous, warm light. No main characters needed.
```

**02_mi_nuong**
```text
Scene 2. Mị Nương (image 4) in the palace garden at dusk, half-body portrait, standing by a wooden railing covered with flowers, gentle and radiant, with a faint thoughtful look because she has reached the age of marriage. Soft lantern light, blurred palace architecture behind her. Her face and outfit exactly as in the reference.
```

**03_hung_vuong_ban_lenh**
```text
Scene 3. King Hùng Vương (image 3) seated on the throne in the royal hall of Phong Châu, one hand raised, proclaiming a decree to the whole land. Officials bowing on both sides, heralds beating bronze drums, messengers leaving through the open gate into a beam of sunlight. Feather banners and bronze ornaments. Dignified, epic mood.
```

**04_son_tinh**
```text
Scene 4. Sơn Tinh (image 1) arrives at the royal court, heroic low-angle shot, standing on a rocky outcrop in front of the palace gate, one hand pointing forward. Behind him mountains rise out of the earth, boulders and trees growing, green and gold light, his feather mantle fluttering, confident expression.
```

**05_thuy_tinh**
```text
Scene 5. Thủy Tinh (image 2) arrives at the royal court, heroic low-angle shot, standing before a stormy sky with huge waves rising behind him, one hand raised commanding the wind and rain, water swirling around his feet, blue and silver light, his long hair whipping in the wind, confident expression.
```

**06_doi_dau**
```text
Scene 6. Inside the throne hall, a symmetrical tense composition. Sơn Tinh (image 1) on the left with faint mountains and warm earthy light, Thủy Tinh (image 2) on the right with faint waves and cool blue light, both facing the throne. Hùng Vương (image 3) sits in the center background, thoughtful and torn, unable to choose. Mị Nương (image 4) barely visible behind the throne curtain.
```

**07_sinh_le**
```text
Scene 7. The three bridal gifts as an epic still life in the royal courtyard at sunset: a giant elephant with nine tusks, a rooster with nine spurs, a horse with nine red manes, each glowing softly and majestic. Hùng Vương (image 3) stands small in the background pointing at them. Mythical, golden light, Đông Sơn style ornaments on the ground.
```

**08_cuoc_dua**
```text
Scene 8. Dramatic wide shot of two paths splitting from the royal gate. On the left Sơn Tinh (image 1), seen from behind, walks toward misty mountains. On the right Thủy Tinh (image 2), seen from behind, walks toward a stormy sea. Both are small against a huge sunset sky. A tiny figure of Mị Nương (image 4) watches from the palace balcony in the middle. Epic mood, the race has begun.
```

## Đưa vào game

1. Lưu ảnh thành `01_van_lang.png`, `02_mi_nuong.png`... (PNG hoặc JPG đều được), bỏ vào `ArtSource/Story/`.
2. Báo Claude. Claude sẽ:
   - chép vào `Assets/Art/Story/`, đổi loại thành Sprite (2D and UI), cắt về 16:9 nếu cần;
   - gán vào đúng dòng: ảnh 1 → dòng 1, ảnh 2 → dòng 2, ảnh 3 → dòng 4, ảnh 4 → dòng 6, ảnh 5 → dòng 7, ảnh 6 → dòng 8, ảnh 7 → dòng 10, ảnh 8 → dòng 11 (đếm từ 1);
   - chạy thử Prologue và chụp lại để kiểm tra (phần hiện ảnh này chưa từng được thử với ảnh thật).
3. Tự làm tay thì: kéo ảnh vào `Assets/Art/Story/`, chọn ảnh, Texture Type = Sprite (2D and UI), Apply;
   mở `Assets/Data/Dialogue/Dialogue_Prologue.asset`, mở từng dòng ở danh sách `Lines`, kéo sprite vào ô **Illustration**.

Hùng Vương và Mị Nương còn xuất hiện ở cutscene phán xét và 2 đoạn kết, nên giữ 4 ảnh tham chiếu và cuộc trò chuyện làm ảnh sau cho khớp.
