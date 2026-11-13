namespace GCWorkbench.UI;

internal static class Examples
{
    internal static DialogSpec Dependencies() => new()
    {
        Title = "External dependencies",
        PrimaryAction = "Install",
        Items = [
            new("GC Workbench Tools", "", "Not installed"),
            new("Git for Windows", "", "Installed", "ready")
        ]
    };

    internal static DialogSpec Updates() => new()
    {
        Title = "Update mods",
        PrimaryAction = "Update selected",
        Fields = [
            new() { Id = "lobby", Label = "Lobby2", Kind = "check", Checked = true },
            new() { Id = "core", Label = "GC Core", Kind = "check", Checked = true },
            new() { Id = "units", Label = "GC Units", Kind = "check", Checked = true },
            new() { Id = "missions", Label = "GC Missions", Kind = "check", Checked = true }
        ]
    };

    internal static DialogSpec Submission() => new()
    {
        Title = "Submit mission",
        PrimaryAction = "Review submission",
        Fields = [
            new() { Id = "title", Label = "Mission name", Value = "Operation Breakwater", Required = true },
            new() { Id = "summary", Label = "Changes", Kind = "multiline", Required = true },
            new() { Id = "tested", Label = "Tested in Workbench", Kind = "check" }
        ]
    };

    internal static IEnumerable<(string Name, DialogSpec Spec)> All()
    {
        yield return ("dependencies", Dependencies());
        yield return ("updates", Updates());
        yield return ("submission", Submission());
    }
}
