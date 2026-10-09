# Dragon Turtle - Blender animation source

Nguồn Meshy ban đầu là một mesh đơn, chưa có armature. Thư mục này chứa bản gốc, bản Blender đã rig, bản FBX đã xuất và hình kiểm tra các animation.

## File chính

- `DragonTurtle_Meshy_Source.fbx`: FBX Meshy gốc, được sao chép nguyên vẹn từ file người dùng cung cấp.
- `DragonTurtle_Rigged_Animated.blend`: file Blender chính, gồm mesh, custom armature, skin weights và các Action.
- `DragonTurtle_Animated.fbx`: bản xuất dành cho Unity, chứa skinned mesh, armature và cả ba animation clip.

## Animation clips

| Action | Frame | Loại | Mô tả |
|---|---:|---|---|
| `Move` | 1-25 | Loop | Đi về hướng `-Y`, có root motion, bốn chân bước so le, thân nhún, đầu và đuôi chuyển động bù. |
| `Attack` | 1-36 | One-shot | Thu người, há hàm, chồm tới cắn/đập đầu; impact ở frame 19. |
| `Defeated` | 1-58 | One-shot | Loạng choạng, khuỵu chân, ngã sang bên trái và nằm yên ở frame 58. |

Scene dùng 24 fps, trục tiến `-Y`, trục lên `+Z`, đơn vị mét.

## Mở và xem trong Blender

1. Mở `DragonTurtle_Rigged_Animated.blend`.
2. Chọn `Dragon_Turtle_Rig`.
3. Chuyển một vùng làm việc sang **Dope Sheet > Action Editor**.
4. Chọn `Move`, `Attack` hoặc `Defeated` trong danh sách Action rồi nhấn Play.

## Import vào Unity

Trong tab **Animation** của `DragonTurtle_Animated.fbx`, Unity sẽ nhận ba take có tên dạng `Dragon_Turtle_Rig|Move`, `Dragon_Turtle_Rig|Attack` và `Dragon_Turtle_Rig|Defeated`.

- Bật **Loop Time** cho `Move`.
- Tắt **Loop Time** cho `Attack` và `Defeated`.
- Nếu gameplay tự điều khiển vị trí nhân vật, có thể bật **Bake Into Pose** cho Root Transform Position của `Move`; nếu dùng root motion thì giữ chuyển động gốc.

## Scripts và previews

- `rig_and_animate_dragon_turtle.py`: tạo armature, skin weights, Action, lưu `.blend` và xuất FBX.
- `render_animation_previews.py`: render hình kiểm tra các clip.
- `Previews/DragonTurtle_Move.png`, `DragonTurtle_Attack.png`, `DragonTurtle_Defeated.png`: key pose đã được render bằng Blender để kiểm tra nhanh.
