using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SonTinhThuyTinh.Cutscene
{
    // One actor of a cutscene (Hùng Vương, Mị Nương, a suitor): plays named clips over the character's own Animator Controller, whatever
    // that controller is, so a cutscene can use any Humanoid clip (the hand-keyed gestures of tools/cutscene_gestures.py, the walk clips of
    // the characters) without the controller having a state for it.
    //  - Play(name)  fades the clip in over the controller's idle (loops if the clip loops, otherwise holds its last frame);
    //  - Release()   fades back to the controller's idle;
    //  - WalkTo(...) walks to a point with the Walk clip, facing the way it goes;
    //  - Face / LookAt turn the body / the head (neck and head bones, clamped) towards a point or another actor.
    // The Animator is set up for playables only: its controller keeps running as the base layer of a mixer.
    [DefaultExecutionOrder(100)]
    public class ActorRig : MonoBehaviour
    {
        [Serializable]
        public struct NamedClip
        {
            public string name;
            public AnimationClip clip;
        }

        [SerializeField] Animator animator;
        [SerializeField] NamedClip[] clips;
        [Tooltip("How far the head may turn from the body when it follows a LookAt target, degrees.")]
        [SerializeField] float maxHeadYaw = 65f;
        [SerializeField] float maxHeadPitch = 25f;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimatorControllerPlayable baseLayer;
        readonly AnimationClipPlayable[] slot = new AnimationClipPlayable[2];
        readonly float[] weight = new float[2];
        readonly float[] target = new float[2];
        readonly float[] fadeSpeed = new float[2];
        readonly AnimationClip[] slotClip = new AnimationClip[2];
        int active = -1;
        float baseWeight = 1f, baseTarget = 1f, baseFade = 4f;
        bool built;

        Transform head, neck, lookTarget;
        Vector3 lookOffset = new(0f, 1.6f, 0f);
        float lookWeight, lookGoal, yaw, pitch;

        public Animator Animator => animator;
        public bool Has(string name) => Find(name) != null;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            Build();
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        void Build()
        {
            if (built || animator == null) return;
            built = true;
            animator.applyRootMotion = false;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            graph = PlayableGraph.Create("Actor " + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            output.SetSourcePlayable(mixer);
            if (animator.runtimeAnimatorController != null)
            {
                baseLayer = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
                graph.Connect(baseLayer, 0, mixer, 0);
            }
            mixer.SetInputWeight(0, 1f);
            graph.Play();
        }

        AnimationClip Find(string clipName)
        {
            if (clips == null) return null;
            foreach (var c in clips) if (c.name == clipName) return c.clip;
            return null;
        }

        // Fades the clip in over `fade` seconds (the previous clip fades out). speed scales its playback. Returns its length.
        public float Play(string clipName, float fade = 0.25f, float speed = 1f)
        {
            Build();
            var clip = Find(clipName);
            if (clip == null) { Debug.LogWarning($"ActorRig {name}: no clip '{clipName}'"); return 0f; }
            int next = active == 0 ? 1 : 0;
            if (slot[next].IsValid()) { mixer.DisconnectInput(next + 1); slot[next].Destroy(); }
            slot[next] = AnimationClipPlayable.Create(graph, clip);
            slot[next].SetSpeed(speed);
            graph.Connect(slot[next], 0, mixer, next + 1);
            slotClip[next] = clip;
            weight[next] = 0f; target[next] = 1f; fadeSpeed[next] = 1f / Mathf.Max(fade, 0.01f);
            if (active >= 0) { target[active] = 0f; fadeSpeed[active] = 1f / Mathf.Max(fade, 0.01f); }
            active = next;
            baseTarget = 0f; baseFade = 1f / Mathf.Max(fade, 0.01f);
            return clip.length / Mathf.Max(0.01f, speed);
        }

        [SerializeField] string restClip;   // set by the scene builder; empty = the controller's idle

        void Start()
        {
            if (!string.IsNullOrEmpty(restClip) && Has(restClip)) Play(restClip, 0.01f);
        }

        // The clip Release() goes back to instead of the controller's idle (e.g. Mị Nương's own idle leans to one side).
        public void SetRest(string clipName, float fade = 0.01f)
        {
            restClip = clipName;
            Release(fade);
        }

        // Back to the resting clip if there is one, else to the controller's idle.
        public void Release(float fade = 0.35f)
        {
            if (!string.IsNullOrEmpty(restClip) && Has(restClip)) { Play(restClip, fade); return; }
            if (active >= 0) { target[active] = 0f; fadeSpeed[active] = 1f / Mathf.Max(fade, 0.01f); }
            baseTarget = 1f; baseFade = 1f / Mathf.Max(fade, 0.01f);
            active = -1;
        }

        public IEnumerator PlayFor(string clipName, float seconds, float fade = 0.25f)
        {
            Play(clipName, fade);
            yield return new WaitForSeconds(seconds);
        }

        void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < 2; i++)
            {
                if (!slot[i].IsValid()) continue;
                weight[i] = Mathf.MoveTowards(weight[i], target[i], fadeSpeed[i] * dt);
                // a clip that is not meant to loop but is used as a loop (Walk): wrap it by hand
                var c = slotClip[i];
                if (c != null && !c.isLooping && loopByHand && slot[i].GetTime() >= c.length) slot[i].SetTime(0);
                if (weight[i] <= 0f && target[i] <= 0f && i != active) { mixer.DisconnectInput(i + 1); slot[i].Destroy(); slot[i] = default; slotClip[i] = null; }
            }
            baseWeight = Mathf.MoveTowards(baseWeight, baseTarget, baseFade * dt);
            // the controller's layer takes what the clips leave
            float clipSum = 0f;
            for (int i = 0; i < 2; i++) clipSum += slot[i].IsValid() ? weight[i] : 0f;
            mixer.SetInputWeight(0, Mathf.Clamp01(Mathf.Max(baseWeight, 1f - clipSum)));
            for (int i = 0; i < 2; i++) if (slot[i].IsValid()) mixer.SetInputWeight(i + 1, weight[i]);
        }

        bool loopByHand;

        // ---------------------------------------------------------------- movement

        public IEnumerator WalkTo(Vector3 point, float speed = 1.4f, string clip = "Walk", bool stopAtEnd = true, float turnSpeed = 220f)
        {
            point.y = transform.position.y;
            loopByHand = true;
            Play(clip, 0.25f, Mathf.Clamp(speed / 1.4f, 0.6f, 1.5f));
            while ((point - transform.position).sqrMagnitude > 0.04f)
            {
                Vector3 d = point - transform.position; d.y = 0f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
                transform.position = Vector3.MoveTowards(transform.position, point, speed * Time.deltaTime);
                yield return null;
            }
            loopByHand = false;
            if (stopAtEnd) Release();
        }

        public IEnumerator FaceTo(Vector3 point, float seconds = 0.5f)
        {
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) yield break;
            Quaternion from = transform.rotation, to = Quaternion.LookRotation(d);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                transform.rotation = Quaternion.Slerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }
            transform.rotation = to;
        }

        public void Face(Vector3 point)
        {
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        // ---------------------------------------------------------------- head look

        // The head (and a bit of the neck) follows the target (its position + `offset`); weight 0 releases it. Smooth, clamped.
        public void LookAt(Transform t, float weightGoal = 1f, Vector3? offset = null)
        {
            lookTarget = t;
            lookGoal = t == null ? 0f : weightGoal;
            if (offset.HasValue) lookOffset = offset.Value;
        }

        public void StopLooking() => lookGoal = 0f;

        void LateUpdate()
        {
            if (head == null) return;
            lookWeight = Mathf.MoveTowards(lookWeight, lookGoal, 2.2f * Time.deltaTime);
            if (lookWeight <= 0.001f && lookGoal <= 0f) { yaw = Mathf.Lerp(yaw, 0f, 5f * Time.deltaTime); pitch = Mathf.Lerp(pitch, 0f, 5f * Time.deltaTime); if (Mathf.Abs(yaw) < 0.05f && Mathf.Abs(pitch) < 0.05f) return; }
            float wantYaw = 0f, wantPitch = 0f;
            if (lookTarget != null && lookGoal > 0f)
            {
                Vector3 to = lookTarget.position + lookOffset - head.position;
                Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
                wantYaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, Vector3.up), -maxHeadYaw, maxHeadYaw);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -maxHeadPitch, maxHeadPitch);
            }
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            yaw = Mathf.Lerp(yaw, wantYaw * lookWeight, k);
            pitch = Mathf.Lerp(pitch, wantPitch * lookWeight, k);
            Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, transform.right);
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.35f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, turn, neck != null ? 0.65f : 1f) * head.rotation;
        }
    }
}
