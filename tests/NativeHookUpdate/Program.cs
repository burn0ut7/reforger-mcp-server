using System.Text.Json;
using ReforgerMcp;

int count = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
var root = Path.Combine(Path.GetTempPath(), "reforger-hooks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "src", "ReforgerMcp"));
try
{
    File.WriteAllText(Path.Combine(root, "src", "ReforgerMcp", "NativeWorkbench.cs"), """
        Call(0x123456); e.Q(image + 0x123456);
        CheckCode(exe, 0x234567); CheckCode(qt, 0x345678);
        foreach (var rva in new[] { 0x456789, 0x56789a }) CheckCode(exe, rva);
        Call(command.Action == "redo" ? 0x6789ab : 0x789abc);
        var flags = 0x10047a; var field = app + 0x164e; // Call(0xabcdef)
        e.Q(widgets + 0x987654);
        private const int ResourceLeakSuppressionRva = 0x89abcd;
        """);
    var hooks = NativeHookUpdate.Inventory(root);
    Check(hooks.Length == 7, "explicit Workbench hooks included; Qt, flags, fields and comments excluded");
    Check(hooks.Select(h => h.Rva).SequenceEqual(new[] { "123456", "234567", "456789", "56789a", "6789ab", "789abc", "89abcd" }), "inventory deterministic numeric order");
    Check(hooks[0].Uses.Length == 1 && hooks[0].Uses[0].Line == 1, "duplicate same-line use deduplicated with source provenance");
    File.WriteAllText(Path.Combine(root, "src", "ReforgerMcp", "NativeWorkbench.Profile.cs"), """
        MaintenanceProfile Legacy = new(false,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            0x123456, [0x234567], 0, 0);
        MaintenanceProfile Current = new(true,
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            0x111111, [0x222222], 0x333333, 0x444444);
        """);
    Check(NativeHookUpdate.Inventory(root, new string('a',64)).Length == 7, "legacy inventory excludes new profile operands");
    Check(NativeHookUpdate.Inventory(root, new string('b',64)).Select(h=>h.Rva).SequenceEqual(new[]{"111111","222222","333333","444444"}), "limited profile excludes unported legacy addresses");
    try { NativeHookUpdate.Inventory(root, new string('c',64)); throw new Exception("accepted unknown profile"); }
    catch(InvalidDataException) { Check(true, "unknown baseline rejected"); }
    try { NativeHookUpdate.Inventory(root); throw new Exception("accepted unspecified profile"); }
    catch(InvalidDataException) { Check(true, "multi-profile source requires baseline"); }
    using var result = JsonDocument.Parse("""
        {"hooks":[
          {"id":"workbench_123456","state":"candidate","candidateCount":1,"candidateRvas":["234567"]},
          {"id":"workbench_234567","state":"ambiguous","candidateCount":2,"candidateRvas":["345678","456789"]},
          {"id":"workbench_456789","state":"unresolved","candidateCount":0,"candidateRvas":[]}
        ]}
        """);
    var source = NativeHookUpdate.CandidateSource(result.RootElement, new string('a', 64));
    Check(source.Contains("workbench_123456 = 0x234567") && !source.Contains("uint workbench_234567") && !source.Contains("uint workbench_456789"), "only unique candidates emitted");
    Check(source.Contains("Not compiled into the server") && source.Contains(new string('a', 64)), "candidate warning and binary identity retained");
    using var invalid = JsonDocument.Parse("""{"hooks":[{"id":"workbench_123456","state":"candidate","candidateCount":2,"candidateRvas":["abc"]}]}""");
    try { NativeHookUpdate.CandidateSource(invalid.RootElement, new string('a', 64)); throw new Exception("accepted inconsistent result"); }
    catch (InvalidDataException) { Check(true, "inconsistent candidate count rejected"); }
    try { NativeHookUpdate.CandidateSource(result.RootElement, "bad"); throw new Exception("accepted bad identity"); }
    catch (InvalidDataException) { Check(true, "invalid binary identity rejected"); }
    var path = Path.Combine(root, "hash.txt"); File.WriteAllText(path, "one"); var before = NativeHookUpdate.HashFile(path);
    File.WriteAllText(path, "two"); Check(before != NativeHookUpdate.HashFile(path), "changed source bytes invalidate identity");
    Console.WriteLine($"Passed {count} checks.");
}
finally { Directory.Delete(root, recursive: true); }

