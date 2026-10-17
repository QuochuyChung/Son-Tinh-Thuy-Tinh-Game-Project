using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.Player
{
    // Fading copies of the character's own pose left behind while it moves fast: the dodge's after-images. Every copy is a snapshot baked
    // out of the skinned mesh at that moment (so it holds the roll pose exactly), wearing clones of the actor's own materials so the trail
    // reads as the character rather than as one flat colour, and faded to nothing. That lets the eye follow the path, which is what makes
    // a dash read as speed instead of as a short teleport.
    //
    // Added to the player by PlayerController.Start when the dodge has a spacing set. The dodge state calls Emit() as it covers distance.
    [DisallowMultipleComponent]
    public sealed class AfterImageTrail : MonoBehaviour
    {
        [Tooltip("Opacity of a fresh copy (0 = invisible, 1 = solid). The colours always come from the actor's own materials.")]
        [SerializeField, Range(0f, 1f)] float peakAlpha = 0.75f;
        [Tooltip("How far the copy's colours are washed out toward the background. 0 keeps the actor's colours at full strength; " +
                 "1 fades them all the way out. Driven through opacity, so it reads as a fade rather than a flat tint.")]
        [SerializeField, Range(0f, 1f)] float wash = 0.35f;
        [Tooltip("Seconds a copy is held at full opacity before it starts to fade.")]
        [SerializeField, Min(0.05f)] float lifetime = 0.3f;
        [Tooltip("Seconds the fade out takes.")]
        [SerializeField, Min(0.05f)] float fadeSeconds = 0.2f;
        [Tooltip("Scale multiplier for the copies, if they should read slightly smaller than the character.")]
        [SerializeField, Range(0.1f, 2f)] float ghostScale = 1f;
        [Tooltip("Snapshot only the largest skinned mesh (the body). The cape and skirt are driven by spring bones, so baking them " +
                 "catches a flailed pose that makes each copy read far larger than the character.")]
        [SerializeField] bool bodyOnly = true;

        // False suppresses the copies without removing the component, so the effect can be switched off from outside (a cutscene, a
        // status effect, a design toggle) while the dodge keeps calling Emit(). Emit() itself is still gated by the dodge state.
        public bool IsLive { get; set; } = true;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        SkinnedMeshRenderer[] sources;
        bool warnedNoSources;
        int emitted;

        // Every skinned mesh under the character, so the copies match whatever the rig actually is (a Meshy body, a nested model prefab).
        void Awake() => sources = GetComponentsInChildren<SkinnedMeshRenderer>(true);

        // Picks the body mesh. The rig carries three (body, cape, skirt); the cape and skirt are spring-bone driven, so baking them
        // catches a flailed pose. Filtering to the body keeps each copy the size and shape of the character.
        IEnumerable<SkinnedMeshRenderer> Sources()
        {
            if (!bodyOnly || sources == null || sources.Length <= 1) return sources;

            SkinnedMeshRenderer body = null;
            int mostVertices = -1;
            foreach (SkinnedMeshRenderer candidate in sources)
            {
                if (candidate == null || candidate.sharedMesh == null) continue;
                int vertices = candidate.sharedMesh.vertexCount;
                if (vertices <= mostVertices) continue;
                mostVertices = vertices;
                body = candidate;
            }

            return body == null ? sources : new[] { body };
        }

        // Snapshots the character's current pose.
        public void Emit()
        {
            if (!IsLive) return;
            if (sources == null || sources.Length == 0) sources = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (sources == null) return;

            foreach (SkinnedMeshRenderer source in Sources())
            {
                if (source == null || source.sharedMesh == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;

                // A fresh snapshot every call: the pose has moved on, and a shared mesh would be re-baked over copies still fading.
                var mesh = new Mesh { name = "AfterImageGhost" };
                // Meshy bodies are dense; a snapshot that overflows 16-bit indices bakes into an empty, invisible mesh.
                mesh.indexFormat = IndexFormat.UInt32;
                // BakeMesh with useScale bakes the renderer's own local scale, which is 1 here - the ~2.05 that makes the character its
                // visible size lives on the model instance ancestor, not on the renderer. The copy is parented to nothing, so its
                // localScale IS its world scale and has to be given the full lossyScale.
                source.BakeMesh(mesh, true);

                // BakeMesh carries the pose but NOT normals, and the copies keep the actor's own URP Lit materials - a lit shader with no
                // normals has nothing to shade against, so every fragment comes out black and the texture is unreadable. Normals have to be
                // rebuilt off the snapshot, or the copies cannot mimic the actor's look at all.
                EnsureNormals(mesh);

                // The actor's own materials, cloned so every copy can fade on its own. This is what makes the trail take the character's
                // colours instead of a fixed tint.
                Material[] materials = ClonesFor(source.sharedMaterials);
                if (materials.Length == 0)
                {
                    Destroy(mesh);
                    continue;
                }

                var copy = new GameObject("AfterImageGhost");
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale * ghostScale;

                copy.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                emitted++;

                StartCoroutine(Fade(copy, renderer, materials, mesh));
            }

            if (emitted == 0 && !warnedNoSources)
            {
                warnedNoSources = true;
                Debug.LogWarning("[AfterImageTrail] No skinned mesh was snapshot - the after-images will not show. " +
                                 "Check that the character body imports as a SkinnedMeshRenderer.", this);
            }
        }

        // BakeMesh writes positions but leaves the normal stream empty, so a lit copy has no shading and reads as flat black. Rebuilding
        // them off the snapshot's own triangles gives the copies real normals, which is what lets the actor's own URP Lit materials light
        // them the same way they light the character. Skinned normals are technically the transformed bone normals rather than a plain
        // recalculate, but for a trail that lives a third of a second the difference is not visible.
        static void EnsureNormals(Mesh mesh)
        {
            if (mesh.normals != null && mesh.normals.Length == mesh.vertexCount) return;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // Character materials are opaque, and an opaque copy would sit in front of the actor as a solid double. Each clone is switched to
        // transparent - the actor's own look kept, only its opacity made drivable.
        static Material[] CloneTransparent(Material[] originals)
        {
            if (originals == null || originals.Length == 0) return System.Array.Empty<Material>();

            var clones = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                if (originals[i] == null) continue;
                clones[i] = new Material(originals[i]) { name = originals[i].name + " (AfterImage)" };
                MakeTransparent(clones[i]);
            }
            return clones;
        }

        Material[] ClonesFor(Material[] originals)
        {
            Material[] clones = CloneTransparent(originals);
            if (wash > 0f)
                foreach (Material clone in clones)
                    if (clone != null) ApplyWash(clone);
            return clones;
        }

        // The actor's materials are URP Lit, opaque, and their _Surface/_Blend live in a shader GUI block, so setting them via HasProperty
        // finds nothing - the keywords and render queue have to be set directly.
        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int BlendId = Shader.PropertyToID("_Blend");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // The wash is applied as a pull on opacity: 0 keeps the actor's colours at full strength, 1 fades them all the way out.
        float EffectiveAlpha(float alpha) => alpha * (1f - wash);

        // Fades the copy's colours toward white. The base colour is already white on these materials, so raising it toward white cannot
        // brighten the texture - the fade is done by raising opacity instead (see SetAlpha), which is what blends the copy toward the
        // background. Emission was tried here first and is deliberately not used: it adds unblended light, and its alpha tints that
        // addition, which turned the copies into flat cyan silhouettes that lost the actor's colours entirely.
        void ApplyWash(Material material)
        {
            if (material.HasProperty(EmissionColorId)) material.SetColor(EmissionColorId, Color.black);
            material.DisableKeyword("_EMISSION");
        }

        static void MakeTransparent(Material material)
        {
            if (material.HasProperty(SurfaceId)) material.SetFloat(SurfaceId, 1f);   // 1 = Transparent
            if (material.HasProperty(BlendId)) material.SetFloat(BlendId, 0f);       // 0 = Alpha
            if (material.HasProperty(SrcBlendId)) material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            if (material.HasProperty(DstBlendId)) material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty(ZWriteId)) material.SetFloat(ZWriteId, 0f);
            if (material.HasProperty(AlphaClipId)) material.SetFloat(AlphaClipId, 0f);

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        static Color ReadColour(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return Color.white;
        }

        // Drains opacity only, so the copies keep the actor's colours all the way down.
        static void SetAlpha(Material[] materials, Color[] palette, float alpha)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;

                Color value = palette[i];
                value.a = alpha;

                // _BaseColor and _Color are aliases in URP Lit, so these must be exclusive - writing both lets the second
                // clobber the first and the alpha never survives, which rendered every copy fully opaque.
                if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, value);
                else if (material.HasProperty(ColorId)) material.SetColor(ColorId, value);
            }
        }

        IEnumerator Fade(GameObject copy, Renderer renderer, Material[] materials, Mesh mesh)
        {
            // Read the colours back off the clones before anything is faded, so the fade can restore them rather than compound.
            var palette = new Color[materials.Length];
            for (int i = 0; i < materials.Length; i++) palette[i] = ReadColour(materials[i]);
            SetAlpha(materials, palette, EffectiveAlpha(peakAlpha));

            float hold = lifetime - fadeSeconds;
            if (hold > 0f) yield return new WaitForSeconds(hold);

            float elapsed = 0f;
            while (elapsed < fadeSeconds && copy != null && renderer != null)
            {
                elapsed += Time.deltaTime;
                SetAlpha(materials, palette, EffectiveAlpha(Mathf.Lerp(peakAlpha, 0f, Mathf.Clamp01(elapsed / fadeSeconds))));
                yield return null;
            }

            if (copy != null) Destroy(copy);
            foreach (Material material in materials)
                if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
