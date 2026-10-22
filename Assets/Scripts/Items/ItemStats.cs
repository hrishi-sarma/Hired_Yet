using UnityEngine;

namespace Items
{
    [System.Serializable]
    public struct ItemStats
    {
        public int quality, relevance, credibility, presentation;   // 0-100

        public ItemStats(int q, int r, int c, int p)
        { quality = q; relevance = r; credibility = c; presentation = p; }

        public ItemStats Clamped()
        {
            return new ItemStats(C(quality), C(relevance), C(credibility), C(presentation));
        }
        static int C(int v) { return Mathf.Clamp(v, 0, 100); }

        public int Total { get { return quality + relevance + credibility + presentation; } }
    }
}
