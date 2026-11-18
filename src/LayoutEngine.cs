namespace DDS;

public static class LayoutEngine
{
    public static Layout Capture(List<IconPos> icons, List<MonitorInfo> mons, string key, string display)
    {
        var l = new Layout { ConfigKey = key, Display = display, SavedUtc = DateTime.UtcNow };
        foreach (var i in icons)
        {
            var m = DisplayInfo.For(mons, i.X, i.Y);
            var w = m.Work;
            l.Icons.Add(new IconEntry
            {
                Key = i.Key, X = i.X, Y = i.Y, Mon = m.Model, Primary = m.Primary,
                RX = w.Width > 0 ? Math.Clamp((i.X - w.Left) / (double)w.Width, 0, 1) : 0,
                RY = w.Height > 0 ? Math.Clamp((i.Y - w.Top) / (double)w.Height, 0, 1) : 0,
            });
        }
        return l;
    }

    public static List<IconPos> Absolute(Layout l) => l.Icons.Select(e => new IconPos(e.Key, e.X, e.Y)).ToList();

    /// <summary>
    /// Re-creates a layout on a different screen setup: each icon keeps its relative place
    /// (top-left stays top-left, middle stays middle) and snaps to the nearest free grid cell.
    /// </summary>
    public static List<IconPos> Proportional(Layout l, List<MonitorInfo> mons, Size spacing)
    {
        var result = new List<IconPos>();
        if (mons.Count == 0) return result;
        var primary = mons.FirstOrDefault(m => m.Primary) ?? mons[0];
        var used = new Dictionary<string, HashSet<(int, int)>>();

        foreach (var e in l.Icons)
        {
            MonitorInfo t = e.Primary
                ? primary
                : mons.FirstOrDefault(m => !m.Primary && m.Model == e.Mon) ?? primary;
            var w = t.Work;
            int cw = Math.Max(20, spacing.Width), ch = Math.Max(20, spacing.Height);
            int cols = Math.Max(1, w.Width / cw), rows = Math.Max(1, w.Height / ch);
            int col = Math.Clamp((int)Math.Floor(e.RX * cols + 0.5), 0, cols - 1);
            int row = Math.Clamp((int)Math.Floor(e.RY * rows + 0.5), 0, rows - 1);

            if (!used.TryGetValue(t.Device, out var set)) used[t.Device] = set = new HashSet<(int, int)>();
            (col, row) = NearestFree(set, col, row, cols, rows);
            set.Add((col, row));
            result.Add(new IconPos(e.Key, w.Left + col * cw, w.Top + row * ch));
        }
        return result;
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
                int d = dc * dc + dr * dr;
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
}
