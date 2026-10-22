using UnityEngine;

namespace Items
{
    public static class ItemPal
    {
        public static readonly Color None = new Color(0, 0, 0, 0);
        public static readonly Color Paper = new Color(0.99f, 0.97f, 0.92f);
        public static readonly Color Ink = new Color(0.27f, 0.25f, 0.35f);
        public static readonly Color Pen = new Color(0.22f, 0.42f, 0.92f);
        public static readonly Color Line = new Color(0.66f, 0.64f, 0.72f);
        public static readonly Color Gold = new Color(0.98f, 0.84f, 0.45f);
        public static readonly Color Pink = new Color(0.98f, 0.76f, 0.80f), Mint = new Color(0.72f, 0.92f, 0.82f),
            Sky = new Color(0.70f, 0.85f, 0.98f), Lilac = new Color(0.80f, 0.74f, 0.96f),
            Butter = new Color(0.99f, 0.93f, 0.68f), Peach = new Color(0.99f, 0.82f, 0.70f);
        static readonly Color[] set = { Pink, Mint, Sky, Lilac, Butter, Peach };
        public static Color Accent(int seed, int salt) { return set[new System.Random(seed * 131 + salt * 17).Next(set.Length)]; }
    }

    // One generator per layout. Name field is returned as a rect and left blank (underline only);
    // ItemTextures draws the name layer on top of a copy.
    public static class ItemLayouts
    {
        static readonly string[] companies = { "BRIGHTLY", "NOVAWORKS", "PIXELPOND", "LUMEN CO", "QUILLBASE", "MOSSLEAF", "ORBITAL", "TEAPOT LABS" };
        static readonly string[] colleges = { "NORTHBRIDGE", "HOLLOWAY", "ASTORIA UNI", "MAPLE HILL" };
        static readonly string[] titles = { "DESIGNER", "ENGINEER", "ANALYST", "MANAGER", "RECRUITER", "WRITER" };
        static string Pick(string[] a, int seed, int salt) { return a[new System.Random(seed * 7 + salt).Next(a.Length)]; }

        public static Vector2Int SizeFor(ItemDef d)
        {
            switch (d.id)
            {
                case 11: case 12: case 14: case 15: return new Vector2Int(512, 320);
                case 13: return new Vector2Int(256, 640);
                case 16: return new Vector2Int(512, 640);
                case 18: return new Vector2Int(512, 360);
                case 19: case 20: return new Vector2Int(64, 64);
                default: return new Vector2Int(512, 724);   // A4-ish paper (avoids stretching on the quad)
            }
        }

        public static ItemCanvas Build(ItemDef d, int seed, out RectInt nr)
        {
            nr = new RectInt(0, 0, 0, 0);
            switch (d.id)
            {
                case 0: return PlayerResume(seed, out nr);
                case 1: case 2: case 3: return Resume(seed, out nr);
                case 4: case 5: case 6: return Letter(seed, false, out nr);
                case 10: return Letter(seed, true, out nr);
                case 7: case 8: case 9: return Certificate(seed, out nr);
                case 11: return BusinessCard(seed, out nr);
                case 12: return Freelance(seed);
                case 13: return Slip(seed);
                case 14: return IdCard(seed, false, out nr);
                case 15: return IdCard(seed, true, out nr);
                case 16: return Portfolio(seed);
                case 17: return Report(seed);
                case 18: return Envelope(seed);
                default: return new ItemCanvas(64, 64, d.id == 19 ? ItemPal.Peach : ItemPal.Mint);
            }
        }

        static void Under(ItemCanvas c, RectInt nr) { c.Rect(nr.x, nr.yMax + 2, nr.width, 4, ItemPal.Ink); }
        static void Face(ItemCanvas c, float cx, float cy, float s, Color col)
        {
            c.Circle(cx, cy, 17 * s, col, ItemPal.Ink, 3);
            c.RRect(cx - 28 * s, cy + 24 * s, 56 * s, 30 * s, 14 * s, col, ItemPal.Ink, 3);
        }

