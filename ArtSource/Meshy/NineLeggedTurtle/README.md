# Nine-Legged Turtle - Meshy source

Gói model có texture được đổi tên từ bản Meshy người dùng cung cấp.

- `NineLeggedTurtle_Textured.fbx`: model Meshy có UV/material source.
- `Textures/NineLeggedTurtle_BaseColor.png`: màu bề mặt.
- `Textures/NineLeggedTurtle_Normal.png`: normal map.
- `Textures/NineLeggedTurtle_Metallic.png`: metallic map.
- `Textures/NineLeggedTurtle_Roughness.png`: roughness map gốc để lưu trữ và dùng khi cần đóng gói PBR.

Các texture tương ứng được sao chép vào `Assets/Art/Characters/NineLeggedTurtle/Textures` và gắn lên `M_NineLeggedTurtle.mat`. Model chiến đấu trong `Assets/Art/Characters/NineLeggedTurtle/NineLeggedTurtle_Animated.fbx` đã được rig lại trực tiếp từ FBX có texture này để giữ đúng UV khi chạy animation.
