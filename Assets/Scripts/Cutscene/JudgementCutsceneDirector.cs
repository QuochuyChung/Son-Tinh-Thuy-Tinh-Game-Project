using System.Collections;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using UnityEngine;
using Ease = SonTinhThuyTinh.Cutscene.CinematicCamera.Ease;

namespace SonTinhThuyTinh.Cutscene
{
    // Hùng Vương's judgement in front of the palace (scene Cutscene_PhanXu, built by Tools ▸ Son Tinh Thuy Tinh ▸ Build Judgement Cutscene):
    // the two suitors walk in down the avenue, answer the king in turn, the king cannot choose and sets a duel; the rival taunts, the chosen
    // character answers in slow motion; the king calls the start and the duel scene loads. Two dialogues, one per chosen character
    // (me = the chosen one, rival = the other; Sơn Tinh always stands on the left of the avenue, Thủy Tinh on the right).
    public class JudgementCutsceneDirector : CinematicDirector
    {
        [Header("Cast")]
        [SerializeField] ActorRig king;
        [SerializeField] ActorRig miNuong;
        [SerializeField] ActorRig sonTinh;
        [SerializeField] ActorRig thuyTinh;
        [Header("Marks")]
        [SerializeField] Transform kingMark;
        [SerializeField] Transform miNuongMark;
        [SerializeField] Transform sonTinhMark;
        [SerializeField] Transform thuyTinhMark;
        [Header("Text")]
        [SerializeField] DialogueSequence sonTinhScript;
        [SerializeField] DialogueSequence thuyTinhScript;
        [SerializeField] string nextScene = SceneNames.FinalBattle;
        [Tooltip("Used when the scene is played on its own, without character select.")]
        [SerializeField] CharacterId fallbackPlayer = CharacterId.SonTinh;

        ActorRig me, rival;
        float s;   // the chosen character's side of the avenue: -1 left (Sơn Tinh), +1 right (Thủy Tinh)
        bool meIsSon;

        static readonly Color Morning = new(1f, 0.88f, 0.70f), MorningFog = new(0.88f, 0.84f, 0.78f);
        static readonly Color Tense = new(1f, 0.62f, 0.40f), TenseFog = new(0.72f, 0.52f, 0.42f);

        static Vector3 P(float x, float y, float z) => new(x, y, z);
        Vector3 Mx(float x, float y, float z) => new(x * s, y, z);   // a point given for the chosen character on the left, mirrored if he is on the right

        protected override IEnumerator Run()
        {
            var id = GameSession.SelectedCharacter ?? fallbackPlayer;
            meIsSon = id == CharacterId.SonTinh;
            me = meIsSon ? sonTinh : thuyTinh; rival = meIsSon ? thuyTinh : sonTinh;
            s = meIsSon ? -1f : 1f;
            SetUp();
            yield return Chapter(Arrival());   // the walk in has no text: the first line starts under it
            yield return Dialogue(meIsSon ? sonTinhScript : thuyTinhScript, Beat);
            Go(fx.Fade(1f, 0.8f, Color.black));
            yield return Wait(0.9f);
            SceneLoader.Load(nextScene);
        }

        void SetUp()
        {
            king.transform.SetPositionAndRotation(kingMark.position, kingMark.rotation);
            miNuong.transform.SetPositionAndRotation(miNuongMark.position, miNuongMark.rotation);
            // the suitors start down the avenue, facing the palace
            sonTinh.transform.SetPositionAndRotation(P(sonTinhMark.position.x, sonTinhMark.position.y, 10f), sonTinhMark.rotation);
            thuyTinh.transform.SetPositionAndRotation(P(thuyTinhMark.position.x, thuyTinhMark.position.y, 10f), thuyTinhMark.rotation);
            king.Release(0.01f); miNuong.Release(0.01f);
            king.Play("CrossArms", 0.01f);
            miNuong.LookAt(king.transform, 0.5f, new Vector3(0f, 1.4f, 0f));
            StartCoroutine(stage.Mood(Morning, 1.1f, MorningFog, 0.003f, 1.05f, 0.05f));
            stage.Rain(0f);
            cam.Handheld = 0.6f;
        }

        // ---------------------------------------------------------------- the walk in (the text starts as they arrive)

        IEnumerator Arrival()
        {
            fx.FadeNow(1f);
            cam.Set(P(0f, 44f, -4f), P(0f, 15f, 30f), 52f);
            Go(fx.Letterbox(true, 1.2f));
            Go(fx.Fade(0f, 1.6f));
            Go(sonTinh.WalkTo(P(sonTinhMark.position.x, 14f, sonTinhMark.position.z), 2.3f));
            Go(thuyTinh.WalkTo(P(thuyTinhMark.position.x, 14f, thuyTinhMark.position.z), 2.3f));
            yield return cam.Move(P(0f, 44f, -4f), P(0f, 20f, 9f), P(0f, 15f, 30f), P(0f, 15.4f, 29f), 52f, 42f, 3.6f, Ease.Smooth);
            cam.Handheld = 1f;
            yield return cam.Move(P(0.4f, 15.3f, 8f), P(0f, 15.9f, 18f), P(0f, 15.4f, 30f), P(0f, 16.4f, 32f), 40f, 34f, 5f, Ease.Smooth);
            cam.Handheld = 0.6f;
        }

