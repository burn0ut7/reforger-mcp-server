# One shared local MCP server

Stdio clients launch their own child process. Concurrent chats or client refreshes
can therefore attempt to start several servers for the same data directory. The
exclusive store lock rejects the additional process before its MCP handshake.
Do not remove that lock or give each agent a duplicate analysis store.

Use the explicit HTTP mode for a shared local installation. It hosts the same
official-SDK tool catalogue and singleton capability services as stdio. All
clients share one configured Workbench setup and data owner. Native operation
gates continue to reject overlapping mutations. Navigation tokens can become
stale when another client changes the editor or refreshes an inventory; inspect
again on a stale result. Never replay an uncertain mutation on reconnect.

Add this section to the existing private configuration:

```json
"localHttp": {
  "port": 5780,
  "tokenFile": "shared-server.key"
}
```

Create the token file with at least 32 cryptographically random URL-safe
characters. Keep it private and outside source control. Relative paths are
resolved beside the configuration. The server requires this token on every
request, binds only IPv4 loopback, rejects Origin headers and validates the Host
header. It is intended for trusted clients on the same Windows account, not
remote access or browser-hosted clients. It does not bypass tool authority checks.

Start once, independently of the client:

```powershell
ReforgerMcp.exe --config C:/Tools/ReforgerMcp/configuration.json --serve-http
```

Configure clients for Streamable HTTP at `http://127.0.0.1:5780/mcp` with
`Authorization: Bearer <the token>`. For Codex, replace the server's command/args
with `url` and `http_headers.Authorization` in its private MCP configuration.
Do not configure both command and URL. Refresh the client's MCP configuration
after changing transport. A missing host is reported as unavailable; clients must
not start a stdio copy automatically.

For unattended startup, launch that same command as a single Windows logon task
with overlapping task instances disabled and a hidden window. Stop the owning
server before an upgrade, then launch the new package against the same private
configuration. Server restart invalidates in-memory operation and navigation
tokens. Inspect Workbench state before continuing; restart does not roll back
operations already dispatched to Workbench.

The HTTP transport is stateless; closing a chat does not stop the host. The store
lock also prevents a second HTTP host or a stdio process from owning that store.
Other MCP products shown in Task Manager are separate services, not necessarily
Reforger duplicates.

## Validation

The 2026-10-24 self-contained package passed eight discovery requests and eight
service-backed tool calls at concurrency four, a fresh initialization followed by
another tool call, missing-token and Origin/Host rejection, ambient Kestrel
endpoint rejection, and duplicate-store startup rejection with the original host
still healthy. Explicit stdio initialization, discovery and service invocation
also passed. Local evidence is retained under
`.temp/local/evidence/shared-http-20261024/`.

The deployed shared host subsequently captured the actual Workbench window and
completed Play stop, project-context and runtime-adapter status calls. The local
Codex configuration was changed to the shared endpoint; this alone does not prove
that an already-open chat has refreshed its tool catalogue. No prolonged stress
or remote-client support is claimed.

Transport follows the official SDK's [Streamable HTTP documentation](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).