        // The player's starter resume = a photograph of the submitted resume-generation form.
        // Falls back to the generic layout when no form was filled in (e.g. starting directly in the world scene).
        static ItemCanvas PlayerResume(int seed, out RectInt nr)
        {
            var px = ResumeSession.ResumePixels; int w = ResumeSession.ResumeW, h = ResumeSession.ResumeH;
            if (px == null || w <= 0 || h <= 0 || px.Length != w * h) return Resume(seed, out nr);
            nr = new RectInt(0, 0, 0, 0);   // name is already written on the form
            var c = new ItemCanvas(w, h, ItemPal.Paper);
            System.Array.Copy(px, c.px, px.Length);
            return c;
        }

        // ---------------- Paper layouts (512x724) ----------------
        static ItemCanvas Resume(int seed, out RectInt nr)
        {
            var r = new System.Random(seed); var c = new ItemCanvas(512, 724, ItemPal.Paper);
            Color ac = ItemPal.Accent(seed, 1), ac2 = ItemPal.Accent(seed, 2), ln = ItemPal.Line;
            c.Rect(0, 140, 210, 584, Color.Lerp(ItemPal.Paper, ac, 0.2f));
            c.Rect(0, 0, 512, 140, ac);
            nr = new RectInt(36, 34, 300, 56); Under(c, nr);
            c.TextLines(36, 114, 220, 1, 0, 6, Color.Lerp(ac, Color.white, 0.6f), seed + 3);
            c.RRect(386, 20, 96, 96, 16, Color.white, ItemPal.Ink, 4);
            c.Circle(434, 55, 17, ac2, ItemPal.Ink, 3); c.RRect(406, 78, 56, 30, 14, ac2, ItemPal.Ink, 3);
            c.Text("SKILLS", 30, 170, 3, ItemPal.Ink);
            for (int i = 0; i < 5; i++)
            {
                float y = 205 + i * 34, fw = 40 + r.Next(0, 115);
                c.RRect(30, y, 160, 14, 7, new Color(1, 1, 1, 0.8f)); c.RRect(30, y, fw, 14, 7, ac2, ItemPal.Ink, 2);
            }
            c.Text("CONTACT", 30, 395, 3, ItemPal.Ink); c.TextLines(30, 438, 150, 3, 26, 6, ln, seed + 9);
            c.Text("EXPERIENCE", 235, 170, 3, ItemPal.Ink);
            for (int i = 0; i < 3; i++)
            {
                float y = 210 + i * 120;
                c.RRect(235, y, 130, 12, 6, ItemPal.Ink); c.RRect(235, y + 22, 90, 8, 4, ln);
                c.TextLines(235, y + 48, 250, 3, 22, 6, ln, seed + i);
            }
            c.Text("EDUCATION", 235, 580, 3, ItemPal.Ink);
            c.RRect(235, 620, 130, 12, 6, ItemPal.Ink); c.TextLines(235, 654, 250, 2, 22, 6, ln, seed + 20);
            return c;
        }

        static ItemCanvas Letter(int seed, bool reference, out RectInt nr)
        {
            var c = new ItemCanvas(512, 724, ItemPal.Paper); Color ac = ItemPal.Accent(seed, 3), ln = ItemPal.Line;
            c.Circle(64, 64, 26, ac, ItemPal.Ink, 3); c.Circle(64, 64, 10, Color.white);
            c.RRect(104, 50, 160, 14, 7, ItemPal.Ink); c.RRect(104, 74, 110, 8, 4, ln);
            c.RRect(380, 56, 100, 8, 4, ln); c.Rect(0, 112, 512, 6, ac);
            if (reference) { c.Text("REFERENCE", 40, 132, 4, ItemPal.Ink); nr = new RectInt(40, 182, 260, 40); }
            else nr = new RectInt(40, 150, 260, 40);
            Under(c, nr);
            if (!reference) c.TextLines(40, 214, 200, 2, 20, 5, ln, seed + 1);
            c.RRect(40, 272, 120, 10, 5, ItemPal.Ink);
            c.TextLines(40, 312, 430, 5, 22, 6, ln, seed + 2); c.TextLines(40, 430, 430, 5, 22, 6, ln, seed + 3);
            c.TextLines(40, 548, 430, 4, 22, 6, ln, seed + 4, 0.5f);
            c.RRect(40, 650, 90, 9, 4, ItemPal.Ink);
            c.Squiggle(40, 690, 150, 12, 4, new Color(0.2f, 0.25f, 0.5f), seed + 5);
            if (reference)
            {
                c.Circle(410, 650, 44, ItemPal.None, ItemPal.Pink, 6); c.Circle(410, 650, 32, ItemPal.None, ItemPal.Pink, 3);
                c.TextCentered("REF", 410, 636, 4, ItemPal.Pink);
            }
            return c;
        }

