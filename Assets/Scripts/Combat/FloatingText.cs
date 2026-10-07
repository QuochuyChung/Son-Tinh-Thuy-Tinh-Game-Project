using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // A number that rises, fades and faces the camera (damage numbers over the training dummy).
    public class FloatingText : MonoBehaviour
    {
        TextMesh text;
        float age;

        public static void Spawn(Vector3 position, string value, Color color)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = position;
            var tm = go.AddComponent<TextMesh>();
            tm.text = value;
            tm.fontSize = 72;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;
            go.AddComponent<FloatingText>().text = tm;
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (1.6f * Time.deltaTime);
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            Color c = text.color;
            c.a = Mathf.Clamp01(1.4f - age);
            text.color = c;
            if (age > 1.4f) Destroy(gameObject);
        }
    }
}
