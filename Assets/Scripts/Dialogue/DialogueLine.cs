using System;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue
{
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Leave empty for narration.")]
        public string speaker;
        [TextArea(2, 5)]
        public string text;
        [Tooltip("Optional full-screen image behind the text. Lines without one keep the previous image.")]
        public Sprite illustration;
    }
}
