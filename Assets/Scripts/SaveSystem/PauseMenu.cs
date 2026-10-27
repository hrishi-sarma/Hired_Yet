using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

/// <summary>ESC pause menu for the interview room and the open world (RESUME / SETTINGS / QUIT TO MENU), post-it styled like the main menu.
/// Also owns the subtle translucent "saving" indicator (top-right).</summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>true once the world is actually frozen (it only freezes after the pause backdrop has covered the game; stays true while the board slides away).</summary>
    public static bool Paused { get; private set; }
    /// <summary>Frame time that stops while paused (interview timers / minigames use it instead of Time.unscaledDeltaTime).</summary>
    public static float Dt { get { return Paused ? 0f : Time.unscaledDeltaTime; } }

    static readonly Color Ink = new Color(0.2f, 0.15f, 0.25f),
        Mint = new Color(0.74f, 0.95f, 0.84f), Sky = new Color(0.72f, 0.88f, 1f), Lav = new Color(0.82f, 0.79f, 0.98f),
        Pink = new Color(1f, 0.78f, 0.86f), Lemon = new Color(1f, 0.96f, 0.72f), Cream = new Color(1f, 0.98f, 0.9f);

    PauseBoard board; readonly List<Canvas> hiddenHud = new List<Canvas>();
    float prevScale = 1f; CursorLockMode prevLock; bool prevVisible;
    bool wasInvOpen, busy, worldFrozen, warmed, open, didFreeze;
    float freezeT;
    readonly List<Behaviour> frozen = new List<Behaviour>();
    Sprite dotSprite;

    // saving indicator
    GameObject saveGo; CanvasGroup saveCg; Text saveText; float saveT = -1f, saveDur;
    readonly RectTransform[] saveDots = new RectTransform[3]; readonly Image[] saveDotImgs = new Image[3];

    void OnEnable() { SaveSystem.Saved += OnSaved; }
    void OnDisable() { SaveSystem.Saved -= OnSaved; }
    void OnSaved() { ShowSaving(1.6f); }

    Font F { get { return SaveSystem.UiFont != null ? SaveSystem.UiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } }

    IEnumerator WarmAll()
    {
        yield return StartCoroutine(PauseBoard.Warm());
        yield return new WaitForSecondsRealtime(4f);
        while (open || busy) yield return null;
        PauseBoard.Prewarm = true;
        var pb = new GameObject("Pause Board Warm").AddComponent<PauseBoard>();
        pb.Init(F, null, null);
        PauseBoard.Prewarm = false;
        float guard = 0f;
        while (pb != null && !pb.Ready && guard < 3f) { guard += Time.unscaledDeltaTime; yield return null; }
        for (int i = 0; i < 3; i++) yield return null;
        if (pb != null) WorldUtil.Kill(pb.gameObject);
    }

    void Update()
    {
        UpdateSaving();
        if (!warmed && SaveSystem.InGame) { warmed = true; StartCoroutine(WarmAll()); }   // pre-build menu textures a few per frame
        // the world keeps running (player, cursor, timers too) until the backdrop has fully covered it, then everything freezes at once:
        // no visible stall while the menu elements are still coming in. The fallback timer uses a capped dt so a hitch can't trigger it early.
        if (open && !worldFrozen && !busy && board != null)
        {
            freezeT += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (board.BackdropUp || freezeT > 1.5f) FreezeWorld();
        }
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !busy)
        {
            if (open) { if (board != null && board.InSettings) board.Back(); else RequestResume(); }
            else if (CanPause() && !wasInvOpen) Pause();
        }
    }

    void LateUpdate() { wasInvOpen = Items.InventoryUI.IsOpen; }

    static bool CanPause() { return SaveSystem.InGame && !WorldLauncher.Launching && !DevConsole.Active && !PhotoMode.Active && !Booklet.Active; }

    // ------------------------------------------------------------ pause / resume
    void Pause()
    {
        open = true;
        prevScale = Time.timeScale; prevLock = Cursor.lockState; prevVisible = Cursor.visible;
        freezeT = 0f; worldFrozen = false; didFreeze = false;
        board = new GameObject("Pause Board").AddComponent<PauseBoard>();
        board.Init(F, RequestResume, () => StartCoroutine(QuitRoutine()));
    }

    // backdrop is up: stop the world, hand the cursor over and switch off the player's own scripts, all in the same frame
    void FreezeWorld()
    {
        Paused = true; worldFrozen = true; didFreeze = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        FreezePlayer(true);
        HideHud(true);
    }

    // board slides away (table, board, backdrop) and then the game continues
    void RequestResume()
    {
        if (busy || board == null) return;
        busy = true;
        if (worldFrozen) { Time.timeScale = prevScale <= 0f ? 1f : prevScale; worldFrozen = false; }   // world runs again as the layers slide away
        HideHud(false);
        board.Close(() => { busy = false; Resume(); });
    }

    void Resume()
    {
        Paused = false; worldFrozen = false; open = false;
        FreezePlayer(false);
        HideHud(false);
        if (board != null) WorldUtil.Kill(board.gameObject);
        board = null;
        Time.timeScale = prevScale <= 0f ? 1f : prevScale;
        if (didFreeze) { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }   // untouched if the menu was closed before the world froze
        didFreeze = false;
    }

    // screen-space overlay HUD canvases would be drawn over the 3D menu, so they sleep while paused
    void HideHud(bool on)
    {
        if (on)
        {
            hiddenHud.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c != null && c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name != "Saving Indicator") { c.enabled = false; hiddenHud.Add(c); }
        }
        else { foreach (var c in hiddenHud) if (c != null) c.enabled = true; hiddenHud.Clear(); }
    }

    // mouse-look scripts keep reading the mouse while timeScale is 0, so the player's own scripts are switched off while paused
    void FreezePlayer(bool on)
    {
        if (on)
        {
            frozen.Clear();
            Transform p = SaveSystem.FindPlayer();
            if (p == null) return;
            foreach (var b in p.GetComponents<MonoBehaviour>()) if (b != null && b.enabled) { b.enabled = false; frozen.Add(b); }
            foreach (var pi in p.GetComponentsInChildren<PlayerInput>()) if (pi != null && pi.enabled) { pi.enabled = false; frozen.Add(pi); }
        }
        else
        {
            foreach (var b in frozen) if (b != null) b.enabled = true;
            frozen.Clear();
        }
    }

    IEnumerator QuitRoutine()
    {
        if (busy || board == null) yield break;
        busy = true;
        SaveSystem.SaveNow(false);                 // save the unsaved progress first
        ShowSaving(1.3f);                          // subtle translucent indicator while the menu slides away
        bool closed = false;
        board.Close(() => closed = true);
        while (!closed) yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        HideHud(false);                            // persistent (DontDestroyOnLoad) canvases such as the loading screen must not stay switched off
        Paused = false; open = false; frozen.Clear();
        busy = false;
        bool faded = false;
        ScreenFade.ToBlack(0.6f, () => faded = true);   // fade out, then the menu's loading screen appears out of the dark
        while (!faded) yield return null;
        SaveSystem.QuitToMenu(false);
    }

    void MakeSprites()
    {
        if (dotSprite != null) return;
        const int S = 32;
        var r = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear }; var rp = new Color[S * S];   // soft round dot
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float d = new Vector2(x + 0.5f - S / 2f, y + 0.5f - S / 2f).magnitude;
            rp[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(S / 2f - d));
        }
        r.SetPixels(rp); r.Apply(); dotSprite = Sprite.Create(r, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------ subtle "saving" indicator: tiny translucent "saving" + three softly fluttering dots, top-right
    static readonly Color SaveShadow = new Color(0.1f, 0.05f, 0.15f, 0.35f);

    void ShowSaving(float dur)
    {
        MakeSprites();
        if (saveGo == null)
        {
            saveGo = new GameObject("Saving Indicator", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            DontDestroyOnLoad(saveGo);
            var cv = saveGo.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 450;
            var sc = saveGo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;
            saveCg = saveGo.GetComponent<CanvasGroup>(); saveCg.blocksRaycasts = false; saveCg.interactable = false; saveCg.alpha = 0f;

            for (int i = 0; i < 3; i++)
            {
                var dg = new GameObject("Dot" + i, typeof(Image)); var dr = dg.GetComponent<RectTransform>(); dr.SetParent(saveGo.transform, false);
                dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(1f, 0.5f); dr.sizeDelta = new Vector2(6f, 6f);
                dr.anchoredPosition = new Vector2(-30f - (2 - i) * 10f, -40f);
                var im = dg.GetComponent<Image>(); im.sprite = dotSprite; im.color = new Color(1f, 1f, 1f, 0.5f); im.raycastTarget = false;
                var sh = dg.AddComponent<Shadow>(); sh.effectColor = SaveShadow; sh.effectDistance = new Vector2(1f, -1f);
                saveDots[i] = dr; saveDotImgs[i] = im;
            }

            var tg = new GameObject("Text", typeof(Text)); var tr = tg.GetComponent<RectTransform>(); tr.SetParent(saveGo.transform, false);
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(1f, 1f); tr.anchoredPosition = new Vector2(-62f, -29f); tr.sizeDelta = new Vector2(120f, 22f);
            saveText = tg.GetComponent<Text>(); saveText.font = F; saveText.fontSize = 18; saveText.fontStyle = FontStyle.Bold; saveText.text = "saving";
            saveText.alignment = TextAnchor.MiddleRight; saveText.color = new Color(1f, 1f, 1f, 0.55f); saveText.raycastTarget = false;
            var sh2 = tg.AddComponent<Shadow>(); sh2.effectColor = SaveShadow; sh2.effectDistance = new Vector2(1f, -1f);
        }
        saveText.font = F;
        saveT = 0f; saveDur = dur;
    }

    void UpdateSaving()
    {
        if (saveGo == null || saveT < 0f) return;
        saveT += Time.unscaledDeltaTime;
        for (int i = 0; i < 3; i++)      // each dot floats on a slow sine, offset from its neighbour, so the three ripple like a soft flutter
        {
            float w = 0.5f + 0.5f * Mathf.Sin(saveT * 5f - i * 0.9f);
            saveDots[i].anchoredPosition = new Vector2(-30f - (2 - i) * 10f, -40f + (w - 0.5f) * 6f);
            saveDotImgs[i].color = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 0.7f, w));
        }
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(saveT / 0.35f)), fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((saveDur - saveT) / 0.5f));
        saveCg.alpha = fadeIn * fadeOut;
        if (saveT >= saveDur) { saveCg.alpha = 0f; saveT = -1f; }
    }
}
