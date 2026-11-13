namespace GCWorkbench.UI;

// Presentation only. Callers own operations, permissions and completion checks.
public sealed record DialogSpec
{
    public string Title { get; init; } = "GC Workbench";
    public string Description { get; init; } = "";
    public string Section { get; init; } = "";
    public string PrimaryAction { get; init; } = "Continue";
    public string SecondaryAction { get; init; } = "Cancel";
    public string Note { get; init; } = "";
    public string Accent { get; init; } = "#d4b874";
    public List<StatusItem> Items { get; init; } = [];
    public List<InputField> Fields { get; init; } = [];
    public List<ToolAction> Actions { get; init; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(PrimaryAction))
            throw new ArgumentException("A title and primary action are required.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(Accent, "^#[0-9a-fA-F]{6}$"))
            throw new ArgumentException("Accent must be a six-digit hex color.");
        if (Fields.Select(f => f.Id).Distinct().Count() != Fields.Count)
            throw new ArgumentException("Field IDs must be unique.");
        foreach (var field in Fields)
            if (string.IsNullOrWhiteSpace(field.Id) || string.IsNullOrWhiteSpace(field.Label) ||
                field.Kind is not ("text" or "multiline" or "check"))
                throw new ArgumentException("Each field needs a unique ID, a label and a supported kind.");
        foreach (var item in Items)
            if (item.State is not ("ready" or "pending" or "warning" or "error"))
                throw new ArgumentException("Unsupported status state.");
        if (Actions.Select(a => a.Id).Distinct().Count() != Actions.Count ||
            Actions.Any(a => string.IsNullOrWhiteSpace(a.Id) || string.IsNullOrWhiteSpace(a.Label) || a.Id == "cancel"))
            throw new ArgumentException("Actions need unique IDs and labels; cancel is reserved.");
    }
}

public sealed record StatusItem(string Title, string Description, string Status, string State = "pending");
public sealed record InputField
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Help { get; init; } = "";
    public string Kind { get; init; } = "text";
    public string Value { get; init; } = "";
    public bool Checked { get; init; }
    public bool Required { get; init; }
    public bool ReadOnly { get; init; }
}

public sealed record DialogResult(string Action, Dictionary<string, object> Values);
public sealed record ToolAction(string Id, string Label);