        static ItemCanvas Certificate(int seed, out RectInt nr)
        {
            var c = new ItemCanvas(512, 724, ItemPal.Paper); Color ac = ItemPal.Accent(seed, 4), ac2 = ItemPal.Accent(seed, 5);
            c.RRect(18, 18, 476, 688, 22, ItemPal.Paper, ac, 10); c.RRect(40, 40, 432, 644, 14, ItemPal.None, ItemPal.Ink, 3);
            foreach (var p in new[] { new Vector2(62, 62), new Vector2(450, 62), new Vector2(62, 662), new Vector2(450, 662) })
                c.Circle(p.x, p.y, 14, ac2, ItemPal.Ink, 3);
            c.TextCentered("CERTIFICATE", 256, 120, 6.5f, ItemPal.Ink);
            c.TextCentered("OF ACHIEVEMENT", 256, 190, 3, ItemPal.Line);
            c.Line(130, 240, 382, 240, 4, ac); c.Circle(256, 240, 8, ac2, ItemPal.Ink, 2);
            c.TextCentered("AWARDED TO", 256, 290, 3, ItemPal.Line);
            nr = new RectInt(86, 340, 340, 70); Under(c, nr);
            c.TextLines(106, 462, 300, 3, 26, 6, ItemPal.Line, seed + 2, 0.7f);
            c.Tri(232, 590, 206, 672, 240, 654, ac); c.Tri(280, 590, 306, 672, 272, 654, ac2);
            c.Circle(256, 580, 48, ItemPal.Gold, ItemPal.Ink, 3); c.Circle(256, 580, 34, ItemPal.Butter, ItemPal.Ink, 2);
            c.Squiggle(80, 600, 120, 10, 4, new Color(0.2f, 0.25f, 0.5f), seed + 6); c.Line(70, 625, 210, 625, 4, ItemPal.Ink);
            c.Line(330, 625, 450, 625, 4, ItemPal.Ink);
            return c;
        }

        static ItemCanvas Report(int seed)
        {
            var r = new System.Random(seed); var c = new ItemCanvas(512, 724, ItemPal.Paper);
            Color ac = ItemPal.Accent(seed, 6), ac2 = ItemPal.Accent(seed, 7), ln = ItemPal.Line;
            c.Rect(0, 0, 512, 170, ac); c.TextCentered("PROJECT", 256, 34, 7, ItemPal.Ink); c.TextCentered("REPORT", 256, 98, 7, ItemPal.Ink);
            c.RRect(156, 196, 200, 12, 6, ln); c.RRect(196, 220, 120, 8, 4, ln);
            c.Line(60, 420, 60, 250, 5, ItemPal.Ink); c.Line(60, 420, 330, 420, 5, ItemPal.Ink);
            Color[] cols = { ItemPal.Pink, ItemPal.Mint, ItemPal.Sky, ItemPal.Lilac, ItemPal.Butter };
            for (int i = 0; i < 5; i++) { int hh = r.Next(40, 160); c.RRect(75 + i * 50, 414 - hh, 34, hh, 6, cols[i], ItemPal.Ink, 3); }
            c.Circle(430, 330, 48, ac, ItemPal.Ink, 4); c.Tri(430, 330, 478, 330, 430, 282, ac2);
            for (int i = 0; i < 4; i++)
            {
                float y = 480 + i * 50; c.Circle(60, y + 6, 7, ac2, ItemPal.Ink, 2); c.TextLines(82, y + 6, 380, 1, 0, 6, ln, seed + i + 30, 0.9f);
            }
            c.RRect(40, 690, 140, 8, 4, ln);
            return c;
        }

