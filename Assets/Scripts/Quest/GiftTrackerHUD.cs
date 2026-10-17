using System.Collections;
using System.Text;
using SonTinhThuyTinh.UI;
using TMPro;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Checklist of the quest's gifts, plus a short banner each time one is collected.
    public class GiftTrackerHUD : MonoBehaviour
    {
        [SerializeField] GiftQuest quest;
        [SerializeField] TMP_Text checklistText;
        [SerializeField] CanvasGroup banner;
        [SerializeField] TMP_Text bannerText;
        [SerializeField] float bannerDuration = 2.5f;
        [SerializeField] Color collectedColor = new(0.56f, 0.86f, 0.46f);
        [SerializeField] Color pendingColor = new(0.85f, 0.85f, 0.85f);

        Coroutine bannerRoutine;

        void Awake()
        {
            // the health-bar look (gold frame, dark blue track) with rounded corners, on the checklist and on the banner
            if (checklistText != null && checklistText.transform.parent is RectTransform list)
                UiFrame.Apply(list, new Color(0.03f, 0.05f, 0.12f, 0.92f), new Color(0.10f, 0.14f, 0.28f, 0.92f));
            if (bannerText != null && bannerText.transform.parent is RectTransform note)
                UiFrame.Apply(note, new Color(0.16f, 0.03f, 0.03f, 0.94f), new Color(0.36f, 0.08f, 0.06f, 0.94f));
        }

        void OnEnable()
        {
            GiftTracker.Collected += OnCollected;
            GiftTracker.Notice += ShowNotice;
            banner.alpha = 0f;
            Refresh();
        }

        void OnDisable()
        {
            GiftTracker.Collected -= OnCollected;
            GiftTracker.Notice -= ShowNotice;
        }

        // The spawned character's own gift list (PlayerSpawner), in place of the one set in the scene.
        public void SetQuest(GiftQuest characterQuest)
        {
            if (characterQuest == null) return;
            quest = characterQuest;
            if (isActiveAndEnabled) Refresh();
        }

        void ShowNotice(string message)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(ShowBanner(message));
        }

        void OnCollected(GiftItem gift)
        {
            Refresh();

            string message = string.IsNullOrEmpty(gift.ReceivedMessage)
                ? $"Đã có <b>{gift.DisplayName}</b>"
                : string.Format(gift.ReceivedMessage, $"<b>{gift.DisplayName}</b>");
            if (quest.IsComplete) message += "\nĐã đủ sính lễ!";

            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(ShowBanner(message));
        }

        void Refresh()
        {
            string collectedHex = ColorUtility.ToHtmlStringRGB(collectedColor);
            string pendingHex = ColorUtility.ToHtmlStringRGB(pendingColor);

            var builder = new StringBuilder();
            builder.Append("<b>").Append(quest.Title).Append("</b>  ")
                .Append(quest.CollectedCount).Append('/').Append(quest.RequiredGifts.Count);

            foreach (GiftItem gift in quest.RequiredGifts)
            {
                bool has = GiftTracker.Has(gift);
                builder.Append("\n<color=#").Append(has ? collectedHex : pendingHex).Append('>')
                    .Append(gift.DisplayName).Append(has ? "  (đã có)" : "")
                    .Append("</color>");
            }

            checklistText.text = builder.ToString();
        }

        IEnumerator ShowBanner(string message)
        {
            bannerText.text = message;
            yield return CanvasGroupFade.Run(banner, banner.alpha, 1f, 0.2f);
            yield return new WaitForSeconds(bannerDuration);
            yield return CanvasGroupFade.Run(banner, 1f, 0f, 0.5f);
            bannerRoutine = null;
        }
    }
}
