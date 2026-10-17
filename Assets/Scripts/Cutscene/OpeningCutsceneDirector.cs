using System.Collections;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using UnityEngine;
using Ease = SonTinhThuyTinh.Cutscene.CinematicCamera.Ease;

namespace SonTinhThuyTinh.Cutscene
{
    // The opening of the game, "Kén rể" (scene Cutscene_Opening, built by Tools ▸ Son Tinh Thuy Tinh ▸ Build Opening Cutscene).
    // In front of Hùng Vương's palace at dawn: a crane flight and the title; Mị Nương and the king on his throne; the king's call to the
    // four corners; Sơn Tinh rises out of the shaking ground in a ring of rock, Thủy Tinh steps out of a gathering pool in a storm; they
    // face each other in slow motion; the king names the bride-price (voi chín ngà, gà chín cựa, ngựa chín hồng mao); the two set out in
    // opposite directions and the camera rises into the sky: "Còn người, sẽ bước vào vai ai?" -> character select.
    // The text is Dialogue_Prologue (11 lines); Beat(i) is what the camera and the actors do on line i.
    public class OpeningCutsceneDirector : CinematicDirector
    {
        [Header("Cast")]
        [SerializeField] ActorRig king;
        [SerializeField] ActorRig miNuong;
        [SerializeField] ActorRig sonTinh;
        [SerializeField] ActorRig thuyTinh;
        [Header("Marks")]
        [SerializeField] Transform kingSeat;      // on the dais, facing the avenue
        [SerializeField] Transform kingStand;     // where he stands when he rises: on the steps in front of the seat
        [SerializeField] Transform miNuongMark;
        [SerializeField] Transform sonTinhMark;
        [SerializeField] Transform thuyTinhMark;
        [SerializeField] Transform sonTinhExit;
        [SerializeField] Transform thuyTinhExit;
        [Header("Text")]
        [SerializeField] DialogueSequence prologue;
        [SerializeField] string nextScene = SceneNames.CharacterSelect;

        static readonly Color Dawn = new(1f, 0.80f, 0.58f), DawnFog = new(0.93f, 0.78f, 0.64f);
        static readonly Color Storm = new(0.55f, 0.66f, 0.80f), StormFog = new(0.30f, 0.38f, 0.46f);
        static readonly Color DawnTint = new(1f, 0.95f, 0.9f), StormTint = new(0.72f, 0.84f, 1f);

        Vector3 SonPos => sonTinhMark.position;
        Vector3 ThuyPos => thuyTinhMark.position;
        Vector3 Seat => kingSeat.position;
        Vector3 Mark(Transform t, float x, float y, float z) => t.TransformPoint(new Vector3(x, y, z));

        protected override IEnumerator Run()
        {
            GameSession.StartNewGame();
            SetUp();
            yield return Chapter(Title());
            yield return Dialogue(prologue, Beat);
            Go(fx.Fade(1f, 1.3f));
            yield return Wait(1.4f);
            SceneLoader.Load(nextScene);
        }

        // everyone on their marks: the king seated, Mị Nương beside the dais, the two gods not yet there
        void SetUp()
        {
            king.transform.SetPositionAndRotation(Seat, kingSeat.rotation);
            miNuong.transform.SetPositionAndRotation(miNuongMark.position, miNuongMark.rotation);
            sonTinh.transform.SetPositionAndRotation(SonPos, sonTinhMark.rotation);
            thuyTinh.transform.SetPositionAndRotation(ThuyPos, thuyTinhMark.rotation);
            sonTinh.gameObject.SetActive(false);
            thuyTinh.gameObject.SetActive(false);
            king.Play("SitIdle", 0.01f);
            miNuong.Release(0.01f);
            miNuong.LookAt(king.transform, 0.6f, new Vector3(0f, 1.2f, 0f));
            king.LookAt(miNuong.transform, 0.5f, new Vector3(0f, 1.4f, 0f));
            StartCoroutine(stage.Mood(Dawn, 1.55f, DawnFog, 0.0032f, 1.1f, 0.05f, 0.3f, 13f, DawnTint));
            stage.Rain(0f);
            cam.Handheld = 0.5f;
        }

