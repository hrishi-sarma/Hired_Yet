using UnityEngine;

/// <summary>
/// Decides whether it is night so animals can sleep. Checked in this order:
///  1. AnimalSleepClock.IsNightProvider (assign a function from your day/night script), or
///  2. AnimalSleepClock.GameHour (set it 0-24 every frame/tick from your day/night script), or
///  3. automatic: your AnimeSkyController's timeOfDay (sun below the horizon = night), else the scene's sun (RenderSettings.sun or the first directional light) being below the horizon.
/// </summary>
public enum DayPhase { Night, Dawn, Day, Dusk }

public static class AnimalSleepClock
{
    /// <summary>Optional: return true when it is night. Overrides everything else.</summary>
    public static System.Func<bool> IsNightProvider;
    /// <summary>Optional: current in-game hour (0-24). Leave at -1 if you don't have one.</summary>
    public static float GameHour = -1f;

    static AnimeSkyController sky;
    static Light sun;
    static float nextSunSearch;
    static int cachedFrame = -1;
    static bool cachedNight;

    public static bool IsNight(AnimalAISettings s)
    {
        if (s == null || !s.sleepAtNight) return false;
        if (cachedFrame == Time.frameCount) return cachedNight;
        cachedFrame = Time.frameCount;
        cachedNight = Evaluate(s);
        return cachedNight;
    }

    static bool Evaluate(AnimalAISettings s)
    {
        if (s.forceNight) return true;
        if (IsNightProvider != null) return IsNightProvider();
        if (GameHour >= 0f)
        {
            float h = Mathf.Repeat(GameHour, 24f);
            float a = s.nightStartHour, b = s.nightEndHour;
            return a > b ? (h >= a || h < b) : (h >= a && h < b);
        }
        if (sky == null && Time.unscaledTime >= nextSunSearch)
            sky = Object.FindFirstObjectByType<AnimeSkyController>();
        if (sky != null)
        {
            // same sun path as AnimeSkyController.ApplySky: sunDir.y = sin(timeOfDay*360 - 90 deg)
            float sunY = Mathf.Sin((sky.timeOfDay * 360f - 90f) * Mathf.Deg2Rad);
            return Mathf.Asin(Mathf.Clamp(sunY, -1f, 1f)) * Mathf.Rad2Deg < s.nightSunElevation;
        }
        if (sun == null && Time.unscaledTime >= nextSunSearch)
        {
            nextSunSearch = Time.unscaledTime + 5f;
            sun = RenderSettings.sun;
            if (sun == null)
            {
                Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i] != null && lights[i].type == LightType.Directional) { sun = lights[i]; break; }
            }
        }
        if (sun == null) return false;
        // light shines along its forward vector; forward.y > 0 means the sun is below the horizon
        float elevationDeg = Mathf.Asin(Mathf.Clamp(-sun.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        return elevationDeg < s.nightSunElevation;
    }

    static int phaseFrame = -1;
    static DayPhase cachedPhase;

    /// <summary>Night / Dawn / Day / Dusk, used for daily routines (sunbathing, drinking, hiding...).</summary>
    public static DayPhase Phase(AnimalAISettings s)
    {
        if (phaseFrame == Time.frameCount) return cachedPhase;
        phaseFrame = Time.frameCount;
        cachedPhase = EvaluatePhase(s);
        return cachedPhase;
    }

    static DayPhase EvaluatePhase(AnimalAISettings s)
    {
        if (s == null) return DayPhase.Day;
        if (IsNight(s)) return DayPhase.Night;
        if (IsNightProvider != null) return DayPhase.Day;
        if (GameHour >= 0f)
        {
            float h = Mathf.Repeat(GameHour, 24f);
            float since = Mathf.Repeat(h - s.nightEndHour, 24f);
            float until = Mathf.Repeat(s.nightStartHour - h, 24f);
            if (since < 2.5f) return DayPhase.Dawn;
            if (until < 2.5f) return DayPhase.Dusk;
            return DayPhase.Day;
        }
        if (sky != null)
        {
            float t = Mathf.Repeat(sky.timeOfDay, 1f);
            float sunY = Mathf.Sin((t * 360f - 90f) * Mathf.Deg2Rad);
            float el = Mathf.Asin(Mathf.Clamp(sunY, -1f, 1f)) * Mathf.Rad2Deg;
            if (el < 16f) return t < 0.5f ? DayPhase.Dawn : DayPhase.Dusk;
            return DayPhase.Day;
        }
        if (sun != null)
        {
            float el = Mathf.Asin(Mathf.Clamp(-sun.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (el < 16f) return DayPhase.Dusk;
        }
        return DayPhase.Day;
    }
}
