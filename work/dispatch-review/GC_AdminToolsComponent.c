//! Full-screen tool workspace. Presentation never grants administration.
class GC_AdminToolsComponent : SCR_ScriptedWidgetComponent
{
	protected static Widget s_wPanel;
	protected GC_LobbyManager m_Manager;
	protected GC_LobbyPlayerController m_Controller;
	protected GC_AdminPlayerPicker m_Targets;
	protected GC_AdminReservationsComponent m_Reservations;
	protected GC_AdminMapPlansComponent m_MapPlans;
	protected GC_AdminBoundariesComponent m_Boundaries;
	protected GC_AdminEndingComponent m_Ending;
	protected GC_AdminMissionsComponent m_Missions;
	protected GC_AdminPlayerPicker m_From;
	protected GC_AdminPlayerPicker m_To;
	protected GC_AdminModerationComponent m_Moderation;
	protected GC_AdminLogsComponent m_Logs;
	protected GC_AdminStatusComponent m_ServerStatus;
	protected int m_iTool;
	protected GC_EAdminHealScope m_eScope;
	protected int m_iPending = -1;
	protected RplId m_MakePlayableEntity;
	protected RplId m_MakePlayableGroup;
	protected SCR_ButtonTextComponent m_Apply;
	protected TextWidget m_Status;
	protected SCR_SliderComponent m_HealRadius;
	protected int m_iHealRadius = GC_LobbyManager.GC_DEFAULT_HEAL_RADIUS;
	protected GC_AdminPlayerPicker m_MessagePlayers;
	protected MultilineEditBoxWidget m_MessageText;
	protected SCR_EventHandlerComponent m_MessageEvents;
	protected GC_EAdminMessageAudience m_eMessageAudience = GC_EAdminMessageAudience.SELECTED_PLAYERS;
	protected string m_sTargetFaction;
	protected int m_iMessagePending = -1;
	protected ref map<SCR_ButtonBaseComponent, string> m_mFactionButtons = new map<SCR_ButtonBaseComponent, string>();
	protected bool m_bTeleportFaction;
	protected bool m_bConfirmFactionTeleport;

	//------------------------------------------------------------------------------------------------
	static void OpenMessage(int targetId)
	{
		Open(targetId);
		if (!s_wPanel)
			return;
		GC_AdminToolsComponent panel = GC_AdminToolsComponent.Cast(s_wPanel.FindHandler(GC_AdminToolsComponent));
		if (!panel || panel.m_iPending >= 0 || panel.m_iMessagePending >= 0)
			return;
		panel.m_iTool = 3;
		panel.m_eMessageAudience = GC_EAdminMessageAudience.SELECTED_PLAYERS;
		panel.m_MessagePlayers.SetMultiple(false);
		panel.m_MessagePlayers.SelectPlayer(targetId);
		panel.m_MessagePlayers.SetMultiple(true);
		panel.Refresh();
	}

	//------------------------------------------------------------------------------------------------
	static void Open(int targetId = -1, bool useSelectedPlayer = true)
	{
		GC_LobbyManager manager = GC_LobbyManager.GetManager();
		PlayerController controller = GetGame().GetPlayerController();
		if (!manager || !controller || !SCR_Global.IsAdmin(controller.GetPlayerId()))
			return;
		MenuBase menu = GetGame().GetMenuManager().OpenMenu(ChimeraMenuPreset.GC_AdminToolsMenu, 0, true);
		if (menu)
			s_wPanel = menu.GetRootWidget();
		if (!s_wPanel)
			return;
		GC_AdminToolsComponent panel = GC_AdminToolsComponent.Cast(s_wPanel.FindHandler(GC_AdminToolsComponent));
		if (targetId <= 0 && useSelectedPlayer)
			targetId = manager.GetAdminSelectedPlayer();
		if (panel && panel.m_iPending < 0 && panel.m_iMessagePending < 0 && targetId > 0)
		{
			panel.m_iTool = 0;
			panel.m_eScope = GC_EAdminHealScope.SELECTED;
			panel.m_Targets.SetMultiple(false);
			panel.m_Targets.SelectPlayer(targetId);
			panel.m_Targets.SetMultiple(true);
			panel.m_From.SelectPlayer(targetId);
			panel.Refresh();
		}
	}

	//------------------------------------------------------------------------------------------------
	static void Close()
	{
		GetGame().GetMenuManager().CloseMenuByPreset(ChimeraMenuPreset.GC_AdminToolsMenu);
		s_wPanel = null;
	}

	//------------------------------------------------------------------------------------------------
	static void GearUnavailable()
	{
		if (!s_wPanel)
			return;
		TextWidget status = TextWidget.Cast(s_wPanel.FindAnyWidget("ToolStatus"));
		if (status)
			status.SetText("Sign in as admin and take control of a character to open Gear.");
	}

	//------------------------------------------------------------------------------------------------
	static void ReviewMakePlayable(IEntity entity)
	{
		GC_LobbyManager manager = GC_LobbyManager.GetManager();
		if (!manager || !entity)
			return;
		SCR_AIGroup group = manager.GetPlayableCharacterGroup(entity);
		if (!group)
			return;
		RplComponent replication = RplComponent.Cast(entity.FindComponent(RplComponent));
		if (!replication)
			return;
		Open();
		if (!s_wPanel)
			return;
		GC_AdminToolsComponent panel = GC_AdminToolsComponent.Cast(s_wPanel.FindHandler(GC_AdminToolsComponent));
		if (!panel || panel.m_iPending >= 0)
			return;
		panel.m_MakePlayableEntity = replication.Id();
		panel.m_MakePlayableGroup = Replication.FindItemId(group);
		panel.m_iTool = 2;
		panel.Refresh();
	}

