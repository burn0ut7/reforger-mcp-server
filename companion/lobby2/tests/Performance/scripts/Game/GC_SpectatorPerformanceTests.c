//! Counters and readback exist only in the explicitly loaded development fixture.
modded class GC_LobbyRoster
{
	static int s_iGC_TestEntityLookups;
	override GC_Slot GetSlotByEntity(IEntity entity)
	{
		s_iGC_TestEntityLookups++;
		return super.GetSlotByEntity(entity);
	}
}

modded class GC_SpectatorCharacterEditableEntityUIComponent
{
	bool GC_TestFocused()
	{
		return m_bFocused;
	}
}

modded class GC_SpectatorEditableEntityMarker
{
	int GC_TestFocus(int focusedPlayer)
	{
		if (!IsActive() || !m_CharacterUI)
			return 0;
		bool expected = focusedPlayer > 0 && GC_LobbyPlayerController.GC_GetSpectatorEntityPlayer(m_Entity.GetOwner()) == focusedPlayer;
		if (m_CharacterUI.GC_TestFocused() != expected)
			return -1;
		if (expected)
			return 2;
		return 1;
	}
}

modded class GC_SpectatorMarkerOverlay
{
	string GC_TestUpdates(int focusedPlayer)
	{
		Update();
		GC_LobbyRoster.s_iGC_TestEntityLookups = 0;
		for (int iteration; iteration < 10; iteration++)
			Update();
		int lookups = GC_LobbyRoster.s_iGC_TestEntityLookups;
		int active;
		int focused;
		int mismatches;
		foreach (SCR_EditableEntityComponent entity, GC_SpectatorEditableEntityMarker marker : m_mMarkers)
		{
			int result = marker.GC_TestFocus(focusedPlayer);
			if (result != 0)
				active++;
			if (result == 2)
				focused++;
			if (result < 0)
				mismatches++;
		}
		return string.Format("active=%1 focused=%2 mismatches=%3 rosterSearchesFor10Updates=%4", active, focused, mismatches, lookups);
	}
}

modded class GC_LobbyPlayerController
{
	string GC_RunMarkerChecks(int focusPlayer, bool health, bool reopen)
	{
		if (!m_SpectatorMarkerOverlay || !GC_LobbyPlayerController.Cast(this).GC_CanJumpSpectatorCamera())
			return "BLOCKED: active local spectator required";
		GC_LobbyPlayerController.Cast(this).GC_SelectSpectatorPlayer(focusPlayer);
		if (GC_LobbyPlayerController.Cast(this).GC_AreWorldHealthBarsEnabled() != health)
			GC_LobbyPlayerController.Cast(this).GC_ToggleWorldHealth();
		if (reopen)
		{
			m_SpectatorMarkerOverlay.SetInterfaceVisible(false);
			m_SpectatorMarkerOverlay.SetInterfaceVisible(true);
		}
		return m_SpectatorMarkerOverlay.GC_TestUpdates(focusPlayer);
	}
}

modded class GC_LobbyManager
{
	string GC_TestSpectatorFocus(bool selectLocal, bool health, bool reopen)
	{
		GC_LobbyPlayerController controller = GC_LobbyPlayerController.Cast(GetGame().GetPlayerController());
		if (!controller)
			return "BLOCKED: local controller required";
		int focusedPlayer;
		if (selectLocal)
			focusedPlayer = controller.GetPlayerId();
		return controller.GC_RunMarkerChecks(focusedPlayer, health, reopen);
	}
}