        // ---------------------------------------------------------------- the title: a crane over the avenue to the palace

        IEnumerator Title()
        {
            cam.Set(new Vector3(0f, 62f, -60f), new Vector3(0f, 15f, 34f), 55f);
            Go(fx.Letterbox(true, 1.4f));
            Go(fx.Fade(0f, 2.2f));
            Go(Then(1.6f, fx.NameCard("SƠN TINH  –  THỦY TINH", "Truyền thuyết non cao biển cả", 3.6f, 0.9f)));
            yield return cam.Move(new Vector3(0f, 62f, -60f), new Vector3(0f, 22f, 4f), new Vector3(0f, 15f, 34f), new Vector3(0f, 15.6f, 35f), 55f, 38f, 9f, Ease.Smooth);
        }

        // ---------------------------------------------------------------- the lines

        // A beat is cut short when the player presses on, and the gods' appearances (a hidden actor shown again after a wait, the rocks that
        // sink) never finish: whatever a skipped beat left undone is put right when the next line starts.
        void EnsureCast(int line)
        {
            if (line > 3 && kingStand != null) king.transform.position = kingStand.position;   // (a skipped KingCalls leaves him where he stands up)
            if (line > 5) sonTinh.gameObject.SetActive(true);
            if (line > 6) { thuyTinh.gameObject.SetActive(true); stage.ClearRocksNow(); }
        }

        IEnumerator Beat(int line)
        {
            EnsureCast(line);
            switch (line)
            {
                case 0: return Peace();
                case 1: return PrincessPortrait();
                case 2: return KingWorries();
                case 3: return KingCalls();
                case 4: return NewsSpreads();
                case 5: return SonTinhRises();
                case 6: return ThuyTinhArrives();
                case 7: return Standoff();
                case 8: return KingJudges();
                case 9: return BridePrice();
                case 10: return SetOut();
                default: return Skyward();
            }
        }

        // "Đời Hùng Vương thứ mười tám, nước Văn Lang thái bình…": the court at peace, a low glide up the avenue
        IEnumerator Peace()
        {
            king.Play("SitIdle", 0.3f);
            cam.FocusOff();
            yield return cam.Move(new Vector3(-7f, 15.6f, 8f), new Vector3(-2.5f, 16.4f, 22f), new Vector3(0f, 17f, 36f), new Vector3(0f, 15.8f, 33f), 46f, 40f, 7.5f, Ease.Smooth);
            yield return cam.Move(new Vector3(3.2f, 15.7f, 24f), new Vector3(1.6f, 15.8f, 27f), Pt.On(king.transform, 1.5f), 36f, 31f, 6f, Ease.Smooth);
        }

        // "Vua có một người con gái tên là Mị Nương…": Mị Nương, the camera circling her slowly
        IEnumerator PrincessPortrait()
        {
            miNuong.Release();
            cam.Focus(Pt.On(miNuong.transform, 1.5f), 2.4f);
            Vector3 c = miNuong.transform.position;
            yield return cam.Orbit(Pt.On(miNuong.transform, 0f), 3.4f, 2.6f, 1.55f, 1.65f, 215f, 150f, 1.45f, 30f, 26f, 9f, Ease.Smooth);
        }