	//------------------------------------------------------------------------------------------------
	override void HandlerAttached(Widget w)
	{
		super.HandlerAttached(w);
		m_Manager = GC_LobbyManager.GetManager();
		m_Controller = GC_LobbyPlayerController.Cast(GetGame().GetPlayerController());
		m_ServerStatus = GC_AdminStatusComponent.Cast(w.FindAnyWidget("StatusFields").FindHandler(GC_AdminStatusComponent));
		m_Logs = GC_AdminLogsComponent.Cast(w.FindAnyWidget("LogFields").FindHandler(GC_AdminLogsComponent));
		m_Moderation = GC_AdminModerationComponent.Cast(w.FindAnyWidget("ModerationFields").FindHandler(GC_AdminModerationComponent));
		m_Targets = GC_AdminPlayerPicker.Cast(w.FindAnyWidget("HealPlayers").FindHandler(GC_AdminPlayerPicker));
		m_From = GC_AdminPlayerPicker.Cast(w.FindAnyWidget("FromPlayer").FindHandler(GC_AdminPlayerPicker));
		m_To = GC_AdminPlayerPicker.Cast(w.FindAnyWidget("ToPlayer").FindHandler(GC_AdminPlayerPicker));
		m_Targets.SetMultiple(true);
		m_MessagePlayers = GC_AdminPlayerPicker.Cast(w.FindAnyWidget("MessagePlayers").FindHandler(GC_AdminPlayerPicker));
		m_MessagePlayers.SetMultiple(true);
		m_MessagePlayers.m_OnChanged.Insert(OnSelection);
		m_MessagePlayers.m_OnOpened.Insert(OnRosterOpened);
		m_MessageText = MultilineEditBoxWidget.Cast(w.FindAnyWidget("MessageText"));
		m_MessageEvents = SCR_EventHandlerComponent.Cast(m_MessageText.FindHandler(SCR_EventHandlerComponent));
		m_MessageEvents.GetOnChange().Insert(OnMessageChanged);
		m_Apply = SCR_ButtonTextComponent.GetButtonText("ToolApply", w);
		m_Status = TextWidget.Cast(w.FindAnyWidget("ToolStatus"));
		m_HealRadius = SCR_SliderComponent.Cast(w.FindAnyWidget("HealRadius").FindHandler(SCR_SliderComponent));
		m_HealRadius.SetSliderSettings(GC_LobbyManager.GC_MIN_HEAL_RADIUS, GC_LobbyManager.GC_MAX_HEAL_RADIUS, 1, "%1 m");
		m_HealRadius.SetValue(m_iHealRadius);
		m_HealRadius.m_OnChanged.Insert(OnHealRadiusChanged);
		Bind("AdminClose", "Close", "exit");
		Bind("ToolHeal", "Heal", "plus");
		Bind("ToolGear", "Gear", "player");
		Bind("ToolGearLoadout", "Gear Loadout", "player");
		Bind("ToolForcedClothing", "Forced Clothing", "server-locked");
		SCR_ButtonTextComponent.GetButtonText("ToolAllowClothing", w).SetText("Unlock clothing");
		SCR_ButtonTextComponent.GetButtonText("ToolAllowClothing", w).m_OnClicked.Insert(OnApply);
		ImageWidget.Cast(w.FindAnyWidget("ToolAllowClothing").FindAnyWidget("Icon")).LoadImageFromSet(0,
			"{2EFEA2AF1F38E7F0}UI/Textures/Icons/icons_wrapperUI-64.imageset", "server-unlocked");
		Bind("ToolFreeze", "Freeze", "pause");
		Bind("ToolUnstuck", "Unstuck", "respawn");
		Bind("ToolWarn", "Warn", "warning");
		Bind("MessageNormal", "Message", "comments");
		Bind("ToolLogs", "Logs", "listView");
		Bind("ToolServerStatus", "Status", "career_server");
		SCR_ButtonTextComponent.GetButtonText("ToolUnfreeze", w).m_OnClicked.Insert(OnApply);
		ImageWidget.Cast(w.FindAnyWidget("ToolUnfreeze").FindAnyWidget("Icon")).LoadImageFromSet(0,
			"{2EFEA2AF1F38E7F0}UI/Textures/Icons/icons_wrapperUI-64.imageset", "play");
		Bind("ToolTeleport", "Teleport", "move");
		Bind("ToolMessage", "Message", "comments");
		Bind("ToolReservations", "Reservations", "server-locked");
		Bind("ToolMapPlans", "Markers", "pinPlay");
		Bind("ToolBoundaries", "Boundaries", "ruler");
		Bind("ToolEndMatch", "End match", "pause");
		Bind("MissionBrowse", "Mission browser", "listView");
		Bind("ToolMissions", "Missions", "listView");
		m_Missions = GC_AdminMissionsComponent.Cast(w.FindAnyWidget("MissionsPanel").FindHandler(GC_AdminMissionsComponent));
		m_Ending = GC_AdminEndingComponent.Cast(w.FindAnyWidget("EndingPanel").FindHandler(GC_AdminEndingComponent));
		m_Boundaries = GC_AdminBoundariesComponent.Cast(w.FindAnyWidget("BoundaryPanel").FindHandler(GC_AdminBoundariesComponent));
		m_MapPlans = GC_AdminMapPlansComponent.Cast(w.FindAnyWidget("MapPlanPanel").FindHandler(GC_AdminMapPlansComponent));
		m_Reservations = GC_AdminReservationsComponent.Cast(w.FindAnyWidget("ReservationPanel").FindHandler(GC_AdminReservationsComponent));
		Bind("ToolModeration", "Kick / Ban", "admin");
		Bind("MessageSelected", "Selected players", "player");
		Bind("MessageEveryone", "Everyone", "players");
		Bind("MessageFaction", "Faction", "faction");
		Bind("ScopeSelected", "Selected players", "player");
		Bind("ScopeEveryone", "Everyone", "players");
		Bind("ScopeNearby", "Nearby", "compass");
		Bind("ScopeFaction", "Faction", "faction");
		Bind("TeleportPlayer", "Player", "player");
		Bind("TeleportFaction", "Faction", "faction");
		m_Apply.m_OnClicked.Insert(OnApply);
		m_Targets.m_OnChanged.Insert(OnSelection);
		m_From.m_OnChanged.Insert(OnSelection);
		m_To.m_OnChanged.Insert(OnSelection);
		m_Targets.m_OnOpened.Insert(OnRosterOpened);
		m_From.m_OnOpened.Insert(OnRosterOpened);
		m_To.m_OnOpened.Insert(OnRosterOpened);
		if (m_Manager)
		{
			m_Manager.m_OnPlayerRoleChanged.Insert(OnRoleChanged);
			m_Manager.m_OnFactionTopologyChanged.Insert(OnFactionsChanged);
			m_Manager.m_OnSlotPresentationChanged.Insert(OnFactionSlotChanged);
			m_Manager.m_OnPlayerDisconnected.Insert(OnFactionPlayerChanged);
			m_Manager.m_OnPlayerConnected.Insert(OnFactionPlayerChanged);
		}
		if (m_Controller)
		{
			m_Controller.m_OnAdminPlayerToolResult.Insert(OnResult);
			m_Controller.m_OnAdminActionResult.Insert(OnLegacyResult);
			m_Controller.m_OnAdminMessageResult.Insert(OnMessageResult);
		}
		RebuildFactions();
		Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected void Bind(string name, string label, string icon)
	{
		SCR_ButtonTextComponent button = SCR_ButtonTextComponent.GetButtonText(name, m_wRoot);
		button.SetText(label);
		ImageWidget.Cast(button.GetRootWidget().FindAnyWidget("Icon")).LoadImageFromSet(0,
			"{2EFEA2AF1F38E7F0}UI/Textures/Icons/icons_wrapperUI-64.imageset", icon);
		button.m_OnClicked.Insert(OnAction);
	}

	//------------------------------------------------------------------------------------------------
	protected bool HasAccess()
	{
		return m_Manager && m_Manager == GC_LobbyManager.GetManager() && m_Controller && SCR_Global.IsAdmin(m_Controller.GetPlayerId());
	}

	//------------------------------------------------------------------------------------------------
	protected void OnAction(SCR_ButtonBaseComponent button)
	{
		string name = button.GetRootWidget().GetName();
		if (name == "AdminClose")
		{
			Close();
			return;
		}
		if (!HasAccess() || m_iPending >= 0 || m_iMessagePending >= 0 || m_Moderation.IsBusy())
			return;
		if (name == "ToolGearLoadout")
		{
			m_Controller.GC_RequestGearOpen();
			return;
		}
		OnRosterOpened(null);
		m_bConfirmFactionTeleport = false;
		int previousTool = m_iTool;
		GC_AdminPlayerPicker previousPicker = m_Targets;
		if (previousTool == 1 || previousTool == 10)
			previousPicker = m_From;
		else if (previousTool == 3 || previousTool == 11)
			previousPicker = m_MessagePlayers;
		if (name == "ToolHeal")
			m_iTool = 0;
		else if (name == "ToolGear")
			m_iTool = 13;
		else if (name == "ToolForcedClothing")
			m_iTool = 14;
		else if (name == "ToolFreeze")
			m_iTool = 8;
		else if (name == "ToolUnstuck")
			m_iTool = 10;
		else if (name == "ToolWarn")
			m_iTool = 11;
		else if (name == "ToolServerStatus")
			m_iTool = 12;
		else if (name == "ToolLogs")
			m_iTool = 9;
		else if (name == "ToolTeleport")
			m_iTool = 1;
		else if (name == "ToolMessage" || name == "MessageNormal")
			m_iTool = 3;
		else if (name == "ToolReservations")
			m_iTool = 4;
		else if (name == "ToolBoundaries")
			m_iTool = 7;
		else if (name == "ToolEndMatch")
			m_iTool = 15;
		else if (name == "ToolMissions" || name == "MissionBrowse")
			m_iTool = 16;
		else if (name == "ToolMapPlans")
			m_iTool = 6;
		else if (name == "ToolModeration")
		{
			m_iTool = 5;
			m_Moderation.Open();
		}
		else if (name == "MessageSelected")
			m_eMessageAudience = GC_EAdminMessageAudience.SELECTED_PLAYERS;
		else if (name == "MessageEveryone")
			m_eMessageAudience = GC_EAdminMessageAudience.ALL_PLAYERS;
		else if (name == "MessageFaction")
		{
			m_eMessageAudience = GC_EAdminMessageAudience.FACTION;
			RebuildFactions();
		}
		else if (name == "ScopeSelected")
			m_eScope = GC_EAdminHealScope.SELECTED;
		else if (name == "ScopeEveryone")
			m_eScope = GC_EAdminHealScope.EVERYONE;
		else if (name == "ScopeNearby")
			m_eScope = GC_EAdminHealScope.NEARBY;
		else if (name == "ScopeFaction")
		{
			m_eScope = GC_EAdminHealScope.FACTION;
			RebuildFactions();
		}
		else if (name == "TeleportPlayer")
		{
			m_iTool = 1;
			m_bTeleportFaction = false;
		}
		else if (name == "TeleportFaction")
		{
			m_iTool = 1;
			m_bTeleportFaction = true;
			RebuildFactions();
		}
		if (previousTool != m_iTool)
		{
			GC_AdminPlayerPicker nextPicker = m_Targets;
			if (m_iTool == 1 || m_iTool == 10)
				nextPicker = m_From;
			else if (m_iTool == 3 || m_iTool == 11)
				nextPicker = m_MessagePlayers;
			nextPicker.CopySelectionFrom(previousPicker);
		}
		m_Status.SetText("");
		Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected void OnSelection()
	{
		m_bConfirmFactionTeleport = false;
		if (m_iPending < 0)
		{
			m_Status.SetText("");
			Refresh();
		}
	}

	//------------------------------------------------------------------------------------------------
	protected void OnHealRadiusChanged(SCR_SliderComponent slider, float value)
	{
		if (!HasAccess() || m_iPending >= 0)
			return;
		m_iHealRadius = Math.Round(value);
		m_Status.SetText("");
		Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected void OnRosterOpened(GC_AdminPlayerPicker opened)
	{
		if (m_MessagePlayers != opened)
			m_MessagePlayers.CloseRoster();
		if (m_Targets != opened)
			m_Targets.CloseRoster();
		if (m_From != opened)
			m_From.CloseRoster();
		if (m_To != opened)
			m_To.CloseRoster();
	}

	//------------------------------------------------------------------------------------------------
	void HidePlayerLists()
	{
		m_MessagePlayers.CloseRoster();
		m_Targets.CloseRoster();
		m_From.CloseRoster();
		m_To.CloseRoster();
	}

	//------------------------------------------------------------------------------------------------
	protected void GC_PositionClothingScope(bool clothing)
	{
		float actionLeft = 0.815;
		if (clothing)
			actionLeft = 0.78;
		FrameSlot.SetAnchorMin(m_Apply.GetRootWidget(), actionLeft, 0.9);
		FrameSlot.SetAnchorMax(m_Apply.GetRootWidget(), 0.98, 0.965);
		float offset;
		if (clothing)
			offset = 0.1;
		Widget scope = m_wRoot.FindAnyWidget("HealScope");
		FrameSlot.SetAnchorMin(scope, 0.24, 0.18 + offset);
		FrameSlot.SetAnchorMax(scope, 0.94, 0.275 + offset);
		Widget faction = m_wRoot.FindAnyWidget("ScopeFaction");
		FrameSlot.SetAnchorMin(faction, 0.779, 0.218 + offset);
		FrameSlot.SetAnchorMax(faction, 0.94, 0.275 + offset);
		Widget radius = m_wRoot.FindAnyWidget("HealRadius");
		FrameSlot.SetAnchorMin(radius, 0.24, 0.31 + offset);
		FrameSlot.SetAnchorMax(radius, 0.8, 0.46 + offset);
		Widget label = m_wRoot.FindAnyWidget("HealSelectionLabel");
		FrameSlot.SetAnchorMin(label, 0.24, 0.31 + offset);
		FrameSlot.SetAnchorMax(label, 0.8, 0.35 + offset);
		Widget players = m_wRoot.FindAnyWidget("HealPlayers");
		FrameSlot.SetAnchorMin(players, 0.24, 0.355 + offset);
		FrameSlot.SetAnchorMax(players, 0.8, 0.43 + offset);
	}

	//------------------------------------------------------------------------------------------------
	protected void Refresh()
	{
		if (!HasAccess())
		{
			GetGame().GetCallqueue().CallLater(Close, 0);
			return;
		}
		bool heal = m_iTool == 0;
		bool gearHome = m_iTool == 13;
		bool clothing = m_iTool == 14;
		bool gear = gearHome || clothing;
		m_wRoot.FindAnyWidget("GearMode").SetVisible(gear);
		m_wRoot.FindAnyWidget("GearSelectedBar").SetVisible(gear);
		m_wRoot.FindAnyWidget("ToolAllowClothing").SetVisible(clothing);
		SCR_ButtonTextComponent.GetButtonText("ToolGear", m_wRoot).SetToggled(gear, false, false);
		SCR_ButtonTextComponent.GetButtonText("ToolForcedClothing", m_wRoot).SetToggled(clothing, false, false);
		GC_PositionClothingScope(clothing);
		bool unstuck = m_iTool == 10;
		bool warn = m_iTool == 11;
		bool individual = unstuck || warn;
		SCR_ButtonTextComponent.GetButtonText("ToolUnstuck", m_wRoot).SetToggled(unstuck, false, false);
		SCR_ButtonTextComponent.GetButtonText("ToolWarn", m_wRoot).SetToggled(warn, false, false);
		bool serverStatus = m_iTool == 12;
		m_ServerStatus.SetActive(serverStatus);
		m_wRoot.FindAnyWidget("StatusSelectedBar").SetVisible(serverStatus);
		SCR_ButtonTextComponent.GetButtonText("ToolServerStatus", m_wRoot).SetToggled(serverStatus, false, false);
		bool logs = m_iTool == 9;
		m_wRoot.FindAnyWidget("LogsSelectedBar").SetVisible(logs);
		m_Logs.SetActive(logs);
		SCR_ButtonTextComponent.GetButtonText("ToolLogs", m_wRoot).SetToggled(logs, false, false);
		bool freeze = m_iTool == 8;
		bool playerScope = heal || freeze || clothing;
		m_wRoot.FindAnyWidget("ToolUnfreeze").SetVisible(freeze);
		m_wRoot.FindAnyWidget("FreezeSelectedBar").SetVisible(freeze);
		bool teleport = m_iTool == 1;
		bool message = m_iTool == 3;
		m_wRoot.FindAnyWidget("MessageMode").SetVisible(message || warn);
		SCR_ButtonTextComponent.GetButtonText("MessageNormal", m_wRoot).SetToggled(message, false, false);
		bool reservations = m_iTool == 4;
		bool boundaries = m_iTool == 7;
		bool ending = m_iTool == 15;
		bool missions = m_iTool == 16 || ending;
		m_wRoot.FindAnyWidget("MissionSections").SetVisible(missions);
		m_Missions.SetActive(missions && !ending);
		SCR_ButtonTextComponent.GetButtonText("MissionBrowse", m_wRoot).SetToggled(missions && !ending, false, false);
		SCR_ButtonTextComponent.GetButtonText("ToolMissions", m_wRoot).SetToggled(missions, false, false);
		m_Ending.SetActive(ending);
		SCR_ButtonTextComponent.GetButtonText("ToolEndMatch", m_wRoot).SetToggled(ending, false, false);
		m_wRoot.FindAnyWidget("BoundariesSelectedBar").SetVisible(boundaries);
		m_Boundaries.SetActive(boundaries);
		m_wRoot.FindAnyWidget("ToolDescription").SetVisible(!boundaries);
		m_wRoot.FindAnyWidget("ContextDivider").SetVisible(!boundaries);
		SCR_ButtonTextComponent.GetButtonText("ToolBoundaries", m_wRoot).SetToggled(boundaries, false, false);
		bool mapPlans = m_iTool == 6;
		m_MapPlans.SetActive(mapPlans);
		m_wRoot.FindAnyWidget("MapPlansSelectedBar").SetVisible(mapPlans);
		SCR_ButtonTextComponent.GetButtonText("ToolMapPlans", m_wRoot).SetToggled(mapPlans, false, false);
		m_Reservations.SetActive(reservations);
		m_wRoot.FindAnyWidget("ReservationsSelectedBar").SetVisible(reservations);
		SCR_ButtonTextComponent.GetButtonText("ToolReservations", m_wRoot).SetToggled(reservations, false, false);
		bool moderation = m_iTool == 5;
		m_wRoot.FindAnyWidget("ModerationSelectedBar").SetVisible(moderation);
		m_wRoot.FindAnyWidget("ModerationFields").SetVisible(moderation);
		m_wRoot.FindAnyWidget("ToolApply").SetVisible(!missions && !ending && !gearHome && !moderation && !reservations && !mapPlans && !boundaries && !logs && !serverStatus);
		m_wRoot.FindAnyWidget("ToolSummary").SetVisible(!moderation && !reservations && !mapPlans && !boundaries && !logs && !serverStatus);
		m_wRoot.FindAnyWidget("ToolStatus").SetVisible(!missions && !ending && !moderation && !reservations && !mapPlans && !boundaries && !logs && !serverStatus);
		bool factionTarget = (playerScope && m_eScope == GC_EAdminHealScope.FACTION) || (teleport && m_bTeleportFaction);
		bool selectedMessage = warn || (message && m_eMessageAudience == GC_EAdminMessageAudience.SELECTED_PLAYERS);
		m_wRoot.FindAnyWidget("MessageFields").SetVisible(message || warn);
		m_wRoot.FindAnyWidget("MessageAudienceGroup").SetVisible(message);
		m_wRoot.FindAnyWidget("MessageRecipientGroup").SetVisible(selectedMessage);
		m_wRoot.FindAnyWidget("MessageFactionSpace").SetVisible(message && m_eMessageAudience == GC_EAdminMessageAudience.FACTION);
		m_wRoot.FindAnyWidget("MessageAudienceLabel").SetVisible(message);
		m_wRoot.FindAnyWidget("MessageSelected").SetVisible(message);
		m_wRoot.FindAnyWidget("MessageEveryone").SetVisible(message);
		m_wRoot.FindAnyWidget("MessageFaction").SetVisible(message);
		m_wRoot.FindAnyWidget("MessagePlayers").SetVisible(selectedMessage);
		m_wRoot.FindAnyWidget("MessagePlayersLabel").SetVisible(selectedMessage);
		Widget factionScroll = m_wRoot.FindAnyWidget("MessageFactionScroll");
		factionScroll.SetVisible(factionTarget || (message && m_eMessageAudience == GC_EAdminMessageAudience.FACTION));
		if (teleport)
		{
			FrameSlot.SetAnchorMin(factionScroll, 0.24, 0.30);
			FrameSlot.SetAnchorMax(factionScroll, 0.94, 0.30);
			FrameSlot.SetOffsets(factionScroll, 0, 0, 0, -88);
		}
		else
		{
			float factionOffset;
			if (clothing)
				factionOffset = 0.1;
			FrameSlot.SetAnchorMin(factionScroll, 0.24, 0.36 + factionOffset);
			FrameSlot.SetAnchorMax(factionScroll, 0.94, 0.46 + factionOffset);
			FrameSlot.SetOffsets(factionScroll, 0, 0, 0, 0);
		}
		m_wRoot.FindAnyWidget("TeleportScope").SetVisible(teleport || unstuck);
		m_wRoot.FindAnyWidget("ScopeFaction").SetVisible(playerScope);
		m_wRoot.FindAnyWidget("FromPlayer").SetVisible(unstuck || !m_bTeleportFaction);
		m_wRoot.FindAnyWidget("FromLabel").SetVisible(unstuck || !m_bTeleportFaction);
		m_wRoot.FindAnyWidget("ToPlayer").SetVisible(teleport);
		m_wRoot.FindAnyWidget("ToLabel").SetVisible(teleport);
		m_wRoot.FindAnyWidget("MessageSelectedBar").SetVisible(message || warn);
		m_wRoot.FindAnyWidget("HealScope").SetVisible(playerScope);
		m_wRoot.FindAnyWidget("HealRadius").SetVisible(playerScope && m_eScope == GC_EAdminHealScope.NEARBY);
		m_wRoot.FindAnyWidget("HealPlayers").SetVisible(playerScope && m_eScope == GC_EAdminHealScope.SELECTED);
		m_wRoot.FindAnyWidget("HealSelectionLabel").SetVisible(playerScope && m_eScope == GC_EAdminHealScope.SELECTED);
		m_wRoot.FindAnyWidget("HealSelectedBar").SetVisible(heal);
		m_wRoot.FindAnyWidget("TeleportSelectedBar").SetVisible(teleport || unstuck);
		m_wRoot.FindAnyWidget("TeleportFields").SetVisible(teleport || unstuck);
		string title = "Make playable";
		string description = "Create a playable slot from this character. Its group and position are retained; its AI will be disabled.";
		string action = "Make playable";
		string summary;
		bool enabled = m_iPending < 0 && m_iMessagePending < 0;
		m_wRoot.FindAnyWidget("MessageFields").SetEnabled(enabled);
		m_wRoot.FindAnyWidget("MessageFactionScroll").SetEnabled(enabled);
		m_HealRadius.GetRootWidget().SetEnabled(enabled);
		m_Targets.GetRootWidget().SetEnabled(enabled);
		m_From.GetRootWidget().SetEnabled(enabled);
		m_To.GetRootWidget().SetEnabled(enabled);
		array<int> players;
		array<string> identities;
		if (playerScope)
		{
			title = "Heal";
			description = "Heals living players; respawns the dead.";
			action = "Heal everyone";
			summary = "Heal all connected players. Dead players respawn in their claimed slots.";
			if (m_eScope == GC_EAdminHealScope.NEARBY)
			{
				action = string.Format("Heal within %1m", m_iHealRadius);
				summary = string.Format("Players and vehicles within %1m of you. Dead players respawn; wrecks are skipped.", m_iHealRadius);
			}
			else if (m_eScope == GC_EAdminHealScope.SELECTED)
			{
				bool selected = m_Targets.GetSelection(players, identities);
				enabled = enabled && selected;
				action = string.Format("Heal %1 players", players.Count());
				if (players.Count() == 1)
					action = "Heal player";
				summary = string.Format("%1 players selected.", players.Count());
				if (players.Count() == 1)
					summary = "1 player selected.";
			}
			else if (m_eScope == GC_EAdminHealScope.FACTION)
			{
				action = "Heal faction";
				summary = string.Format("All connected players in %1 when applied, including distant players.", GetSelectedFactionName());
				enabled = enabled && !m_sTargetFaction.IsEmpty();
			}
		}
		if (freeze)
		{
			title = "Freeze";
			description = "Holds players and their driven vehicles.";
			action = "Freeze players";
			if (m_eScope == GC_EAdminHealScope.EVERYONE)
				summary = "All connected players.";
			else if (m_eScope == GC_EAdminHealScope.NEARBY)
				summary = string.Format("Players within %1m of you when applied.", m_iHealRadius);
			SCR_ButtonTextComponent unfreeze = SCR_ButtonTextComponent.GetButtonText("ToolUnfreeze", m_wRoot);
			unfreeze.SetText("Unfreeze players");
			unfreeze.SetEnabled(enabled);
		}
		if (gear)
		{
			title = "Gear";
			description = "";
			summary = "";
			if (clothing)
			{
				action = "Lock clothing";
				SCR_ButtonTextComponent.GetButtonText("ToolAllowClothing", m_wRoot).SetEnabled(enabled);
			}
		}

		if (teleport)
		{
			title = "Teleport";
			description = "";
			array<int> destinations;
			array<string> destinationIdentities;
			bool fromSelected = m_From.GetSelection(players, identities);
			bool toSelected = m_To.GetSelection(destinations, destinationIdentities);
			if (m_bTeleportFaction)
			{
				description = "Dead and seated players are skipped.";
				enabled = enabled && !m_sTargetFaction.IsEmpty() && toSelected;
				summary = string.Format("All connected players in %1 when confirmed.", GetSelectedFactionName());
				if (toSelected)
					summary = string.Format("Move all current members of %1 to %2.", GetSelectedFactionName(), GetGame().GetPlayerManager().GetPlayerName(destinations[0]));
				action = "Review teleport";
				if (m_bConfirmFactionTeleport)
					action = "Confirm teleport";
			}
			else
			{
				enabled = enabled && fromSelected && toSelected;
				if (fromSelected && toSelected)
				{
					enabled = enabled && players[0] != destinations[0];
					summary = GetGame().GetPlayerManager().GetPlayerName(players[0]) + " -> " + GetGame().GetPlayerManager().GetPlayerName(destinations[0]);
				}
				action = "Teleport player";
			}
		}
		if (message)
		{
			title = "Message";
			description = "";
			action = "Send message";
			string text = m_MessageText.GetText();
			text.TrimInPlace();
			enabled = enabled && !text.IsEmpty() && text.Length() <= 512 && !text.Contains("<") && !text.Contains(">");
			if (text.Length() > 512)
				m_Status.SetText("Shorten the message to 512 characters or fewer.");
			else if (text.Contains("<") || text.Contains(">"))
				m_Status.SetText("Remove < and > from the message before sending.");
			summary = "Everyone, including spectators.";
			if (m_eMessageAudience == GC_EAdminMessageAudience.SELECTED_PLAYERS)
			{
				bool hasRecipients = m_MessagePlayers.GetSelection(players, identities);
				enabled = enabled && hasRecipients && players.Count() <= 64;
				summary = string.Format("%1 players selected.", players.Count());
				if (players.Count() == 1)
					summary = "1 player selected.";
			}
			else if (m_eMessageAudience == GC_EAdminMessageAudience.FACTION)
			{
				enabled = enabled && !m_sTargetFaction.IsEmpty();
				summary = "Current faction members.";
			}
			TextWidget.Cast(m_wRoot.FindAnyWidget("MessageLabel")).SetTextFormat("MESSAGE (%1 / 512)", m_MessageText.GetText().Length());
		}
		if (individual)
		{
			title = "Teleport";
			action = "Unstuck";
			description = "Move one player to nearby clear ground.";
			GC_AdminPlayerPicker picker = m_From;
			if (warn)
				picker = m_MessagePlayers;
			enabled = enabled && picker.GetSelection(players, identities) && players.Count() == 1;
			if (warn)
			{
				title = "Message";
				action = "Send warning";
				description = "";
				string reason = m_MessageText.GetText();
				reason.TrimInPlace();
				enabled = enabled && !reason.IsEmpty() && reason.Length() <= 256 && !reason.Contains("\n")
					&& !reason.Contains("\r") && !reason.Contains("<") && !reason.Contains(">");
				TextWidget.Cast(m_wRoot.FindAnyWidget("MessageLabel")).SetTextFormat("REASON (%1 / 256)", reason.Length());
			}
			summary = "Select one player.";
			if (players && players.Count() == 1)
				summary = string.Empty;
		}
		if (moderation)
		{
			title = "Kick / Ban";
			description = "";
		}
		if (reservations)
		{
			title = "Slot reservations";
			description = "";
		}
		if (boundaries)
		{
			title = "Boundaries";
			description = string.Empty;
		}
		if (mapPlans)
		{
			title = "Markers";
			description = string.Empty;
		}
		if (logs)
		{
			title = "Logs";
			description = "Current mission";
		}
		if (serverStatus)
		{
			title = "Status";
			description = "";
		}
		if (ending || missions)
		{
			title = "Missions";
			description = "";
		}
		TextWidget.Cast(m_wRoot.FindAnyWidget("ToolTitle")).SetText(title);
		TextWidget.Cast(m_wRoot.FindAnyWidget("ToolDescription")).SetText(description);
		m_wRoot.FindAnyWidget("ToolDescription").SetVisible(!description.IsEmpty());
		m_wRoot.FindAnyWidget("ToolSummary").SetVisible(individual || (teleport && m_bTeleportFaction)
			|| (playerScope && m_eScope != GC_EAdminHealScope.SELECTED));
		TextWidget.Cast(m_wRoot.FindAnyWidget("ToolSummary")).SetText(summary);
		m_Apply.SetText(action);
		string actionIcon = "check";
		if (heal)
			actionIcon = "plus";
		else if (freeze)
			actionIcon = "pause";
		else if (clothing)
			actionIcon = "server-locked";
		else if (teleport)
			actionIcon = "move";
		else if (unstuck)
			actionIcon = "respawn";
		else if (message)
			actionIcon = "comments";
		else if (warn)
			actionIcon = "warning";
		ImageWidget.Cast(m_Apply.GetRootWidget().FindAnyWidget("Icon")).LoadImageFromSet(0,
			"{2EFEA2AF1F38E7F0}UI/Textures/Icons/icons_wrapperUI-64.imageset", actionIcon);
		m_Apply.SetEnabled(enabled);
		TextWidget actionText = TextWidget.Cast(m_Apply.GetRootWidget().FindAnyWidget("Text"));
		if (enabled)
			actionText.SetColor(Color.FromRGBA(240, 240, 240, 255));
		else
			actionText.SetColor(Color.FromRGBA(150, 150, 150, 255));
		if (clothing)
			TextWidget.Cast(m_wRoot.FindAnyWidget("ToolAllowClothing").FindAnyWidget("Text")).SetColor(actionText.GetColor());
		SCR_ButtonTextComponent.GetButtonText("ToolHeal", m_wRoot).SetToggled(heal, false, false);
		SCR_ButtonTextComponent.GetButtonText("ToolFreeze", m_wRoot).SetToggled(freeze, false, false);
		SCR_ButtonTextComponent.GetButtonText("ToolTeleport", m_wRoot).SetToggled(teleport || unstuck, false, false);
		SCR_ButtonTextComponent.GetButtonText("ToolMessage", m_wRoot).SetToggled(message || warn, false, false);
		SCR_ButtonTextComponent.GetButtonText("ToolModeration", m_wRoot).SetToggled(moderation, false, false);
		SCR_ButtonTextComponent.GetButtonText("MessageSelected", m_wRoot).SetToggled(m_eMessageAudience == GC_EAdminMessageAudience.SELECTED_PLAYERS, false, false);
		SCR_ButtonTextComponent.GetButtonText("MessageEveryone", m_wRoot).SetToggled(m_eMessageAudience == GC_EAdminMessageAudience.ALL_PLAYERS, false, false);
		SCR_ButtonTextComponent.GetButtonText("MessageFaction", m_wRoot).SetToggled(m_eMessageAudience == GC_EAdminMessageAudience.FACTION, false, false);
		foreach (SCR_ButtonBaseComponent factionButton, string factionKey : m_mFactionButtons)
			factionButton.SetToggled(factionKey == m_sTargetFaction, false, false);
		SCR_ButtonTextComponent.GetButtonText("ScopeSelected", m_wRoot).SetToggled(m_eScope == GC_EAdminHealScope.SELECTED, false, false);
		SCR_ButtonTextComponent.GetButtonText("ScopeEveryone", m_wRoot).SetToggled(m_eScope == GC_EAdminHealScope.EVERYONE, false, false);
		SCR_ButtonTextComponent.GetButtonText("ScopeNearby", m_wRoot).SetToggled(m_eScope == GC_EAdminHealScope.NEARBY, false, false);
		SCR_ButtonTextComponent.GetButtonText("ScopeFaction", m_wRoot).SetToggled(m_eScope == GC_EAdminHealScope.FACTION, false, false);
		SCR_ButtonTextComponent.GetButtonText("TeleportPlayer", m_wRoot).SetToggled(teleport && !m_bTeleportFaction, false, false);
		SCR_ButtonTextComponent.GetButtonText("TeleportFaction", m_wRoot).SetToggled(teleport && m_bTeleportFaction, false, false);
	}

	//------------------------------------------------------------------------------------------------
	protected void OnApply(SCR_ButtonBaseComponent button)
	{
		if (!HasAccess() || m_iPending >= 0 || m_iMessagePending >= 0 || !m_Apply.GetRootWidget().IsEnabled())
			return;
		array<int> players = {};
		array<string> identities = {};
		if (m_iTool == 10 && (!m_From.GetSelection(players, identities) || players.Count() != 1))
			return;
		if (m_iTool == 11 && (!m_MessagePlayers.GetSelection(players, identities) || players.Count() != 1))
			return;
		if (m_iTool == 3)
		{
			if (m_eMessageAudience == GC_EAdminMessageAudience.SELECTED_PLAYERS && !m_MessagePlayers.GetSelection(players, identities))
				return;
			m_iMessagePending = m_Controller.NextAdminMessageRequestId();
			OnRosterOpened(null);
			m_Status.SetText("Sending...");
			Refresh();
			m_Controller.RequestAdminMessage(m_eMessageAudience, m_MessageText.GetText(), identities, m_iMessagePending, m_sTargetFaction);
			return;
		}
		if ((m_iTool == 0 || m_iTool == 8 || m_iTool == 14) && m_eScope == GC_EAdminHealScope.SELECTED && !m_Targets.GetSelection(players, identities))
			return;
		array<int> destinations;
		array<string> destinationIdentities;
		if (m_iTool == 1)
		{
			if (!m_To.GetSelection(destinations, destinationIdentities))
				return;
			if (!m_bTeleportFaction && !m_From.GetSelection(players, identities))
				return;
			if (m_bTeleportFaction && !m_bConfirmFactionTeleport)
			{
				m_bConfirmFactionTeleport = true;
				m_Status.SetText("Review the faction and destination, then confirm to move these players.");
				Refresh();
				return;
			}
		}
		m_iPending = m_Controller.ReserveAdminPlayerToolRequest();
		OnRosterOpened(null);
		m_Status.SetText("Applying...");
		Refresh();
		if (m_iTool == 0)
			m_Controller.RequestAdminHeal(m_eScope, players, identities, m_iPending, m_iHealRadius, m_sTargetFaction);
		else if (m_iTool == 10)
			m_Controller.GC_RequestAdminUnstuck(players[0], identities[0], m_iPending);
		else if (m_iTool == 11)
			m_Controller.GC_RequestAdminWarn(players[0], identities[0], m_MessageText.GetText(), m_iPending);
		else if (m_iTool == 8)
			m_Controller.GC_RequestAdminControlFreeze(m_eScope, players, identities, m_iPending, m_iHealRadius, m_sTargetFaction, button.GetRootWidget().GetName() != "ToolUnfreeze");
		else if (m_iTool == 14)
			m_Controller.GC_RequestForcedClothing(m_eScope, players, identities, m_iPending, m_iHealRadius, m_sTargetFaction, button.GetRootWidget().GetName() == "ToolAllowClothing");
		else if (m_iTool == 1 && m_bTeleportFaction)
			m_Controller.GC_RequestFactionTeleport(m_sTargetFaction, destinations[0], destinationIdentities[0], m_iPending);
		else if (m_iTool == 1)
			m_Controller.GC_RequestAdminTeleport(players[0], identities[0], destinations[0], destinationIdentities[0], m_iPending);
		else
			m_Controller.RequestAdminMakePlayable(m_MakePlayableEntity, m_MakePlayableGroup);
	}

	//------------------------------------------------------------------------------------------------
	protected void OnResult(int requestId, string outcome)
	{
		if (requestId != m_iPending)
			return;
		m_iPending = -1;
		m_bConfirmFactionTeleport = false;
		m_Status.SetText(outcome);
		Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected void OnLegacyResult(string outcome)
	{
		if (m_iTool == 2 && m_iPending >= 0)
			OnResult(m_iPending, GC_AdminActionResultText.Get(outcome));
	}

	//------------------------------------------------------------------------------------------------
	protected void OnRoleChanged(int playerId)
	{
		if (!HasAccess())
			Close();
	}

	//------------------------------------------------------------------------------------------------
	protected void OnMessageChanged(Widget w)
	{
		if (m_iMessagePending < 0)
		{
			m_Status.SetText("");
			Refresh();
		}
	}

	//------------------------------------------------------------------------------------------------
	protected void OnMessageResult(int requestId, string outcome)
	{
		if (requestId != m_iMessagePending)
			return;
		m_iMessagePending = -1;
		if (outcome.StartsWith("dispatched"))
			m_MessageText.SetText("");
		m_Status.SetText(GC_AdminActionResultText.Get(outcome));
		Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected void OnFactionsChanged(Faction faction)
	{
		m_bConfirmFactionTeleport = false;
		RebuildFactions();
		Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected void RebuildFactions()
	{
		if (!m_Manager)
			return;
		Widget rows = m_wRoot.FindAnyWidget("MessageFactionRows");
		m_mFactionButtons.Clear();
		while (rows.GetChildren())
			rows.GetChildren().RemoveFromHierarchy();
		array<string> keys = {};
		array<Faction> factions = {};
		GetGame().GetFactionManager().GetFactionsList(factions);
		foreach (Faction faction : factions)
		{
			if (!faction)
				continue;
			string key = faction.GetFactionKey();
			if (key.IsEmpty() || keys.Contains(key))
				continue;
			keys.Insert(key);
			Widget row = GetGame().GetWorkspace().CreateWidgets("{6A5A000000000010}UI/layouts/Admin/Lobby_AdminOption.layout", rows);
			SCR_ButtonTextComponent button = SCR_ButtonTextComponent.Cast(row.FindHandler(SCR_ButtonTextComponent));
			button.SetText(faction.GetFactionName());
			button.m_OnClicked.Insert(OnFactionSelected);
			m_mFactionButtons.Set(button, key);
		}
		if (!keys.Contains(m_sTargetFaction))
			m_sTargetFaction = "";
	}

	//------------------------------------------------------------------------------------------------
	protected void OnFactionSelected(SCR_ButtonBaseComponent button)
	{
		if (!HasAccess() || m_iMessagePending >= 0 || m_iPending >= 0)
			return;
		m_bConfirmFactionTeleport = false;
		if (m_mFactionButtons.Find(button, m_sTargetFaction))
			Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected string GetSelectedFactionName()
	{
		Faction faction = GetGame().GetFactionManager().GetFactionByKey(m_sTargetFaction);
		if (faction)
			return faction.GetFactionName();
		return "the selected faction";
	}

	//------------------------------------------------------------------------------------------------
	protected void OnFactionSlotChanged(GC_Slot slot)
	{
		m_bConfirmFactionTeleport = false;
		if (m_iPending < 0)
			Refresh();
	}

	//------------------------------------------------------------------------------------------------
	protected void OnFactionPlayerChanged(int playerId)
	{
		OnFactionSlotChanged(null);
	}

	//------------------------------------------------------------------------------------------------
	override void HandlerDeattached(Widget w)
	{
		if (m_MessageEvents)
			m_MessageEvents.GetOnChange().Remove(OnMessageChanged);
		if (m_HealRadius)
			m_HealRadius.m_OnChanged.Remove(OnHealRadiusChanged);
		if (m_Manager)
		{
			m_Manager.m_OnPlayerRoleChanged.Remove(OnRoleChanged);
			m_Manager.m_OnFactionTopologyChanged.Remove(OnFactionsChanged);
			m_Manager.m_OnSlotPresentationChanged.Remove(OnFactionSlotChanged);
			m_Manager.m_OnPlayerDisconnected.Remove(OnFactionPlayerChanged);
			m_Manager.m_OnPlayerConnected.Remove(OnFactionPlayerChanged);
		}
		if (m_Controller)
		{
			m_Controller.m_OnAdminPlayerToolResult.Remove(OnResult);
			m_Controller.m_OnAdminActionResult.Remove(OnLegacyResult);
			m_Controller.m_OnAdminMessageResult.Remove(OnMessageResult);
		}
		s_wPanel = null;
		super.HandlerDeattached(w);
	}
}