        // ---------------- Cards (512x320) ----------------
        static ItemCanvas BusinessCard(int seed, out RectInt nr)
        {
            var c = new ItemCanvas(512, 320, ItemPal.Paper); Color ac = ItemPal.Accent(seed, 8), ac2 = ItemPal.Accent(seed, 9);
            c.RRect(2, 2, 508, 316, 18, Color.white); c.RRect(2, 2, 150, 316, 18, ac); c.Rect(100, 2, 52, 316, ac);
            c.Circle(75, 90, 38, Color.white, ItemPal.Ink, 4); c.Circle(75, 90, 18, ac2, ItemPal.Ink, 2);
            string co = Pick(companies, seed, 1); float sc = Mathf.Min(2.5f, 130f / (co.Length * 6f)); c.TextCentered(co, 77, 160, sc, ItemPal.Ink);
            nr = new RectInt(180, 66, 300, 54); Under(c, nr);
            c.Text(Pick(titles, seed, 2) + (seed % 2 == 0 ? "" : " II"), 180, 146, 2.5f, ItemPal.Line);
            for (int i = 0; i < 3; i++) { c.Circle(192, 212 + i * 36, 9, ac, ItemPal.Ink, 2); c.TextLines(214, 212 + i * 36, 200, 1, 0, 6, ItemPal.Line, seed + i, 0.9f); }
            c.RRect(2, 2, 508, 316, 18, ItemPal.None, ItemPal.Ink, 4);
            return c;
        }

        static ItemCanvas Freelance(int seed)
        {
            var c = new ItemCanvas(512, 320, ItemPal.Butter); Color ac = ItemPal.Accent(seed, 10);
            for (int i = -2; i < 10; i++) c.Line(i * 60, 0, i * 60 + 320, 320, 22, new Color(1, 1, 1, 0.35f));
            c.RRect(40, 24, 432, 272, 26, Color.white, ItemPal.Ink, 5);
            c.RRect(80, 40, 352, 52, 26, ac, ItemPal.Ink, 4); c.TextCentered("AVAILABLE FOR HIRE", 256, 56, 3, ItemPal.Ink);
            for (int i = 0; i < 3; i++)
            {
                float x = 160 + i * 96;
                c.Circle(x, 170, 34, ItemPal.Gold, ItemPal.Ink, 4); c.Circle(x, 170, 24, ItemPal.None, ItemPal.Ink, 2);
                c.TextCentered("$", x, 156, 4, ItemPal.Ink); c.RRect(x - 30, 226, 60, 10, 5, ItemPal.Line);
            }
            return c;
        }

        static ItemCanvas IdCard(int seed, bool college, out RectInt nr)
        {
            var c = new ItemCanvas(512, 320, ItemPal.Paper);
            Color ac = college ? ItemPal.Lilac : ItemPal.Accent(seed, 11), ac2 = college ? ItemPal.Sky : ItemPal.Accent(seed, 12);
            c.RRect(2, 2, 508, 316, 20, Color.white); c.RRect(2, 2, 508, 92, 20, ac); c.Rect(2, 50, 508, 44, ac);
            c.RRect(216, 12, 80, 18, 9, ItemPal.Paper, ItemPal.Ink, 3);
            string org = college ? Pick(colleges, seed, 3) : Pick(companies, seed, 4);
            if (college)
            {
                c.Circle(56, 62, 24, Color.white, ItemPal.Ink, 3); c.RRect(44, 58, 11, 14, 3, ac2, ItemPal.Ink, 2); c.RRect(57, 58, 11, 14, 3, ac2, ItemPal.Ink, 2);
                c.Text(org, 94, 46, 3, ItemPal.Ink); c.Text("STUDENT ID", 94, 74, 1.8f, ItemPal.Ink);
                c.RRect(352, 118, 130, 164, 14, Color.Lerp(Color.white, ac2, 0.5f), ItemPal.Ink, 4); Face(c, 417, 170, 1f, ac2);
                nr = new RectInt(30, 128, 300, 54); Under(c, nr);
                c.Text("MAJOR " + Pick(titles, seed, 5), 30, 200, 2, ItemPal.Line); c.Text("ID " + (100000 + new System.Random(seed).Next(899999)), 30, 244, 3, ItemPal.Ink);
            }
            else
            {
                c.Text(org, 30, 46, 3, ItemPal.Ink); c.Text("EMPLOYEE", 30, 74, 1.8f, ItemPal.Ink);
                c.RRect(30, 118, 130, 164, 14, Color.Lerp(Color.white, ac2, 0.5f), ItemPal.Ink, 4); Face(c, 95, 170, 1f, ac2);
                nr = new RectInt(185, 128, 300, 54); Under(c, nr);
                c.Text(Pick(titles, seed, 6), 185, 200, 2, ItemPal.Line); c.Text("ID " + (100000 + new System.Random(seed).Next(899999)), 185, 244, 3, ItemPal.Ink);
            }
            c.RRect(30, 296, 452, 10, 5, ac2); c.RRect(2, 2, 508, 316, 20, ItemPal.None, ItemPal.Ink, 4);
            return c;
        }

