using System.Collections.Generic;
using UnityEngine;

/// <summary>Cars: animals get out of the way (each species in its own way) and cars brake for animals on the road.</summary>
public sealed partial class AnimalAgent
{
    public static readonly List<AnimalAgent> Active = new List<AnimalAgent>();

    /// <summary>Radius cars use when deciding whether this animal is in their way.</summary>
    public float CarRadius
    {
        get
        {
            switch (Species)
            {
                case AnimalSpecies.Deer: return 0.6f;
                case AnimalSpecies.Dog: return 0.35f;
                case AnimalSpecies.Cat: return 0.22f;
                default: return 0.18f;
            }
        }
    }

    public bool BlocksCars { get { return !hidden && isActiveAndEnabled; } }

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    // 0 = not yet decided, 1 = dog doesn't notice until the last moment, 2 = dog barks at the car, 3 = aware (cats, wildlife, careful dogs)
    int carReaction;
    float nextCarScan, carEvadeUntil, carBarkUntil, nextCarBark, carBoostUntil;
    bool haveThreat;
    AnimalCarSense.Threat threat;
    Car evadeCar;

    void ScanCars()
    {
        if (Time.time < nextCarScan) return;
        nextCarScan = Time.time + 0.12f;
        haveThreat = AnimalCarSense.Find(director, transform.position, 3.0f + CarRadius, out threat);
        if (!haveThreat && Time.time >= carEvadeUntil && Time.time >= carBarkUntil) carReaction = 0;
    }

    float CarSpeedBoost() { return Time.time < carBoostUntil ? 1.35f : 1f; }

    /// <summary>Returns true while the animal is busy dealing with a car (evading or barking), overriding other behaviour.</summary>
    bool UpdateVehicleAvoid(float dt)
    {
        if (motion == Motion.Pat) return false;
        Vector3 pos = transform.position;
        Vector2 p2 = new Vector2(pos.x, pos.z);

        // 1. Already running: keep going until clear of the car's line, then settle.
        if (Time.time < carEvadeUntil)
        {
            if (evadeCar != null && !evadeCar.Pooled)
            {
                Vector2 h = evadeCar.Heading;
                Vector2 rel = p2 - evadeCar.Pos2D;
                float lat = Mathf.Abs(rel.x * h.y - rel.y * h.x);
                float ahead = Vector2.Dot(rel, h);
                if (lat > 7f || ahead < -8f) carEvadeUntil = 0f;
            }
            if (Time.time < carEvadeUntil)
            {
                motion = Motion.Flee;
                motionTimer = Mathf.Max(motionTimer, 0.4f);
                return true;
            }
            motion = Motion.Idle;
            motionTimer = RandomRange(0.8f, 2f);
            carReaction = 0;
            return false;
        }

        if (!haveThreat || threat.car == null || threat.car.Pooled) return false;
        Car c = threat.car;
        float ttc = threat.ttc;
        if (carReaction == 0)
        {
            if (Species == AnimalSpecies.Dog)
            {
                float r = RandomValue();
                carReaction = r < 0.4f ? 1 : (r < 0.7f ? 2 : 3);
            }
            else carReaction = 3;
        }

        // 2. Dog barking at the approaching car.
        if (Species == AnimalSpecies.Dog && (carReaction == 2 || Time.time < carBarkUntil))
        {
            if (ttc > 1.6f)
            {
                if (Time.time >= carBarkUntil && carReaction == 2) carBarkUntil = Time.time + 4f;
                Vector2 toCar = c.Pos2D - p2;
                if (toCar.sqrMagnitude > 0.01f)
                {
                    Quaternion face = Quaternion.LookRotation(new Vector3(toCar.x, 0f, toCar.y), Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, face, dt * 8f);
                }
                motion = Motion.Idle;
                motionTimer = 0.5f;
                alertPose = true;
                if (Time.time >= nextCarBark)
                {
                    nextCarBark = Time.time + RandomRange(0.7f, 1.1f);
                    PlayReaction();
                }
                return true;
            }
            carBarkUntil = 0f;
            StartEvade(c, true);
            return true;
        }

        // 3. Decide when to bolt, by temperament.
        float reactTtc;
        if (Species == AnimalSpecies.Dog) reactTtc = carReaction == 1 ? 1.2f : 3f;   // oblivious dogs jolt at the last second
        else if (Species == AnimalSpecies.Cat) reactTtc = 3.8f;                        // cats are always aware
        else reactTtc = 3.5f;
        if (ttc > reactTtc) return false;
        StartEvade(c, Species == AnimalSpecies.Dog && carReaction == 1);
        return true;
    }

    void StartEvade(Car c, bool jolt)
    {
        Vector2 h = c.Heading;
        Vector2 rel = new Vector2(transform.position.x - c.Pos2D.x, transform.position.z - c.Pos2D.y);
        float lat = rel.x * h.y - rel.y * h.x;
        float side = Mathf.Abs(lat) < 0.2f ? (RandomValue() < 0.5f ? -1f : 1f) : Mathf.Sign(lat);
        Vector3 Dir(float sd) { Vector2 v = new Vector2(h.y, -h.x) * sd + h * 0.25f; v.Normalize(); return new Vector3(v.x, 0f, v.y); }
        Vector3 dir = Dir(side);
        if (!CanMoveInDirection(dir, 3f))
        {
            Vector3 other = Dir(-side);
            if (CanMoveInDirection(other, 3f)) dir = other;
        }
        if (socialRole != SocialRole.None) EndSocial(true);
        sleeping = false;
        wakeUntil = Time.time + RandomRange(10f, 20f);
        moveDirection = dir;
        motion = Motion.Flee;
        motionTimer = 2f;
        evadeCar = c;
        carEvadeUntil = Time.time + RandomRange(2.5f, 4f);
        carBoostUntil = Time.time + (jolt ? 1.6f : 0.8f);
        fleeCooldown = 0.5f;
        if (jolt) PlayReaction();   // yelp as it suddenly notices the car
    }
}
