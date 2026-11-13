namespace GCWorkbench.UI;

internal static class ToolPages
{
    internal static DialogSpec Submission() => new()
    {
        Title = "Submit mission", PrimaryAction = "Review submission", SecondaryAction = "Close",
        Items = [new("Source", "", "Current Workbench mission"), new("Destination", "", "Mission pull request")]
    };

    internal static DialogSpec Dependencies() => new()
    {
        Title = "External dependencies", PrimaryAction = "Install / update", SecondaryAction = "Close",
        Items = [new("GC Workbench Tools", "", "Checking..."), new("Git for Windows", "", "Checking...")]
    };

    internal static DialogSpec GitHub() => new()
    {
        Title = "GitHub", PrimaryAction = "Sign in on GitHub", SecondaryAction = "Close",
        Items = [new("Connection", "", "Checking…")]
    };

    internal static IEnumerable<(string Name, DialogSpec Spec)> All()
    {
        yield return ("submission", Submission());
        yield return ("updates", Examples.Updates() with { SecondaryAction = "Close" });
        yield return ("github", GitHub());
        yield return ("dependencies", Dependencies());
    }

    internal static string Action(string title) => title switch
    {
        "Submit mission" => "submit", "Update mods" => "update", "External dependencies" => "install",
        _ => throw new ArgumentException("Unknown tool page.")
    };

    // Contract with GC_ToolsPlugin: 100 + the selected mod bits (1, 2, 4, 8).
    internal static int ModMask(Dictionary<string, object> values) =>
        new[] { "lobby", "core", "units", "missions" }.Select((id, index) => values.TryGetValue(id, out var selected) && selected is true ? 1 << index : 0).Sum();
}
