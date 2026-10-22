using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

public sealed partial class WorkbenchSession
{
    internal Task<object> CreateChildPrefab(int processId, string ancestorResource, string prefabPath, CancellationToken ct)
    {
        if (processId <= 0 || !Regex.IsMatch(ancestorResource, "^\\{[0-9A-Fa-f]{16}\\}.+\\.et$", RegexOptions.IgnoreCase) ||
            ancestorResource.Any(char.IsControl) || ancestorResource.Contains(';') || Encoding.UTF8.GetByteCount(ancestorResource) > 240)
            throw new ArgumentException("Use a positive processId and a GUID-qualified .et ancestor resource, at most 240 UTF-8 bytes.");
        if (!Path.IsPathFullyQualified(prefabPath) || !prefabPath.EndsWith(".et", StringComparison.OrdinalIgnoreCase) ||
            prefabPath.Any(char.IsControl) || prefabPath.Contains(';'))
            throw new ArgumentException("prefabPath must be an absolute new .et path.");
        var destination = Path.GetFullPath(prefabPath).Replace('\\', '/');
        if (Encoding.UTF8.GetByteCount(destination) > 240 || !Directory.Exists(Path.GetDirectoryName(destination)))
            throw new ArgumentException("Use an existing destination directory and a path of at most 240 UTF-8 bytes.");
        if (File.Exists(destination) || Directory.Exists(destination) || File.Exists(destination + ".meta") || Directory.Exists(destination + ".meta"))
            throw new ArgumentException("Destination prefab or metadata already exists; inherited creation never intentionally overwrites.");
        return Start(token =>
        {
            using var process = SelectProcess();
            if (process.Id != processId) throw new InvalidOperationException("Workbench process changed.");
            var windows = Windows(process.Id);
            RequiredWindow(windows, ResourceManager);
            if (windows.Any(IsModal) || windows.Any(w => !w.Current.IsEnabled))
                throw Blocked(process, "Resolve modal dialogs before creating an inherited prefab.");
            editObservations.Clear();
            return NativeWorkbench.Execute(process, token, childPrefab: new(ancestorResource, destination));
        }, ct);
    }
}

[McpServerToolType]
public sealed class WorkbenchPrefabTools(WorkbenchSession session)
{
    [McpServerTool(Name = "workbench_create_child_prefab", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create a new inherited .et prefab through Workbench's native resource-creation owner. ancestorResource must be GUID-qualified; prefabPath must be an absolute new path in an existing writable addon directory. Rejects existing resource or metadata. Creates a detached child with an explicit ancestor and a new resource identity; does not create a world entity or export an instance. Waits for resource database acknowledgement and returns metadata identity evidence. Creation is outside world undo; incomplete results can leave files. Never blindly replay. Poll pending workbench_operation on this connection.")]
    public async Task<object> CreateChild(int processId, string ancestorResource, string prefabPath, CancellationToken cancellationToken = default)
    {
        try { return await session.CreateChildPrefab(processId, ancestorResource, prefabPath, cancellationToken); }
        catch (Exception e) when (e is not (McpException or OperationCanceledException)) { throw new McpException(e.Message); }
    }
}
