using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>Toggle with settings.car.traffic.debugKey (F9): route, state, personality, mood and signal per car.</summary>
public class TrafficDebug : MonoBehaviour
{
    static readonly List<IAgent> buf = new List<IAgent>(64);
    static Material lineMat;
    GUIStyle style;
    bool on;

    void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCamera;
        Camera.onPostRender += OnPost;
        on = NpcDirector.Instance != null && NpcDirector.Instance.settings.car.traffic.debugView;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        Camera.onPostRender -= OnPost;
    }

    void Update()
    {
        NpcDirector d = NpcDirector.Instance;
        if (d == null) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[d.settings.car.traffic.debugKey].wasPressedThisFrame) on = !on;
    }

    void OnEndCamera(ScriptableRenderContext ctx, Camera cam)
    {
        Draw(cam);
    }

    void OnPost(Camera cam)
    {
        if (GraphicsSettings.currentRenderPipeline == null) Draw(cam);
    }

    static Color MoodColor(float m)
    {
        return Color.Lerp(new Color(0.2f, 1f, 0.3f), new Color(1f, 0.15f, 0.1f), m);
    }

    static Color SigColor(SignalState s)
    {
        switch (s)
        {
            case SignalState.Red: return Color.red;
            case SignalState.Amber: return new Color(1f, 0.7f, 0f);
            case SignalState.Flash: return new Color(1f, 0.85f, 0.2f);
            default: return Color.green;
        }
    }

    void Gather(NpcDirector d)
    {
        d.Registry.Query(new Vector2(d.Player.position.x, d.Player.position.z), d.settings.car.traffic.debugRadius, buf);
    }

    void Draw(Camera cam)
    {
        NpcDirector d = NpcDirector.Instance;
        if (!on || d == null || !d.Running || d.Player == null || cam == null || cam != d.PlayerCamera) return;
        if (lineMat == null)
        {
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return;
            lineMat = new Material(sh);
            lineMat.hideFlags = HideFlags.HideAndDontSave;
            lineMat.SetInt("_ZTest", (int)CompareFunction.Always);
            lineMat.SetInt("_Cull", (int)CullMode.Off);
        }
        Gather(d);
        lineMat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);
        for (int i = 0; i < buf.Count; i++)
        {
            Car c = buf[i] as Car;
            if (c == null || c.Path == null) continue;
            Color col = MoodColor(c.Mind.stress);
            if (c.Far) col.a = 0.5f;
            GL.Color(col);
            Vector3 p = c.transform.position + Vector3.up * 1.2f;
            RoadPath cur = c.Path;
            Seg(p, cur, c.PathS, col);
            for (int k = 0; k < 6; k++)
            {
                RoadPath r = c.RouteAt(k);
                if (r == null) break;
                Seg(Vector3.zero, r, 0f, col, true);
                if (r.next != null) Seg(Vector3.zero, r.next, 0f, col, true);
            }
            if (c.HasDest)
            {
                Vector2 dp = c.DestPos;
                GL.Color(new Color(1f, 1f, 1f, 0.5f));
                GL.Vertex(p);
                GL.Vertex(new Vector3(dp.x, p.y + 8f, dp.y));
            }
            if (cur.kind == PathKind.Lane && cur.node != null && cur.node.signal != null)
            {
                Vector2 e = cur.pts[cur.pts.Length - 1];
                GL.Color(SigColor(c.Signal));
                GL.Vertex(new Vector3(e.x, cur.ys[cur.ys.Length - 1] + 0.3f, e.y));
                GL.Vertex(new Vector3(e.x, cur.ys[cur.ys.Length - 1] + 3.5f, e.y));
            }
        }
        GL.End();
        GL.PopMatrix();
    }

    static void Seg(Vector3 start, RoadPath path, float fromS, Color col, bool whole = false)
    {
        GL.Color(col);
        int n = path.pts.Length;
        Vector3 prev = whole ? new Vector3(path.pts[0].x, path.ys[0] + 0.6f, path.pts[0].y) : start;
        int first = 0;
        if (!whole)
        {
            while (first < n - 1 && path.cum[first + 1] < fromS) first++;
            first++;
        }
        else first = 1;
        for (int i = first; i < n; i++)
        {
            Vector3 q = new Vector3(path.pts[i].x, path.ys[i] + 0.6f, path.pts[i].y);
            GL.Vertex(prev);
            GL.Vertex(q);
            prev = q;
        }
    }

    void OnGUI()
    {
        NpcDirector d = NpcDirector.Instance;
        if (!on || d == null || !d.Running || d.Player == null || d.PlayerCamera == null) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 11;
            style.richText = true;
            style.normal.textColor = Color.white;
        }
        Gather(d);
        Camera cam = d.PlayerCamera;
        for (int i = 0; i < buf.Count; i++)
        {
            Car c = buf[i] as Car;
            if (c == null || c.Mind == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * 2.2f);
            if (sp.z < 0f) continue;
            string hex = ColorUtility.ToHtmlStringRGB(MoodColor(c.Mind.stress));
            string txt = "<color=#" + hex + ">" + c.Personality + "</color> " + c.State + (c.Far ? " [far]" : "") +
                "\nmood " + c.Mind.stress.ToString("F2") + "  v " + c.Speed.ToString("F1") +
                "\nmem " + c.Mind.last + "  sig " + c.Signal +
                "\ntrip " + (c.HasDest ? (c.Returning ? "home<-" : "->") + c.DestZone + " (" + c.RouteLeft + ")" : "wander") +
                "  mood@road " + TrafficMood.Get(c.Pos2D).ToString("F2");
            GUI.Label(new Rect(sp.x - 90f, Screen.height - sp.y - 30f, 240f, 70f), txt, style);
        }
        GUI.Label(new Rect(10f, 10f, 420f, 22f), "Traffic debug [" + d.settings.car.traffic.debugKey + "]  hour " + TrafficClock.Hour.ToString("F1") + (TrafficSignals.Flashing ? "  signals: flashing" : ""), style);
    }
}