        // "Mị Nương đã đến tuổi lấy chồng…": the king looks at his daughter, she lowers her eyes
        IEnumerator KingWorries()
        {
            miNuong.Play("Shy", 0.5f);
            king.Play("SitTalk", 0.4f);
            cam.Focus(Pt.On(king.transform, 1.4f), 2.6f);
            Go(cam.Move(new Vector3(1.8f, 15.5f, 29.4f), new Vector3(1.2f, 15.55f, 30.2f), Pt.On(king.transform, 1.3f), 34f, 30f, 6f));
            yield return Wait(6.2f);
            cam.Focus(Pt.On(miNuong.transform, 1.45f), 2.4f);
            yield return cam.Move(new Vector3(-1.1f, 15.4f, 29.6f), new Vector3(-1.6f, 15.45f, 30.2f), Pt.On(miNuong.transform, 1.35f), 30f, 27f, 6f);
        }

        // "Truyền lệnh khắp bốn phương…": the king stands and raises his hand; low camera, a drum-beat shake
        IEnumerator KingCalls()
        {
            cam.FocusOff();
            miNuong.Release();
            king.Play("StandUp", 0.3f);
            if (kingStand != null) Go(KingRises());
            Go(cam.Move(new Vector3(1.4f, 14.4f, 27.8f), new Vector3(0.6f, 14.9f, 29.4f), Pt.On(king.transform, 1.6f), Pt.On(king.transform, 2.1f), 42f, 34f, 7f, Ease.Out));
            yield return Wait(1.7f);
            king.Play("Command", 0.4f);
            cam.Shake(0.55f, 0.9f);
            yield return Wait(2.6f);
            king.Play("Talk", 0.5f);
        }

        // He does not rise through the chair: he steps forward off the seat onto the step in front of it while the clip stands him up.
        IEnumerator KingRises()
        {
            Vector3 from = king.transform.position, to = kingStand.position;
            yield return Wait(0.45f);
            const float seconds = 1.1f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds; k = k * k * (3f - 2f * k);
                king.transform.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
            king.transform.position = to;
        }

        // "Tin truyền đi khắp nơi…": the news runs down the avenue, then the camera lifts away over the land
        IEnumerator NewsSpreads()
        {
            king.Release();
            cam.Handheld = 1f;
            yield return cam.Move(new Vector3(-2.5f, 15.4f, 34f), new Vector3(-1.5f, 15.2f, 4f), new Vector3(0f, 15.2f, -10f), new Vector3(0f, 15.6f, -22f), 62f, 70f, 4.2f, Ease.In);
            cam.Handheld = 0.5f;
            yield return cam.Move(new Vector3(0f, 17f, 6f), new Vector3(0f, 44f, -26f), new Vector3(0f, 15f, 30f), 46f, 60f, 6f, Ease.Smooth);
        }

        // "Ta là Sơn Tinh…": the ground shakes, rock heaves up round a figure rising out of the dust
        IEnumerator SonTinhRises()
        {
            cam.FocusOff();
            king.Release(); miNuong.Release();
            sonTinh.gameObject.SetActive(false);
            cam.Set(Mark(sonTinhMark, 4.2f, 0.9f, -8.5f), SonPos + Vector3.up * 1.0f, 50f);
            cam.Shake(0.6f, 1.8f);
            Go(stage.RaiseRocks(SonPos, 12f, 4, 1.5f, ThuyPos, 9f, true, 95f));
            stage.Dust(SonPos, 1.4f, 70);
            yield return Wait(0.9f);
            sonTinh.gameObject.SetActive(true);
            sonTinh.Play("CrossArms", 0.05f);
            stage.Dust(SonPos, 1.1f, 40);
            Go(cam.Orbit(Pt.On(sonTinh.transform, 0f), 8.5f, 6.0f, 1.0f, 1.7f, 200f, 385f, 1.05f, 42f, 33f, 6.5f, Ease.Smooth));
            yield return Wait(1.6f);
            sonTinh.Play("RaiseSword", 0.35f);
            stage.Dust(SonPos, 1.6f, 50);
            cam.Shake(0.4f, 0.6f);
            Go(fx.NameCard("SƠN TINH", "Chúa vùng non cao Tản Viên", 2.4f, 0.6f));
            yield return Wait(3.4f);
            sonTinh.Play("Talk", 0.5f);
        }

