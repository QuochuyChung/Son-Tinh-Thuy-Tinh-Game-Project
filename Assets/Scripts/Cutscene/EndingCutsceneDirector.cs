using System.Collections;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using TMPro;
using UnityEngine;
using Ease = SonTinhThuyTinh.Cutscene.CinematicCamera.Ease;

namespace SonTinhThuyTinh.Cutscene
{
    // The ending in front of the palace (scene Cutscene_Ending, built by Tools ▸ Son Tinh Thuy Tinh ▸ Build Ending Cutscene), after the
    // final duel of Map_FinalBattle: the winner and the loser both stand (neither kneels) in the golden light after the fight; the king
    // names the winner, the winner answers, the loser accepts, Mị Nương comes down to the winner, flower petals fall for the feast, the
    // camera rises. Then the epilogue card (Space returns to the main menu with a fresh session). Two dialogues, one per winner.
    public class EndingCutsceneDirector : CinematicDirector
    {
        [Header("Cast")]
        [SerializeField] ActorRig king;
        [SerializeField] ActorRig miNuong;
        [SerializeField] ActorRig sonTinh;
        [SerializeField] ActorRig thuyTinh;
        [Header("Marks")]
        [SerializeField] Transform kingMark;
        [SerializeField] Transform miNuongMark;
        [SerializeField] Transform miNuongWalkTo;   // beside the winner
        [SerializeField] Transform winnerMark;      // Sơn Tinh's mark when he wins, Thủy Tinh's when he wins: the left one
        [SerializeField] Transform loserMark;
        [Header("Text")]
        [SerializeField] DialogueSequence sonTinhWins;
        [SerializeField] DialogueSequence thuyTinhWins;
        [SerializeField] string nextScene = SceneNames.MainMenu;
        [Tooltip("Used when the scene is played on its own, without the duel.")]
        [SerializeField] CharacterId fallbackPlayer = CharacterId.SonTinh;

        ActorRig winner, loser;
        bool winnerIsSon;

        static readonly Color Golden = new(1f, 0.70f, 0.44f), GoldenFog = new(0.93f, 0.68f, 0.52f);
        static readonly Color Joy = new(1f, 0.86f, 0.66f), JoyFog = new(0.95f, 0.82f, 0.70f);

        static Vector3 P(float x, float y, float z) => new(x, y, z);

        protected override IEnumerator Run()
        {
            var id = GameSession.SelectedCharacter ?? fallbackPlayer;
            winnerIsSon = id == CharacterId.SonTinh;
            winner = winnerIsSon ? sonTinh : thuyTinh; loser = winnerIsSon ? thuyTinh : sonTinh;
            SetUp();
            yield return Chapter(Aftermath());
            yield return Dialogue(winnerIsSon ? sonTinhWins : thuyTinhWins, Beat);
            yield return Epilogue();
        }

        void SetUp()
        {
            king.transform.SetPositionAndRotation(kingMark.position, kingMark.rotation);
            miNuong.transform.SetPositionAndRotation(miNuongMark.position, miNuongMark.rotation);
            winner.transform.SetPositionAndRotation(winnerMark.position, winnerMark.rotation);
            loser.transform.SetPositionAndRotation(loserMark.position, loserMark.rotation);
            winner.Play("CrossArms", 0.01f);
            loser.Play("Bow", 0.01f, 0.35f);
            king.Play("CrossArms", 0.01f);
            miNuong.Release(0.01f);
            winner.LookAt(loser.transform, 0.8f, Vector3.up * 1.4f);
            miNuong.LookAt(winner.transform, 0.8f, Vector3.up * 1.5f);
            StartCoroutine(stage.Mood(Golden, 0.95f, GoldenFog, 0.0045f, 0.95f, 0.05f));
            stage.Rain(0f);
            cam.Handheld = 0.7f;
        }

        // ---------------------------------------------------------------- after the fight (no text yet)