        static ItemCanvas Slip(int seed)
        {
            var r = new System.Random(seed); var c = new ItemCanvas(256, 640, ItemPal.Paper); Color ac = ItemPal.Accent(seed, 13);
            c.Rect(0, 40, 256, 80, ac); c.TextCentered("REFERRAL", 128, 66, 4, ItemPal.Ink);
            c.Text("REFERRED BY", 62, 160, 2, ItemPal.Line); c.RRect(48, 196, 160, 10, 5, ItemPal.Ink); c.TextLines(48, 226, 160, 2, 20, 6, ItemPal.Line, seed, 0.6f);
            c.Circle(128, 330, 36, ItemPal.Butter, ItemPal.Ink, 4); c.TextCentered("OK", 128, 316, 4, ItemPal.Ink);
            float x = 30; while (x < 222) { float bw = r.Next(2, 7); c.Rect(x, 410, bw, 110, ItemPal.Ink); x += bw + r.Next(2, 6); }
            c.TextCentered("REF-" + (1000 + r.Next(8999)), 128, 540, 3, ItemPal.Ink);
            c.TearEdges(8, 8, seed);
            return c;
        }

        // ---------------- Booklet / envelope ----------------
        static ItemCanvas Portfolio(int seed)
        {
            var c = new ItemCanvas(512, 640, ItemPal.Accent(seed, 14));
            c.RRect(30, 30, 452, 580, 30, ItemPal.Paper, ItemPal.Ink, 5);
            c.TextCentered("PORTFOLIO", 256, 66, 7, ItemPal.Ink); c.RRect(156, 146, 200, 12, 6, ItemPal.Line);
            Color[] cols = { ItemPal.Pink, ItemPal.Mint, ItemPal.Sky, ItemPal.Lilac, ItemPal.Butter, ItemPal.Peach };
            for (int i = 0; i < 6; i++)
            {
                float x = 54 + (i % 2) * 214, y = 200 + (i / 2) * 120;
                c.RRect(x, y, 190, 100, 14, cols[(i + seed) % 6], ItemPal.Ink, 4);
                switch ((seed + i) % 3)
                {
                    case 0: c.Circle(x + 50, y + 34, 14, Color.white); c.Tri(x + 20, y + 88, x + 80, y + 50, x + 130, y + 88, Color.white); break;
                    case 1: for (int k = 0; k < 4; k++) c.RRect(x + 24 + k * 36, y + 80 - (20 + k * 12), 22, 20 + k * 12, 4, Color.white); break;
                    default: c.Line(x + 24, y + 80, x + 100, y + 24, 8, Color.white); c.Line(x + 70, y + 80, x + 160, y + 30, 8, Color.white); break;
                }
            }
            return c;
        }

        static ItemCanvas Envelope(int seed)
        {
            var c = new ItemCanvas(512, 360, ItemPal.Paper); Color ac = ItemPal.Accent(seed, 15), ac2 = ItemPal.Accent(seed, 16);
            c.RRect(4, 4, 504, 352, 14, ItemPal.Paper, ItemPal.Ink, 4);
            c.Tri(10, 10, 502, 10, 256, 205, Color.Lerp(ItemPal.Paper, ac, 0.4f));
            c.Line(8, 8, 256, 205, 4, ItemPal.Ink); c.Line(504, 8, 256, 205, 4, ItemPal.Ink);
            c.Line(8, 352, 190, 215, 3, ItemPal.Line); c.Line(504, 352, 322, 215, 3, ItemPal.Line);
            c.Circle(256, 205, 34, ac2, ItemPal.Ink, 4); c.Circle(256, 205, 18, ac, ItemPal.Ink, 2);
            c.TextLines(60, 275, 240, 3, 24, 7, ItemPal.Line, seed, 0.6f);
            c.RRect(420, 250, 64, 76, 6, ItemPal.Butter, ItemPal.Ink, 3); c.Circle(452, 288, 16, ac, ItemPal.Ink, 2);
            return c;
        }
    }
}
