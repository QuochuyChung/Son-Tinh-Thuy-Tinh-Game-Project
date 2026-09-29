using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue
{
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Dialogue Sequence", fileName = "Dialogue_")]
    public class DialogueSequence : ScriptableObject
    {
        [SerializeField] DialogueLine[] lines;

        public IReadOnlyList<DialogueLine> Lines => lines;
    }
}