        IEnumerator Aftermath()
        {
            fx.FadeNow(1f);
            cam.Set(P(0f, 52f, 26f), P(0f, 15f, 26f), 52f);
            Go(fx.Letterbox(true, 1.4f));
            Go(fx.Fade(0f, 2f));
            Go(SlowMotion(0.4f, 5f));
            stage.Dust(winner.transform.position, 1.2f, 25);
            yield return cam.Move(P(0f, 52f, 26f), P(0f, 17f, 14f), P(0f, 15f, 26f), P(0f, 15.5f, 28f), 52f, 36f, 7f, Ease.Smooth);
        }

        // ---------------------------------------------------------------- the lines

        IEnumerator Beat(int line)
        {
            switch (line)
            {
                case 0: return TheyStand();
                case 1: return KingNamesWinner();
                case 2: return WinnerAnswers();
                case 3: return LoserAccepts();
                case 4: return PrincessThanks();
                case 5: return KingFeast();
                case 6: return OneLand();
                default: return Together();
            }
        }

        // "Trận chiến trên pháp đàn đã kết thúc…": the two face each other, the winner upright, the loser's head bowed
        IEnumerator TheyStand()
        {
            Vector3 mid = (winner.transform.position + loser.transform.position) / 2f;
            cam.FocusOff();
            yield return cam.Orbit(mid, 6.5f, 4.6f, 1.3f, 1.8f, 100f, 215f, 1.4f, 38f, 32f, 9f, Ease.Smooth);
        }

        // "Trận đấu đã phân định. … là người thắng. Trời đất chứng giám, lời ta không rút lại.": the king, low, with his banners
        IEnumerator KingNamesWinner()
        {
            king.Play("Command", 0.4f);
            cam.Focus(Pt.On(king.transform, 1.8f), 2.6f);
            Go(cam.Move(P(1.6f, 14.5f, 28.4f), P(0.4f, 15.0f, 30f), Pt.On(king.transform, 1.7f), Pt.On(king.transform, 2.1f), 42f, 33f, 8f, Ease.Out));
            yield return Wait(1.4f);
            cam.Shake(0.4f, 0.7f);
            yield return Wait(3f);
            king.Play("Talk", 0.5f);
        }

        // "Thần xin tuân lệnh bệ hạ. …": the winner bows and speaks
        IEnumerator WinnerAnswers()
        {
            king.Play("CrossArms", 0.5f);
            winner.LookAt(king.transform, 1f, Vector3.up * 1.7f);
            float len = winner.Play("Bow", 0.3f);
            cam.Focus(Pt.On(winner.transform, 1.6f), 2.4f);
            float side = winnerIsSon ? 1f : -1f;
            Go(cam.Orbit(Pt.On(winner.transform, 0f), 3.4f, 2.8f, 1.55f, 1.7f, 25f * side, -25f * side, 1.45f, 34f, 29f, 8.5f, Ease.Smooth));
            yield return Wait(len + 0.2f);
            winner.Play("Talk", 0.5f);
        }

        // "Thần xin chịu thua. … thần không oán trách, xin lui về lo việc của mình.": the loser, head bowed, steady
        IEnumerator LoserAccepts()
        {
            winner.Play("CrossArms", 0.4f);
            loser.LookAt(king.transform, 0.6f, Vector3.up * 1.6f);
            loser.Play("Bow", 0.3f);
            cam.Focus(Pt.On(loser.transform, 1.6f), 2.3f);
            float side = winnerIsSon ? 1f : -1f;
            yield return cam.Orbit(Pt.On(loser.transform, 0f), 3.6f, 2.9f, 1.5f, 1.65f, -25f * side, 20f * side, 1.4f, 34f, 29f, 8.5f, Ease.Smooth);
        }