        // ---------------------------------------------------------------- the lines

        IEnumerator Beat(int line)
        {
            switch (line)
            {
                case 0: return Court();
                case 1: return KingSpeaks();
                case 2: return MeAnswers();
                case 3: return RivalAnswers();
                case 4: return KingPuzzled();
                case 5: return PrincessSpeaks();
                case 6: return KingDecrees();
                case 7: return KingSetsDuel();
                case 8: return RivalTaunts();
                case 9: return MeReplies();
                case 10: return PrincessWishes();
                default: return KingBegins();
            }
        }

        // "Sáng sớm hôm sau, trước sân rồng thành Phong Châu, cả triều đình nín thở…": the court holds its breath
        IEnumerator Court()
        {
            me.Play("CrossArms", 0.5f); rival.Play("CrossArms", 0.5f);
            king.Play("CrossArms", 0.4f);
            me.LookAt(king.transform, 1f, Vector3.up * 1.7f); rival.LookAt(king.transform, 1f, Vector3.up * 1.7f);
            cam.FocusOff();
            yield return cam.Move(P(0f, 16.8f, 17f), P(0f, 15.9f, 22f), P(0f, 15.8f, 31f), 38f, 33f, 9f, Ease.Smooth);
        }

        // "Hôm qua ta đã nói: ai mang đủ sính lễ đến trước, người ấy được rước Mị Nương.": over the suitors' backs to the king
        IEnumerator KingSpeaks()
        {
            king.Play("Talk", 0.4f);
            cam.Focus(Pt.On(king.transform, 1.7f), 3f);
            Go(cam.Move(P(0f, 15.2f, 20.4f), P(0f, 15.35f, 21.8f), Pt.On(king.transform, 1.75f), 38f, 30f, 9f, Ease.Smooth));
            yield return Wait(9f);
            // the king, a close three-quarter
            cam.Focus(Pt.On(king.transform, 1.75f), 2.4f);
            yield return cam.Move(P(1.2f, 15.9f, 29.4f), P(0.7f, 16.05f, 30.3f), Pt.On(king.transform, 1.8f), 30f, 26f, 6f);
        }

        // "Muôn tâu bệ hạ, thần là …": the chosen one bows and answers
        IEnumerator MeAnswers()
        {
            rival.Play("CrossArms", 0.4f);
            float len = me.Play("Bow", 0.3f);
            cam.Focus(Pt.On(me.transform, 1.6f), 2.4f);
            Go(cam.Orbit(Pt.On(me.transform, 0f), 3.4f, 2.8f, 1.6f, 1.7f, -30f * s, 20f * s, 1.45f, 34f, 29f, 8f, Ease.Smooth));
            yield return Wait(len + 0.2f);
            me.Play("Talk", 0.5f);
        }

        // "Thần là …": the rival answers in his turn
        IEnumerator RivalAnswers()
        {
            me.Play("CrossArms", 0.4f);
            float len = rival.Play("Bow", 0.3f);
            cam.Focus(Pt.On(rival.transform, 1.6f), 2.4f);
            Go(cam.Orbit(Pt.On(rival.transform, 0f), 3.4f, 2.8f, 1.6f, 1.7f, 30f * s, -20f * s, 1.45f, 34f, 29f, 8f, Ease.Smooth));
            yield return Wait(len + 0.2f);
            rival.Play("Talk", 0.5f);
        }

        // "Lạ thay... Hai người cùng đến một lúc…": the king puzzled, his eyes going from one to the other
        IEnumerator KingPuzzled()
        {
            rival.Play("CrossArms", 0.5f);
            king.Play("HeadShake", 0.3f);
            cam.FocusOff();
            Go(cam.Move(P(0f, 16.2f, 14f), P(0f, 15.8f, 19f), P(0f, 15.6f, 27f), 40f, 34f, 4.5f));
            yield return Wait(3.6f);
            king.Play("Talk", 0.5f);
            king.LookAt(sonTinh.transform, 1f, Vector3.up * 1.6f);
            cam.Focus(Pt.On(king.transform, 1.7f), 2.4f);
            Go(cam.Move(P(-1.6f, 15.8f, 29f), P(-1.1f, 15.9f, 30f), Pt.On(king.transform, 1.75f), 32f, 28f, 6.5f));
            yield return Wait(2.3f);
            king.LookAt(thuyTinh.transform, 1f, Vector3.up * 1.6f);
            yield return Wait(4f);
        }

        // "Phụ vương... con xin nghe theo lời cha định đoạt.": Mị Nương, eyes lowered
        IEnumerator PrincessSpeaks()
        {
            king.LookAt(null, 0f);
            miNuong.LookAt(null, 0f);
            miNuong.Play("Shy", 0.4f);
            cam.Focus(Pt.On(miNuong.transform, 1.5f), 2.3f);
            yield return cam.Move(P(-1.3f, 15.5f, 29.4f), P(-1.7f, 15.55f, 30.1f), Pt.On(miNuong.transform, 1.4f), 28f, 25f, 7f);
        }

