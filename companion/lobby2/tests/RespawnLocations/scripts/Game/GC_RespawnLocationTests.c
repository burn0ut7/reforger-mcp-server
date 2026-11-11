//! Explicit runtime checks; no automatic execution and no production test dependencies.
modded class GC_LobbyManager
{
	protected int m_iGC_LocationChecks;
	protected int m_iGC_LocationFailures;
	protected IEntity m_GC_WaveAnchor;
	protected vector m_vGC_OriginalWavePosition;
	protected int m_iGC_WavePlayer;
	protected string m_sGC_WaveFaction;
	protected bool m_bGC_WaveBriefingOnly;
	protected vector m_vGC_WaveExpected;
	protected int m_iGC_WaveExpectedTickets;
	protected int m_iGC_WaveCase;
	protected IEntity m_GC_WavePrevious;

	string GC_PrepareLocationWaveTest(bool briefingOnly = false)
	{
		GC_LobbyGameMode mode = GC_LobbyGameMode.GetGameMode();
		PlayerController player = GetGame().GetPlayerController();
		if (!Replication.IsServer() || !mode || !player || GetLobbyPhase() == SCR_EGameModeState.GAME)
			return "BLOCKED: start from a fresh pre-match local Play session";
		mode.GC_SetLobbyState(SCR_EGameModeState.SLOTTING);
		m_iGC_WavePlayer = player.GetPlayerId();
		GC_Slot chosen;
		foreach (GC_Slot slot : GetSlots())
		{
			if (!slot.HasClaim() && CanPlayerRequestSlot(m_iGC_WavePlayer, slot))
			{
				chosen = slot;
				break;
			}
		}
		if (!chosen || !TryAssignPlayerToSlot(m_iGC_WavePlayer, chosen.GetID()))
			return "BLOCKED: no selectable slot";
		mode.GC_SetLobbyState(SCR_EGameModeState.BRIEFING);
		if (briefingOnly)
			QueueBriefingSlotSpawns();
		else if (!StartLobbyGame())
			return "BLOCKED: could not start match";
		m_sGC_WaveFaction = ResolveSlotGroup(chosen).GetFaction().GetFactionKey();
		m_bGC_WaveBriefingOnly = briefingOnly;
		return "queued; call GC_FinishLocationWavePreparation after staging completes";
	}

	string GC_FinishLocationWavePreparation()
	{
		GC_Slot chosen = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (!chosen || !chosen.GetEntity() || m_bStartGamePending || m_bBriefingSpawnQueued)
			return "PENDING";
		if (!m_bGC_WaveBriefingOnly && GetLobbyPhase() != SCR_EGameModeState.GAME)
			return "FAIL: match did not start";
		m_vGC_OriginalWavePosition = chosen.GetEntity().GetOrigin();
		m_GC_WaveAnchor = GetGame().SpawnEntity(GenericEntity, GetGame().GetWorld());
		m_GC_WaveAnchor.SetName("GC_RespawnWaveAnchor");
		m_GC_WaveAnchor.SetOrigin(m_vGC_OriginalWavePosition + "10 0 10");
		GC_EntityRespawnLocation named = new GC_EntityRespawnLocation();
		named.m_aEntities = {"GC_RespawnWaveAnchor"};
		named.m_bSnapToTerrain = true;
		GC_LobbyManager.Cast(this).GC_SetRespawnLocation({m_sGC_WaveFaction}, named);
		GC_LobbyManager.Cast(this).GC_SetFactionRespawnTickets(m_sGC_WaveFaction, 2);
		return "ready; kill then run cases 0, 1, 2 separately";
	}

	string GC_DeleteBriefingTestBody(bool removeAnchor)
	{
		GC_Slot slot = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (GetLobbyPhase() != SCR_EGameModeState.BRIEFING || !slot || !slot.GetEntity())
			return "BLOCKED: briefing test body required";
		if (removeAnchor && m_GC_WaveAnchor)
		{
			SCR_EntityHelper.DeleteEntityAndChildren(m_GC_WaveAnchor);
			m_GC_WaveAnchor = null;
		}
		SCR_EntityHelper.DeleteEntityAndChildren(slot.GetEntity());
		return "deletion requested";
	}

	string GC_CheckBriefingLocation(bool expectMissing)
	{
		GC_Slot slot = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (!slot || GetLobbyPhase() != SCR_EGameModeState.BRIEFING)
			return "BLOCKED: briefing test slot required";
		QueueBriefingSlotSpawns();
		if (expectMissing)
			return string.Format("missing passed=%1", slot.IsKIA() && !slot.GetEntity());
		if (!m_GC_WaveAnchor || !slot.GetEntity())
			return "FAIL: replacement missing";
		vector expected = m_GC_WaveAnchor.GetOrigin();
		expected[1] = GetGame().GetWorld().GetSurfaceY(expected[0], expected[2]);
		return string.Format("briefing passed=%1 distance=%2", !slot.IsKIA() && vector.Distance(slot.GetEntity().GetOrigin(), expected) < 1,
			vector.Distance(slot.GetEntity().GetOrigin(), expected));
	}

	string GC_KillLocationWavePlayer()
	{
		GC_Slot slot = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (!slot || !slot.GetEntity() || slot.IsKIA())
			return "BLOCKED: living test slot required";
		ChimeraCharacter.Cast(slot.GetEntity()).GetCharacterController().ForceDeath();
		return "death requested";
	}

	string GC_RunLocationWaveCase(int testCase)
	{
		GC_LobbyManager manager = GC_LobbyManager.Cast(this);
		GC_Slot slot = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (!slot || !slot.IsKIA() || !m_GC_WaveAnchor || manager.GC_IsRespawnWavePending())
			return "BLOCKED: wait for KIA state and prior wave completion";
		int before = manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction);
		GC_RespawnLocation destination;
		vector expected = m_vGC_OriginalWavePosition;
		if (testCase == -1)
		{
			GC_EntityRespawnLocation missing = new GC_EntityRespawnLocation();
			missing.m_aEntities = {"GC_MissingRespawnAnchor"};
			destination = missing;
		}
		else if (testCase == 0)
			expected = m_vGC_OriginalWavePosition + "20 0 20";
		else if (testCase == 1)
		{
			GC_PositionRespawnLocation position = new GC_PositionRespawnLocation();
			position.m_vPosition = m_vGC_OriginalWavePosition + "30 0 30";
			position.m_bSnapToTerrain = true;
			destination = position;
			expected = position.m_vPosition;
		}
		else
			destination = new GC_RespawnLocation();
		expected[1] = GetGame().GetWorld().GetSurfaceY(expected[0], expected[2]);
		bool tickets = testCase != 2;
		m_GC_WavePrevious = slot.GetEntity();
		if (!manager.GC_QueueRespawnWave({m_sGC_WaveFaction}, 1, tickets, false, destination))
			return "FAIL: wave rejected";
		if (!slot.IsKIA() || slot.GetEntity() != m_GC_WavePrevious || manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction) != before)
			return "FAIL: queue call spawned or spent tickets synchronously";
		if (testCase == 0)
			m_GC_WaveAnchor.SetOrigin(expected);
		m_iGC_WaveCase = testCase;
		m_vGC_WaveExpected = expected;
		m_iGC_WaveExpectedTickets = before;
		if (tickets && testCase != -1)
			m_iGC_WaveExpectedTickets--;
		return "queued; call GC_CheckLocationWaveCase after completion";
	}

	string GC_CheckLocationWaveCase()
	{
		GC_LobbyManager manager = GC_LobbyManager.Cast(this);
		if (manager.GC_IsRespawnWavePending())
			return "PENDING";
		GC_Slot slot = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (!slot)
			return "FAIL: test slot missing";
		IEntity character = slot.GetEntity();
		bool passed = manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction) == m_iGC_WaveExpectedTickets;
		if (m_iGC_WaveCase == -1)
			passed = passed && slot.IsKIA() && character == m_GC_WavePrevious;
		else
			passed = passed && !slot.IsKIA() && character
				&& GetGame().GetPlayerManager().GetPlayerControlledEntity(m_iGC_WavePlayer) == character
				&& vector.Distance(character.GetOrigin(), m_vGC_WaveExpected) < 1;
		return string.Format("case=%1 passed=%2 tickets=%3", m_iGC_WaveCase, passed, manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction));
	}

	string GC_TestWaveCancellation()
	{
		GC_LobbyManager manager = GC_LobbyManager.Cast(this);
		GC_Slot slot = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (GetLobbyPhase() != SCR_EGameModeState.GAME || !slot || !slot.IsKIA())
			return "BLOCKED: KIA test player in match required";
		IEntity previous = slot.GetEntity();
		int tickets = manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction);
		if (!manager.GC_QueueRespawnWave({m_sGC_WaveFaction}, 1, false))
			return "FAIL: wave rejected";
		if (!GC_LobbyGameMode.GetGameMode().GetBoundaryAdmin().ForceMissionEnd())
			return "FAIL: mission end rejected";
		bool passed = !manager.GC_IsRespawnWavePending() && slot.GetEntity() == previous
			&& manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction) == tickets;
		return string.Format("cancellation passed=%1 phase=%2", passed, GetLobbyPhase());
	}

	protected void GC_CheckLocation(bool passed, string label)
	{
		m_iGC_LocationChecks++;
		if (!passed)
			m_iGC_LocationFailures++;
		PrintFormat("GC_RESPAWN_LOCATION_CHECK passed=%1 %2", passed, label);
	}

	string GC_RunIndividualLocationCase(int testCase)
	{
		GC_LobbyManager manager = GC_LobbyManager.Cast(this);
		GC_Slot slot = GetSlotByPlayerId(m_iGC_WavePlayer);
		if (!slot || !slot.IsKIA() || !slot.GetEntity() || !m_GC_WaveAnchor)
			return "BLOCKED: wait for KIA test slot";
		IEntity previous = slot.GetEntity();
		int tickets = manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction);
		string denied = ExecuteAdminSlotAction(0, slot.GetID(), slot.GetUUID(), GC_EAdminSlotAction.HEAL_OR_RESPAWN);
		if (denied != "rejected-not-logged-in" || slot.GetEntity() != previous || !slot.IsKIA())
			return "FAIL: unauthenticated recovery mutated slot";
		if (!SCR_Global.IsAdmin(m_iGC_WavePlayer))
			return "BLOCKED: local test player is not logged in as admin";
		vector expected = m_vGC_OriginalWavePosition + "40 0 40";
		m_GC_WaveAnchor.SetOrigin(expected);
		GC_EAdminSlotAction action = GC_EAdminSlotAction.HEAL_OR_RESPAWN;
		if (testCase == 2)
		{
			action = GC_EAdminSlotAction.RESPAWN_CURRENT;
			expected = previous.GetOrigin();
		}
		else if (testCase == 3)
		{
			action = GC_EAdminSlotAction.RESPAWN_ORIGINAL;
			expected = m_vGC_OriginalWavePosition;
		}
		else
			expected[1] = GetGame().GetWorld().GetSurfaceY(expected[0], expected[2]);
		string outcome;
		if (testCase == 1)
			outcome = manager.GC_ExecuteAdminHeal(m_iGC_WavePlayer, GC_EAdminHealScope.SELECTED, {m_iGC_WavePlayer}, {GetPlayerVoiceIdentity(m_iGC_WavePlayer)}, 1, 10);
		else
			outcome = ExecuteAdminSlotAction(m_iGC_WavePlayer, slot.GetID(), slot.GetUUID(), action);
		IEntity body = slot.GetEntity();
		bool passed = body && body != previous && !slot.IsKIA()
			&& GetGame().GetPlayerManager().GetPlayerControlledEntity(m_iGC_WavePlayer) == body
			&& vector.Distance(body.GetOrigin(), expected) < 1
			&& manager.GC_GetFactionRespawnTickets(m_sGC_WaveFaction) == tickets;
		return string.Format("individual=%1 passed=%2 outcome=%3", testCase, passed, outcome);
	}

	string GC_TestRespawnLocations()
	{
		if (!Replication.IsServer() || GetManager() != this || GetLobbyPhase() == SCR_EGameModeState.POSTGAME)
			return "BLOCKED: active server mission required";
		m_iGC_LocationChecks = 0;
		m_iGC_LocationFailures = 0;
		BaseWorld world = GetGame().GetWorld();
		EntitySpawnParams result;
		GC_RespawnLocation original = new GC_RespawnLocation();
		GC_CheckLocation(original.Resolve(world, null, 0, result) && !result, "original requests unchanged spawn path");
		GC_PositionRespawnLocation fixed = new GC_PositionRespawnLocation();
		fixed.m_vPosition = "100 200 300";
		fixed.m_vOffset = "1 3 4";
		GC_CheckLocation(fixed.Resolve(world, null, 0, result) && vector.Distance(result.Transform[3], "101 203 304") < 0.01, "fixed position and offset");
		GC_CheckLocation(fixed.Resolve(world, null, 5, result) && vector.Distance(result.Transform[3], "103 203 302") < 0.01, "grid separates later players");
		fixed.m_fHeading = 90;
		GC_CheckLocation(fixed.Resolve(world, null, 0, result) && vector.Distance(result.Transform[3], "104 203 299") < 0.01, "heading rotates local offset");
		fixed.m_bSnapToTerrain = true;
		GC_CheckLocation(fixed.Resolve(world, null, 0, result) && Math.AbsFloat(result.Transform[3][1] - world.GetSurfaceY(104, 299) - 3) < 0.01, "terrain snap retains height offset");
		fixed.m_iColumns = 0;
		GC_CheckLocation(!fixed.Resolve(world, null, 0, result) && !result, "invalid placement rejects without stale result");
		fixed.m_iColumns = 4;
		GC_EntityRespawnLocation named = new GC_EntityRespawnLocation();
		named.m_aEntities = {"GC_RespawnCheckAnchor_A", "GC_RespawnCheckAnchor_B"};
		if (world.FindEntityByName(named.m_aEntities[0]) || world.FindEntityByName(named.m_aEntities[1]))
			return "BLOCKED: test entity names already exist";
		IEntity anchorA = GetGame().SpawnEntity(GenericEntity, world);
		IEntity anchorB = GetGame().SpawnEntity(GenericEntity, world);
		if (!anchorA || !anchorB)
		{
			if (anchorA)
				SCR_EntityHelper.DeleteEntityAndChildren(anchorA);
			if (anchorB)
				SCR_EntityHelper.DeleteEntityAndChildren(anchorB);
			return "BLOCKED: could not create test anchors";
		}
		anchorA.SetName(named.m_aEntities[0]);
		anchorB.SetName(named.m_aEntities[1]);
		anchorA.SetOrigin("100 200 300");
		anchorB.SetOrigin("200 300 400");
		GC_CheckLocation(named.Resolve(world, null, 1, result) && vector.Distance(result.Transform[3], anchorB.GetOrigin()) < 0.01, "list distributes players to second anchor");
		GC_CheckLocation(named.Resolve(world, null, 2, result) && vector.Distance(result.Transform[3], "102 200 300") < 0.01, "list wraps with spacing");
		anchorA.SetOrigin("300 400 500");
		anchorA.SetYawPitchRoll("90 0 0");
		named.m_vOffset = "0 0 5";
		GC_CheckLocation(named.Resolve(world, null, 0, result) && vector.Distance(result.Transform[3], "305 400 500") < 0.01, "moving and rotating anchor changes next resolution");
		SCR_EntityHelper.DeleteEntityAndChildren(anchorB);
		GC_CheckLocation(!named.Resolve(world, null, 1, result) && !result, "deleted selected anchor rejects rather than falls back");
		named.m_aEntities = {"GC_RespawnCheckAnchor_A", "GC_RespawnCheckAnchor_A"};
		GC_CheckLocation(!named.IsValid(), "duplicate anchor entries rejected");
		named.m_aEntities = {"GC_RespawnCheckAnchor_A"};

		GC_Slot candidate;
		foreach (GC_Slot slot : GetSlots())
		{
			if (!slot.HasClaim() && !slot.GetEntity() && ResolveSlotGroup(slot) && ResolveSlotGroup(slot).GetFaction())
			{
				candidate = slot;
				break;
			}
		}
		if (candidate)
		{
			string faction = ResolveSlotGroup(candidate).GetFaction().GetFactionKey();
			GC_RespawnLocation prior = GC_LobbyManager.Cast(this).GC_GetRespawnLocation(faction);
			GC_CheckLocation(GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction}, named) && GC_LobbyManager.Cast(this).GC_GetRespawnLocation(faction) == named, "set faction default");
			GC_CheckLocation(!GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction, "GC_INVALID_FACTION"}, fixed) && GC_LobbyManager.Cast(this).GC_GetRespawnLocation(faction) == named, "invalid scope leaves existing default unchanged");
			GC_CheckLocation(GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction}, fixed) && GC_LobbyManager.Cast(this).GC_GetRespawnLocation(faction) == fixed, "replace default");
			GC_CheckLocation(GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction}, null) && !GC_LobbyManager.Cast(this).GC_GetRespawnLocation(faction), "clear default");
			GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction}, prior);
			GC_SlotState oldState = candidate.GetState();
			ResourceName oldPrefab = candidate.GetResolvedPrefab();
			fixed.m_bSnapToTerrain = true;
			fixed.m_vOffset = "0 0 0";
			fixed.m_vPosition = ResolveSlotGroup(candidate).GetOrigin();
			fixed.Resolve(world, candidate, 0, result);
			string spawned = RespawnAdminSlot(candidate, false, fixed);
			IEntity body = candidate.GetEntity();
			GC_CheckLocation(spawned == "applied" && body && vector.Distance(body.GetOrigin(), result.Transform[3]) < 0.1, "production respawn owner spawns character at override");
			GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction}, fixed);
			fixed.m_vPosition = fixed.m_vPosition + "10 0 10";
			fixed.Resolve(world, candidate, 0, result);
			spawned = RespawnAdminSlot(candidate, true);
			body = candidate.GetEntity();
			GC_CheckLocation(spawned == "applied" && body && vector.Distance(body.GetOrigin(), result.Transform[3]) < 0.1, "individual respawn uses default ahead of current fallback");
			GC_EntityRespawnLocation missing = new GC_EntityRespawnLocation();
			missing.m_aEntities = {"GC_MissingIndividualRespawn"};
			GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction}, missing);
			GC_CheckLocation(RespawnAdminSlot(candidate, false) == "rejected-respawn-location" && candidate.GetEntity() == body, "missing individual destination retains previous body");
			GC_LobbyManager.Cast(this).GC_SetRespawnLocation({faction}, prior);
			if (body)
				SCR_EntityHelper.DeleteEntityAndChildren(body);
			candidate.SetEntity(null);
			candidate.SetState(oldState);
			candidate.SetResolvedPrefab(oldPrefab);
			UpdateSlot(candidate);
		}
		else
			GC_CheckLocation(false, "no empty slot for production spawn/default checks");
		GC_WaveRespawnAction wave = new GC_WaveRespawnAction();
		wave.m_Location = named;
		GC_CheckLocation(wave.BuildAction().IsValid() && wave.BuildAction().m_RespawnLocation == named, "wave compiles destination");
		GC_SetRespawnLocationAction setting = new GC_SetRespawnLocationAction();
		GC_CheckLocation(!setting.BuildAction().IsValid(), "set action requires explicit factions");
		setting.m_aFactions = {"US"};
		GC_CheckLocation(setting.BuildAction().IsValid(), "set action supports reset to original");
		GC_CheckLocation(!GC_LobbyManager.Cast(this).GC_ExecuteFrameworkAction(new GC_MissionRuleEngine(), setting.BuildAction()), "unowned framework engine rejected");
		SCR_EntityHelper.DeleteEntityAndChildren(anchorA);
		return string.Format("checks=%1 failures=%2", m_iGC_LocationChecks, m_iGC_LocationFailures);
	}
}
