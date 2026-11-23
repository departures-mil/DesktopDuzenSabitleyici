// CI helper: builds and verifies update patches.
//   DeltaMaker make  <old.exe> <new.exe> <out.patch>
//   DeltaMaker apply <old.exe> <in.patch> <out.exe>
using DDS;

if (args.Length != 4) { Console.Error.WriteLine("usage: make|apply a b c"); return 2; }
if (args[0] == "make")
{
    var patch = Delta.Create(File.ReadAllBytes(args[1]), File.ReadAllBytes(args[2]));
    File.WriteAllBytes(args[3], patch);
    Console.WriteLine($"patch {args[3]}: {patch.Length / 1048576.0:F2} MB (full {new FileInfo(args[2]).Length / 1048576.0:F2} MB)");
    return 0;
}
if (args[0] == "apply") { Delta.Apply(args[1], args[2], args[3]); Console.WriteLine("ok"); return 0; }
return 2;
