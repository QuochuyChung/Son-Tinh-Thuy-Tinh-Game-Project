using System.Collections;
using System.Linq;
using System.Reflection;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Cutscene;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SonTinhThuyTinh.Trailer
{
    // The scripted shots of the draft trailer. Each one is started in Play mode of its scene (Assets/Editor/Trailer/TrailerRecorder.cs
    // opens the scene, enters Play mode with the shot name in Trailer_Output/pending_shot.txt) and records one or more clips with
    // TrailerCapture. Map shots use their own camera (never the gameplay camera), so the player's camera rig and HUD do not matter.
    public class TrailerShots : MonoBehaviour
    {
        public static bool Busy => instance != null && instance.running;
        static TrailerShots instance;
        bool running;
        Camera shotCam;

        public static string PendingFile => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Trailer_Output", "pending_shot.txt"));
        static string pending;

        // before the scene's Awake: pick the suitor (PlayerSpawner reads it) and keep a map's intro / quest scripts from holding the game
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ReadPending()
        {
            pending = null;
            if (!Application.isEditor || !System.IO.File.Exists(PendingFile)) return;
            pending = System.IO.File.ReadAllText(PendingFile).Trim();
            System.IO.File.Delete(PendingFile);
            GameSession.StartNewGame();
            GameSession.SelectedCharacter = pending.EndsWith("_thuy") || pending == "thuytinh_map" || pending == "v2_thuy" || pending == "v3_beasts_thuy" ? CharacterId.ThuyTinh : CharacterId.SonTinh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RunPending()
        {
            if (!string.IsNullOrEmpty(pending))
            {
                // no scene change in the middle of a shot: the doors to other scenes (SceneTransitionTrigger) are switched off for this Play session
                foreach (var door in FindObjectsByType<SceneTransitionTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    foreach (var c in door.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                // two palace torches (Generated/Roadside/Torch at x = ±4.4, z = 31) have their Fire 1.6-1.9 m above the bowl; every
                // other torch has it at 2.05 m. Put it back for this Play session only
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == "Torch" && t.Find("Fire") is Transform fire && fire.localPosition.y > 2.5f) fire.localPosition = new Vector3(0f, 2.05f, 0f);
                Run(pending);
            }
            pending = null;
        }

        public static void Run(string shot)
        {
            if (instance == null)
            {
                var go = new GameObject("TrailerShots");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<TrailerShots>();
            }
            instance.running = true;
            instance.StartCoroutine(instance.Play(shot));
        }

        IEnumerator Play(string shot)
        {
            IEnumerator body = shot switch
            {
                "opening" => Opening(),
                "palace" => Palace(),
                "sontinh_map" => SonTinhMap(),
                "thuytinh_map" => ThuyTinhMap(),
                "skills_son" => Skills("son", new[] { 0, 1, 2 }),
                "skills_thuy" => Skills("thuy", new[] { 0, 1, 2 }),
                "clash" => Clash(),
                "v2_palace" => V2Palace(),
                "v2_son" => V2Son(),
                "v2_thuy" => V2Thuy(),
                "v2_duel_son" => V2Duel("son"),
                "v2_duel_thuy" => V2Duel("thuy"),
                "v2_clash" => V2Clash(),
                "v2_fix_palace" => V2FixPalace(),
                "v2_fix_son" => V2FixSon(),
                "v3_palace" => V2FixPalace(),
                "v3_beasts_son" => V3BeastsSon(),
                "v3_beasts_thuy" => V3BeastsThuy(),
                "v3_horse_fix" => V3HorseFix(),
                "v4_tour_son" => MapTour("v4_son"),
                "v4_tour_thuy" => MapTour("v4_thuy"),
                "v4_clash_side" => V4ClashSide("v4_clash_side", false),
                "v4_clash_high" => V4ClashSide("v4_clash_high", true),
                "v5_hero_son" => V5Hero("v5_hero_son"),
                "v5_hero_thuy" => V5Hero("v5_hero_thuy"),
                "v3_turtle_fix_thuy" => V3TurtleFix(),
                _ => null,
            };
            if (body == null) Debug.LogError("[Trailer] unknown shot " + shot);
            else yield return body;
            Time.timeScale = 1f;
            running = false;
            Debug.Log("[Trailer] shot finished " + shot);
        }

        // ---------------------------------------------------------------- tools

        static readonly MethodInfo NextLine = typeof(DialogueRunner).GetMethod("ShowNextLine", BindingFlags.Instance | BindingFlags.NonPublic);

        Camera ShotCamera()
        {
            if (shotCam != null) return shotCam;
            var go = new GameObject("TrailerCamera");
            shotCam = go.AddComponent<Camera>();
            shotCam.enabled = false;   // rendered by TrailerCapture only
            shotCam.nearClipPlane = 0.1f; shotCam.farClipPlane = 2000f;
            var main = Camera.main;
            if (main != null)
            {
                var src = main.GetComponent<UniversalAdditionalCameraData>();
                var dst = shotCam.GetUniversalAdditionalCameraData();
                dst.renderPostProcessing = true;
                if (src != null) { dst.antialiasing = src.antialiasing; dst.volumeLayerMask = src.volumeLayerMask; }
                shotCam.cullingMask = main.cullingMask;
                shotCam.clearFlags = main.clearFlags; shotCam.backgroundColor = main.backgroundColor;
            }
            return shotCam;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        // a straight move with a look point that also moves
        IEnumerator Move(Vector3 p0, Vector3 p1, Vector3 l0, Vector3 l1, float fov0, float fov1, float seconds)
        {
            var cam = ShotCamera();
            var cap = TrailerCapture.Current;
            float start = cap.Seconds;
            while (true)
            {
                if (cam == null || cap == null) yield break;
                float t = Mathf.Clamp01((cap.Seconds - start) / seconds), e = Smooth(t);
                cam.transform.position = Vector3.Lerp(p0, p1, e);
                cam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(l0, l1, e) - cam.transform.position);
                cam.fieldOfView = Mathf.Lerp(fov0, fov1, e);
                if (t >= 1f || cap.Done) yield break;
                yield return null;
            }
        }

        IEnumerator Orbit(System.Func<Vector3> centre, float radius0, float radius1, float h0, float h1, float deg0, float deg1, float lookUp, float fov, float seconds)
        {
            var cam = ShotCamera();
            var cap = TrailerCapture.Current;
            float start = cap.Seconds;
            while (true)
            {
                if (cam == null || cap == null) yield break;
                float t = Mathf.Clamp01((cap.Seconds - start) / seconds), e = Smooth(t);
                float a = Mathf.Lerp(deg0, deg1, e) * Mathf.Deg2Rad;
                Vector3 c = centre();
                Vector3 p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Lerp(radius0, radius1, e) + Vector3.up * Mathf.Lerp(h0, h1, e);
                float ground = GroundY(p) + 0.6f;
                if (p.y < ground) p.y = ground;
                cam.transform.position = p;
                cam.transform.rotation = Quaternion.LookRotation(c + Vector3.up * lookUp - p);
                cam.fieldOfView = fov;
                if (t >= 1f || cap.Done) yield break;
                yield return null;
            }
        }

        static float GroundY(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return float.MinValue;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 4f);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject Find(string name) => FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name)?.gameObject;

        // dialogue boxes (quest keepers, intros) hold the game: press through them
        static void SkipDialogue()
        {
            foreach (var r in FindObjectsByType<DialogueRunner>(FindObjectsSortMode.None))
                for (int i = 0; i < 40 && r.IsPlaying; i++) NextLine.Invoke(r, null);
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        IEnumerator Warm(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) { SkipDialogue(); yield return null; }
        }

        static PlayerController ThePlayer() => FindFirstObjectByType<PlayerSpawner>()?.Player;

        static void Teleport(PlayerController p, Vector3 pos, Vector3? face = null)
        {
            p.SetBodyEnabled(false);
            p.transform.position = pos + Vector3.up * 0.2f;
            if (face.HasValue) { Vector3 d = face.Value - pos; d.y = 0f; if (d.sqrMagnitude > 0.01f) p.transform.rotation = Quaternion.LookRotation(d); }
            p.SetBodyEnabled(true);
        }

        // a map's intro fly-through (MapIntroCutscene), its lines advanced every few seconds of video time
        IEnumerator RecordIntro(string clip, float maxSeconds, float perLine)
        {
            var intro = FindObjectsByType<MapIntroCutscene>(FindObjectsSortMode.None).FirstOrDefault();
            var runner = FindObjectsByType<DialogueRunner>(FindObjectsSortMode.None).FirstOrDefault(r => r.IsPlaying);
            if (intro == null || runner == null) { Debug.LogWarning("[Trailer] no intro playing for " + clip); yield break; }
            var cap = TrailerCapture.Begin(clip, maxSeconds);
            float next = perLine;
            while (!cap.Done)
            {
                if (!runner.IsPlaying) { cap.Stop(); break; }
                if (cap.Seconds >= next) { NextLine.Invoke(runner, null); next += perLine; }
                yield return null;
            }
            yield return cap.WaitDone();
        }

        // ---------------------------------------------------------------- the shots

        // The whole "Kén rể" cutscene in one take (the trailer cuts excerpts out of it): the dialogue box is not in the picture, so the
        // lines are advanced on a timer of video time instead of key presses.
        IEnumerator Opening()
        {
            float[] lineSeconds = { 13.8f, 9.2f, 12.8f, 6.2f, 10.5f, 7.2f, 7.6f, 10.5f, 11.8f, 9.8f, 12.5f };
            float total = 9.3f;
            foreach (float s in lineSeconds) total += s;
            var cap = TrailerCapture.Begin("opening", total);
            var runner = FindAnyObjectByType<DialogueRunner>();
            while (!runner.IsPlaying && !cap.Done) yield return null;
            for (int i = 0; i < lineSeconds.Length && !cap.Done; i++)
            {
                float until = cap.Seconds + lineSeconds[i];
                while (cap.Seconds < until && !cap.Done) yield return null;
                if (i < lineSeconds.Length - 1 && runner.IsPlaying) NextLine.Invoke(runner, null);
            }
            yield return cap.WaitDone();
        }

        // Map_HungVuong: a drone pass over the square and up the avenue to the palace, then around the throne
        IEnumerator Palace()
        {
            yield return Warm(2f);
            var palace = Find("PalaceFront"); var throne = Find("Throne");
            Bounds pb = palace != null ? BoundsOf(palace) : new Bounds(new Vector3(0f, 20f, 40f), Vector3.one * 20f);
            Vector3 front = new(pb.center.x, pb.min.y, pb.min.z);
            var cap = TrailerCapture.Begin("palace_fly", 6f, ShotCamera());
            yield return Move(front + new Vector3(-14f, 22f, -70f), front + new Vector3(4f, 9f, -16f), front + new Vector3(0f, 6f, 0f), front + new Vector3(0f, 9f, 10f), 50f, 44f, 6f);
            yield return cap.WaitDone();
            if (throne != null)
            {
                Bounds tb = BoundsOf(throne);
                cap = TrailerCapture.Begin("palace_throne", 4.5f, ShotCamera());
                yield return Orbit(() => tb.center - Vector3.up * tb.extents.y, 7f, 5f, 2.5f, 1.8f, 200f, 150f, 1.6f, 40f, 4.5f);
                yield return cap.WaitDone();
            }
        }

        // Map_SonTinh: the intro fly-through, the ninja fight at the boulder ring, the stable and the heirloom bell
        IEnumerator SonTinhMap()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            yield return RecordIntro("sontinh_intro", 30f, 5f);
            yield return Warm(1f);
            var player = ThePlayer();
            // ninja fight
            var rooster = Find("RoosterQuest");
            if (rooster != null && player != null)
            {
                var centre = rooster.transform.Find("Marks/RingCentre"); var mark = rooster.transform.Find("Marks/SonTinhMark");
                Teleport(player, centre.position - (centre.position - mark.position).normalized * 17f, centre.position);
                yield return Warm(4f);   // the bandits' cutscene, pressed through
                Teleport(player, centre.position + (mark.position - centre.position).normalized * 3f, centre.position);
                yield return Warm(1.5f);
                var cap = TrailerCapture.Begin("ninja_fight", 7f, ShotCamera());
                var input = player.InputReader;
                input.AiActivate();
                StartCoroutine(Brawl(player, cap));
                yield return Orbit(() => player.transform.position, 6.5f, 5f, 2.2f, 1.6f, 120f, 40f, 1.1f, 45f, 7f);
                yield return cap.WaitDone();
                input.AiDeactivate();
            }
            // the stable and the bell
            foreach (var (name, clip) in new[] { ("Stable", "stable"), ("BellSpot", "bell") })
            {
                var go = Find(name);
                if (go == null) { Debug.LogWarning("[Trailer] missing " + name); continue; }
                Bounds b = BoundsOf(go);
                float r = Mathf.Clamp(b.extents.magnitude * 1.6f, 5f, 18f);
                var cap = TrailerCapture.Begin(clip, 3.5f, ShotCamera());
                yield return Orbit(() => new Vector3(b.center.x, b.min.y, b.center.z), r, r * 0.85f, r * 0.35f, r * 0.25f, 30f, -20f, Mathf.Min(b.size.y * 0.5f, 3f), 45f, 3.5f);
                yield return cap.WaitDone();
            }
        }

        // the player fights whatever attacks it: light combo, now and then a spell, never dies
        IEnumerator Brawl(PlayerController player, TrailerCapture cap)
        {
            var input = player.InputReader;
            int n = 0;
            while (!cap.Done)
            {
                player.Health.Heal(1000f);
                player.FaceCombatTarget(1f, true);
                player.AcquireCombatTarget(12f);
                if (n % 25 == 0) input.AiPressLight();
                if (n == 60) input.AiPressSpell(2);
                if (n == 140) input.AiPressSpell(0);
                n++;
                yield return null;
            }
        }

        // Map_ThuyTinh: the intro fly-through, the lighthouse and boats, then Sấu Chín Đuôi wakes on its island
        IEnumerator ThuyTinhMap()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            yield return RecordIntro("thuytinh_intro", 30f, 5f);
            yield return Warm(1f);
            var tower = Find("LighthouseTower") ?? Find("Lighthouse");
            if (tower != null)
            {
                Bounds b = BoundsOf(tower);
                float h = Mathf.Max(b.size.y, 8f);
                var cap = TrailerCapture.Begin("lighthouse", 3.5f, ShotCamera());
                yield return Orbit(() => new Vector3(b.center.x, b.min.y, b.center.z), h * 1.6f, h * 1.3f, h * 1.1f, h * 0.8f, 210f, 160f, h * 0.55f, 48f, 3.5f);
                yield return cap.WaitDone();
            }
            var player = ThePlayer();
            var boss = FindFirstObjectByType<SauChinDuoiBoss>(FindObjectsInactive.Include);
            var arena = Find("SauChinDuoiArena");
            if (player != null && boss != null && arena != null)
            {
                Vector3 centre = arena.transform.Find("ArenaCenter").position, respawn = arena.transform.Find("PlayerRespawn").position;
                // the fight is locked behind the elder's quiz: unlock it for this Play session only
                var reward = typeof(SauChinDuoiBoss).GetField("reward", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boss) as SonTinhThuyTinh.Quest.GiftItem;
                SonTinhThuyTinh.Quest.QuestQuizProgress.Unlock(reward);
                SauChinDuoiBoss.ResetIntro();
                player.Health.Heal(1000f);
                Teleport(player, Vector3.Lerp(respawn, centre, 0.75f), centre);
                float until = Time.realtimeSinceStartup + 10f;
                while (!boss.IsAwake && Time.realtimeSinceStartup < until) yield return null;
                // its intro cutscene (the croc rising from the lake), through the cutscene's own camera
                var intro = TrailerCapture.Begin("croc_intro", 9f);
                StartCoroutine(KeepAlive(intro, player));
                yield return intro.WaitDone();
                yield return Warm(3f);
                var cap = TrailerCapture.Begin("croc_fight", 7f, ShotCamera());
                player.InputReader.AiActivate();
                StartCoroutine(Brawl(player, cap));
                yield return Orbit(() => boss.transform.position, 11f, 9f, 3.5f, 2.5f, 250f, 190f, 1.0f, 46f, 7f);
                yield return cap.WaitDone();
                player.InputReader.AiDeactivate();
            }
            else Debug.LogWarning("[Trailer] croc fight skipped (player/boss/arena missing)");
        }

        // Map_HungVuong, on the paved square with the palace behind: the player and the rival (spawned like SandboxBossSpawner does,
        // without its AI), facing each other across x
        IEnumerator Duel(System.Action<PlayerController, PlayerController> found)
        {
            yield return Warm(2.5f);
            var spawner = FindFirstObjectByType<PlayerSpawner>();
            var player = spawner.Player;
            CharacterId rivalId = spawner.Character == CharacterId.SonTinh ? CharacterId.ThuyTinh : CharacterId.SonTinh;
            var rival = Instantiate(spawner.Roster.Get(rivalId).PlayerPrefab, Vector3.zero, Quaternion.identity);
            rival.name = "Boss_" + rivalId;
            var ai = rival.GetComponent<BossAI>(); if (ai != null) ai.enabled = false;
            rival.InputReader.AiActivate();
            Vector3 centre = new(0f, 0f, 8f);   // on the avenue, the palace gate behind the rival (but well away from its door)
            centre.y = GroundY(centre + Vector3.up * 30f);
            Teleport(player, centre - Vector3.forward * 5f, centre);
            Teleport(rival, centre + Vector3.forward * 5f, centre);
            yield return Warm(1f);
            found(player, rival);
        }

        IEnumerator KeepAlive(TrailerCapture cap, params PlayerController[] who)
        {
            while (!cap.Done) { foreach (var p in who) if (p != null) p.Health.Heal(1000f); yield return null; }
        }

        // one short clip per spell (Z / X / C) and one of the light combo, rival standing in as the target
        IEnumerator Skills(string who, int[] spells)
        {
            PlayerController player = null, rival = null;
            yield return Duel((p, r) => { player = p; rival = r; });
            if (player == null || rival == null) { Debug.LogWarning("[Trailer] skills: no player/rival"); yield break; }
            Vector3 mid = (player.transform.position + rival.transform.position) * 0.5f;
            Vector3 dir = (rival.transform.position - player.transform.position); dir.y = 0f; dir.Normalize();
            Teleport(player, mid - dir * 4.5f, mid); Teleport(rival, mid + dir * 4.5f, mid);
            yield return Warm(1f);
            player.AcquireCombatTarget(20f);
            var input = player.InputReader;
            input.AiActivate();
            float side = 1f;
            foreach (int s in spells.Concat(new[] { -1 }))
            {
                Teleport(player, mid - dir * 4.5f, mid); Teleport(rival, mid + dir * 4.5f, mid);
                yield return Warm(0.6f);
                string clip = $"skill_{who}_{(s < 0 ? "combo" : s.ToString())}";
                var cap = TrailerCapture.Begin(clip, 2.6f, ShotCamera());
                StartCoroutine(KeepAlive(cap, player, rival));
                Vector3 p0 = player.transform.position;
                Vector3 right = Vector3.Cross(Vector3.up, dir) * side;
                if (s >= 0) player.TryCast(s);
                else StartCoroutine(Combo(input, cap));
                yield return Move(p0 - dir * 3.5f + right * 4.5f + Vector3.up * 1.6f, p0 - dir * 2f + right * 3.2f + Vector3.up * 1.3f,
                                  p0 + dir * 3f + Vector3.up * 1.2f, p0 + dir * 4.5f + Vector3.up * 1.0f, 50f, 44f, 2.6f);
                yield return cap.WaitDone();
                side = 1f;
            }
            input.AiDeactivate();
        }

        IEnumerator Combo(PlayerInputReader input, TrailerCapture cap)
        {
            while (!cap.Done) { input.AiPressLight(); yield return new WaitForSeconds(0.28f); }
        }

        // Sơn Tinh vs Thủy Tinh: the wave comes, the rock wall rises, slow motion on the impact
        IEnumerator Clash()
        {
            PlayerController son = null, thuy = null;
            yield return Duel((p, r) => { son = p; thuy = r; });
            if (son == null || thuy == null) { Debug.LogWarning("[Trailer] clash: no fighters"); yield break; }
            Vector3 mid = (son.transform.position + thuy.transform.position) * 0.5f;
            Vector3 dir = (thuy.transform.position - son.transform.position); dir.y = 0f; dir.Normalize();
            Teleport(son, mid - dir * 5f, mid); Teleport(thuy, mid + dir * 5f, mid);
            yield return Warm(1f);
            son.AcquireCombatTarget(20f); thuy.AcquireCombatTarget(20f);
            son.InputReader.AiActivate(); thuy.InputReader.AiActivate();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            var cap = TrailerCapture.Begin("clash", 9f, ShotCamera());
            StartCoroutine(KeepAlive(cap, son, thuy));
            StartCoroutine(Move(mid - dir * 10f + right * 4f + Vector3.up * 1.9f, mid - dir * 7.5f + right * 2.6f + Vector3.up * 1.2f, mid + Vector3.up * 1.4f, mid + Vector3.up * 1.6f, 50f, 42f, 9f));
            while (cap.Seconds < 0.6f) yield return null;
            son.FaceCombatTarget(1f, true);
            Debug.Log($"[Trailer] clash wall cast {son.TryCast(1)}");   // the rock wall
            while (cap.Seconds < 1.0f) yield return null;
            thuy.FaceCombatTarget(1f, true);
            Debug.Log($"[Trailer] clash wave cast {thuy.TryCast(0)} (rival stamina {thuy.GetComponent<Stamina>()?.Current}, state {thuy.CurrentStateName})");   // the wave
            while (cap.Seconds < 1.7f) yield return null;
            Time.timeScale = 0.3f;                                     // the impact, slowed
            while (cap.Seconds < 5.6f) yield return null;
            Time.timeScale = 1f;
            son.FaceCombatTarget(1f, true); son.TryCast(2);            // the spikes answer
            while (cap.Seconds < 7f) yield return null;
            thuy.TryCast(2);
            yield return cap.WaitDone();
            son.InputReader.AiDeactivate(); thuy.InputReader.AiDeactivate();
        }

        // ================================================================ v2 (director's pass): every attack aimed at its target,
        // every subject head to feet in frame, the camera always moving, SFX cues logged for the edit

        static readonly FieldInfo CamField = typeof(PlayerController).GetField("cameraTransform", BindingFlags.NonPublic | BindingFlags.Instance);
        public static string CueFile => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Trailer_Output", "cues.txt"));

        // a sound for the edit: clip name, time in the clip, Audio/Sfx file
        static void Cue(string sfx)
        {
            var cap = TrailerCapture.Current;
            if (cap == null) return;
            System.IO.File.AppendAllText(CueFile, $"{cap.Clip}\t{cap.Seconds:F3}\t{sfx}\n");
        }

        // A character's attacks go where its camera looks when there is no stick input (PlayerController.FaceAttackInput): for the
        // length of a clip its camera is a helper that always looks from it to the target, so every hit and spell goes at the target.
        // Only this Play session's instance is touched (no asset or prefab changes).
        IEnumerator AimAt(PlayerController p, System.Func<Vector3> target, TrailerCapture cap)
        {
            var helper = new GameObject("TrailerAim").transform;
            object old = CamField.GetValue(p);
            CamField.SetValue(p, helper);
            while (cap != null && !cap.Done && p != null && helper != null)
            {
                Vector3 d = target() - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) helper.rotation = Quaternion.LookRotation(d);
                yield return null;
            }
            if (p != null) CamField.SetValue(p, old);
            if (helper != null) Destroy(helper.gameObject);
        }

        static void Face(PlayerController p, Vector3 target)
        {
            Vector3 d = target - p.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) p.FaceTowards(d.normalized, 360f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        IEnumerator CueAt(TrailerCapture cap, float t, string sfx)
        {
            while (!cap.Done && cap.Seconds < t) yield return null;
            if (!cap.Done) Cue(sfx);
        }

        IEnumerator KeepHealth(TrailerCapture cap, Health h)
        {
            while (!cap.Done && h != null) { if (h.Current < h.Max * 0.5f) h.Heal(h.Max); yield return null; }
        }

        static void UnlockAllGifts()
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<SonTinhThuyTinh.Quest.GiftItem>()) SonTinhThuyTinh.Quest.QuestQuizProgress.Unlock(g);
        }

        // a slow orbit around a subject, sized from its bounds so it is whole in frame
        IEnumerator OrbitSubject(string clip, GameObject subject, float seconds, float deg0, float deg1, string sfx = null, float sfxAt = 0.3f, float tightness = 1f)
        {
            if (subject == null) { Debug.LogWarning("[Trailer] missing subject for " + clip); yield break; }
            Bounds b = BoundsOf(subject);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float r = Mathf.Max(size * 1.9f * tightness, 3.2f);
            var cap = TrailerCapture.Begin(clip, seconds, ShotCamera());
            if (sfx != null) StartCoroutine(CueAt(cap, sfxAt, sfx));
            Vector3 foot = new(b.center.x, b.min.y, b.center.z);
            yield return Orbit(() => foot, r * 1.1f, r * 0.9f, b.size.y * 0.55f, b.size.y * 0.4f, deg0, deg1, b.size.y * 0.5f, 42f, seconds);
            yield return cap.WaitDone();
        }

        // the player stands near the target, faces it and casts (or swings), the camera low on the side, both whole in frame
        IEnumerator AttackShot(string clip, PlayerController p, GameObject target, float distance, int spell, float seconds, string sfx, float side = 1f)
        {
            if (p == null || target == null) { Debug.LogWarning("[Trailer] attack shot skipped " + clip); yield break; }
            Bounds tb = BoundsOf(target);
            Vector3 tpos = new(tb.center.x, tb.min.y, tb.center.z);
            Vector3 dir = Flat(tpos - p.transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            Vector3 stand = tpos - dir * (distance + Mathf.Max(tb.extents.x, tb.extents.z) * 0.6f);
            float g = GroundY(stand);
            if (g > float.MinValue) stand.y = g;
            Teleport(p, stand, tpos);
            yield return Warm(0.4f);
            Face(p, tpos);
            var cap = TrailerCapture.Begin(clip, seconds, ShotCamera());
            StartCoroutine(AimAt(p, () => tb.center, cap));
            StartCoroutine(KeepAlive(cap, p));
            var hp = target.GetComponentInParent<Health>();
            if (hp != null) StartCoroutine(KeepHealth(cap, hp));
            p.BeginCombatAim(target.transform);
            Vector3 mid = Vector3.Lerp(p.transform.position, tpos, 0.5f);
            float span = Vector3.Distance(Flat(p.transform.position), Flat(tpos)) + tb.size.magnitude * 0.5f;
            Vector3 right = Vector3.Cross(Vector3.up, dir) * side;
            float back = Mathf.Max(span * 0.9f, 5f);
            StartCoroutine(Move(mid + right * back - dir * back * 0.35f + Vector3.up * 1.6f, mid + right * back * 0.8f - dir * back * 0.15f + Vector3.up * 1.1f,
                                mid + Vector3.up * 1.0f, mid + Vector3.up * 1.2f, 46f, 40f, seconds));
            while (cap.Seconds < 0.25f) yield return null;
            if (spell >= 0) { Face(p, tpos); if (p.TryCast(spell)) Cue(sfx); }
            else
            {
                float next = 0f;
                while (!cap.Done) { if (cap.Seconds >= next) { Face(p, tpos); p.InputReader.AiPressLight(); Cue(sfx); next += 0.45f; } yield return null; }
            }
            yield return cap.WaitDone();
            p.EndCombatAim();
        }

        // Map_HungVuong at sunset: low on the avenue among the banners, craning up to the palace (the hook); then a drone over the plateau
        IEnumerator V2Palace()
        {
            yield return Warm(2f);
            var palace = Find("PalaceFront");
            Bounds pb = palace != null ? BoundsOf(palace) : new Bounds(new Vector3(0f, 20f, 40f), Vector3.one * 20f);
            float gy = GroundY(new Vector3(0f, 50f, 0f));
            Vector3 gate = new(pb.center.x, pb.min.y, pb.min.z);
            var cap = TrailerCapture.Begin("v2_crane", 5f, ShotCamera());
            yield return Move(new Vector3(2.2f, gy + 0.5f, -26f), new Vector3(0.6f, gy + 16f, -40f), new Vector3(1.5f, gy + 2.5f, -16f), gate + Vector3.up * 9f, 38f, 46f, 5f);
            yield return cap.WaitDone();
            cap = TrailerCapture.Begin("v2_drone", 4f, ShotCamera());
            yield return Orbit(() => new Vector3(0f, gy, 12f), 75f, 62f, 38f, 30f, 215f, 160f, 4f, 50f, 4f);
            yield return cap.WaitDone();
        }

        // Map_SonTinh: Sơn Tinh's hero shot, the horse, the rooster, the Ninja fight (aimed at the Ninja), the elephant and his spells on it
        IEnumerator V2Son()
        {
            yield return Warm(3f);
            UnlockAllGifts();
            var p = ThePlayer();
            yield return OrbitSubject("v2_son_hero", p.gameObject, 3f, 200f, 150f, null, 0f, 1.25f);
            yield return OrbitSubject("v2_horse", Find("NguaChinHongMao"), 2.6f, 60f, 10f, "HorseNeigh_0", 0.4f);
            var rooster = Find("RoosterQuest");
            var centre = rooster.transform.Find("Marks/RingCentre"); var mark = rooster.transform.Find("Marks/SonTinhMark");
            yield return OrbitSubject("v2_rooster", Find("GaChinCua"), 2.4f, 140f, 100f, "RoosterCluck_0", 0.3f, 1.6f);
            // the Ninja: their cutscene first (pressed through), then the fight, aimed at the Ninja (never the rooster)
            Teleport(p, centre.position - (centre.position - mark.position).normalized * 17f, centre.position);
            yield return Warm(5f);
            for (int k = 0; k < 2; k++)
            {
                var ninja = FindObjectsByType<NinjaEnemy>(FindObjectsSortMode.None).Where(n => n.isActiveAndEnabled && !n.Health.IsDead && n.name.StartsWith("Ninja"))
                            .OrderBy(n => Vector3.Distance(n.transform.position, centre.position)).Skip(k).FirstOrDefault();
                if (ninja == null) break;
                p.InputReader.AiActivate();
                yield return AttackShot("v2_ninja_" + k, p, ninja.gameObject, 1.2f, k == 0 ? -1 : 2, 2.6f, k == 0 ? "SwingLight_0" : "EarthSpell2_0", k == 0 ? 1f : -1f);
                p.InputReader.AiDeactivate();
            }
            var elephant = Find("VoiChinNga_Boss");
            yield return OrbitSubject("v2_elephant", elephant, 2.8f, 230f, 180f, "ElephantRoar_0", 0.4f);
            p.InputReader.AiActivate();
            yield return AttackShot("v2_son_golem_elephant", p, elephant, 9f, 0, 2.8f, "EarthSpell0_0");
            yield return Warm(1.5f);
            yield return AttackShot("v2_son_spikes_elephant", p, elephant, 7f, 2, 2.4f, "EarthSpell2_0", -1f);
            p.InputReader.AiDeactivate();
        }

        // Map_ThuyTinh: Thủy Tinh's hero shot, the bay, then the three sea beasts with his spells aimed at them
        IEnumerator V2Thuy()
        {
            yield return Warm(3f);
            UnlockAllGifts();
            var p = ThePlayer();
            yield return OrbitSubject("v2_thuy_hero", p.gameObject, 3f, 160f, 210f, null, 0f, 1.25f);
            var boats = Find("Boats");
            if (boats != null)
            {
                Bounds b = BoundsOf(boats);
                var cap = TrailerCapture.Begin("v2_bay", 3.2f, ShotCamera());
                yield return Move(b.center + new Vector3(-30f, 25f, -40f), b.center + new Vector3(-10f, 14f, -22f), b.center, b.center + Vector3.up * 2f, 50f, 46f, 3.2f);
                yield return cap.WaitDone();
            }
            var shark = Find("Map9Vay_Boss");
            yield return OrbitSubject("v2_shark", shark, 2.6f, 200f, 150f, "Splash_0", 0.3f);
            p.InputReader.AiActivate();
            yield return AttackShot("v2_thuy_wave_shark", p, shark, 8f, 0, 2.6f, "WaterSpell0_0");
            p.InputReader.AiDeactivate();
            yield return OrbitSubject("v2_turtle", Find("Rua9Chan_Boss"), 2.6f, 120f, 70f, "Splash_1", 0.3f);
            // the crocodile: woken on its island (its intro left to run out), then hit with the orb and the combo
            var boss = FindFirstObjectByType<SauChinDuoiBoss>(FindObjectsInactive.Include);
            var arena = Find("SauChinDuoiArena");
            if (boss != null && arena != null)
            {
                SauChinDuoiBoss.ResetIntro();
                Vector3 c = arena.transform.Find("ArenaCenter").position, respawn = arena.transform.Find("PlayerRespawn").position;
                Teleport(p, Vector3.Lerp(respawn, c, 0.75f), c);
                float until = Time.realtimeSinceStartup + 10f;
                while (!boss.IsAwake && Time.realtimeSinceStartup < until) yield return null;
                yield return Warm(9f);
                p.InputReader.AiActivate();
                yield return OrbitSubject("v2_croc", boss.gameObject, 2.6f, 250f, 200f, "CrocodileRoar_0", 0.3f);
                yield return AttackShot("v2_thuy_orb_croc", p, boss.gameObject, 6f, 2, 2.4f, "WaterSpell2_0");
                yield return AttackShot("v2_thuy_combo_croc", p, boss.gameObject, 1.6f, -1, 2.4f, "SwingHeavy_0", -1f);
                p.InputReader.AiDeactivate();
            }
        }

        // on the avenue: every spell and the combo against the rival, aimed, from the side, both whole in frame
        IEnumerator V2Duel(string who)
        {
            PlayerController player = null, rival = null;
            yield return Duel((pl, r) => { player = pl; rival = r; });
            if (player == null || rival == null) yield break;
            player.InputReader.AiActivate();
            string[] sfx = who == "son" ? new[] { "EarthSpell0_0", "EarthSpell1_0", "EarthSpell2_0" } : new[] { "WaterSpell0_0", "WaterSpell1_0", "WaterSpell2_0" };
            Vector3 home = player.transform.position;
            for (int s = 0; s < 3; s++)
            {
                Teleport(player, home, rival.transform.position);
                yield return AttackShot($"v2_{who}_skill{s}", player, rival.gameObject, s == 1 ? 4f : 7f, s, 2.4f, sfx[s], s % 2 == 0 ? 1f : -1f);
                yield return Warm(0.6f);
            }
            yield return AttackShot($"v2_{who}_combo", player, rival.gameObject, 1.3f, -1, 2.4f, "SwingLight_0", -1f);
            player.InputReader.AiDeactivate();
        }

        // the clash again, both fighters aimed at each other, the camera low behind Sơn Tinh
        IEnumerator V2Clash()
        {
            PlayerController son = null, thuy = null;
            yield return Duel((pl, r) => { son = pl; thuy = r; });
            if (son == null || thuy == null) yield break;
            Vector3 mid = (son.transform.position + thuy.transform.position) * 0.5f;
            Vector3 dir = Flat(thuy.transform.position - son.transform.position).normalized;
            Teleport(son, mid - dir * 5f, mid); Teleport(thuy, mid + dir * 5f, mid);
            yield return Warm(1f);
            son.InputReader.AiActivate(); thuy.InputReader.AiActivate();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            var cap = TrailerCapture.Begin("v2_clash", 8.5f, ShotCamera());
            StartCoroutine(AimAt(son, () => thuy.transform.position, cap));
            StartCoroutine(AimAt(thuy, () => son.transform.position, cap));
            son.BeginCombatAim(thuy.transform); thuy.BeginCombatAim(son.transform);
            StartCoroutine(KeepAlive(cap, son, thuy));
            StartCoroutine(Move(mid - dir * 10f + right * 5.8f + Vector3.up * 2.0f, mid - dir * 7.5f + right * 4.4f + Vector3.up * 1.3f, mid + Vector3.up * 1.2f, mid + Vector3.up * 1.4f, 46f, 40f, 8.5f));   // to the right of the bronze drum
            while (cap.Seconds < 0.6f) yield return null;
            Face(son, thuy.transform.position); if (son.TryCast(1)) Cue("EarthSpell1_0");
            while (cap.Seconds < 1.0f) yield return null;
            Face(thuy, son.transform.position); if (thuy.TryCast(0)) Cue("WaterSpell0_0");
            while (cap.Seconds < 1.7f) yield return null;
            Time.timeScale = 0.3f;
            Cue("Thunder_0");
            while (cap.Seconds < 5.4f) yield return null;
            Time.timeScale = 1f;
            Face(son, thuy.transform.position); if (son.TryCast(2)) Cue("EarthSpell2_0");
            while (cap.Seconds < 6.6f) yield return null;
            Face(thuy, son.transform.position); if (thuy.TryCast(2)) Cue("WaterSpell2_0");
            yield return cap.WaitDone();
            son.InputReader.AiDeactivate(); thuy.InputReader.AiDeactivate();
        }

        // ---------------------------------------------------------------- v2 re-shoots (shots that failed the director's review)

        // the crane again, along the open avenue (the first one flew through a tree)
        IEnumerator V2FixPalace()
        {
            yield return Warm(2f);
            float gy = GroundY(new Vector3(0f, 50f, 20f));
            var cap = TrailerCapture.Begin("v2_crane2", 5f, ShotCamera());
            yield return Move(new Vector3(0.4f, gy + 0.6f, 21f), new Vector3(0f, gy + 11f, 4f), new Vector3(0f, gy + 2.2f, 34f), new Vector3(0f, gy + 8f, 42f), 40f, 48f, 5f);
            yield return cap.WaitDone();
        }

        // Sơn Tinh closer; the horse from inside the stable, above the fence; the elephant brought out of the forest into its arena
        IEnumerator V2FixSon()
        {
            yield return Warm(3f);
            UnlockAllGifts();
            var p = ThePlayer();
            yield return OrbitSubject("v2_son_hero2", p.gameObject, 3f, 200f, 150f, null, 0f, 0.75f);
            var horse = Find("NguaChinHongMao");
            if (horse != null)
            {
                Bounds hb = BoundsOf(horse);
                Vector3 foot = new(hb.center.x, hb.min.y, hb.center.z);
                var cap = TrailerCapture.Begin("v2_horse2", 2.6f, ShotCamera());
                StartCoroutine(CueAt(cap, 0.4f, "HorseNeigh_0"));
                yield return Orbit(() => foot, 3.0f, 2.6f, 2.3f, 2.0f, horse.transform.eulerAngles.y + 40f, horse.transform.eulerAngles.y - 10f, hb.size.y * 0.55f, 50f, 2.6f);
                yield return cap.WaitDone();
            }
            var elephant = Find("VoiChinNga_Boss");
            var arena = Find("ElephantArena");
            Transform spot = arena != null ? arena.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ArenaCentre") : null;
            if (spot == null) { var c = Find("ArenaCentre"); spot = c != null ? c.transform : null; }
            if (elephant != null && spot != null)
            {
                Vector3 to = spot.position; float g = GroundY(to + Vector3.up * 20f); if (g > float.MinValue) to.y = g;
                elephant.transform.position = to;
                Physics.SyncTransforms();
                Teleport(p, to + new Vector3(0f, 0f, -14f), to);
                yield return Warm(1f);
                elephant.transform.position = to;
                yield return OrbitSubject("v2_elephant2", elephant, 2.8f, 200f, 160f, "ElephantRoar_0", 0.4f, 0.8f);
                p.InputReader.AiActivate();
                yield return AttackShot("v2_son_golem_elephant2", p, elephant, 9f, 0, 2.8f, "EarthSpell0_0");
                yield return Warm(1.5f);
                yield return AttackShot("v2_son_spikes_elephant2", p, elephant, 7f, 2, 2.4f, "EarthSpell2_0", -1f);
                p.InputReader.AiDeactivate();
            }
            else Debug.LogWarning("[Trailer] elephant re-shoot skipped");
        }

        // ---------------------------------------------------------------- v3: the six gift beasts, no fights, every one unobstructed

        // an orbit from above (the camera looks down over fences, rocks and water), sized from the subject's bounds
        IEnumerator OrbitHigh(string clip, GameObject subject, float seconds, float deg0, float deg1, float heightMul, float radiusMul, string sfx)
        {
            if (subject == null) { Debug.LogWarning("[Trailer] missing subject for " + clip); yield break; }
            Bounds b = BoundsOf(subject);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z, 1.5f);
            float r = size * radiusMul;
            Vector3 foot = new(b.center.x, b.min.y, b.center.z);
            var cap = TrailerCapture.Begin(clip, seconds, ShotCamera());
            if (sfx != null) StartCoroutine(CueAt(cap, 0.3f, sfx));
            yield return Orbit(() => foot, r * 1.08f, r * 0.92f, size * heightMul, size * heightMul * 0.85f, deg0, deg1, b.size.y * 0.45f, 42f, seconds);
            yield return cap.WaitDone();
        }

        // stand a beast on open ground for its portrait (Play session only): its stall / AI scripts are paused first
        static void Stage(GameObject beast, Vector3 at)
        {
            foreach (var mb in beast.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            beast.transform.position = at;
            Physics.SyncTransforms();
            // feet on the real ground: the first solid surface below that is not the beast itself, and the lowest point of its mesh on it
            float g = float.MinValue;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (!hit.collider.transform.IsChildOf(beast.transform)) { g = hit.point.y; break; }
            if (g > float.MinValue)
            {
                float low = BoundsOf(beast).min.y;
                beast.transform.position += Vector3.up * (g - low);
                Physics.SyncTransforms();
            }
        }

        // ---------------------------------------------------------------- v4: map tours on the intro's camera marks, the clash from the side

        // the map's own intro fly-through marks (MapIntroCutscene, built on the landmarks), flown with the trailer camera, one clip per shot
        IEnumerator MapTour(string prefix)
        {
            yield return Warm(3f);
            var intros = FindObjectsByType<MapIntroCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var shotsField = typeof(MapIntroCutscene).GetField("shots", BindingFlags.NonPublic | BindingFlags.Instance);
            var triggerField = typeof(MapIntroCutscene).GetField("giftCountTrigger", BindingFlags.NonPublic | BindingFlags.Instance);
            int n = 0;
            foreach (var intro in intros.OrderBy(i => (int)triggerField.GetValue(i)))
            {
                foreach (MapIntroCutscene.Shot s in (MapIntroCutscene.Shot[])shotsField.GetValue(intro))
                {
                    if (s.from == null || s.look == null) continue;
                    Transform to = s.to != null ? s.to : s.from;
                    float secs = Mathf.Clamp(s.seconds, 3f, 4f);
                    var cap = TrailerCapture.Begin($"{prefix}_{n++}", secs, ShotCamera());
                    yield return Move(s.from.position, to.position, s.look.position, s.look.position, s.fovFrom > 1f ? s.fovFrom : 45f, s.fovTo > 1f ? s.fovTo : 45f, secs);
                    yield return cap.WaitDone();
                }
            }
            Debug.Log($"[Trailer] {prefix}: {n} tour shots");
        }

        // v5: the hero shot from the FRONT: a slow arc in front of the player's face (yaw ± 28°), low, head to feet in frame
        IEnumerator V5Hero(string clip)
        {
            yield return Warm(3f);
            var p = ThePlayer();
            // the pretty background is in front of where the player spawns looking: turn the player round (Play session only) so the
            // camera, standing where it would show the player's back, now sees the face with that background behind
            p.transform.rotation = Quaternion.Euler(0f, p.transform.eulerAngles.y + 180f, 0f);
            yield return null;
            // no floating name tags (the quest keeper's "NGƯỜI GIỮ NHIỆM VỤ") in the picture
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (cv.renderMode == RenderMode.WorldSpace) cv.enabled = false;
            foreach (var tm in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None)) tm.enabled = false;
            float yaw = p.transform.eulerAngles.y;
            Bounds b = BoundsOf(p.gameObject);
            Vector3 foot = new(b.center.x, b.min.y, b.center.z);
            var cap = TrailerCapture.Begin(clip, 3.2f, ShotCamera());
            yield return Orbit(() => foot, 3.9f, 3.4f, 0.9f, 1.1f, yaw + 28f, yaw - 18f, 1.0f, 42f, 3.2f);
            yield return cap.WaitDone();
        }

        // Sơn Tinh's rock wall against Thủy Tinh's wave, in profile: two takes of the same action (side, and higher side) for the cut
        IEnumerator V4ClashSide(string clip, bool high)
        {
            PlayerController son = null, thuy = null;
            yield return Duel((pl, r) => { son = pl; thuy = r; });
            if (son == null || thuy == null) yield break;
            Vector3 mid = new(0f, 0f, 19f);
            mid.y = GroundY(mid + Vector3.up * 30f);
            Teleport(son, mid - Vector3.forward * 3.6f, mid); Teleport(thuy, mid + Vector3.forward * 3.6f, mid);
            yield return Warm(1f);
            son.InputReader.AiActivate(); thuy.InputReader.AiActivate();
            var cap = TrailerCapture.Begin(clip, 8f, ShotCamera());
            StartCoroutine(AimAt(son, () => thuy.transform.position, cap));
            StartCoroutine(AimAt(thuy, () => son.transform.position, cap));
            son.BeginCombatAim(thuy.transform); thuy.BeginCombatAim(son.transform);
            StartCoroutine(KeepAlive(cap, son, thuy));
            // camera on the right of the line between them, looking across it: Sơn Tinh left of frame, Thủy Tinh right
            if (high) StartCoroutine(Move(mid + new Vector3(-7.5f, 4.2f, 0.6f), mid + new Vector3(-6.8f, 3.4f, 0f), mid + Vector3.up * 0.9f, mid + Vector3.up * 1.0f, 50f, 46f, 8f));
            else StartCoroutine(Move(mid + new Vector3(-7.8f, 1.3f, -0.4f), mid + new Vector3(-6.6f, 1.2f, 0.2f), mid + Vector3.up * 1.1f, mid + Vector3.up * 1.1f, 56f, 50f, 8f));
            while (cap.Seconds < 0.6f) yield return null;
            Face(son, thuy.transform.position); if (son.TryCast(1)) Cue("EarthSpell1_0");
            while (cap.Seconds < 1.0f) yield return null;
            Face(thuy, son.transform.position); if (thuy.TryCast(0)) Cue("WaterSpell0_0");
            while (cap.Seconds < 1.7f) yield return null;
            Time.timeScale = 0.3f;
            Cue("Thunder_0");
            while (cap.Seconds < 5.6f) yield return null;
            Time.timeScale = 1f;
            yield return cap.WaitDone();
            son.InputReader.AiDeactivate(); thuy.InputReader.AiDeactivate();
        }

        IEnumerator V3BeastsSon()
        {
            yield return Warm(3f);
            var p = ThePlayer();
            // the elephant, in its open arena
            var elephant = Find("VoiChinNga_Boss");
            var spot = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "ArenaCentre");
            if (elephant != null && spot != null)
            {
                Stage(elephant, spot.position);
                Teleport(p, spot.position + new Vector3(0f, 0f, -40f), spot.position);   // the player out of the picture
                yield return Warm(1f);
                yield return OrbitHigh("v3_elephant", elephant, 2.2f, 200f, 160f, 0.55f, 1.15f, "ElephantRoar_0");
            }
            // the rooster in the boulder ring
            yield return OrbitHigh("v3_rooster", Find("GaChinCua"), 2.2f, 140f, 100f, 0.9f, 2.6f, "RoosterCluck_0");
            // the horse, led out of its stall onto the open path in front of the stable
            var horse = Find("NguaChinHongMao"); var stable = Find("Stable");
            if (horse != null && stable != null)
            {
                Vector3 s = stable.transform.position;
                Vector3 toPath = Flat(p.transform.position - s);
                Vector3 at = s + (toPath.sqrMagnitude > 1f ? toPath.normalized : Vector3.right) * 9f;
                Stage(horse, at);
                horse.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, toPath.normalized));
                yield return Warm(0.5f);
                yield return OrbitHigh("v3_horse", horse, 2.2f, 60f, 20f, 0.45f, 1.3f, "HorseNeigh_0");
            }
        }

        IEnumerator V3BeastsThuy()
        {
            yield return Warm(3f);
            UnlockAllGifts();
            var p = ThePlayer();
            // the crocodile: woken (its intro runs out), then the player sent away so it just stands and roars on its island
            var boss = FindFirstObjectByType<SauChinDuoiBoss>(FindObjectsInactive.Include);
            var arena = Find("SauChinDuoiArena");
            if (boss != null && arena != null)
            {
                SauChinDuoiBoss.ResetIntro();
                Vector3 c = arena.transform.Find("ArenaCenter").position, respawn = arena.transform.Find("PlayerRespawn").position;
                Teleport(p, Vector3.Lerp(respawn, c, 0.75f), c);
                float until = Time.realtimeSinceStartup + 10f;
                while (!boss.IsAwake && Time.realtimeSinceStartup < until) yield return null;
                yield return Warm(9f);
                Teleport(p, respawn, c);
                yield return OrbitHigh("v3_croc", boss.gameObject, 2.2f, 240f, 200f, 0.9f, 1.0f, "CrocodileRoar_0");
            }
            yield return OrbitHigh("v3_shark", Find("Map9Vay_Boss"), 2.2f, 200f, 160f, 1.3f, 1.2f, "Splash_0");
            yield return OrbitHigh("v3_turtle", Find("Rua9Chan_Boss"), 2.2f, 120f, 80f, 1.3f, 1.2f, "Splash_1");
        }

        // re-shoot: the horse on its mountain top (HorseSummit, open sky; the path in front of the stable is under trees)
        IEnumerator V3HorseFix()
        {
            yield return Warm(3f);
            var p = ThePlayer();
            var horse = Find("NguaChinHongMao"); var summit = Find("HorseSummit");
            if (horse == null || summit == null) { Debug.LogWarning("[Trailer] horse fix skipped"); yield break; }
            // the summit has its own horse (HorseSummit, a HorseStall with the model under it): film that one where it stands
            Teleport(p, summit.transform.position + summit.transform.forward * 25f, summit.transform.position);
            yield return Warm(1.5f);
            // the quest keeps both horses hidden at this point of a new game: put a fresh copy of the horse prefab on the summit for the shot
            GameObject model = null;
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/NguaChinHongMao.prefab");
            if (prefab != null) model = Instantiate(prefab, summit.transform.position, Quaternion.Euler(0f, summit.transform.eulerAngles.y + 90f, 0f));
#endif
            if (model == null) { Debug.LogWarning("[Trailer] horse prefab missing"); yield break; }
            Stage(model, summit.transform.position);
            foreach (var an in model.GetComponentsInChildren<Animator>()) an.enabled = true;
            yield return Warm(0.5f);
            yield return OrbitHigh("v3_horse2", model, 2.2f, summit.transform.eulerAngles.y + 200f, summit.transform.eulerAngles.y + 160f, 0.7f, 1.4f, "HorseNeigh_0");   // from the open side (the other side is the cliff)
        }

        // re-shoot: the turtle, orbiting away from the rock that covered the second half
        IEnumerator V3TurtleFix()
        {
            yield return Warm(3f);
            yield return OrbitHigh("v3_turtle2", Find("Rua9Chan_Boss"), 2.2f, 165f, 125f, 1.3f, 1.2f, "Splash_1");
        }
    }
}
