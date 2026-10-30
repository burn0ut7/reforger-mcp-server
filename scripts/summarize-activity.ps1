[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, Position=0)][string[]]$Path,
    [ValidateRange(1,30)][int]$Top = 8,
    [ValidateSet('textBytes','totalMs','failures','calls')][string]$SortBy = 'textBytes',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$files = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
foreach ($item in $Path) {
    $resolved = @(Resolve-Path -LiteralPath $item -ErrorAction Stop)
    foreach ($entry in $resolved) {
        if (Test-Path -LiteralPath $entry.Path -PathType Container) {
            Get-ChildItem -LiteralPath $entry.Path -Filter 'session-*.log' -File | ForEach-Object { $files.Add($_) }
        } else { $files.Add([System.IO.FileInfo]$entry.ProviderPath) }
    }
}

function Get-JsonField([string]$line, [string]$name) {
    $m = [regex]::Match($line, '(?:^| )' + [regex]::Escape($name) + '=(\{.*?\})(?= [A-Za-z_][A-Za-z0-9_]*=|$)')
    if (!$m.Success) {
        if ($line -match ('(?:^| )' + [regex]::Escape($name) + '=')) { throw "Truncated JSON in $name" }
        return $null
    }
    try { return ($m.Groups[1].Value | ConvertFrom-Json -AsHashtable -ErrorAction Stop) }
    catch { throw "Malformed JSON in $name" }
}
function Get-Scalar([string]$line, [string]$name) {
    $m = [regex]::Match($line, '(?:^| )' + [regex]::Escape($name) + '=([^ ]*)')
    if ($m.Success) { return $m.Groups[1].Value }; return $null
}

$results = [System.Collections.Generic.List[object]]::new()
foreach ($file in ($files | Sort-Object FullName -Unique)) {
    try {
        $reader = [IO.StreamReader]::new([IO.FileStream]::new($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite))
        try { $lines = $reader.ReadToEnd() -split '\r?\n' } finally { $reader.Dispose() }
        $summaryLine = @($lines | Where-Object { $_ -match ' SUMMARY ' } | Select-Object -Last 1)
        if (!$summaryLine) { [Console]::Error.WriteLine("Skipping $($file.FullName): no SUMMARY line."); continue }
        $line = [string]$summaryLine[0]
        $counts = @{}; foreach ($n in 'calls','completed','failures','in_flight','omitted_detail_lines','elapsed_ms','accumulated_tool_ms') { $counts[$n] = Get-Scalar $line $n }
        if ($counts.calls -notmatch '^\d+$' -or $counts.omitted_detail_lines -notmatch '^\d+$') { throw 'Malformed summary scalar fields' }
        $per = Get-JsonField $line 'per_tool'; $timing = Get-JsonField $line 'timing_per_tool'; $response = Get-JsonField $line 'response_per_tool'
        if ($null -eq $per) { throw 'Missing or malformed per_tool JSON section' }
        if ($null -eq $timing) { $timing = @{} }
        $totalBytes = if ($null -ne $response) { [long]($response.Values | Measure-Object TextBytes -Sum).Sum } else { $null }
        if ($null -eq $response) { $response = @{} }
        $mvid = $null
        foreach ($l in $lines) { if ($l -match ' SESSION ') { $mvid = Get-Scalar $l 'server_mvid'; break } }
        $rows = foreach ($tool in $per.Keys) {
            $p = $per[$tool]; $t = $timing[$tool]; $r = $response[$tool]
            $complete = if ($null -ne $t) { $t.Completed } else { $null }
            $fails = if ($null -ne $t) { $t.Failures } else { $null }
            $total = if ($null -ne $t) { $t.TotalMs } else { $null }
            $max = if ($null -ne $t) { $t.MaxMs } else { $null }
            $callsForTool = [long]$p
            $bytes = if ($null -ne $r) { [long]$r.TextBytes } else { $null }
            [pscustomobject]@{ tool=$tool; calls=$callsForTool; responses=$(if($null -ne $r){$r.Responses}else{$null}); textBytes=$bytes; averageBytes=$(if($null -ne $r -and $r.Responses){[math]::Round($bytes/[double]$r.Responses,1)}else{$null}); completed=$complete; failures=$fails; totalMs=$total; maxMs=$max }
        }
        $rows = @($rows | Sort-Object $SortBy -Descending | Select-Object -First $Top)
        $adopt = @{ singleReads=0; batchReads=0; singleSearches=0; batchSearches=0; logReads=0; logReadsWithStartLine=0; polls=0; pollsWithRevision=0 }
        foreach ($l in $lines) {
            if ($l -notmatch ' START .* tool=([^ ]+) params=(\{.*\})$') { continue }
            $tool=$Matches[1]; try { $params=$Matches[2] | ConvertFrom-Json -AsHashtable -ErrorAction Stop } catch { continue }
            switch ($tool) {
                'pak_read' { if ($params.ContainsKey('entryId')) {$adopt.singleReads++}; if ($params.ContainsKey('reads')) {$adopt.batchReads++} }
                'pak_search_text' { if ($params.ContainsKey('query')) {$adopt.singleSearches++}; if ($params.ContainsKey('queries')) {$adopt.batchSearches++} }
                'log_read' { $adopt.logReads++; if ($params.ContainsKey('startLine')) {$adopt.logReadsWithStartLine++} }
                {$_ -in 'workbench_operation','workbench_window_action_status'} { $adopt.polls++; if ($params.ContainsKey('knownRevision')) {$adopt.pollsWithRevision++} }
            }
        }
        $results.Add([pscustomobject]@{ path=$file.FullName; timestamp=($line -split ' ',2)[0]; server_mvid=$mvid; calls=[long]$counts.calls; completed=$(if($counts.completed){[long]$counts.completed}else{$null}); failures=$(if($counts.failures){[long]$counts.failures}else{$null}); in_flight=$(if($counts.in_flight){[long]$counts.in_flight}else{$null}); textBytes=$totalBytes; elapsedMs=$counts.elapsed_ms; accumulatedToolMs=$counts.accumulated_tool_ms; omitted=[long]$counts.omitted_detail_lines; detailsComplete=([long]$counts.omitted_detail_lines -eq 0); perTool=$rows; observedDetails=$adopt })
    } catch { [Console]::Error.WriteLine("Skipping $($file.FullName): $($_.Exception.Message)") }
}
if ($results.Count -eq 0) { throw 'No readable activity summaries found.' }
if ($Json) { ConvertTo-Json -InputObject @($results) -Depth 8; exit 0 }
foreach ($r in $results) {
    "{0}  {1} calls, {2} completed, {3} failures, {4} in flight; {5:N0} text bytes; {6} detail lines omitted" -f $r.path,$r.calls,$r.completed,$r.failures,$r.in_flight,$r.textBytes,$r.omitted
    $r.perTool | Format-Table tool,calls,responses,textBytes,averageBytes,completed,failures,totalMs,maxMs -AutoSize
    $suffix = if ($r.detailsComplete) { '' } else { ' (partial: details capped)' }
    "Observed requests{0}: reads single/batch={1}/{2}; searches single/batch={3}/{4}; logs with startLine={5}/{6}; polls with revision={7}/{8}. Presence does not establish correct reuse or duplicate requests." -f $suffix,$r.observedDetails.singleReads,$r.observedDetails.batchReads,$r.observedDetails.singleSearches,$r.observedDetails.batchSearches,$r.observedDetails.logReadsWithStartLine,$r.observedDetails.logReads,$r.observedDetails.pollsWithRevision,$r.observedDetails.polls
}

