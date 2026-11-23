namespace DDS;

public static class LayoutEngine
{
    public static Profile Shape(List<MonitorInfo> mons, string key, string display) =>
        new() { Key = key, Display = display, Monitors = mons.Select(MonitorRec.From).ToList() };

    public static Snapshot Capture(List<IconPos> icons, List<MonitorInfo> mons, Size spacing, string source)
    {
        var s = new Snapshot { SavedUtc = DateTime.UtcNow, Source = source, SpacingW = spacing.Width, SpacingH = spacing.Height };
        foreach (var i in icons)
        {
            var m = DisplayInfo.For(mons, i.X, i.Y);
            var w = m.Work;
            s.Icons.Add(new IconEntry
            {
                Key = i.Key, X = i.X, Y = i.Y, Mon = m.Model, Primary = m.Primary,
                RX = w.Width > 0 ? Math.Clamp((i.X - w.Left) / (double)w.Width, 0, 1) : 0,
                RY = w.Height > 0 ? Math.Clamp((i.Y - w.Top) / (double)w.Height, 0, 1) : 0,
            });
        }
        return s;
    }

    public static bool SameIcons(Snapshot a, Snapshot b)
    {
        if (a.Icons.Count != b.Icons.Count) return false;
        var d = new Dictionary<string, IconEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in a.Icons) d[e.Key] = e;
        foreach (var e in b.Icons)
            if (!d.TryGetValue(e.Key, out var o) || Math.Abs(o.X - e.X) > 3 || Math.Abs(o.Y - e.Y) > 3) return false;
        return true;
    }

    /// <summary>Two display setups that differ only in where the monitors sit relative to each other.</summary>
    public static bool SameScreens(Profile p, List<MonitorInfo> mons)
    {
        if (p.Monitors.Count != mons.Count) return false;
        var a = p.Monitors.Select(m => $"{m.Model}:{m.W}x{m.H}~{m.Dpi}{(m.Primary ? "P" : "")}").OrderBy(x => x);
        var b = mons.Select(m => $"{m.Model}:{m.Bounds.Width}x{m.Bounds.Height}~{m.Dpi}{(m.Primary ? "P" : "")}").OrderBy(x => x);
        return a.SequenceEqual(b);
    }

    /// <summary>
    /// Builds target positions for the current screens from a saved snapshot.
    /// Same monitor (same size and scale): exact pixel positions, only shifted if the monitor moved.
    /// Different monitor: every icon keeps its distance in grid cells from the nearest edges
    /// (an icon 2 columns from the right edge stays 2 columns from the right edge), so groups stay
    /// together instead of being stretched across a bigger screen.
    /// </summary>
    public static List<IconPos> Build(Profile src, Snapshot snap, List<MonitorInfo> mons, Size targetSpacing, out bool exact)
    {
        exact = true;
        var result = new List<IconPos>();
        if (mons.Count == 0) return result;
        var primary = mons.FirstOrDefault(m => m.Primary) ?? mons[0];
        var srcMons = src.Monitors.Count > 0 ? src.Monitors : StoreData.MonitorsFromKey(src.Key);
        var srcSpacing = SpacingOf(snap, srcMons);
        int tw = Math.Max(20, targetSpacing.Width), th = Math.Max(20, targetSpacing.Height);
        var used = new Dictionary<string, HashSet<(int, int)>>();
        var pending = new List<(IconEntry e, MonitorRec? sm, MonitorInfo tm)>();

        foreach (var e in snap.Icons)
        {
            var sm = SourceMonitor(srcMons, e);
            var tm = TargetMonitor(mons, primary, e, sm);
            if (sm != null && sm.W == tm.Bounds.Width && sm.H == tm.Bounds.Height && sm.Dpi == tm.Dpi)
            {
                int x = e.X - sm.X + tm.Bounds.X, y = e.Y - sm.Y + tm.Bounds.Y;
                result.Add(new IconPos(e.Key, x, y));
                Mark(used, tm, (x - tm.Work.Left + tw / 2) / tw, (y - tm.Work.Top + th / 2) / th);
            }
            else pending.Add((e, sm, tm));
        }

        if (pending.Count > 0) exact = false;

        // Grid cell of every pending icon on its source monitor.
        var cells = new List<(IconEntry e, MonitorRec? sm, MonitorInfo tm, int sc, int sr, int sCols, int sRows)>();
        foreach (var (e, sm, tm) in pending)
        {
            if (sm == null) { cells.Add((e, null, tm, -1, -1, 0, 0)); continue; }
            var sWork = sm.Work;
            int sw = Math.Max(20, srcSpacing.Width), sh = Math.Max(20, srcSpacing.Height);
            int sCols = Math.Max(1, sWork.Width / sw), sRows = Math.Max(1, sWork.Height / sh);
            int sc = Math.Clamp((int)Math.Round((e.X - sWork.Left) / (double)sw), 0, sCols - 1);
            int sr = Math.Clamp((int)Math.Round((e.Y - sWork.Top) / (double)sh), 0, sRows - 1);
            cells.Add((e, sm, tm, sc, sr, sCols, sRows));
        }

        // Every source monitor is cut once vertically and once horizontally, along the line that splits
        // the fewest icon groups (an empty gap if there is one), as close to the middle as possible.
        // Everything left of the cut keeps its distance from the left edge, everything right of it its
        // distance from the right edge (same for top/bottom). Whole rows and columns therefore stay aligned,
        // groups are never torn apart, and a bigger screen only gets extra empty space along the cuts.
        var colSplit = new Dictionary<MonitorRec, int>();
        var rowSplit = new Dictionary<MonitorRec, int>();
        foreach (var g in cells.Where(c => c.sm != null).GroupBy(c => c.sm!))
        {
            var f = g.First();
            colSplit[g.Key] = Split(g.Select(c => (c.sc, c.sr)), f.sCols);
            rowSplit[g.Key] = Split(g.Select(c => (c.sr, c.sc)), f.sRows);
        }

        // Place icons nearest to a corner first so the outer grid keeps its shape.
        foreach (var c in cells.OrderBy(c => Score(c.e)))
        {
            var tWork = c.tm.Work;
            int cols = Math.Max(1, tWork.Width / tw), rows = Math.Max(1, tWork.Height / th);
            int col, row;
            if (c.sm != null)
            {
                col = c.sc <= colSplit[c.sm] ? c.sc : cols - 1 - (c.sCols - 1 - c.sc);
                row = c.sr <= rowSplit[c.sm] ? c.sr : rows - 1 - (c.sRows - 1 - c.sr);
            }
            else
            {
                col = (int)Math.Round(c.e.RX * (cols - 1));
                row = (int)Math.Round(c.e.RY * (rows - 1));
            }
            col = Math.Clamp(col, 0, cols - 1);
            row = Math.Clamp(row, 0, rows - 1);
            if (!used.TryGetValue(c.tm.Device, out var set)) used[c.tm.Device] = set = new HashSet<(int, int)>();
            (col, row) = NearestFree(set, col, row, cols, rows);
            set.Add((col, row));
            result.Add(new IconPos(c.e.Key, tWork.Left + col * tw, tWork.Top + row * th));
        }
        return result;
    }

    /// <summary>
    /// Best cut along one axis: cells at index &lt;= result stick to the start edge, the rest to the end edge.
    /// <paramref name="occ"/> holds (index on this axis, index on the other axis).
    /// </summary>
    internal static int Split(IEnumerable<(int a, int b)> occ, int n)
    {
        if (n <= 1) return n - 1;
        var set = new HashSet<(int, int)>(occ);
        int best = n - 1;
        double bestScore = double.MaxValue, mid = (n - 1) / 2.0;
        for (int k = 0; k <= n - 2; k++)
        {
            int bridges = set.Count(o => o.Item1 == k && set.Contains((k + 1, o.Item2)));
            double score = bridges * 1000 + Math.Abs(k + 0.5 - mid);
            if (score <= bestScore) { bestScore = score; best = k; }
        }
        return best;
    }

    /// <summary>
    /// Like <see cref="Build"/>, and icons that are on the desktop now but missing from that record
    /// (added later, e.g. on the other screen) are placed where the newest other record that knows them puts them.
    /// </summary>
    public static List<IconPos> BuildAll(IEnumerable<Profile> all, Profile src, Snapshot snap, List<MonitorInfo> mons,
        Size spacing, IEnumerable<string>? present, out bool exact)
    {
        var result = Build(src, snap, mons, spacing, out exact);
        if (present == null) return result;
        var have = new HashSet<string>(result.Select(r => r.Key), StringComparer.OrdinalIgnoreCase);
        var missing = present.Where(k => !have.Contains(k)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (missing.Count == 0) return result;
        int tw = Math.Max(20, spacing.Width) / 2, th = Math.Max(20, spacing.Height) / 2;
        foreach (var p in all.Where(p => p != src && p.Latest != null).OrderByDescending(p => p.Latest!.SavedUtc))
        {
            var snapP = p.Latest!;
            if (!snapP.Icons.Any(i => missing.Contains(i.Key))) continue;
            foreach (var t in Build(p, snapP, mons, spacing, out _).Where(t => missing.Contains(t.Key)))
            {
                if (result.Any(r => Math.Abs(r.X - t.X) < tw && Math.Abs(r.Y - t.Y) < th)) continue; // cell taken
                result.Add(t);
                missing.Remove(t.Key);
            }
            if (missing.Count == 0) break;
        }
        return result;
    }

    static double Score(IconEntry e)
    {
        double rx = e.RX, ry = e.RY;
        return Math.Min(rx, 1 - rx) + Math.Min(ry, 1 - ry);
    }

    static void Mark(Dictionary<string, HashSet<(int, int)>> used, MonitorInfo m, int c, int r)
    {
        if (!used.TryGetValue(m.Device, out var set)) used[m.Device] = set = new HashSet<(int, int)>();
        set.Add((c, r));
    }

    static MonitorRec? SourceMonitor(List<MonitorRec> srcMons, IconEntry e)
    {
        foreach (var m in srcMons) if (m.Bounds.Contains(e.X, e.Y)) return m;
        return srcMons.FirstOrDefault(m => m.Model == e.Mon && m.Primary == e.Primary)
               ?? srcMons.FirstOrDefault(m => m.Model == e.Mon)
               ?? srcMons.FirstOrDefault(m => m.Primary == e.Primary);
    }

    static MonitorInfo TargetMonitor(List<MonitorInfo> mons, MonitorInfo primary, IconEntry e, MonitorRec? sm)
    {
        string model = sm?.Model ?? e.Mon;
        bool prim = sm?.Primary ?? e.Primary;
        return mons.FirstOrDefault(m => m.Model == model)
               ?? (prim ? primary : mons.FirstOrDefault(m => !m.Primary) ?? primary);
    }

    /// <summary>Grid spacing of a snapshot: stored value, otherwise measured from the icon positions.</summary>
    static Size SpacingOf(Snapshot s, List<MonitorRec> mons)
    {
        if (s.SpacingW > 10 && s.SpacingH > 10) return new Size(s.SpacingW, s.SpacingH);
        int dpi = mons.FirstOrDefault(m => m.Primary)?.Dpi ?? 96;
        int gx = MinGap(s.Icons.Select(i => i.X)), gy = MinGap(s.Icons.Select(i => i.Y));
        return new Size(gx > 0 ? gx : (int)Math.Round(76 * dpi / 96.0), gy > 0 ? gy : (int)Math.Round(98 * dpi / 96.0));
    }

    static int MinGap(IEnumerable<int> values)
    {
        var v = values.Distinct().OrderBy(x => x).ToList();
        int best = 0;
        for (int i = 1; i < v.Count; i++)
        {
            int g = v[i] - v[i - 1];
            if (g >= 30 && g <= 400 && (best == 0 || g < best)) best = g;
        }
        return best;
    }

    static (int, int) NearestFree(HashSet<(int, int)> used, int c, int r, int cols, int rows)
    {
        if (!used.Contains((c, r))) return (c, r);
        int maxR = Math.Max(cols, rows);
        for (int rad = 1; rad <= maxR; rad++)
        {
            (int, int)? best = null;
            int bestD = int.MaxValue;
            for (int dc = -rad; dc <= rad; dc++)
            for (int dr = -rad; dr <= rad; dr++)
            {
                if (Math.Max(Math.Abs(dc), Math.Abs(dr)) != rad) continue;
                int nc = c + dc, nr = r + dr;
                if (nc < 0 || nr < 0 || nc >= cols || nr >= rows || used.Contains((nc, nr))) continue;
                int d = dc * dc * 2 + dr * dr; // prefer staying in the same column
                if (d < bestD) { bestD = d; best = (nc, nr); }
            }
            if (best != null) return best.Value;
        }
        return (c, r);
    }

    /// <summary>Number of icons in <paramref name="current"/> that are not where <paramref name="target"/> wants them.</summary>
    public static int Mismatches(List<IconPos> target, List<IconPos> current, Size spacing)
    {
        var cur = new Dictionary<string, IconPos>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in current) cur[c.Key] = c;
        int tolX = Math.Max(8, spacing.Width / 2), tolY = Math.Max(8, spacing.Height / 2), bad = 0;
        foreach (var t in target)
            if (cur.TryGetValue(t.Key, out var c) && (Math.Abs(c.X - t.X) > tolX || Math.Abs(c.Y - t.Y) > tolY))
                bad++;
        return bad;
    }

    /// <summary>
    /// True when the change from <paramref name="a"/> to <paramref name="b"/> looks like Windows reshuffling
    /// the desktop (many icons moved by different amounts) rather than the user dragging a selection.
    /// </summary>
    public static bool LooksLikeReshuffle(Dictionary<string, Point> a, Dictionary<string, Point> b)
    {
        var deltas = new List<Point>();
        foreach (var kv in b)
            if (a.TryGetValue(kv.Key, out var p) && (Math.Abs(p.X - kv.Value.X) > 3 || Math.Abs(p.Y - kv.Value.Y) > 3))
                deltas.Add(new Point(kv.Value.X - p.X, kv.Value.Y - p.Y));
        if (deltas.Count < 3) return false;
        var d0 = deltas[0];
        int different = deltas.Count(d => Math.Abs(d.X - d0.X) > 3 || Math.Abs(d.Y - d0.Y) > 3);
        return different >= 2;
    }
}
