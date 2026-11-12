using ReforgerMcp;

internal static class AuthoringStartupChecks
{
    internal static Task Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "gc-startup-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var first = WorkbenchAuthoringStartup.AcquireLease(path))
            {
                if (first is null) throw new Exception("Initial startup owner missing");
                using var duplicate = WorkbenchAuthoringStartup.AcquireLease(path);
                if (duplicate is not null) throw new Exception("Duplicate startup owner admitted");
            }
            using var restarted = WorkbenchAuthoringStartup.AcquireLease(path);
            if (restarted is null) throw new Exception("Stale lock blocked a new module initialization");
            Console.WriteLine("PASS per-host startup duplicate exclusion and stale-lock recovery");
        }
        finally { File.Delete(path); }

        var control = Path.Combine(Path.GetTempPath(), "gc-attempt-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Versioned installations both use this account-level owner, independent
            // of their separate configuration/data paths.
            WorkbenchAuthoringStartup.ClaimLoadAttempt(control, 123, 1000);
            var refused = false;
            try { WorkbenchAuthoringStartup.ClaimLoadAttempt(control, 123, 1000); }
            catch (IOException) { refused = true; }
            if (!refused) throw new Exception("Upgrade replayed an uncertain load for the same process");
            WorkbenchAuthoringStartup.ClaimLoadAttempt(control, 123, 2000);
            Console.WriteLine("PASS shared startup attempt gate rejects replay and permits a fresh process identity");
            var dll = Path.Combine(control, "rstauthoring.dll");
            if (WorkbenchAuthoringStartup.HasVerifiedLoad(control, 123, 1000, dll, "abc")) throw new Exception("Unverified resident DLL accepted");
            WorkbenchAuthoringStartup.RecordVerifiedLoad(control, 123, 1000, dll, "abc");
            if (!WorkbenchAuthoringStartup.HasVerifiedLoad(control, 123, 1000, dll, "ABC")) throw new Exception("Verified installation rejected");
            if (WorkbenchAuthoringStartup.HasVerifiedLoad(control, 123, 1000, dll + ".other", "abc")
                || WorkbenchAuthoringStartup.HasVerifiedLoad(control, 123, 1000, dll, "changed")
                || WorkbenchAuthoringStartup.HasVerifiedLoad(control, 123, 2000, dll, "abc")) throw new Exception("Foreign path/build/process reused verification");
            File.WriteAllText(Path.Combine(control, "attempts", "123-1000.verified"), "incomplete");
            if (WorkbenchAuthoringStartup.HasVerifiedLoad(control, 123, 1000, dll, "abc")) throw new Exception("Incomplete verification accepted");
            Console.WriteLine("PASS resident DLL requires completed verification for the same process, path and bytes");
            var configuration = Path.Combine(control, "configuration.json");
            if (WorkbenchAuthoringStartup.IsActive(control, configuration)) throw new Exception("Missing activation admitted");
            File.WriteAllText(Path.Combine(control, "active.json"), System.Text.Json.JsonSerializer.Serialize(configuration));
            if (!WorkbenchAuthoringStartup.IsActive(control, configuration.ToUpperInvariant())) throw new Exception("Active installation refused");
            if (WorkbenchAuthoringStartup.IsActive(control, configuration + ".other")) throw new Exception("Superseded installation admitted");
            File.Delete(Path.Combine(control, "active.json"));
            if (WorkbenchAuthoringStartup.IsActive(control, configuration)) throw new Exception("Uninstalled activation admitted");
            Console.WriteLine("PASS activation identity, upgrade deactivation and uninstall deactivation");
        }
        finally
        {
            File.Delete(Path.Combine(control, "active.json"));
            File.Delete(Path.Combine(control, "attempts", "123-1000"));
            File.Delete(Path.Combine(control, "attempts", "123-2000"));
            File.Delete(Path.Combine(control, "attempts", "123-1000.verified"));
            Directory.Delete(Path.Combine(control, "attempts"));
            Directory.Delete(control);
        }
        return Task.CompletedTask;
    }
}

