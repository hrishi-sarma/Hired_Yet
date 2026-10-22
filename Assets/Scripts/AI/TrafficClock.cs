using UnityEngine;

/// <summary>Hour-of-day source for traffic. Reads the sky controllers; falls back to the vehicle night value.</summary>
public static class TrafficClock
{
    static StylizedHDRISkyController hdri;
    static AnimeSkyController anime;
    static float nextFind;
    public static float debugHour = -1f;

    public static float Hour
    {
        get
        {
            if (debugHour >= 0f) return debugHour;
            if (Time.unscaledTime >= nextFind && hdri == null && anime == null)
            {
                nextFind = Time.unscaledTime + 5f;
                hdri = Object.FindFirstObjectByType<StylizedHDRISkyController>();
                if (hdri == null) anime = Object.FindFirstObjectByType<AnimeSkyController>();
            }
            if (hdri != null) return hdri.CurrentHour;
            if (anime != null) return Mathf.Repeat(anime.timeOfDay * 24f, 24f);
            return StylizedVehiclePainter.CurrentNight > 0.5f ? 23f : 12f;
        }
    }

    public static float NightFactor
    {
        get { return Mathf.Max(StylizedVehiclePainter.CurrentNight, HourNight(Hour)); }
    }

    static float HourNight(float h)
    {
        if (h >= 21f || h < 5f) return 1f;
        if (h >= 19f) return (h - 19f) / 2f;
        if (h < 6.5f) return 1f - (h - 5f) / 1.5f;
        return 0f;
    }

    /// <summary>Late night: quiet streets, drunk / sleepy drivers, flashing signals.</summary>
    public static bool Late { get { float h = Hour; return h >= 23f || h < 4.5f; } }

    public static bool Rush { get { float h = Hour; return (h >= 7.5f && h < 10f) || (h >= 17f && h < 19.5f); } }

    public static bool HeadlightsOn { get { return NightFactor > 0.35f; } }

    /// <summary>Multiplier on car count and spawn rate.</summary>
    public static float Density
    {
        get
        {
            float h = Hour;
            if (Rush) return 1.35f;
            if (Late) return 0.6f;
            if (h >= 4.5f && h < 7.5f) return 0.7f;
            if (h >= 21f) return 0.8f;
            return 1f;
        }
    }

    /// <summary>Per-zone spawn multiplier. By night the city stays alive (a bit thinner) while suburbs and the highway go nearly empty.</summary>
    public static float ZoneNight(ZoneType zone)
    {
        float n = Mathf.Clamp01(NightFactor);
        float night;
        switch (zone)
        {
            case ZoneType.City: night = 0.75f; break;
            case ZoneType.Suburb: night = 0.08f; break;
            case ZoneType.Highway: night = 0.4f; break;
            default: night = 0.5f; break;
        }
        return Mathf.Lerp(1f, night, n);
    }
}
