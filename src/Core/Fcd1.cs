using System.IO.Compression;
using System.Security.Cryptography;

namespace DDS.Core;

/// <summary>
/// FCD1 ikili yama uygulayıcısı (cekirdek/delta/FORMAT.md, normatif). Ortak release iş akışı
/// (reusable-release.yml → fcdelta.js) her sürümde son 5 sürümden bu biçimde yama üretir.
///   "FCD1" | codec(1)=0x01 zlib | srcLen u64 LE | srcSha256(32) | dstLen u64 LE | dstSha256(32) | zlib(op akışı)
///   op: 0x00 END · 0x01 COPY varint off, varint len · 0x02 INSERT varint len, bayt
/// Kaynak dosya seek+read ile okunur (belleğe alınmaz), çıktı geçici dosyaya yazılır, doğrulanınca yerine taşınır.
/// </summary>
public static class Fcd1
{
    public const int HeaderLen = 85;
    static readonly byte[] Magic = "FCD1"u8.ToArray();

    public sealed record Header(byte Codec, long SrcLen, byte[] SrcSha, long DstLen, byte[] DstSha);

    public static Header ReadHeader(Stream s)
    {
        var h = new byte[HeaderLen];
        s.ReadExactly(h);
        if (!h.AsSpan(0, 4).SequenceEqual(Magic)) throw new InvalidDataException("FCD1: magic yok");
        if (h[4] != 0x01) throw new InvalidDataException("FCD1: bilinmeyen codec " + h[4]);
        long srcLen = (long)BitConverter.ToUInt64(h, 5), dstLen = (long)BitConverter.ToUInt64(h, 45);
        if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException();
        if (srcLen < 0 || dstLen < 0) throw new InvalidDataException("FCD1: uzunluk");
        return new Header(h[4], srcLen, h[13..45], dstLen, h[53..85]);
    }

    /// <summary>Yamayı uygular; sonuç <paramref name="outFile"/>'a yalnızca tam doğrulamadan sonra yazılır (atomik taşıma).</summary>
    public static Header Apply(string srcFile, string patchFile, string outFile)
    {
        using var p = new FileStream(patchFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hdr = ReadHeader(p);

        using var src = new FileStream(srcFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (src.Length != hdr.SrcLen) throw new InvalidDataException("kurulu dosya yamanın kaynağı değil (uzunluk)");
        if (!SHA256.HashData(src).AsSpan().SequenceEqual(hdr.SrcSha)) throw new InvalidDataException("kurulu dosya yamanın kaynağı değil (SHA-256)");

        var tmp = outFile + ".fcd1-tmp";
        try
        {
            using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                long written = 0;
                var buf = new byte[1 << 16];
                void Emit(byte[] b, int n)
                {
                    written += n;
                    if (written > hdr.DstLen) throw new InvalidDataException("FCD1: çıktı dstLen'i aşıyor");
                    dst.Write(b, 0, n);
                    sha.AppendData(b, 0, n);
                }

                using (var z = new ZLibStream(p, CompressionMode.Decompress, leaveOpen: true))
                {
                    var zs = new BufferedStream(z, 1 << 16);
                    while (true)
                    {
                        int op = zs.ReadByte();
                        if (op < 0) throw new InvalidDataException("FCD1: END'siz akış");
                        if (op == 0x00) break;
                        if (op == 0x01)
                        {
                            ulong off = ReadVar(zs), len = ReadVar(zs);
                            if (off > (ulong)hdr.SrcLen || len > (ulong)hdr.SrcLen - off) throw new InvalidDataException("FCD1: COPY aralığı");
                            src.Position = (long)off;
                            long left = (long)len;
                            while (left > 0)
                            {
                                int n = src.Read(buf, 0, (int)Math.Min(buf.Length, left));
                                if (n <= 0) throw new EndOfStreamException();
                                Emit(buf, n); left -= n;
                            }
                        }
                        else if (op == 0x02)
                        {
                            long left = (long)ReadVar(zs);
                            if (left < 0 || left > hdr.DstLen) throw new InvalidDataException("FCD1: INSERT uzunluğu");
                            while (left > 0)
                            {
                                int n = zs.Read(buf, 0, (int)Math.Min(buf.Length, left));
                                if (n <= 0) throw new EndOfStreamException("FCD1: INSERT yarım");
                                Emit(buf, n); left -= n;
                            }
                        }
                        else throw new InvalidDataException("FCD1: bilinmeyen op " + op);
                    }
                    // END'den sonra açılmış veri kalmamalı
                    if (zs.ReadByte() >= 0) throw new InvalidDataException("FCD1: END'den sonra veri");
                }
                if (written != hdr.DstLen) throw new InvalidDataException("FCD1: uzunluk uyuşmuyor");
                if (!sha.GetHashAndReset().AsSpan().SequenceEqual(hdr.DstSha)) throw new InvalidDataException("FCD1: SHA-256 uyuşmuyor");
            }
            // zlib akışından sonra dosyada fazladan bayt kalmamalı: ZLibStream iç tamponu yüzünden konumdan anlaşılamaz;
            // sonuç zaten dstLen + dstSha256 ile doğrulandı (asıl güvence budur).
            File.Move(tmp, outFile, true);
            return hdr;
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            throw;
        }
    }

    static ulong ReadVar(Stream s)
    {
        ulong v = 0;
        for (int i = 0, shift = 0; i < 9; i++, shift += 7)
        {
            int b = s.ReadByte();
            if (b < 0) throw new EndOfStreamException("FCD1: varint yarım");
            v |= (ulong)(b & 0x7F) << shift;
            if (b < 0x80) return v;
        }
        throw new InvalidDataException("FCD1: varint çok uzun");
    }

    public static string Sha256Hex(string file)
    {
        using var f = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
    }
}
