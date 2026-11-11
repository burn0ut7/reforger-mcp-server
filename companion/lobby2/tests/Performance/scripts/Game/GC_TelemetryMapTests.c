class GC_PerformanceMapProbe : GC_TelemetryMap
{
	bool IsCapturing() { return m_Snapshot != null; }
}

class GC_PerformanceMapRecorder : GC_PerformanceRecorderProbe
{
	string m_sCaptured;
	override int CommitMapSnapshot(string encoded)
	{
		m_sCaptured = encoded;
		return super.CommitMapSnapshot(encoded);
	}
}

modded class GC_LobbyManager
{
	string GC_TestTelemetryMap()
	{
		if (!Replication.IsServer() || GetLobbyPhase() != SCR_EGameModeState.GAME)
			return "BLOCKED: authority match required";
		if (GetBriefingMarkers().IsEmpty())
			return "BLOCKED: authored marker required as icon template";
		GC_BriefingMarker template = GetBriefingMarkers()[0];
		for (int index; index < 64; index++)
		{
			GC_BriefingMarker marker = new GC_BriefingMarker();
			marker.Set("performance-marker-" + index, template.GetImage(), "", template.GetQuadName(), Vector(index, 0, index), 0, false, 32, 0, "performance-label-" + index);
			if (!UpsertBriefingMarker(marker))
				return "FAIL fixture marker rejected";
		}
		GC_PerformanceMapRecorder recorder = new GC_PerformanceMapRecorder();
		recorder.Prepare(GC_SessionLogComponent.GetInstance());
		GC_PerformanceMapProbe capture = new GC_PerformanceMapProbe();
		int started = System.GetTickCount();
		capture.Sample(recorder, 0, started);
		bool sliced = capture.IsCapturing() && recorder.m_sCaptured.IsEmpty();
		RemoveBriefingMarker("performance-marker-0");
		capture.Sample(recorder, 0, System.GetTickCount());
		bool invalidated = !capture.IsCapturing() && recorder.m_sCaptured.IsEmpty();
		int calls;
		int maximumMs;
		while (recorder.m_sCaptured.IsEmpty() && recorder.IsRunning() && calls < 128)
		{
			started = System.GetTickCount();
			capture.Sample(recorder, 0, started);
			maximumMs = Math.Max(maximumMs, System.GetTickCount(started));
			calls++;
		}
		bool complete = !recorder.m_sCaptured.IsEmpty() && recorder.m_sCaptured.IndexOf("performance-label-0\"") < 0;
		for (int expected = 1; expected < 64; expected++)
		{
			if (recorder.m_sCaptured.IndexOf("performance-label-" + expected + "\"") < 0)
				complete = false;
			RemoveBriefingMarker("performance-marker-" + expected);
		}
		recorder.Close();
		return string.Format("sliced=%1 invalidated=%2 complete=%3 calls=%4 maxCallMs=%5", sliced, invalidated, complete, calls, maximumMs);
	}
}
