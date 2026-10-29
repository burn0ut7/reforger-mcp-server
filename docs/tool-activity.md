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
Strings and nested payloads are redacted by default, except bounded entity/component/method/return-type names for runtime call and preparation. Only a small allowlist of
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

## Runtime comparison evidence

`workbench_runtime_call` combines validation and execution in one adapter request;
legacy prepare/invoke remains measurable alongside it. `RUNTIME` JSON records share
the originating MCP call ID even when work completes after a pending response.
They include action, receipt, entity/component/method names, argument types, outcome,
dispatch status, invocation-attempt flag, exception type, and phase timings. Argument
values, return values, generated source and exception messages are excluded. JSON
escaping prevents target names from creating extra log lines. These records describe
execution, not the AI's reasoning or the user's purpose.

A start record without a terminal record has an unestablished outcome. `not_sent`
means runtime transport was not entered; `sent_outcome_unknown` conservatively means
the transport was entered but no validated result is available (it does not prove
bytes reached the engine). `returned` means the response was validated, not that
gameplay succeeded. Rejected input or operation-lane refusal before runtime work
starts has only the ordinary tool record. Pending polls do not count as executions.

`prepareMs` includes `compileMs`; do not add them together. `invokeMs` covers target
checks and synchronous invocation; absent phases are null. Server elapsed time starts
after process/token selection and includes live preflight and transport. Engine
measurements have millisecond resolution. No phase measures deferred gameplay work.
Session headers identify the server module build and adapter build.

The footer retains counts, outcome totals and phase sums for the first 32
(action, component, method) combinations, with additional combinations under `other`.
These aggregates continue after detail logging fills up. `Failed` includes returned
non-success states; `Uncertain` counts unvalidated transport outcomes. `Timed` counts
operations with a preparation or invocation timing, including zero milliseconds.

For before/after comparison, use the same method, arguments, world and build; compare
legacy prepare-plus-invoke end-to-end time with the combined call, interleaving samples
and excluding warm-up. Count completed invocations separately from public requests.
Do not treat fewer requests as proof of faster compilation. Preserve baseline files
before session retention removes them; capped details cannot reconstruct every call.

## Response size accounting

Each returned tool result records `response_text_bytes`: the sum of UTF-8 byte counts
of SDK `TextContentBlock.Text` values. `response_structured_bytes` counts the raw JSON
text of `StructuredContent` separately when present; it may describe the same content,
so do not assume both are shown to the model. `response_nontext_blocks` counts image,
audio and other non-text blocks without reading or decoding their payloads. These
measurements exclude JSON-RPC/SSE framing and are neither network sizes nor token
counts. No response content is written to the activity log.

The `response_per_tool` footer preserves response count, total/max text bytes,
structured bytes and non-text block count for the same bounded tool groups as
`per_tool` (128 names, then `other`). Totals continue after the detail cap. The 1 MB
file cap reserves 64 KB for the growing summary. Returned MCP errors are measured;
thrown exceptions have unavailable size because no result reached this filter.

Every delivered response counts, including repeated polls and detail retrievals:
these also consume client context. They are attributed to `workbench_operation`,
not counted as fresh runtime executions. Use `runtime_per_method`/receipt records
for execution counts and `response_per_tool` for output volume. Compare calls on
matching server builds and workloads, and separate text from media-heavy tools.
