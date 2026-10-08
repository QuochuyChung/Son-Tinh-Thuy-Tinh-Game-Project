using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SonTinhThuyTinh.Quest
{
    // Something the player can use with E when standing close to it (talk to a villager, pick up an item). InteractionPrompt finds the
    // nearest one in range, shows its prompt and calls Interact(). Disable the component to take it out of play.
    public class Interactable : MonoBehaviour
    {
        static readonly List<Interactable> all = new();

        [SerializeField] string prompt = "Nhấn E để nói chuyện";
        [Tooltip("Metres from this object's position (measured flat, ignoring height).")]
        [SerializeField] float radius = 2.6f;
        [SerializeField] UnityEvent interacted = new();

        public static IReadOnlyList<Interactable> All => all;
        public string Prompt { get => prompt; set => prompt = value; }
        public float Radius => radius;
        public UnityEvent Interacted => interacted;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public void Interact() => interacted.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => all.Clear();
    }
}
