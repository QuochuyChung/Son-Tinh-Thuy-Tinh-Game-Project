using TMPro;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // A number that rises, fades and faces the camera (damage numbers over the training dummy, "Choáng!"). TextMeshPro with the game's
    // default font (Crimson Pro, TMP Settings), a dark outline so it reads over any background.
    public class FloatingText : MonoBehaviour
    {
        TextMeshPro text;
        float age;

        public static void Spawn(Vector3 position, string value, Color color)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = position;
            var tm = go.AddComponent<TextMeshPro>();
            tm.text = value;
            tm.fontSize = 4.5f;
            tm.fontStyle = FontStyles.Bold;
            tm.alignment = TextAlignmentOptions.Center;
            tm.textWrappingMode = TextWrappingModes.NoWrap;
            tm.color = color;
            tm.outlineWidth = 0.18f;
            tm.outlineColor = new Color32(20, 14, 8, 255);
            go.AddComponent<FloatingText>().text = tm;
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (1.6f * Time.deltaTime);
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            text.alpha = Mathf.Clamp01(1.4f - age);
            if (age > 1.4f) Destroy(gameObject);
        }
    }
}
