using System.IO.Compression;
using System.Security.Cryptography;

namespace DDS;

/// <summary>ESKİ (1.0.x) yama biçimi DDSD1 — yalnızca köprü için: 1.0.x istemcileri bu adla (DesktopDuzenSabitleyici-patch-from-X.bin) yama arar.
/// 1.1.0+ istemciler ortak FCD1 biçimini kullanır (src/Core/Fcd1.cs).
/// <summary>
/// Binary delta (patch) between two versions of the exe.
/// Only the parts that actually changed travel over the network; everything else is copied
/// from the exe already on the user's disk. Content-defined chunking makes it robust to
/// inserted/removed bytes (shifted data is still found).
///
/// File format:  "DDSD1" | srcLen(8) | srcSha256(32) | dstLen(8) | dstSha256(32) | Brotli(op stream)
/// Op stream:    0x01 varint(offset) varint(len)  → copy from old file
///               0x02 varint(len) bytes           → new data
///               0x00                             → end
/// </summary>
public static class Delta
{
    static readonly byte[] Magic = "DDSD1"u8.ToArray();
    const int MinChunk = 2 * 1024, MaxChunk = 64 * 1024;
    const ulong Mask = (1UL << 13) - 1; // ~8 KB average chunk

    static readonly ulong[] Gear = BuildGear();
    static ulong[] BuildGear()
    {
        var g = new ulong[256];
        ulong x = 0x9E3779B97F4A7C15UL;
        for (int i = 0; i < 256; i++)
        {
            x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            g[i] = x;
        }
        return g;
    }

    static List<(int Off, int Len)> Chunks(byte[] d)
    {
        var list = new List<(int, int)>();
        int start = 0;
        while (start < d.Length)
        {
            int end = Math.Min(d.Length, start + MaxChunk);
            int cut = end;
            ulong h = 0;
            for (int i = start; i < end; i++)
            {
                h = (h << 1) + Gear[d[i]];
                if (i - start + 1 >= MinChunk && (h & Mask) == 0) { cut = i + 1; break; }
            }
            list.Add((start, cut - start));
            start = cut;
        }
        return list;
    }

    static string Key(byte[] d, int off, int len) =>
        Convert.ToHexString(SHA256.HashData(d.AsSpan(off, len)));

    /// <summary>Creates a patch that turns <paramref name="oldData"/> into <paramref name="newData"/>.</summary>
    public static byte[] Create(byte[] oldData, byte[] newData)
    {
        var index = new Dictionary<string, int>();
        foreach (var (off, len) in Chunks(oldData))
            index.TryAdd(Key(oldData, off, len) + ":" + len, off);

        using var ops = new MemoryStream();
        long copyOff = -1, copyLen = 0;
        var pending = new MemoryStream();

        void FlushData()
        {
            if (pending.Length == 0) return;
            ops.WriteByte(2); WriteVar(ops, (ulong)pending.Length);
            pending.Position = 0; pending.CopyTo(ops); pending.SetLength(0);
        }
        void FlushCopy()
        {
            if (copyLen == 0) return;
            ops.WriteByte(1); WriteVar(ops, (ulong)copyOff); WriteVar(ops, (ulong)copyLen);
            copyLen = 0; copyOff = -1;
        }

        foreach (var (off, len) in Chunks(newData))
        {
            if (index.TryGetValue(Key(newData, off, len) + ":" + len, out int src))
            {
                FlushData();
                if (copyLen > 0 && copyOff + copyLen == src) copyLen += len;   // merge consecutive copies
                else { FlushCopy(); copyOff = src; copyLen = len; }
            }
            else
            {
                FlushCopy();
                pending.Write(newData, off, len);
            }
        }
        FlushData(); FlushCopy();
        ops.WriteByte(0);

        using var outMs = new MemoryStream();
        outMs.Write(Magic);
        WriteI64(outMs, oldData.Length); outMs.Write(SHA256.HashData(oldData));
        WriteI64(outMs, newData.Length); outMs.Write(SHA256.HashData(newData));
        using (var br = new BrotliStream(outMs, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            ops.Position = 0; ops.CopyTo(br);
        }
        return outMs.ToArray();
    }

    /// <summary>Applies a patch to the old file and writes the result. Throws if anything does not match exactly.</summary>
    public static void Apply(string oldFile, string patchFile, string outFile)
    {
        byte[] oldData;
        using (var fs = new FileStream(oldFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            oldData = new byte[fs.Length];
            fs.ReadExactly(oldData);
        }

        using var p = File.OpenRead(patchFile);
        var magic = new byte[Magic.Length]; p.ReadExactly(magic);
        if (!magic.AsSpan().SequenceEqual(Magic)) throw new InvalidDataException("Not a patch file");
        long srcLen = ReadI64(p); var srcHash = new byte[32]; p.ReadExactly(srcHash);
        long dstLen = ReadI64(p); var dstHash = new byte[32]; p.ReadExactly(dstHash);
        if (oldData.Length != srcLen || !SHA256.HashData(oldData).AsSpan().SequenceEqual(srcHash))
            throw new InvalidDataException("Installed file differs from the patch source");

        using (var br = new BrotliStream(p, CompressionMode.Decompress))
        using (var o = File.Create(outFile))
        {
            var buf = new byte[81920];
            while (true)
            {
                int op = br.ReadByte();
                if (op == 0) break;
                if (op == 1)
                {
                    long off = (long)ReadVar(br), len = (long)ReadVar(br);
                    if (off < 0 || off + len > oldData.Length) throw new InvalidDataException("Bad copy range");
                    o.Write(oldData, (int)off, (int)len);
                }
                else if (op == 2)
                {
                    long len = (long)ReadVar(br);
                    while (len > 0)
                    {
                        int n = br.Read(buf, 0, (int)Math.Min(buf.Length, len));
                        if (n <= 0) throw new EndOfStreamException();
                        o.Write(buf, 0, n); len -= n;
                    }
                }
                else throw new InvalidDataException("Bad op");
            }
        }

        using var check = File.OpenRead(outFile);
        if (check.Length != dstLen || !SHA256.HashData(check).AsSpan().SequenceEqual(dstHash))
            throw new InvalidDataException("Patched file checksum mismatch");
    }

    static void WriteVar(Stream s, ulong v)
    {
        while (v >= 0x80) { s.WriteByte((byte)(v | 0x80)); v >>= 7; }
        s.WriteByte((byte)v);
    }
    static ulong ReadVar(Stream s)
    {
        ulong v = 0; int shift = 0;
        while (true)
        {
            int b = s.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            v |= (ulong)(b & 0x7F) << shift;
            if (b < 0x80) return v;
            shift += 7;
        }
    }
    static void WriteI64(Stream s, long v) { Span<byte> b = stackalloc byte[8]; BitConverter.TryWriteBytes(b, v); s.Write(b); }
    static long ReadI64(Stream s) { Span<byte> b = stackalloc byte[8]; s.ReadExactly(b); return BitConverter.ToInt64(b); }
}
