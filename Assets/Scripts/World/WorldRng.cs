using System.Collections.Generic;

public class WorldRng
{
    public readonly System.Random Raw;

    public WorldRng(int seed, string channel)
    {
        Raw = new System.Random(Hash(seed, channel));
    }

    static int Hash(int seed, string channel)
    {
        unchecked
        {
            uint h = 2166136261u;
            for (int i = 0; i < channel.Length; i++)
            {
                h ^= channel[i];
                h *= 16777619u;
            }
            h ^= (uint)seed;
            h *= 16777619u;
            h ^= h >> 15;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    public float Value()
    {
        return (float)Raw.NextDouble();
    }

    public float Range(float min, float max)
    {
        return min + (max - min) * Value();
    }

    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return Raw.Next(minInclusive, maxExclusive);
    }

    public bool Chance(float probability)
    {
        return Value() < probability;
    }

    public T Pick<T>(IList<T> list)
    {
        return list[Range(0, list.Count)];
    }
}
