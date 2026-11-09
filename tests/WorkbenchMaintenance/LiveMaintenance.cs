using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ReforgerMcp;

internal static class LiveMaintenance
{
    internal static async Task<int> Run(string[] args)
    {
        if (args.Length != 4 || args[0] != "--live")
            throw new ArgumentException("--live <configuration.json> <Workbench PID> <new evidence directory>");
        int pid = int.Parse(args[2]);
        var output = Path.GetFullPath(args[3]);
        if (Directory.Exists(output)) throw new IOException("Use a new evidence directory.");
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Save(string name, object value) => File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
        void Check(bool condition,string name)
        {
            checks.Add(new { name, passed = condition });
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
        }
        bool passed = false;
        string? failure = null;
        var started = DateTimeOffset.UtcNow;
        string token = "";
        int sequence = 0;
        try
        {
            var cfg = Configuration.Load(args[1]);
            var http = cfg.LocalHttp ?? throw new Exception("Shared HTTP server required.");
            token = File.ReadAllText(http.TokenFile).Trim();
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri($"http://127.0.0.1:{http.Port}/mcp"),
                AdditionalHeaders = new Dictionary<string,string> { ["Authorization"] = "Bearer " + token }
            }), cancellationToken:deadline.Token);
            async Task<JsonElement> Call(string tool,object arguments,CancellationToken? cleanupToken=null)
            {
                var argsMap=JsonSerializer.Deserialize<Dictionary<string,object?>>(JsonSerializer.Serialize(arguments))!;
                var original = tool;
                while (true)
                {
                    var result=await client.CallToolAsync(tool,argsMap,cancellationToken:cleanupToken ?? deadline.Token);
                    var text=string.Join("\n",result.Content.OfType<TextContentBlock>().Select(b=>b.Text));
                    Save($"{++sequence:000}-{tool}.json",new { arguments=argsMap,result.IsError,text });
                    if(result.IsError==true)throw new Exception(original+": "+text);
                    var value=JsonDocument.Parse(text).RootElement.Clone();
                    if(value.TryGetProperty("poll",out var poll) &&
                        (!value.TryGetProperty("operationCompleted",out var completed) || !completed.GetBoolean()))
                    {
                        tool=poll.GetProperty("tool").GetString()!;
                        if(tool!="workbench_operation")throw new Exception("Unexpected poll owner");
                        argsMap=JsonSerializer.Deserialize<Dictionary<string,object?>>(poll.GetProperty("arguments").GetRawText())!;
                        await Task.Delay(200,cleanupToken ?? deadline.Token);
                        continue;
                    }
                    return value;
                }
            }
            string State(JsonElement e)=>e.GetProperty("state").GetString()!;
            void ThreadResult(JsonElement e) => Check(e.GetProperty("expectedThread").GetUInt32()==e.GetProperty("actualThread").GetUInt32()
                && e.GetProperty("allocationsReleased").GetBoolean(),"Owning editor thread and callback cleanup verified");
            using var before=Process.GetProcessById(pid);
            var processStart=before.StartTime;
            var title=before.MainWindowTitle;
            Save("before.json",new { pid,processStart,title });
            var first=await Call("workbench_project_context",new {processId=pid,scope="all",limit=100});
            Check(State(first)=="observed" && first.GetProperty("processId").GetInt32()==pid,"Live project context observed");
            var addons=first.GetProperty("addons").EnumerateArray().ToArray();
            Check(addons.Length==first.GetProperty("total").GetInt32() && first.GetProperty("totalLoaded").GetInt32()>0,
                "Complete bounded loaded/available addon snapshot");
            Check(addons.Count(a=>a.GetProperty("current").GetBoolean())==1 && !string.IsNullOrEmpty(first.GetProperty("currentProject").GetString()),
                "Exactly one current project");
            var readable=addons.Where(a=>a.GetProperty("declarationState").GetString()=="read").ToArray();
            Check(readable.Length>0 && readable.All(a=>a.GetProperty("guid").GetString()!.Equals(a.GetProperty("declaration").GetProperty("guid").GetString(),StringComparison.OrdinalIgnoreCase)),
                "Native addon GUIDs agree with readable project declarations");
            Check(addons.Any(a=>a.TryGetProperty("mountedRoot",out var mount) && Directory.Exists(mount.GetString())),
                "At least one native mounted root resolves to an existing directory");
            var tokenValue=first.GetProperty("projectToken").GetString();
            var page=await Call("workbench_project_context",new { processId=pid,scope="all",limit=1,offset=1,projectToken=tokenValue });
            Check(page.GetProperty("offset").GetInt32()==1 && page.GetProperty("total").GetInt32()==addons.Length && page.GetProperty("currentProject").GetString()==first.GetProperty("currentProject").GetString() && page.GetProperty("addons")[0].GetProperty("guid").GetString()==addons[1].GetProperty("guid").GetString(),
                "Project-token paging preserves addon identity");
            var focus=await Call("workbench_focus_policy",new { processId=pid,action="status" });
            var focusState=State(focus);
            Check(focusState is "enabled" or "disabled","Focus policy attached");
            try
            {
                Check(State(await Call("workbench_focus_policy",new {processId=pid,action="disable"}))=="disabled","Focus disable readback");
                Check(State(await Call("workbench_focus_policy",new {processId=pid,action="enable"}))=="enabled","Focus enable readback");
            }
            finally
            {
                // Restore even after an assertion failure; no foreground activation is requested.
                using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var restored=await Call("workbench_focus_policy",new {processId=pid,action=focusState=="enabled"?"enable":"disable"},cleanupToken:cleanupDeadline.Token);
                Check(State(restored)==focusState,"Original focus policy restored");
            }
            var authorization=await Call("workbench_script_authorization",new {processId=pid,action="allow"});
            Check(State(authorization)=="observed" && authorization.GetProperty("allowAll").GetBoolean() && authorization.GetProperty("decision").GetInt32()==32768,"Authorization setter readback");
            ThreadResult(authorization);
            authorization=await Call("workbench_script_authorization",new {processId=pid,action="status"});
            Check(State(authorization)=="observed" && authorization.GetProperty("allowAll").GetBoolean(),"Authorization persists on independent status");
            var leak=await Call("workbench_resource_leak_assertion",new {processId=pid,action="suppress"});
            Check(State(leak)=="suppressed" && leak.GetProperty("suppressed").GetBoolean(),"Specific assertion suppression setter readback");
            ThreadResult(leak);
            leak=await Call("workbench_resource_leak_assertion",new {processId=pid,action="status"});
            Check(State(leak)=="observed" && leak.GetProperty("suppressed").GetBoolean(),"Suppression persists on independent status");
            var play = await Call("workbench_play_status",new {processId=pid});
            Check(State(play) is "observed" or "world_unavailable", "Play observation reports present or absent World Editor");
            ThreadResult(play);
            var after=await Call("workbench_project_context",new {processId=pid,scope="all",limit=100});
            string Identity(JsonElement e)=>JsonSerializer.Serialize(e.GetProperty("addons").EnumerateArray()
                .Select(a=>new {guid=a.GetProperty("guid").GetString(),path=a.GetProperty("projectFile").GetString(),loaded=a.GetProperty("loaded").GetBoolean(),current=a.GetProperty("current").GetBoolean()}));
            Check(first.GetProperty("currentProject").GetString()==after.GetProperty("currentProject").GetString() && Identity(first)==Identity(after),
                "Current project and loaded/available addon identities unchanged");
            using var processAfter=Process.GetProcessById(pid);
            Check(processAfter.StartTime==processStart && processAfter.MainWindowTitle==title && processAfter.Responding,
                "Workbench process/title preserved and responding");
            passed=true;
        }
        catch(Exception ex) { failure=token.Length>0?ex.Message.Replace(token,"[redacted]"):ex.Message; }
        Save("report.json",new { started,finished=DateTimeOffset.UtcNow,scope="Workbench maintenance features and read-only Play observation on 1.9; not full MCP certification",
            passed,failure,checks,
            limits="No real focus-steal event or resource-leak condition induced. Native policy/readback plus separate machine-code fixtures tested. No compile, save, Play transition or project switch." });
        Console.WriteLine(passed?"Maintenance and Play-observation acceptance PASSED":"Maintenance acceptance FAILED: "+failure);
        return passed?0:1;
    }
}



