using System.Collections.Generic;
using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI.Map
{
    // The map circle in the corner (a zoomed piece of the illustrated map centred on the player) and the full map that opens
    // with M / gamepad Select / a click on the circle, with a "you are here" marker and the gift stops. Opening freezes the game
    // like the pause menu does. Layout is built by Assets/Editor/MapHudBuilder.cs.
    public class MapHUD : MonoBehaviour
    {
        [SerializeField] WorldMapData map;
        [SerializeField] MapRoute route;

        [Header("Map circle")]
        [SerializeField] RawImage minimapImage;
        [SerializeField] RectTransform minimapArrow;
        [SerializeField] Button minimapButton;
        [Tooltip("Width of the part of the picture shown inside the circle, as a fraction of the whole picture.")]
        [SerializeField] float minimapZoom = 0.2f;

        [Header("Full map")]
        [SerializeField] CanvasGroup panel;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform fullArrow;
        [SerializeField] Image[] giftPins;
        [SerializeField] Color pinPending = new(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] Color pinCollected = new(1f, 0.82f, 0.25f, 1f);

        [Header("Quest guidance")]
        [SerializeField] Color sonTinhGuideColor = new(1f, 0.48f, 0.12f, 0.95f);
        [SerializeField] Color thuyTinhGuideColor = new(0.20f, 0.78f, 1f, 0.95f);

        InputAction toggle;
        PlayerController player;
        GiftQuest quest;
        CharacterId characterId;
        ThirdPersonCameraInput cameraInput;
        PauseMenu pauseMenu;
        RectTransform objectivePanel;
        TMP_Text objectiveText;
        Image objectiveAccent;
        RectTransform minimapGuideLayer;
        RectTransform minimapGuideTarget;
        RectTransform fullGuideLayer;
        RectTransform fullGuideTarget;
        readonly List<RectTransform> minimapDashes = new();
        readonly List<RectTransform> fullMapDashes = new();
        readonly List<Vector2> roadMapPath = new();
        Vector2 objectiveMapPosition;
        Vector3 objectiveWorldPosition;
        bool hasObjectiveTarget;
        Color guideColor;
        float nextPathRefresh;

        public static bool IsOpen { get; private set; }

        // Frame in which the map was closed, so the pause menu does not also react to the same Esc press.
        public static int LastCloseFrame { get; private set; } = -1;

        void Awake()
        {
            toggle = new InputAction("Map", InputActionType.Button);
            toggle.AddBinding("<Keyboard>/m");
            toggle.AddBinding("<Gamepad>/select");

            EnsureGiftPinImages();
            EnsureGuidanceUI();
            SetPanelVisible(false);
            if (minimapButton != null) minimapButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        void EnsureGiftPinImages()
        {
            int required = route != null && route.GiftPins != null ? route.GiftPins.Count : 0;
            int current = giftPins != null ? giftPins.Length : 0;
            if (required <= current || current == 0 || giftPins[0] == null) return;

            Image template = giftPins[0];
            var expanded = new Image[required];
            for (int i = 0; i < current; i++) expanded[i] = giftPins[i];
            for (int i = current; i < required; i++)
            {
                expanded[i] = Instantiate(template, template.transform.parent);
                expanded[i].name = "Pin_" + i;
            }
            giftPins = expanded;
        }

        void OnEnable()
        {
            toggle.Enable();
            GiftTracker.Collected += OnGiftCollected;
            QuestQuizProgress.Changed += OnQuizUnlocked;
            HorseQuest.Changed += OnHorseQuestChanged;
            RoosterQuest.Changed += OnRoosterQuestChanged;
            SeaQuest.Changed += OnSeaQuestChanged;
        }

        void OnDisable()
        {
            toggle.Disable();
            GiftTracker.Collected -= OnGiftCollected;
            QuestQuizProgress.Changed -= OnQuizUnlocked;
            HorseQuest.Changed -= OnHorseQuestChanged;
            RoosterQuest.Changed -= OnRoosterQuestChanged;
            SeaQuest.Changed -= OnSeaQuestChanged;
        }

        void OnDestroy()
        {
            toggle.Dispose();
            if (IsOpen) { IsOpen = false; Time.timeScale = 1f; }
        }

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            IsOpen = false;
            LastCloseFrame = -1;
        }

        public void Bind(PlayerController target, CharacterDefinition character = null)
        {
            player = target;
            quest = character != null ? character.GiftQuest : null;
            characterId = character != null ? character.Id : CharacterId.SonTinh;
            guideColor = character != null && character.Id == CharacterId.ThuyTinh ? thuyTinhGuideColor : sonTinhGuideColor;
            ApplyGuideColor();
            RefreshObjective();
        }

        void Update()
        {
            if (IsOpen)
            {
                bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool back = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
                if (toggle.WasPressedThisFrame() || escape || back) Close();
            }
            else if (toggle.WasPressedThisFrame() && !QuestQuizUI.IsOpen)
            {
                Open();
            }
        }

        void LateUpdate()
        {
            if (player == null || route == null || map == null) return;
            if (!route.TryLocate(player.transform.position, player.transform.eulerAngles.y, out Vector2 position, out float angle)) return;

            UpdateCircle(position, angle);
            if (hasObjectiveTarget && Time.unscaledTime >= nextPathRefresh)
            {
                route.TryBuildRoadPath(player.transform.position, objectiveWorldPosition, roadMapPath);
                nextPathRefresh = Time.unscaledTime + 0.35f;
            }
            UpdateMinimapGuidance();
            if (IsOpen)
            {
                UpdateFullMap(position, angle);
                UpdateFullMapGuidance();
            }
        }

        void UpdateCircle(Vector2 position, float angle)
        {
            // Texture coordinates have v pointing up, the picture positions point down.
            Vector2 centre = new(position.x, 1f - position.y);
            float width = minimapZoom, height = width * map.Aspect;   // a square patch of the picture, whatever its aspect
            float x = Mathf.Clamp(centre.x - width * 0.5f, 0f, 1f - width);
            float y = Mathf.Clamp(centre.y - height * 0.5f, 0f, 1f - height);
            minimapImage.uvRect = new Rect(x, y, width, height);

            // The arrow sits where the player is inside that patch (the middle, except near the edges of the picture).
            Vector2 inside = new((centre.x - x) / width - 0.5f, (centre.y - y) / height - 0.5f);
            Vector2 size = minimapImage.rectTransform.rect.size;
            minimapArrow.anchoredPosition = new Vector2(inside.x * size.x, inside.y * size.y);
            minimapArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        void UpdateFullMap(Vector2 position, float angle)
        {
            fullArrow.anchorMin = fullArrow.anchorMax = new Vector2(position.x, 1f - position.y);
            fullArrow.anchoredPosition = Vector2.zero;
            fullArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            float pulse = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 4f);
            fullArrow.localScale = Vector3.one * pulse;

            if (giftPins == null) return;
            var pins = route.GiftPins;
            for (int i = 0; i < giftPins.Length && i < pins.Count; i++)
            {
                RectTransform pin = giftPins[i].rectTransform;
                pin.anchorMin = pin.anchorMax = new Vector2(pins[i].map.x, 1f - pins[i].map.y);
                pin.anchoredPosition = Vector2.zero;
                giftPins[i].color = GiftTracker.Has(pins[i].gift) ? pinCollected : pinPending;
            }
        }

        void Open()
        {
            if (IsOpen || SceneLoader.IsLoading || QuestQuizUI.IsOpen) return;
            if (pauseMenu == null) pauseMenu = Object.FindAnyObjectByType<PauseMenu>();
            if (pauseMenu != null && pauseMenu.IsPaused) return;

            IsOpen = true;
            Time.timeScale = 0f;
            SetPanelVisible(true);

            // Disabling the camera input also frees the cursor, so the map can be clicked to close.
            if (cameraInput == null) cameraInput = Object.FindAnyObjectByType<ThirdPersonCameraInput>();
            if (cameraInput != null) cameraInput.enabled = false;
        }

        void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            LastCloseFrame = Time.frameCount;
            Time.timeScale = 1f;
            SetPanelVisible(false);
            if (cameraInput != null) cameraInput.enabled = true;
        }

        void OnGiftCollected(GiftItem _) => RefreshObjective();

        void OnQuizUnlocked(GiftItem _) => RefreshObjective();

        void OnHorseQuestChanged(HorseQuestState _) => RefreshObjective();

        void OnRoosterQuestChanged(RoosterQuestState _) => RefreshObjective();

        void OnSeaQuestChanged() => RefreshObjective();

        void RefreshObjective()
        {
            hasObjectiveTarget = false;
            roadMapPath.Clear();
            if (objectiveText == null) return;

            SetObjectiveVisible(false);

            if (quest == null)
            {
                SetGuidanceVisible(false);
                return;
            }

            GiftItem next = QuestQuizProgress.NextGift(quest);

            if (next == null)
            {
                SetObjectiveVisible(true);
                objectiveText.text = "<size=20><b>ĐÃ ĐỦ SÍNH LỄ</b></size>\nTrở về cung điện Vua Hùng";
                SetGuidanceVisible(false);
                return;
            }

            // No mission card or route before the keeper has approved this gift.
            if (!QuestQuizProgress.IsUnlocked(next))
            {
                SetGuidanceVisible(false);
                return;
            }

            if (TrySetStoryObjective()) return;

            SetObjectiveVisible(true);
            objectiveText.text = $"<size=20><b>NHIỆM VỤ TIẾP THEO</b></size>\nTìm <b>{next.DisplayName}</b>\n<size=18>Theo đường gạch trên bản đồ</size>";
            hasObjectiveTarget = route != null && route.TryGetGiftPin(next, out objectiveMapPosition) && route.TryGetGiftWorld(next, out objectiveWorldPosition);
            nextPathRefresh = 0f;
            SetGuidanceVisible(hasObjectiveTarget);
        }

        bool TrySetStoryObjective()
        {
            if (characterId == CharacterId.SonTinh)
            {
                if (HorseQuest.State != HorseQuestState.Completed)
                {
                    switch (HorseQuest.State)
                    {
                        case HorseQuestState.NotStarted:
                            HorseQuestVillager keeper = Object.FindFirstObjectByType<HorseQuestVillager>(FindObjectsInactive.Include);
                            return keeper != null && SetWorldObjective("Gặp <b>người chăn ngựa</b> để hỏi về Ngựa Chín Hồng Mao", keeper.transform.position);
                        case HorseQuestState.Accepted:
                            HorseQuestDirector horseDirector = Object.FindFirstObjectByType<HorseQuestDirector>(FindObjectsInactive.Include);
                            return horseDirector != null && SetWorldObjective("Đuổi theo ngựa và <b>đánh bại bọn trộm</b>", horseDirector.ClearingPosition);
                        case HorseQuestState.Cleared:
                            HorseSummit summit = Object.FindFirstObjectByType<HorseSummit>(FindObjectsInactive.Include);
                            return summit != null && SetWorldObjective("Lên đỉnh gò, đi chậm và <b>rung chuông gọi ngựa</b>", summit.transform.position);
                        case HorseQuestState.Calmed:
                            HorseQuestVillager returningKeeper = Object.FindFirstObjectByType<HorseQuestVillager>(FindObjectsInactive.Include);
                            return returningKeeper != null && SetWorldObjective("Trở về báo tin cho <b>người chăn ngựa</b>", returningKeeper.transform.position);
                    }
                }

                if (RoosterQuest.State != RoosterQuestState.Completed)
                {
                    RoosterQuestDirector rooster = Object.FindFirstObjectByType<RoosterQuestDirector>(FindObjectsInactive.Include);
                    if (rooster == null) return false;
                    string task = RoosterQuest.State switch
                    {
                        RoosterQuestState.NotStarted => "Đến <b>vòng đá</b> tìm Gà Chín Cựa",
                        RoosterQuestState.Fighting => "<b>Đánh bại bọn trộm</b> đang bắt Gà Chín Cựa",
                        _ => "Đến gần và nhấn <b>E</b> để bắt Gà Chín Cựa"
                    };
                    return !string.IsNullOrEmpty(task) && SetWorldObjective(task, rooster.ObjectivePosition);
                }
                return false;
            }

            if (SeaQuest.State == SeaQuestState.NotStarted)
            {
                SeaNpc fisherman = FindSeaNpc(SeaNpc.Role.Fisherman);
                return fisherman != null && SetWorldObjective("Gặp <b>lão ngư ở bến thuyền</b> để hỏi đường", fisherman.transform.position);
            }
            if (SeaQuest.State == SeaQuestState.Accepted)
            {
                SeaFoeDirector pirates = FindSeaFoes(SeaFoeDirector.Kind.Pirates);
                return pirates != null && SetWorldObjective("Dẹp <b>bọn ngư tặc ở Bến Thuyền</b>", pirates.ObjectivePosition);
            }
            if (!SeaQuest.ElderMet)
            {
                SeaNpc elder = FindSeaNpc(SeaNpc.Role.Elder);
                return elder != null && SetWorldObjective("Sang Làng Chài gặp <b>trưởng làng</b>", elder.transform.position);
            }
            if (!SeaQuest.GhostsCleared)
            {
                SeaFoeDirector ghosts = FindSeaFoes(SeaFoeDirector.Kind.Ghosts);
                return ghosts != null && SetWorldObjective("Lên hải đăng và <b>đuổi ma nước</b>", ghosts.ObjectivePosition);
            }
            if (!SeaQuest.LampLit)
            {
                SeaLampBeacon lamp = Object.FindFirstObjectByType<SeaLampBeacon>(FindObjectsInactive.Include);
                return lamp != null && SetWorldObjective("Đến chân tháp và <b>thắp lại hải đăng</b> (E)", lamp.transform.position);
            }
            return false;
        }

        bool SetWorldObjective(string task, Vector3 worldPosition)
        {
            SetObjectiveVisible(true);
            objectiveText.text = $"<size=20><b>NHIỆM VỤ TIẾP THEO</b></size>\n{task}\n<size=18>Theo đường gạch trên bản đồ</size>";
            objectiveWorldPosition = worldPosition;
            hasObjectiveTarget = route != null && route.TryLocate(worldPosition, 0f, out objectiveMapPosition, out _);
            nextPathRefresh = 0f;
            SetGuidanceVisible(hasObjectiveTarget);
            return true;
        }

        static SeaNpc FindSeaNpc(SeaNpc.Role role)
        {
            foreach (SeaNpc npc in Object.FindObjectsByType<SeaNpc>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (npc.NpcRole == role) return npc;
            return null;
        }

        static SeaFoeDirector FindSeaFoes(SeaFoeDirector.Kind kind)
        {
            foreach (SeaFoeDirector group in Object.FindObjectsByType<SeaFoeDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (group.GroupKind == kind) return group;
            return null;
        }

        void EnsureGuidanceUI()
        {
            guideColor = sonTinhGuideColor;

            objectivePanel = NewUiRect("QuestGuidance", transform);
            objectivePanel.anchorMin = objectivePanel.anchorMax = new Vector2(0f, 0.5f);
            objectivePanel.pivot = new Vector2(0f, 0.5f);
            objectivePanel.anchoredPosition = new Vector2(36f, 0f);
            objectivePanel.sizeDelta = new Vector2(410f, 142f);
            Image background = objectivePanel.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.055f, 0.88f);
            background.raycastTarget = false;

            RectTransform accent = NewUiRect("Accent", objectivePanel);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(7f, 0f);
            accent.anchoredPosition = Vector2.zero;
            objectiveAccent = accent.gameObject.AddComponent<Image>();
            objectiveAccent.raycastTarget = false;

            RectTransform textRect = NewUiRect("Text", objectivePanel);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 12f);
            textRect.offsetMax = new Vector2(-16f, -12f);
            objectiveText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            objectiveText.font = TMP_Settings.defaultFontAsset;
            objectiveText.fontSize = 25f;
            objectiveText.color = Color.white;
            objectiveText.alignment = TextAlignmentOptions.MidlineLeft;
            objectiveText.textWrappingMode = TextWrappingModes.Normal;
            objectiveText.raycastTarget = false;

            Transform minimapLayer = minimapImage != null ? minimapImage.transform.parent : null;
            if (minimapLayer != null)
            {
                minimapGuideLayer = NewUiRect("QuestRouteDashes", minimapLayer);
                Stretch(minimapGuideLayer);
                minimapGuideTarget = NewGuidePiece("QuestTarget", minimapLayer, new Vector2(18f, 18f));
                minimapGuideTarget.localRotation = Quaternion.Euler(0f, 0f, 45f);
                minimapArrow.SetAsLastSibling();
            }

            Transform fullLayer = fullArrow != null ? fullArrow.parent : null;
            if (fullLayer != null)
            {
                fullGuideLayer = NewUiRect("QuestRouteDashes", fullLayer);
                Stretch(fullGuideLayer);
                fullGuideTarget = NewGuidePiece("QuestTarget", fullLayer, new Vector2(22f, 22f));
                fullGuideTarget.localRotation = Quaternion.Euler(0f, 0f, 45f);
                fullGuideLayer.SetAsLastSibling();
                fullGuideTarget.SetAsLastSibling();
                fullArrow.SetAsLastSibling();
            }

            ApplyGuideColor();
            SetObjectiveVisible(false);
            SetGuidanceVisible(false);
        }

        void ApplyGuideColor()
        {
            if (objectiveAccent != null) objectiveAccent.color = guideColor;
            Color dashColor = new(guideColor.r, guideColor.g, guideColor.b, 0.88f);
            foreach (RectTransform dash in minimapDashes) Tint(dash, dashColor);
            foreach (RectTransform dash in fullMapDashes) Tint(dash, dashColor);
            Tint(minimapGuideTarget, guideColor);
            Tint(fullGuideTarget, guideColor);
        }

        void UpdateMinimapGuidance()
        {
            if (!hasObjectiveTarget || minimapGuideLayer == null || minimapImage == null) return;

            Rect uv = minimapImage.uvRect;
            Vector2 size = minimapImage.rectTransform.rect.size;
            var points = new List<Vector2>(roadMapPath.Count);
            foreach (Vector2 point in roadMapPath) points.Add(MapToMinimapPoint(point, uv, size));
            Rect clip = new(-size.x * 0.5f, -size.y * 0.5f, size.x, size.y);
            DrawDashedPath(minimapDashes, minimapGuideLayer, points, 12f, 8f, 6f, clip);

            Vector2 targetPoint = MapToMinimapPoint(objectiveMapPosition, uv, size);
            float radius = Mathf.Min(size.x, size.y) * 0.43f;
            bool targetVisible = targetPoint.magnitude <= radius;
            minimapGuideTarget.gameObject.SetActive(targetVisible);
            if (targetVisible)
            {
                minimapGuideTarget.anchoredPosition = targetPoint;
                PulseTarget(minimapGuideTarget, 0.16f);
            }
        }

        void UpdateFullMapGuidance()
        {
            if (!hasObjectiveTarget || fullGuideLayer == null || fullGuideLayer.parent is not RectTransform mapRect) return;
            Vector2 size = mapRect.rect.size;
            var points = new List<Vector2>(roadMapPath.Count);
            foreach (Vector2 point in roadMapPath) points.Add(MapToFullPoint(point, size));
            DrawDashedPath(fullMapDashes, fullGuideLayer, points, 18f, 11f, 7f, null);
            Vector2 end = MapToFullPoint(objectiveMapPosition, size);
            fullGuideTarget.anchoredPosition = end;
            PulseTarget(fullGuideTarget, 0.22f);
        }

        void SetGuidanceVisible(bool visible)
        {
            if (minimapGuideLayer != null) minimapGuideLayer.gameObject.SetActive(visible);
            if (minimapGuideTarget != null) minimapGuideTarget.gameObject.SetActive(visible);
            if (fullGuideLayer != null) fullGuideLayer.gameObject.SetActive(visible);
            if (fullGuideTarget != null) fullGuideTarget.gameObject.SetActive(visible);
        }

        void SetObjectiveVisible(bool visible)
        {
            if (objectivePanel != null) objectivePanel.gameObject.SetActive(visible);
        }

        static Vector2 MapToMinimapPoint(Vector2 mapPoint, Rect uv, Vector2 size)
        {
            Vector2 texturePoint = new(mapPoint.x, 1f - mapPoint.y);
            return new Vector2(((texturePoint.x - uv.x) / uv.width - 0.5f) * size.x,
                ((texturePoint.y - uv.y) / uv.height - 0.5f) * size.y);
        }

        static Vector2 MapToFullPoint(Vector2 mapPoint, Vector2 size) =>
            new((mapPoint.x - 0.5f) * size.x, (0.5f - mapPoint.y) * size.y);

        void DrawDashedPath(List<RectTransform> pool, RectTransform parent, List<Vector2> points,
            float dashLength, float gapLength, float thickness, Rect? clip)
        {
            int used = 0;
            for (int segment = 0; segment + 1 < points.Count; segment++)
            {
                Vector2 a = points[segment], b = points[segment + 1];
                if (clip.HasValue && !ClipLine(clip.Value, ref a, ref b)) continue;
                Vector2 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.5f) continue;
                Vector2 direction = delta / length;
                for (float along = 0f; along < length; along += dashLength + gapLength)
                {
                    float visibleLength = Mathf.Min(dashLength, length - along);
                    if (visibleLength < 1f) continue;
                    RectTransform dash = GetDash(pool, parent, used++);
                    Vector2 start = a + direction * along;
                    SetDash(dash, start, start + direction * visibleLength, thickness);
                }
            }
            for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        RectTransform GetDash(List<RectTransform> pool, RectTransform parent, int index)
        {
            if (index < pool.Count)
            {
                pool[index].gameObject.SetActive(true);
                return pool[index];
            }
            RectTransform dash = NewGuidePiece("Dash_" + index, parent, new Vector2(10f, 6f));
            Tint(dash, new Color(guideColor.r, guideColor.g, guideColor.b, 0.88f));
            pool.Add(dash);
            return dash;
        }

        static void SetDash(RectTransform dash, Vector2 start, Vector2 end, float thickness)
        {
            Vector2 delta = end - start;
            dash.anchorMin = dash.anchorMax = new Vector2(0.5f, 0.5f);
            dash.anchoredPosition = (start + end) * 0.5f;
            dash.sizeDelta = new Vector2(delta.magnitude, thickness);
            dash.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        static bool ClipLine(Rect rect, ref Vector2 a, ref Vector2 b)
        {
            Vector2 delta = b - a;
            float t0 = 0f, t1 = 1f;
            if (!ClipTest(-delta.x, a.x - rect.xMin, ref t0, ref t1) ||
                !ClipTest(delta.x, rect.xMax - a.x, ref t0, ref t1) ||
                !ClipTest(-delta.y, a.y - rect.yMin, ref t0, ref t1) ||
                !ClipTest(delta.y, rect.yMax - a.y, ref t0, ref t1)) return false;
            Vector2 original = a;
            a = original + delta * t0;
            b = original + delta * t1;
            return true;
        }

        static bool ClipTest(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Approximately(p, 0f)) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        static void PulseTarget(RectTransform target, float amount)
        {
            float scale = 1f + amount * Mathf.Sin(Time.unscaledTime * 5f);
            target.localScale = Vector3.one * scale;
        }

        static RectTransform NewUiRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        RectTransform NewGuidePiece(string name, Transform parent, Vector2 size)
        {
            RectTransform rect = NewUiRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return rect;
        }

        static void Tint(RectTransform rect, Color color)
        {
            if (rect != null && rect.TryGetComponent(out Image image)) image.color = color;
        }

        void SetPanelVisible(bool visible)
        {
            if (panel == null) return;
            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
            panel.interactable = visible;
        }
    }
}
