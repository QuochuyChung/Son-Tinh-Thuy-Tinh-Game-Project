using SonTinhThuyTinh.Characters;
using TMPro;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // One keeper is spawned beside the entry point of each gift-branch map.
    // Interacting starts a fresh three-question attempt for the next uncollected gift.
    [RequireComponent(typeof(Interactable), typeof(CapsuleCollider))]
    public sealed class QuestKeeperNpc : MonoBehaviour
    {
        GiftQuest quest;
        Transform body;
        int giftIndex = -1;   // the gift this keeper asks about (QuestKeeperMark); -1 = one keeper for all of them

        // One keeper per QuestKeeperMark of the scene (one at the start of each leg); a scene without marks gets a single keeper beside
        // the entry point, which asks about whichever gift is next.
        public static QuestKeeperNpc Spawn(Transform entrance, GiftQuest giftQuest, CharacterId character, Player.PlayerController player)
        {
            QuestKeeperNpc existing = FindFirstObjectByType<QuestKeeperNpc>();
            if (existing != null) return existing;

            QuestKeeperMark[] marks = FindObjectsByType<QuestKeeperMark>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (marks.Length == 0)
            {
                Vector3 position = entrance.position + entrance.right * 4.2f + entrance.forward * 2.2f;
                return Create(position, Quaternion.LookRotation(-entrance.right, Vector3.up), -1, giftQuest, character, player);
            }

            QuestKeeperNpc first = null;
            System.Array.Sort(marks, (a, b) => a.giftIndex.CompareTo(b.giftIndex));
            foreach (QuestKeeperMark mark in marks)
            {
                QuestKeeperNpc npc = Create(mark.transform.position, mark.transform.rotation, mark.giftIndex, giftQuest, character, player);
                if (first == null) first = npc;
            }
            return first;
        }

        static QuestKeeperNpc Create(Vector3 position, Quaternion rotation, int index, GiftQuest giftQuest, CharacterId character, Player.PlayerController player)
        {
            var root = new GameObject(index < 0 ? "NPC_NguoiGiuNhiemVu" : "NPC_NguoiGiuNhiemVu_" + (index + 1));
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y;   // marks are already on the ground: only settle it, never climb onto a roof or a branch
            root.transform.SetPositionAndRotation(position, rotation);

            var npc = root.AddComponent<QuestKeeperNpc>();
            npc.quest = giftQuest;
            npc.giftIndex = index;
            float targetHeight = 1.7f;
            if (player != null && player.TryGetComponent(out CharacterController controller))
                targetHeight = controller.height * Mathf.Abs(player.transform.lossyScale.y) * 0.92f;
            npc.BuildVisual(character, targetHeight);
            npc.ResizeCollider(targetHeight);
            return npc;
        }

        void Awake()
        {
            Interactable interactable = GetComponent<Interactable>();
            interactable.Prompt = "Nhấn E để hỏi nhiệm vụ kế tiếp";
            interactable.Interacted.AddListener(Talk);
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.42f;
        }

        void Talk()
        {
            FacePlayer();
            GiftItem next = QuestQuizProgress.NextGift(quest);
            if (next == null)
            {
                GiftTracker.Announce("Bạn đã thu thập đủ sính lễ. Hãy trở về cung điện Vua Hùng!");
                return;
            }

            // a keeper of one leg only asks about its own gift
            if (giftIndex >= 0 && giftIndex < quest.RequiredGifts.Count)
            {
                GiftItem mine = quest.RequiredGifts[giftIndex];
                if (GiftTracker.Has(mine))
                {
                    GiftTracker.Announce($"Bạn đã có <b>{mine.DisplayName}</b> rồi\nHãy tới gặp Người Giữ Nhiệm Vụ ở chặng kế tiếp để hỏi về <b>{next.DisplayName}</b>");
                    return;
                }
                if (next != mine)
                {
                    GiftTracker.Announce($"Hãy lấy <b>{next.DisplayName}</b> trước đã\nRồi mới tới hỏi chặng này");
                    return;
                }
            }
            if (QuestQuizProgress.IsUnlocked(next))
            {
                GiftTracker.Announce($"Nhiệm vụ hiện tại: tìm <b>{next.DisplayName}</b>\nHãy đi theo đường gạch trên bản đồ");
                return;
            }
            QuestQuizUI.Show(next);
        }

        void FacePlayer()
        {
            var player = FindAnyObjectByType<Player.PlayerController>();
            if (player == null || body == null) return;
            Vector3 direction = player.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(direction);
        }

        void BuildVisual(CharacterId character, float targetHeight)
        {
            GameObject prefab = Resources.Load<GameObject>("QuestNPC/Models/QuestKeeper_WanderingSage");
            if (prefab != null)
            {
                GameObject model = Instantiate(prefab, transform);
                model.name = "QuestKeeper_WanderingSage_Model";
                // This Meshy FBX keeps its body length on local +Z in Unity. Map +Z to
                // Unity's +Y before measuring it; otherwise the narrow body width is
                // mistaken for its height and NormalizeModel scales it up excessively.
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
                ApplySageMaterial(model);
                NormalizeModel(model.transform, targetHeight);
                body = model.transform;
                BuildNameLabel(targetHeight);
                return;
            }

            // Safe fallback while Unity is still importing the FBX for the first time.
            Color robe = character == CharacterId.ThuyTinh
                ? new Color(0.08f, 0.48f, 0.68f)
                : new Color(0.62f, 0.27f, 0.08f);
            Color trim = new Color(0.96f, 0.72f, 0.22f);
            Material robeMaterial = MakeMaterial(robe);
            Material trimMaterial = MakeMaterial(trim);
            Material skinMaterial = MakeMaterial(new Color(0.78f, 0.58f, 0.42f));

            body = Primitive("Body", PrimitiveType.Capsule, transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.72f, 0.82f, 0.72f), robeMaterial).transform;
            Primitive("Head", PrimitiveType.Sphere, transform, new Vector3(0f, 2.02f, 0f), Vector3.one * 0.52f, skinMaterial);
            Primitive("Hat", PrimitiveType.Cylinder, transform, new Vector3(0f, 2.38f, 0f), new Vector3(0.58f, 0.07f, 0.58f), trimMaterial);
            Primitive("Sash", PrimitiveType.Cube, transform, new Vector3(0f, 1.16f, -0.36f), new Vector3(0.82f, 0.12f, 0.08f), trimMaterial);
            GameObject staff = Primitive("Staff", PrimitiveType.Cylinder, transform, new Vector3(0.62f, 1.05f, 0f), new Vector3(0.07f, 1.1f, 0.07f), trimMaterial);
            staff.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

            float fallbackScale = targetHeight / 2.55f;
            foreach (Transform child in transform)
                if (child.name != "Name")
                {
                    child.localPosition *= fallbackScale;
                    child.localScale *= fallbackScale;
                }
            BuildNameLabel(targetHeight);
        }

        void BuildNameLabel(float characterHeight)
        {
            var labelRoot = new GameObject("Name", typeof(RectTransform), typeof(Canvas));
            labelRoot.transform.SetParent(transform, false);
            labelRoot.transform.localPosition = new Vector3(0f, characterHeight + 0.42f, 0f);
            var canvas = labelRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = labelRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 65f);
            rect.localScale = Vector3.one * 0.006f;
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(labelRoot.transform, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.text = "NGƯỜI GIỮ NHIỆM VỤ";
            text.fontSize = 34f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.86f, 0.4f);
            text.outlineWidth = 0.18f;
        }

        void ResizeCollider(float characterHeight)
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            capsule.height = characterHeight;
            capsule.center = new Vector3(0f, characterHeight * 0.5f, 0f);
            capsule.radius = Mathf.Min(0.36f, characterHeight * 0.22f);
        }

        static void NormalizeModel(Transform model, float targetHeight)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.001f) model.localScale *= targetHeight / bounds.size.y;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 root = model.parent.position;
            model.position += new Vector3(root.x - bounds.center.x, root.y - bounds.min.y, root.z - bounds.center.z);
        }

        static void ApplySageMaterial(GameObject model)
        {
            Texture2D baseColor = Resources.Load<Texture2D>("QuestNPC/Textures/QuestKeeper_WanderingSage_BaseColor");
            Texture2D normal = Resources.Load<Texture2D>("QuestNPC/Textures/QuestKeeper_WanderingSage_Normal");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            var material = new Material(shader) { name = "M_QuestKeeper_WanderingSage_Runtime" };
            if (baseColor != null)
            {
                material.SetTexture("_BaseMap", baseColor);
                material.SetTexture("_MainTex", baseColor);
            }
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.22f);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    continue;
                }
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        void LateUpdate()
        {
            Transform label = transform.Find("Name");
            Camera camera = Camera.main;
            // A world-space Canvas renders its front opposite Transform.forward.
            if (label != null && camera != null) label.rotation = Quaternion.LookRotation(label.position - camera.transform.position);
        }

        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return part;
        }

        static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            return material;
        }
    }
}