        // "Lời vua đã nói ra thì không thể rút lại. Nhưng con gái ta chỉ có một.": the king, low, rising
        IEnumerator KingDecrees()
        {
            miNuong.Release();
            king.Play("Talk", 0.4f);
            cam.FocusOff();
            Go(cam.Move(P(1.5f, 14.4f, 28.8f), P(0.5f, 14.8f, 30.4f), Pt.On(king.transform, 1.7f), Pt.On(king.transform, 2.0f), 42f, 34f, 8f, Ease.Out));
            yield return Wait(3.4f);
            king.Play("Command", 0.4f);
            cam.Shake(0.45f, 0.8f);
        }

        // "Vậy hãy để trời đất làm chứng!…": the king points to the altar behind the palace; the light turns
        IEnumerator KingSetsDuel()
        {
            king.Play("Point", 0.35f);
            Go(stage.Mood(Tense, 0.95f, TenseFog, 0.0045f, 0.95f, 5f));
            cam.FocusOff();
            Go(cam.Move(P(2.8f, 14.5f, 28f), P(1.6f, 16.2f, 30.5f), Pt.On(king.transform, 1.6f), Pt.On(king.transform, 1.7f), 40f, 30f, 6f, Ease.Smooth));
            yield return Wait(6f);
            yield return cam.Move(P(0f, 16.4f, 16f), P(0f, 22f, 14f), P(0f, 16.5f, 34f), 40f, 46f, 8f, Ease.Smooth);
        }

        // "Hô mưa gọi gió là việc của ta…" / "Ngươi dâng nước cao một thước…": the rival taunts, the chosen one turns his head
        IEnumerator RivalTaunts()
        {
            king.Play("CrossArms", 0.5f);
            cam.Handheld = 1.2f;
            rival.Play("Angry", 0.3f);
            me.Play("CrossArms", 0.3f);
            me.LookAt(rival.transform, 1f, Vector3.up * 1.6f);
            cam.Focus(Pt.On(rival.transform, 1.6f), 2.2f);
            Go(cam.Orbit(Pt.On(rival.transform, 0f), 4.4f, 3f, 0.9f, 1.8f, 140f * s, 70f * s, 1.5f, 36f, 30f, 6f, Ease.Smooth));
            yield return Wait(6f);
            cam.Handheld = 0.6f;
        }

        // "Thần xin tuân lệnh…": the chosen one answers; time slows and the light of his power shows
        IEnumerator MeReplies()
        {
            rival.Play("CrossArms", 0.4f);
            me.LookAt(null, 0f);
            me.Play("RaiseSword", 0.35f);
            cam.Focus(Pt.On(me.transform, 1.5f), 2.2f);
            Go(SlowMotion(0.35f, 3.4f));
            if (meIsSon) { stage.Dust(me.transform.position, 1.3f, 50); Go(stage.RaiseRocks(me.transform.position, 3.0f, 6, 1.2f, rival.transform.position, 3.2f)); }
            else { Go(stage.SummonWater(me.transform.position, 3.4f, 3.5f)); stage.Lightning(0.5f); }
            Go(cam.Orbit(Pt.On(me.transform, 0f), 5.2f, 3.2f, 0.6f, 1.6f, 90f * s, 30f * s, 1.5f, 50f, 32f, 5.5f, Ease.Smooth));
            cam.Shake(0.35f, 0.6f);
            yield return Wait(5.6f);
            me.Play("Talk", 0.5f);
        }

        // "Xin hai chàng giữ lời, đấu cho công bằng…": Mị Nương's wish
        IEnumerator PrincessWishes()
        {
            miNuong.Play("Talk", 0.4f);
            cam.FocusOff();
            Go(stage.Mood(Tense, 0.95f, TenseFog, 0.0045f, 0.95f, 1f));
            cam.Focus(Pt.On(miNuong.transform, 1.5f), 2.3f);
            yield return cam.Move(P(-0.8f, 15.4f, 28.4f), P(-1.6f, 15.5f, 29.6f), Pt.On(miNuong.transform, 1.45f), 30f, 26f, 7f);
        }

        // "Được! Trời đất chứng giám. Bắt đầu!": the king's order, the two face off, a shake and into the duel
        IEnumerator KingBegins()
        {
            miNuong.Release();
            king.Play("Command", 0.3f);
            cam.FocusOff();
            sonTinh.LookAt(thuyTinh.transform); thuyTinh.LookAt(sonTinh.transform);
            Go(cam.Move(P(0f, 14.6f, 29.4f), P(0f, 15.3f, 31f), Pt.On(king.transform, 1.7f), Pt.On(king.transform, 2f), 44f, 30f, 3.2f, Ease.Out));
            yield return Wait(1.1f);
            cam.Shake(0.7f, 0.8f);
            fx.Flash(new Color(1f, 0.85f, 0.6f), 0.5f, 0.6f);
            yield return Wait(1.7f);
            yield return cam.Move(P(0f, 16f, 20f), P(0f, 15.7f, 23f), P(0f, 15.5f, 26f), 46f, 36f, 3.5f, Ease.In);
        }
    }
}
