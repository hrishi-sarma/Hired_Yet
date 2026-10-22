using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Report-card grade: a big circular stamp in the top-left, slightly randomised in position and tilt (max 30 degrees).
public partial class InterviewUI
{
    RectTransform gradeRt;
    TextMeshProUGUI gradeLetter;
    Image gradeDisc;
    static Sprite circleSprite;

    static Sprite Circle()
    {
        if (circleSprite != null) return circleSprite;
        const int n = 256;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 1.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    Image Disc(RectTransform parent, string name, float size, Color col, Vector2 pos)
    {
        RectTransform rt = UIKit.NewRect(name, parent);
        UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        return UIKit.AddImage(rt, Circle(), col);
    }

    void BuildGradeBadge(RectTransform parent)
    {
        float d = S(250f);
        gradeRt = UIKit.NewRect("GradeBadge", parent);
        UIKit.Place(gradeRt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(d, d));
        Disc(gradeRt, "Shadow", d, new Color(0f, 0f, 0f, 0.45f), new Vector2(S(8f), -S(8f)));
        Disc(gradeRt, "Outer", d, UIKit.Dark, Vector2.zero);
        gradeDisc = Disc(gradeRt, "Disc", d - S(18f), Color.white, Vector2.zero);
        Disc(gradeRt, "Ring", d - S(58f), UIKit.Dark, Vector2.zero);
        Disc(gradeRt, "Face", d - S(66f), UIKit.Paper, Vector2.zero);

        TextMeshProUGUI cap = UIKit.AddText(gradeRt, "Cap", "GRADE", S(26), UIKit.Dark, TextAlignmentOptions.Center, font, FontStyles.Bold, 6f);
        UIKit.Place(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, S(66f)), new Vector2(d, S(34f)));
        gradeLetter = UIKit.AddText(gradeRt, "Letter", "", S(150), UIKit.Dark, TextAlignmentOptions.Center, font, FontStyles.Bold);
        UIKit.Place(gradeLetter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -S(10f)), new Vector2(d, S(180f)));
        gradeRt.gameObject.SetActive(false);
    }

    static string GradeHex(string g)
    {
        switch (g)
        {
            case "S": return "#F2B705";
            case "A": return "#2FAE5F";
            case "B": return "#2E9CCA";
            case "C": return "#F28C28";
            case "D": return "#D9534F";
        }
        return "#C02020";
    }

    void ShowGrade(string grade)
    {
        if (gradeRt == null) return;
        Color col = UIKit.Hex(GradeHex(grade));
        gradeDisc.color = col;
        gradeLetter.text = grade;
        gradeLetter.color = col;
        // top-left, below the suspicion bar, with a margin; a little uneven and tilted (never more than 30 degrees)
        float x = S(Random.Range(150f, 195f));
        float y = -(S(HudRowHeight) + S(Random.Range(150f, 195f)));
        gradeRt.anchoredPosition = new Vector2(x, y);
        gradeRt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f));
        gradeRt.gameObject.SetActive(true);
    }

    void HideGrade() { if (gradeRt != null) gradeRt.gameObject.SetActive(false); }
}
