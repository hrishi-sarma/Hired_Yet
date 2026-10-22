using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Items
{
    // "Now playing" card in the game's cartoon paper style (cream card, thick ink outline, hard shadow, orange tape).
    // Pops in with a bounce when a tune starts, holds, then slides down and fades.
    public class GuitarSongTitleUI
    {
        GameObject root;
        CanvasGroup group;
        RectTransform card;
        TextMeshProUGUI title;
        float hold, fade, t = -1f;
        const float PopTime = 0.28f, BaseY = 150f, Tilt = -1.5f;

        void Build()
        {
            root = new GameObject("GuitarSongTitle", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Object.DontDestroyOnLoad(root);
            var c = root.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 95;
            var sc = root.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            group = root.GetComponent<CanvasGroup>(); group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

            var font = InteractionSystem.Instance != null && InteractionSystem.Instance.font != null ? InteractionSystem.Instance.font : TMP_Settings.defaultFontAsset;

            card = UIKit.NewRect("Card", root.transform);
            UIKit.Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BaseY), new Vector2(640f, 112f));
            UIKit.Boxy(UIKit.AddImage(card, UIKit.Rounded(18), UIKit.Paper), 4f, true);
            UIKit.Tape(card, -250f, -8f);
            UIKit.Tape(card, 250f, 7f);

            // orange label chip straddling the top edge
            var chip = UIKit.NewRect("Chip", card);
            UIKit.Place(chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(220f, 36f));
            UIKit.Boxy(UIKit.AddImage(chip, UIKit.Rounded(12), UIKit.Hex("#FF9E2E")), 3f, false);
            var lbl = UIKit.AddText(chip, "Label", "NOW PLAYING", 20, UIKit.Dark, TextAlignmentOptions.Center, font, FontStyles.Bold, 3f);
            UIKit.Stretch(lbl.rectTransform);

            title = UIKit.AddText(card, "Title", "", 40, UIKit.Dark, TextAlignmentOptions.Center, font, FontStyles.Bold);
            UIKit.Stretch(title.rectTransform, 28f, 28f, 26f, 8f);
            title.enableAutoSizing = true; title.fontSizeMin = 22f; title.fontSizeMax = 42f;
            title.enableWordWrapping = false; title.overflowMode = TextOverflowModes.Ellipsis;
            card.localRotation = Quaternion.Euler(0f, 0f, Tilt);
        }

        static float OutBack(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            return 1f + 2.70158f * x * x * x + 1.70158f * x * x;
        }

        public void Show(string s, float holdSecs, float fadeSecs)
        {
            if (root == null) Build();
            title.text = s; hold = holdSecs; fade = Mathf.Max(0.01f, fadeSecs); t = 0f; group.alpha = 1f;
            Pose(0f, 1f);
        }

        void Pose(float pop, float outK)
        {
            float k = OutBack(pop);
            card.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, k);
            card.anchoredPosition = new Vector2(0f, BaseY + (1f - outK) * -40f + (1f - Mathf.Clamp01(pop)) * -30f);
            card.localRotation = Quaternion.Euler(0f, 0f, Tilt * Mathf.Lerp(3f, 1f, k));
        }

        public void Tick(float dt)
        {
            if (t < 0f || group == null) return;
            t += dt;
            if (t > hold + fade) { t = -1f; group.alpha = 0f; return; }
            float outK = t <= hold ? 1f : 1f - (t - hold) / fade;
            group.alpha = Mathf.Clamp01(outK * 1.4f);
            Pose(t / PopTime, outK);
        }

        public void Dispose() { if (root != null) WorldUtil.Kill(root); }
    }
}
