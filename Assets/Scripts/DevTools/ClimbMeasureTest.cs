using System.IO;
using System.Text;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    // Dev tool (docs/progress.md 9.13): plays the Climb clip on a bare Thuy Tinh model with root motion on and writes how the root and the
    // hands / feet move to <project>/Temp/climb_measure.txt. The numbers (rise, forward travel, ledge height the clip was made for, where the
    // wall is) go into ClimbSettings. Add it to an empty object in Play mode (it finds the Thuy Tinh model and animator itself); it removes its
    // own model and finishes by itself.
    public class ClimbMeasureTest : MonoBehaviour
    {
        [SerializeField] GameObject modelPrefab;
        [SerializeField] RuntimeAnimatorController controller;

        System.Collections.IEnumerator Start()
        {
#if UNITY_EDITOR
            if (modelPrefab == null) modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ThuyTinh_v2.prefab");
            if (controller == null) controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController");
#endif
            var log = new StringBuilder();
            GameObject model = Instantiate(modelPrefab, new Vector3(60f, 0f, 60f), Quaternion.identity);
            Animator animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.Rebind();
            yield return null;
            animator.Play("Climb", 0, 0f);
            yield return null;

            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes), rt = animator.GetBoneTransform(HumanBodyBones.RightToes);
            Transform lh = animator.GetBoneTransform(HumanBodyBones.LeftHand), rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), head = animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 start = animator.transform.position;
            Quaternion facing = animator.transform.rotation;
            log.AppendLine($"model height (hips y at start) = {hips.position.y - start.y:F2}  head y = {head.position.y - start.y:F2}  start y = {start.y:F2}");

            float length = 0f;
            foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips) if (c.name == "Climb") length = c.length;
            log.AppendLine("clip length " + length.ToString("F3"));

            Vector3 Local(Vector3 world) => Quaternion.Inverse(facing) * (world - start);
            float t = 0f;
            while (t <= length + 0.05f)
            {
                Vector3 root = Local(animator.transform.position);
                Vector3 toe = Local(Vector3.Lerp(lt.position, rt.position, 0.5f));
                log.AppendLine($"t={t:F2}  root(right {root.x:F2} up {root.y:F2} fwd {root.z:F2})  hips(up {Local(hips.position).y:F2} fwd {Local(hips.position).z:F2})  head up {Local(head.position).y:F2}  hand(up {Local(lh.position).y:F2}/{Local(rh.position).y:F2} fwd {Local(lh.position).z:F2}/{Local(rh.position).z:F2})  foot(up {Local(lf.position).y:F2}/{Local(rf.position).y:F2} fwd {Local(lf.position).z:F2}/{Local(rf.position).z:F2})  toes fwd {toe.z:F2}");
                float next = t + 0.1f;
                while (t < next) { t += Time.deltaTime; yield return null; }
            }

            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "climb_measure.txt"), log.ToString());
            Destroy(model);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
