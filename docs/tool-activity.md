# Tool activity logs

Every server run automatically writes compact UTF-8 activity logs under
`<dataDirectory>/tool-activity/`. No configuration, database, or external service
is needed. The newest five session files (including the active run) are retained.
The exclusive data-directory owner creates the log; rejected duplicate servers
do not rotate it.

Stdio has one connection per process. Shared HTTP is intentionally stateless:
its session is the **host run**, not a chat, HTTP request, TCP connection, or SSE
stream. Clients share this log. Closing/reopening a response stream does not end
the session. Each tool invocation gets a server-generated call ID, unique within
the log even when clients reuse JSON-RPC IDs. No client identities or tokens are
recorded. Stateful HTTP would require its own session lifecycle integration.

The central SDK call-tool filter records UTC timestamps, session ID, call ID,
tool name, safe parameter summaries, OK/FAIL, and duration in milliseconds.
Strings and nested payloads are redacted by default. Only a small allowlist of
numeric/boolean controls is shown; at most 16 parameter names and 64 characters
per name are retained. Source text, credentials, images, results, and error bodies
are never copied. Errors identify cancellation, exception type, or a tool-reported
error. A successful tool response describing a pending operation remains OK:
the duration measures this invocation, not a background operation's eventual life.
Protocol failures rejected before the SDK dispatch filter are outside this log.

Each file is capped at 1,000,000 bytes, with space reserved for a summary. After
the detail budget fills, further detail lines are omitted and counted; totals
continue updating. The summary footer is refreshed on every start/completion and
marked `ended` at graceful shutdown. It includes calls, completions, failures,
in-flight calls, per-tool counts, the slowest completed call, elapsed session time,
and accumulated completed tool duration. Concurrent durations overlap, so their
sum can exceed elapsed time. Up to 128 distinct tool names are counted separately;
additional names use `other`. A forced termination can leave an `active` footer
or an incomplete final write; it cannot produce a reliable end-session summary.

File access is serialized only for logging; calls run concurrently. Disk and
serialization errors cannot replace tool results or exceptions. A generic warning
is sent to stderr once if logging fails; no log output is written to MCP stdout.
Retention is best-effort if the filesystem rejects deletion. Existing filesystem
permissions govern access; these are local operational logs, not an audit trail.

Verification: `dotnet build ReforgerMcp.slnx` and
`dotnet run --project tests/ToolActivity/ToolActivity.csproj`.
