using System;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public class SkillGate : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static event Action<bool> Changed;

        void OnEnable()
        {
            IsOpen = true;
            Changed?.Invoke(true);
        }

        void OnDisable()
        {
            IsOpen = false;
            Changed?.Invoke(false);
        }
    }
}
