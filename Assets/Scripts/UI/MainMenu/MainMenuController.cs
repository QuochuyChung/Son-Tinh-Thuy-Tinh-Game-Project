using SonTinhThuyTinh.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button newGameButton;
        [SerializeField] Button quitButton;
        [SerializeField] string firstScene = SceneNames.Prologue;

        void Awake()
        {
            newGameButton.onClick.AddListener(() => SceneLoader.Load(firstScene));
            quitButton.onClick.AddListener(Application.Quit);
        }

        void Start() => EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
    }
}
