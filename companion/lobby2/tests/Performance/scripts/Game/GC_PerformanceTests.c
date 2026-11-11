//! Explicit development checks, outside the release addon. No automatic execution.
class GC_PerformanceRecorderProbe : GC_TelemetryRecorder
{
	void Prepare(GC_SessionLogComponent logs)
	{
		m_Logs = logs;
		m_sRecordingId = "performance-test-" + PersistenceIdUtils.Generate();
		m_fByteLimit = 1048576;
		m_fFrom = Now();
		m_fLastFlush = m_fFrom;
		m_bRunning = true;
		FileIO.MakeDirectory(OUTBOX);
	}

	string RunPublicationCase()
	{
		for (int index; index < 3; index++)
		{
			JsonSaveContext record = BeginRecord("test-before");
			record.WriteValue("index", index);
			CommitRecord(record);
		}
		Flush();
		if (!m_PendingBinary || m_sPending.IsEmpty() || m_iRecordCount != 0)
			return "FAIL freeze";
		// Production event callbacks remain accepted while publication is pending.
		for (int next; next < 4; next++)
		{
			JsonSaveContext entry = BeginRecord("test-during");
			entry.WriteValue("index", next);
			CommitRecord(entry);
		}
		CommitMapSnapshot("{\"faction\":\"test\",\"shapes\":[],\"markers\":[]}");
		Close();
		if (!m_bSealed || m_iRevision != 2 || m_iSequence != 8 || m_bPublicationFailed)
			return "FAIL close " + Report();
		return "PASS " + m_sRecordingId + " " + Report();
	}

	string RunFailureCase()
	{
		CommitRecord(BeginRecord("before-failure"));
		Flush();
		string path = string.Format("%1/%2-telemetry-0.l2t", OUTBOX, m_sRecordingId);
		// A directory cannot be opened as the ready-marker file.
		FileIO.MakeDirectory(path + ".ready");
		CommitRecord(BeginRecord("during-failure"));
		Flush(true);
		bool retained = m_bPublicationFailed && !m_bRunning && !m_bSealed && m_iRevision == 0
			&& m_PendingBinary && !m_sPendingRecords.IsEmpty() && m_iRecordCount == 1;
		return string.Format("readyFailureRetained=%1 path=%2", retained, path);
	}
}

modded class GC_LobbyManager
{
	string GC_TestTelemetryReadyFailure()
	{
		GC_PerformanceRecorderProbe probe = new GC_PerformanceRecorderProbe();
		probe.Prepare(GC_SessionLogComponent.GetInstance());
		return probe.RunFailureCase();
	}

	string GC_TestTelemetryPublication()
	{
		GC_SessionLogComponent logs = GC_SessionLogComponent.GetInstance();
		if (!Replication.IsServer() || !logs)
			return "BLOCKED: authority/session logs required";
		GC_PerformanceRecorderProbe probe = new GC_PerformanceRecorderProbe();
		probe.Prepare(logs);
		return probe.RunPublicationCase();
	}

	string GC_TestTelemetryBinary()
	{
		string directory = "$profile:Lobby2PerformanceTests";
		FileIO.MakeDirectory(directory);
		string path = directory + "/" + PersistenceIdUtils.Generate() + ".l2t";
		GC_TelemetryBinaryBatch batch = new GC_TelemetryBinaryBatch();
		GC_SpectatorSample sample = new GC_SpectatorSample();
		for (int index; index < 512; index++)
			batch.Add(index + 1, index * 200, 1, "1 2 3", "4 5 6", "7 8 9", sample, false, 0);
		int steps;
		int result;
		while (result == 0 && steps < 2048)
		{
			result = batch.SaveVerifiedStep(path, "{}", "[]");
			steps++;
		}
		if (result != 1 || steps <= 19)
			return string.Format("FAIL large batch result=%1 steps=%2", result, steps);
		GC_TelemetryBinaryBatch empty = new GC_TelemetryBinaryBatch();
		if (!empty.SaveVerified(path + ".empty", "{}", "[]"))
			return "FAIL empty tables";
		// No directory exists below this freshly generated filename.
		if (empty.SaveVerifiedStep(path + "/missing/file.l2t", "{}", "[]") != -1)
			return "FAIL write failure not reported";
		if (!empty.SaveVerified(path + ".retry", "{}", "[]"))
			return "FAIL retry after failed open";
		// Corrupt the closed file before readback begins.
		for (int stage; stage < 9; stage++)
		{
			if (empty.SaveVerifiedStep(path + ".corrupt", "{}", "[]") != 0)
				return "FAIL corruption preparation";
		}
		FileHandle corrupt = FileIO.OpenFile(path + ".corrupt", FileMode.WRITE);
		corrupt.Write("bad");
		corrupt.Close();
		if (empty.SaveVerifiedStep(path + ".corrupt", "{}", "[]") != -1)
			return "FAIL corrupt readback accepted";
		return string.Format("PASS steps=%1 bytes=%2 path=%3", steps, batch.GetNumericBytes(), path);
	}
}