        // "Chàng... thiếp xin cảm ơn chàng…": Mị Nương comes down to the winner
        IEnumerator PrincessThanks()
        {
            loser.Release(0.8f);
            miNuong.LookAt(null, 0f);
            cam.FocusOff();
            Go(miNuong.WalkTo(miNuongWalkTo.position, 1.2f));
            cam.Focus(Pt.On(miNuong.transform, 1.5f), 2.6f, false);
            Go(cam.Move(P(-3f, 15.6f, 31f), P(-1.6f, 15.5f, 25f), Pt.On(miNuong.transform, 1.45f), 36f, 32f, 5.5f, Ease.Smooth));
            yield return Wait(5.2f);
            miNuong.Face(winner.transform.position);
            winner.LookAt(miNuong.transform, 1f, Vector3.up * 1.5f);
            miNuong.Play("Shy", 0.4f);
            cam.Focus(Pt.On(miNuong.transform, 1.5f), 2.3f);
            yield return cam.Orbit(Pt.On(miNuong.transform, 0f), 2.8f, 2.4f, 1.5f, 1.55f, 200f, 150f, 1.45f, 30f, 27f, 7f);
        }

        // "Hay lắm! … Hôm nay mở tiệc mừng, cả nước cùng vui!": the king's arms up, petals begin to fall
        IEnumerator KingFeast()
        {
            king.Play("Command", 0.3f);
            cam.FocusOff();
            Go(stage.Mood(Joy, 1.05f, JoyFog, 0.003f, 1.0f, 3f));
            stage.Petals(25f, P(0f, 14f, 27f));
            Go(cam.Move(P(1.4f, 14.4f, 28.8f), P(0.2f, 15.2f, 30.6f), Pt.On(king.transform, 1.9f), Pt.On(king.transform, 2.3f), 46f, 34f, 5f, Ease.Out));
            yield return Wait(1.6f);
            cam.Shake(0.3f, 0.5f);
            yield return Wait(3.4f);
            king.Play("Talk", 0.5f);
            Go(cam.Move(P(-4f, 17f, 36f), P(0f, 18.5f, 37f), P(0f, 15.2f, 22f), 40f, 44f, 7f, Ease.Smooth));
            yield return Wait(7f);
        }

        // "Từ nay, núi sông là một. Hai con hãy cùng nhau gìn giữ bờ cõi.": the king over the four, slowly circling
        IEnumerator OneLand()
        {
            stage.Petals(40f, P(0f, 14f, 27f));
            loser.Release(0.6f);
            loser.Play("CrossArms", 0.6f);
            winner.Play("CrossArms", 0.6f);
            cam.FocusOff();
            yield return cam.Orbit(P(0f, 14f, 27f), 11f, 8f, 3.4f, 2.2f, 175f, 245f, 1.3f, 42f, 36f, 11f, Ease.Smooth);
        }

        // "Vậy là từ nay, … cùng Mị Nương sống hạnh phúc.": the pair together, the camera lifting away into the evening
        IEnumerator Together()
        {
            stage.Petals(60f, P(0f, 14f, 27f));
            winner.Play("CrossArms", 0.5f);
            miNuong.Release(0.6f);
            miNuong.LookAt(winner.transform, 1f, Vector3.up * 1.5f);
            cam.Focus(Pt.On(winner.transform, 1.5f), 3f);
            Go(cam.Orbit(Pt.On(miNuong.transform, 0f), 6.5f, 14f, 1.6f, 9f, 190f, 140f, 1.5f, 36f, 42f, 11f, Ease.Smooth));
            yield return Wait(8f);
            cam.FocusOff();
        }

        // ---------------------------------------------------------------- epilogue card

        IEnumerator Epilogue()
        {
            Go(fx.Fade(1f, 1.4f, Color.black));
            yield return Wait(1.5f);
            stage.Petals(0f, Vector3.zero);
            string title = winnerIsSon ? "Sơn Tinh" : "Thủy Tinh";
            string body = winnerIsSon
                ? "Sơn Tinh rước Mị Nương về núi Tản Viên.\n" + "Núi cao che sóng, muôn dân sống yên."
                : "Thủy Tinh rước Mị Nương về chốn biển khơi.\n" + "Nước lành chảy khắp đồng, muôn dân sống đủ.";
            yield return fx.Epilogue(title, body, "Nhấn Space để về Menu");
            yield return WaitForPress();
            GameSession.StartNewGame();
            SceneLoader.Load(nextScene);
        }
    }
}
