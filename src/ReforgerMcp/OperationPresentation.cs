using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReforgerMcp;

// Caller-owned revisions: observing a result never consumes it for another client.
internal static class OperationPresentation
{
    internal static object Present(object result, string operationId, bool completed,
        string pollTool, string? knownRevision = null, bool details = false)
    {
        var value = JsonSerializer.SerializeToNode(result, Configuration.Json)!.AsObject();
        value["operationId"] = operationId;
        value["operationCompleted"] = completed;
        if (!details && !completed)
        {
            value.Remove("message");
            value.Remove("scope");
            if (value["dialogInspection"] is JsonObject inspection)
            {
                inspection.Remove("scope");
                inspection.Remove("observedAt");
            }
        }
        var identity = value.DeepClone().AsObject();
        // Observation time alone is not progress. Token expiry/IDs remain significant.
        if (identity["dialogInspection"] is JsonObject dialog)
        {
            dialog.Remove("observedAt");
            if (dialog["windows"] is JsonArray { Count: 0 }) dialog.Remove("expiresAt");
        }
        if (identity["blockerInspection"] is JsonObject blocker) blocker.Remove("observedAt");
        var revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity))).ToLowerInvariant();
        if (!details && knownRevision == revision)
            value = new JsonObject
            {
                ["state"] = value["state"]?.DeepClone(), ["operationId"] = operationId,
                ["operationCompleted"] = completed, ["unchanged"] = true
            };
        value["revision"] = revision;
        if (!completed)
            value["poll"] = JsonSerializer.SerializeToNode(new
            {
                tool = pollTool, arguments = new { operationId, knownRevision = revision }
            });
        return value;
    }
}
