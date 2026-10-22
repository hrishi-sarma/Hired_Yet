using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Photographs the finished resume form (exact UI: fonts, ticks, photo, colours) on a paper background.
/// The result is stored in ResumeSession and used as the texture of the player's starter resume.</summary>
public static class ResumeSnapshot
{
    const int W = 768, H = 1086;   // same aspect as the 600x848 form sheet (and the 512x724 item paper)

    public static IEnumerator Capture(SlotPage s)
    {
        // tidy the live page first (it is about to be left anyway)
        foreach (var f in s.frame) f.color = Color.clear;                       // yellow "selected slot" frame
        if (s.charIdx < 0 && s.photoHint != null) s.photoHint.gameObject.SetActive(false);
        if (s.msg != null) s.msg.text = "";
        if (s.picker != null) s.picker.SetActive(false);

        var root = new GameObject("Resume Snapshot"); root.transform.position = new Vector3(0f, -3000f, 0f);
        var cg = new GameObject("Cam"); cg.transform.SetParent(root.transform, false); cg.transform.localPosition = new Vector3(0f, 0f, -10f);
        var cam = cg.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 5f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.995f, 0.975f, 0.925f, 1f); cam.enabled = false;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;

        var cv = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler)); cv.transform.SetParent(root.transform, false);
        var canvas = cv.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 5f;
        var sc = cv.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(600f, 848f); sc.matchWidthOrHeight = 0.5f;

        var page = new GameObject("Page", typeof(RawImage)); var pr = page.GetComponent<RectTransform>(); pr.SetParent(cv.transform, false);
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f); pr.sizeDelta = new Vector2(600f, 848f); pr.anchoredPosition = Vector2.zero;
        var paperTex = DeskKit.Paper(); var bg = page.GetComponent<RawImage>(); bg.texture = paperTex; bg.raycastTarget = false;

        // copy the live form elements (same anchors as the sheet, so positions match 1:1)
        for (int i = 0; i < s.paper.childCount; i++)
        {
            var ch = s.paper.GetChild(i).gameObject;
            if (ch == s.picker || ch == s.saveOverlay || (s.discardBtn != null && ch == s.discardBtn.gameObject) || (s.submitBtn != null && ch == s.submitBtn.gameObject)) continue;   // no SUBMIT button on the resume
            Object.Instantiate(ch, pr, false);
        }

        Canvas.ForceUpdateCanvases();
        yield return null;                  // let dynamic font atlases rebuild
        Canvas.ForceUpdateCanvases();
        cam.Render();

        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        ResumeSession.ResumePixels = tex.GetPixels32(); ResumeSession.ResumeW = W; ResumeSession.ResumeH = H;
        RenderTexture.active = prev; cam.targetTexture = null;
        WorldUtil.Kill(tex); WorldUtil.Kill(paperTex); rt.Release(); WorldUtil.Kill(rt); WorldUtil.Kill(root);
    }
}
