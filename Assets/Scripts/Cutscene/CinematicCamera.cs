using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SonTinhThuyTinh.Cutscene
{
    // A point in the scene, fixed or following a transform (+ an offset): where a camera stands or looks.
    public readonly struct Pt
    {
        readonly Vector3 fixedPoint;
        readonly Transform follow;
        readonly Vector3 offset;
        public Pt(Vector3 v) { fixedPoint = v; follow = null; offset = default; }
        public Pt(Transform t, Vector3 offset) { fixedPoint = default; follow = t; this.offset = offset; }
        public Vector3 Get() => follow != null ? follow.position + offset : fixedPoint;
        public static implicit operator Pt(Vector3 v) => new(v);
        public static Pt On(Transform t, float height = 1.5f) => new(t, Vector3.up * height);
        public static Pt On(Transform t, Vector3 offset) => new(t, offset);
    }

    // The camera of a cutscene: one CinemachineCamera whose transform this script drives, so a shot is a few lines of code:
    //   Set(...)     a cut to a fixed frame;
    //   Move(...)    a dolly / crane between two frames (the look-at points may follow actors), eased;
    //   Orbit(...)   a circle round a point;
    //   Shake(...)   an impact; Handheld   a constant slight sway (real cameras are never still);
    //   Focus        the depth-of-field focus follows a point (CinematicVolume), so the subject is sharp and the background soft.
    // Moves run in unscaled time: a slow-motion shot (Time.timeScale) slows the actors, not the camera work.
    public class CinematicCamera : MonoBehaviour
    {
        public enum Ease { Linear, Smooth, In, Out }

        [SerializeField] CinemachineCamera vcam;
        [SerializeField] Volume volume;
        [Tooltip("Slight constant sway of the camera (metres / degrees at 1).")]
        [SerializeField] float handheld = 0.6f;
        [Tooltip("Content is lifted this fraction of the screen height: the dialogue box and the bottom bar cover the lower part of the picture.")]
        [SerializeField] float frameShift = 0.11f;

        Vector3 basePosition;
        Quaternion baseRotation = Quaternion.identity;
        float baseFov = 40f, roll;
        float shakeAmp, shakeLeft, shakeTime;
        DepthOfField dof;
        bool focusOn;
        Pt focusPoint;
        float focusSpeed = 8f, focusDistance = 10f;
        float seed;

        public CinemachineCamera Vcam => vcam;
        public float Handheld { get => handheld; set => handheld = value; }
        public Vector3 Position => basePosition;

        void Awake()
        {
            if (vcam == null) vcam = GetComponentInChildren<CinemachineCamera>(true);
            if (volume != null && volume.profile != null) volume.profile.TryGet(out dof);
            seed = UnityEngine.Random.value * 100f;
            basePosition = transform.position;
            baseRotation = transform.rotation;
        }

        public void Activate(bool on)
        {
            if (vcam == null) return;
            vcam.gameObject.SetActive(on);
            if (on) vcam.Priority = 200;
        }

        // ---------------------------------------------------------------- placing and moving

        public void Set(Pt position, Pt look, float fov, float rollDeg = 0f)
        {
            basePosition = position.Get();
            Vector3 d = look.Get() - basePosition;
            baseRotation = d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : baseRotation;
            baseFov = fov; roll = rollDeg;
            Apply();
        }

        public IEnumerator Move(Pt p0, Pt p1, Pt look0, Pt look1, float fov0, float fov1, float seconds, Ease ease = Ease.Smooth, float roll0 = 0f, float roll1 = 0f)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Eased(t / seconds, ease);
                Set(Vector3.Lerp(p0.Get(), p1.Get(), k), Vector3.Lerp(look0.Get(), look1.Get(), k), Mathf.Lerp(fov0, fov1, k), Mathf.Lerp(roll0, roll1, k));
                yield return null;
            }
            Set(p1.Get(), look1.Get(), fov1, roll1);
        }

        // Shorthand: the same look-at point throughout.
        public IEnumerator Move(Pt p0, Pt p1, Pt look, float fov0, float fov1, float seconds, Ease ease = Ease.Smooth)
            => Move(p0, p1, look, look, fov0, fov1, seconds, ease);

        // A circle round `center` (horizontal radius, camera height above the centre, degrees 0 = +z side, turning towards +x),
        // looking at the centre + lookHeight. The radius and the height may change over the move.
        public IEnumerator Orbit(Pt center, float radius0, float radius1, float height0, float height1, float deg0, float deg1, float lookHeight, float fov0, float fov1, float seconds, Ease ease = Ease.Smooth)
        {
            for (float t = 0f; t <= seconds + 1e-4f; t += Time.unscaledDeltaTime)
            {
                float k = Eased(t / seconds, ease);
                float a = Mathf.Lerp(deg0, deg1, k) * Mathf.Deg2Rad, r = Mathf.Lerp(radius0, radius1, k);
                Vector3 c = center.Get();
                Set(c + new Vector3(Mathf.Sin(a) * r, Mathf.Lerp(height0, height1, k), Mathf.Cos(a) * r), c + Vector3.up * lookHeight, Mathf.Lerp(fov0, fov1, k));
                yield return null;
            }
        }

        public IEnumerator Orbit(Pt center, float radius, float height, float deg0, float deg1, float lookHeight, float fov, float seconds, Ease ease = Ease.Smooth)
            => Orbit(center, radius, radius, height, height, deg0, deg1, lookHeight, fov, fov, seconds, ease);

        public void Shake(float amplitude, float seconds)
        {
            shakeAmp = Mathf.Max(shakeAmp * (shakeLeft > 0f ? 1f : 0f), amplitude);
            shakeLeft = seconds; shakeTime = seconds;
        }

        // Depth of field: the focus distance follows `point` (the subject), with the given aperture (smaller = more blur).
        public void Focus(Pt point, float aperture = 3.2f, bool snap = true)
        {
            focusPoint = point; focusOn = true;
            if (dof != null)
            {
                dof.active = true; dof.mode.value = DepthOfFieldMode.Bokeh;
                dof.aperture.value = aperture; dof.focalLength.value = 50f;
                if (snap) { focusDistance = Vector3.Distance(Camera.main != null ? Camera.main.transform.position : basePosition, point.Get()); dof.focusDistance.value = focusDistance; }
            }
        }

        public void FocusOff()
        {
            focusOn = false;
            if (dof != null) dof.active = false;
        }

        // ---------------------------------------------------------------- frame

        void LateUpdate() => Apply();

        void Apply()
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 pos = basePosition; Quaternion rot = baseRotation;
            float time = Time.unscaledTime + seed;
            if (handheld > 0f)
            {
                Vector3 sway = new Vector3(Mathf.PerlinNoise(time * 0.55f, 1f) - 0.5f, Mathf.PerlinNoise(2f, time * 0.5f) - 0.5f, Mathf.PerlinNoise(time * 0.45f, 3f) - 0.5f) * (0.05f * handheld);
                pos += rot * sway;
                rot *= Quaternion.Euler((Mathf.PerlinNoise(time * 0.6f, 5f) - 0.5f) * 0.5f * handheld, (Mathf.PerlinNoise(7f, time * 0.6f) - 0.5f) * 0.5f * handheld, 0f);
            }
            if (shakeLeft > 0f)
            {
                shakeLeft -= dt;
                float k = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeTime));
                float a = shakeAmp * k * k;
                pos += rot * (new Vector3(Mathf.PerlinNoise(time * 38f, 0f) - 0.5f, Mathf.PerlinNoise(0f, time * 41f) - 0.5f, 0f) * (0.45f * a));
                rot *= Quaternion.Euler((Mathf.PerlinNoise(time * 33f, 9f) - 0.5f) * 3f * a, (Mathf.PerlinNoise(9f, time * 36f) - 0.5f) * 3f * a, (Mathf.PerlinNoise(time * 30f, 4f) - 0.5f) * 3f * a);
            }
            rot *= Quaternion.Euler(frameShift * baseFov, 0f, 0f);   // tilt down a little: the subject rises in the frame, clear of the text box
            rot *= Quaternion.Euler(0f, 0f, roll);
            if (vcam != null)
            {
                vcam.transform.SetPositionAndRotation(pos, rot);
                var lens = vcam.Lens; lens.FieldOfView = baseFov; vcam.Lens = lens;
            }
            if (focusOn && dof != null)
            {
                float want = Vector3.Distance(pos, focusPoint.Get());
                focusDistance = Mathf.Lerp(focusDistance, want, 1f - Mathf.Exp(-focusSpeed * dt));
                dof.focusDistance.value = Mathf.Max(0.5f, focusDistance);
            }
        }

        static float Eased(float k, Ease e)
        {
            k = Mathf.Clamp01(k);
            switch (e)
            {
                case Ease.Linear: return k;
                case Ease.In: return k * k;
                case Ease.Out: return 1f - (1f - k) * (1f - k);
                default: return k * k * (3f - 2f * k);
            }
        }
    }
}
