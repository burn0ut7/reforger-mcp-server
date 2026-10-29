using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class CaptureTools(WindowCapture capture)
{
    [McpServerTool(Name = "capture_windows", ReadOnly = true, OpenWorld = false)]
    [Description("List configured Workbench/game windows; windowId expires after 5 minutes, another listing or server restart. Minimized windows cannot be captured.")]
    public object Windows()
    {
        try { return capture.List(); }
        catch (Exception ex) when (ex is not McpException) { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "capture_screenshot", ReadOnly = true, OpenWorld = false)]
    [Description("Capture windowId from capture_windows as PNG content plus metadata. One capture at a time; 10-second deadline.")]
    public async Task<CallToolResult> Screenshot(string windowId,
        [Description("Maximum image width/height, preserving aspect ratio without upscaling; 320-3840.")] int maxDimension = 1600,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await capture.Screenshot(windowId, maxDimension, cancellationToken);
            return new CallToolResult
            {
                Content =
                [
                    new TextContentBlock { Text = JsonSerializer.Serialize(result.Metadata, Configuration.Json) },
                    ImageContentBlock.FromBytes(result.Png, "image/png")
                ]
            };
        }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "capture_sequence", ReadOnly = true, OpenWorld = false)]
    [Description("Capture windowId as ordered PNG frames with native timestamps; cadence may skip frames. One capture at a time; 256 MiB/10-second budget.")]
    public async Task<CallToolResult> Sequence(string windowId,
        [Description("Number of frames, 2-16.")] int frameCount = 8,
        [Description("Maximum image width/height, 320-3840, without upscaling.")] int maxDimension = 1280,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (frameCount is < 2 or > 16) throw new ArgumentException("frameCount must be 2-16.");
            var frames = await capture.Sequence(windowId, maxDimension, frameCount, cancellationToken);
            var result = new CallToolResult { Content = [] };
            foreach (var frame in frames)
            {
                result.Content.Add(new TextContentBlock { Text = JsonSerializer.Serialize(frame.Metadata, Configuration.Json) });
                result.Content.Add(ImageContentBlock.FromBytes(frame.Png, "image/png"));
            }
            return result;
        }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }
}
