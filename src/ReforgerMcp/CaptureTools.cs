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
    [Description("List visible top-level windows for workbench.executable and captureExecutables, including separate game clients and dialogs. Returns exact windowId selections valid for five minutes, until the next listing, or server restart. Does not activate windows. Minimized windows are listed but cannot be captured. Native-analysis targets do not implicitly enable capture.")]
    public object Windows()
    {
        try { return capture.List(); }
        catch (Exception ex) when (ex is not McpException) { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "capture_screenshot", ReadOnly = true, OpenWorld = false)]
    [Description("Capture one selected window directly with Windows Graphics Capture. Returns a PNG image content block and metadata through MCP, not a file path. Requires a windowId from capture_windows. No desktop capture, activation, scripts, hooks or fallback. Minimized/stale/unavailable windows fail explicitly. A screenshot is an observation, not proof of application readiness. One capture at a time; ten-second deadline. Windows may show its normal capture border.")]
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
}