        // "Ta là Thủy Tinh…": lightning, a storm, water gathers and the second god steps out of it
        IEnumerator ThuyTinhArrives()
        {
            cam.FocusOff();
            sonTinh.Release(); sonTinh.Play("CrossArms", 0.4f);
            thuyTinh.gameObject.SetActive(false);
            Go(stage.SinkRocks(1.0f));
            cam.Set(Mark(thuyTinhMark, -4.2f, 0.9f, -8.5f), ThuyPos + Vector3.up * 1.0f, 50f);
            stage.Lightning(1f);
            Go(stage.Mood(Storm, 0.5f, StormFog, 0.012f, 0.5f, 2.2f, -0.6f, -35f, StormTint));
            Go(stage.RainTo(2600f, 2.5f, ThuyPos));
            Go(stage.SummonWater(ThuyPos, 4.2f, 5f));
            yield return Wait(1.1f);
            thuyTinh.gameObject.SetActive(true);
            thuyTinh.Play("CrossArms", 0.05f);
            Go(cam.Orbit(Pt.On(thuyTinh.transform, 0f), 8.5f, 6.0f, 1.0f, 1.7f, 160f, -25f, 1.05f, 42f, 33f, 6.5f, Ease.Smooth));
            yield return Wait(1.7f);
            thuyTinh.Play("Command", 0.35f);
            stage.Lightning(0.8f);
            Go(fx.NameCard("THỦY TINH", "Chúa vùng biển cả", 2.4f, 0.6f));
            yield return Wait(3.6f);
            stage.Lightning(0.6f);
            thuyTinh.Play("Talk", 0.5f);
        }

        // "Một người là chúa non cao, một người là chúa nước thẳm…": the two face each other, time slows, the camera circles them
        IEnumerator Standoff()
        {
            Vector3 mid = (SonPos + ThuyPos) / 2f;
            sonTinh.Face(ThuyPos); thuyTinh.Face(SonPos);
            sonTinh.Play("CrossArms", 0.5f); thuyTinh.Play("CrossArms", 0.5f);
            sonTinh.LookAt(thuyTinh.transform); thuyTinh.LookAt(sonTinh.transform);
            Go(SlowMotion(0.3f, 4.2f));
            stage.Dust(SonPos, 1f, 20);
            yield return cam.Orbit(mid, 5.6f, 4.6f, 1.9f, 2.3f, 95f, 265f, 1.3f, 40f, 34f, 8f, Ease.Smooth);
        }

        // "Cả hai đều xứng đáng làm rể ta…": over the king's shoulder at the two, then the king close
        IEnumerator KingJudges()
        {
            stage.Rain(0f);
            Go(stage.Mood(Dawn, 1.55f, DawnFog, 0.0032f, 1.1f, 3.5f, 0.3f, 13f, DawnTint));
            Go(stage.RainTo(0f, 3f, ThuyPos));
            king.Play("Talk", 0.4f);
            king.LookAt(sonTinh.transform, 1f, new Vector3(0f, 1.5f, 0f));
            cam.Focus(new Vector3(0f, 15.5f, 21f), 4f);
            Go(cam.Move(new Vector3(0.7f, 16.5f, 35.4f), new Vector3(0.7f, 16.2f, 32f), new Vector3(0f, 15.6f, 21f), 42f, 36f, 4.5f));
            yield return Wait(2.2f);
            king.LookAt(thuyTinh.transform, 1f, new Vector3(0f, 1.5f, 0f));
            yield return Wait(2.4f);
            cam.Focus(Pt.On(king.transform, 1.7f), 2.6f);
            yield return cam.Move(new Vector3(-1.5f, 15.9f, 29.6f), new Vector3(-0.9f, 16.0f, 30.4f), Pt.On(king.transform, 1.75f), 32f, 28f, 7f);
        }

