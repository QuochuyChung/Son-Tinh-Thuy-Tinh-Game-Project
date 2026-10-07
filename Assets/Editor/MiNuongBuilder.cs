using UnityEditor;

namespace SonTinhThuyTinh.EditorTools
{
    // Mị Nương (NPC for the judgement cutscene and the endings): Assets/Art/Characters/MiNuong/mi_nuong.fbx (armature + MiNuong_Body, the
    // clothed Meshy body, + MiNuong_Drape: the cloth used twice, hanging from both shoulders over the hair, chains Cape_0_* / Cape_1_* under
    // Spine2, made by tools/fit_mi_nuong.py) and her Mixamo clips in Assets/Animations/MiNuong. The work is done by NpcBuilder:
    // AC_MiNuong (Idle by default; triggers Idle / Talk / Bow / Shy / Happy / Walk) and Assets/Prefabs/Characters/MiNuong.prefab,
    // 1.70 m at the head-top bone, drapes Render Face: Both, OutfitSpringBones on the drapes.
    // There is no Shy clip yet: Shy plays Thankful (the same clip as Bow) until one is downloaded.
    public static class MiNuongBuilder
    {
        public static readonly NpcBuilder.Spec Spec = new()
        {
            Name = "MiNuong",
            Prefix = "mi_nuong",
            ArtDir = "Assets/Art/Characters/MiNuong",
            AnimDir = "Assets/Animations/MiNuong",
            HeadTopHeight = 1.70f,
            Clips = new[]
            {
                ("Idle", "mi_nuong_idle.fbx", true, false),
                ("Talk", "mi_nuong_talking.fbx", true, false),
                ("Bow", "mi_nuong_thankful.fbx", false, true),
                ("Shy", "mi_nuong_thankful.fbx", false, true),
                ("Happy", "mi_nuong_happy.fbx", true, false),
                ("Walk", "mi_nuong_walk.fbx", true, false),
            },
            Parts = new[]
            {
                ("Body", "body", false),
                ("Drape", "drape", true),
            },
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Mi Nuong NPC")]
        public static void Build() => NpcBuilder.Build(Spec);

        // batch entry: unity run <project> -- -executeMethod SonTinhThuyTinh.EditorTools.MiNuongBuilder.BuildBatch [-npcShots <dir>]
        public static void BuildBatch() => NpcBuilder.BuildBatch(Spec);
    }
}
