using System.Collections;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.UI;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue
{
    // Opening of the game: title card, then the story of Hùng Vương seeking a husband for Mị Nương, then character select.
    public class PrologueDirector : MonoBehaviour
    {
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence prologue;
        [SerializeField] CanvasGroup titleCard;
        [SerializeField] float titleFadeDuration = 1.2f;
        [SerializeField] float titleHoldDuration = 2.5f;
        [SerializeField] string nextScene = SceneNames.CharacterSelect;

        IEnumerator Start()
        {
            GameSession.StartNewGame();

            titleCard.alpha = 0f;
            yield return CanvasGroupFade.Run(titleCard, 0f, 1f, titleFadeDuration);
            yield return new WaitForSecondsRealtime(titleHoldDuration);
            yield return CanvasGroupFade.Run(titleCard, 1f, 0f, titleFadeDuration);

            runner.Play(prologue, () => SceneLoader.Load(nextScene));
        }
    }
}