        // "Sính lễ là sáu linh thú mang số chín: ba con trên non cao, ba con dưới biển cả…": not a list of six names but two sides, one
        // for each god: the king announces the six, the camera goes to Sơn Tinh (his three, up in the mountains) and to Thủy Tinh
        // (his three, down in the sea), each with a short card.
        IEnumerator BridePrice()
        {
            king.LookAt(null, 0f);
            king.Play("Point", 0.4f);
            cam.FocusOff();
            Go(cam.Move(new Vector3(1.2f, 14.5f, 29.2f), new Vector3(0.4f, 15.2f, 30.4f), Pt.On(king.transform, 1.7f), Pt.On(king.transform, 1.9f), 40f, 32f, 5.2f, Ease.Out));
            yield return Wait(0.8f);
            yield return fx.Pop("SÁU LINH THÚ CHÍN", 1.5f);
            // the three of the mountain: Sơn Tinh, the camera low on him, the hills behind
            sonTinh.Play("RaiseSword", 0.4f);
            Go(cam.Move(Mark(sonTinhMark, -2.6f, 0.5f, 4.6f), Mark(sonTinhMark, -2.0f, 0.9f, 3.8f), Pt.On(sonTinh.transform, 1.6f), Pt.On(sonTinh.transform, 1.9f), 42f, 34f, 3.2f, Ease.Out));
            yield return fx.Pop("BA LINH THÚ NON CAO", 1.3f, new Color(1f, 0.72f, 0.38f));
            sonTinh.Play("CrossArms", 0.4f);
            // the three of the sea: Thủy Tinh, in the same frame from the other side
            thuyTinh.Play("RaiseSword", 0.4f);
            Go(cam.Move(Mark(thuyTinhMark, 2.6f, 0.5f, 4.6f), Mark(thuyTinhMark, 2.0f, 0.9f, 3.8f), Pt.On(thuyTinh.transform, 1.6f), Pt.On(thuyTinh.transform, 1.9f), 42f, 34f, 3.2f, Ease.Out));
            yield return fx.Pop("BA LINH THÚ BIỂN CẢ", 1.3f, new Color(0.55f, 0.84f, 1f));
            thuyTinh.Play("CrossArms", 0.4f);
            yield return fx.Pop("AI MANG ĐỦ ĐẾN TRƯỚC…", 1.4f);
        }

        // "Hai vị thần lập tức lên đường, mỗi người một ngả…": the two walk away in opposite directions, the camera behind the throne
        IEnumerator SetOut()
        {
            king.LookAt(null, 0f);
            king.Play("Command", 0.4f);
            sonTinh.LookAt(null, 0f); thuyTinh.LookAt(null, 0f);
            Go(sonTinh.WalkTo(sonTinhExit.position, 1.9f));
            Go(thuyTinh.WalkTo(thuyTinhExit.position, 1.9f));
            cam.FocusOff();
            Go(cam.Move(new Vector3(0f, 15.6f, 25.2f), new Vector3(0f, 20.5f, 24f), new Vector3(0f, 15f, 10f), new Vector3(0f, 14.6f, 6f), 42f, 46f, 11f, Ease.Smooth));
            yield return Wait(4.5f);
            king.Release(0.8f);
            yield return Wait(8f);
        }

        // "Còn người, sẽ bước vào vai ai?": up into the sky, and the question to the player
        IEnumerator Skyward()
        {
            cam.FocusOff();
            Go(fx.NameCard("CHỌN VỊ THẦN CỦA BẠN", "Sơn Tinh hay Thủy Tinh?", 3f, 0.8f));
            yield return cam.Move(new Vector3(0f, 22f, 38f), new Vector3(0f, 56f, 30f), new Vector3(0f, 15f, 10f), new Vector3(0f, 34f, -10f), 46f, 60f, 10f, Ease.Smooth);
        }
    }
}
