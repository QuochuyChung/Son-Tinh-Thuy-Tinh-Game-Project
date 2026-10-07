using UnityEditor;

namespace SonTinhThuyTinh.EditorTools
{
    // Hùng Vương (NPC for the judgement cutscene): Assets/Art/Characters/HungVuong/hung_vuong.fbx (armature + HungVuong_Body, the clothed
    // Meshy body, + HungVuong_Cape with the Cape_<column>_<segment> chains under Spine2, made by tools/fit_hung_vuong.py) and the four Mixamo
    // clips in Assets/Animations/HungVuong. The work is done by NpcBuilder: AC_HungVuong (Idle by default; triggers Talk / Point / Nod / Idle)
    // and Assets/Prefabs/Characters/HungVuong.prefab, 1.9 m at the head-top bone, cape Render Face: Both, OutfitSpringBones on the cape.
    public static class HungVuongBuilder
    {
        public static readonly NpcBuilder.Spec Spec = new()
        {
            Name = "HungVuong",
            Prefix = "hung_vuong",
            ArtDir = "Assets/Art/Characters/HungVuong",
            AnimDir = "Assets/Animations/HungVuong",
            HeadTopHeight = 1.9f,   // same as the two players
            Clips = new[]
            {
                ("Idle", "hung_vuong_idle.fbx", true, false),
                ("Talk", "hung_vuong_talking.fbx", true, false),
                ("Point", "hung_vuong_pointing.fbx", false, true),
                ("Nod", "hung_vuong_nod.fbx", false, true),
            },
            Parts = new[]
            {
                ("Body", "body", false),
                ("Cape", "cape", true),
            },
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Hung Vuong NPC")]
        public static void Build() => NpcBuilder.Build(Spec);

        // batch entry: unity run <project> -- -executeMethod SonTinhThuyTinh.EditorTools.HungVuongBuilder.BuildBatch [-npcShots <dir>]
        public static void BuildBatch() => NpcBuilder.BuildBatch(Spec);
    }
}
