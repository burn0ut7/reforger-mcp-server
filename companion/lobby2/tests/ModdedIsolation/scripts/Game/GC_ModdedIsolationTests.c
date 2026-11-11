//! Explicit temporary embedded-Play checks. Creates only marked test items, cleaned before Reload.
modded class GC_LobbyPlayerController
{
	protected IEntity m_GCTestJacket;
	protected SCR_InventoryStorageManagerComponent m_GCTestInventory;
	protected IEntity m_GCTestReplacement;
	protected bool m_bGCTestPermission;
	protected int m_iGCTestAdded;
	protected int m_iGCTestRemoved;
	protected int m_iGCTestChecks;
	protected int m_iGCTestFailures;

	protected void GC_TestOnGearItemAdded(IEntity item, BaseInventoryStorageComponent storage)
	{
		if (item == m_GCTestJacket)
			m_iGCTestAdded++;
	}

	protected void GC_TestOnGearItemRemoved(IEntity item, BaseInventoryStorageComponent storage)
	{
		if (item == m_GCTestJacket)
			m_iGCTestRemoved++;
	}

	protected void GC_TestCheck(bool passed, string label)
	{
		m_iGCTestChecks++;
		if (passed)
			return;
		m_iGCTestFailures++;
		Print("GC | Modded isolation FAILED | " + label, LogLevel.ERROR);
	}

	string GC_TestStart()
	{
		if (!Replication.IsServer() || !SCR_Global.IsAdmin(GetPlayerId()) || !GetControlledEntity() || m_GCTestJacket)
			return "unavailable: authoritative admin body required; no replay";
		SCR_CharacterInventoryStorageComponent storage = SCR_CharacterInventoryStorageComponent.Cast(GetControlledEntity().FindComponent(SCR_CharacterInventoryStorageComponent));
		if (!storage || storage.GetClothFromArea(LoadoutJacketArea))
			return "unavailable: test requires empty jacket slot";
		m_bGCTestPermission = GC_LobbyPlayerController.Cast(this).GC_AreClothingChangesAllowed();
		m_GCTestInventory = SCR_InventoryStorageManagerComponent.Cast(GetControlledEntity().FindComponent(SCR_InventoryStorageManagerComponent));
		m_GCTestInventory.m_OnItemAddedInvoker.Insert(GC_TestOnGearItemAdded);
		m_GCTestInventory.m_OnItemRemovedInvoker.Insert(GC_TestOnGearItemRemoved);
		GC_TestCheck(!storage.OnOverrideCanRemoveItem() && !storage.OnOverrideCanReplaceItem(), "native validation switches retained");
		GC_LobbyPlayerController.Cast(this).GC_BindGearAudit();
		GC_LobbyPlayerController.Cast(this).GC_BindGearAudit();
		GC_TestCheck(m_GCTestInventory && m_GCTestInventory.GetOwner() == GetControlledEntity(), "bind current body");
		Resource resource = Resource.Load("{C7861F11D5334C0E}Prefabs/Characters/Uniforms/Jacket_US_BDU.et");
		EntitySpawnParams params = new EntitySpawnParams();
		params.TransformMode = ETransformMode.WORLD;
		params.Transform[3] = GetControlledEntity().GetOrigin();
		m_GCTestJacket = GetGame().SpawnEntityPrefab(resource, GetGame().GetWorld(), params);
		m_GCTestReplacement = GetGame().SpawnEntityPrefab(resource, GetGame().GetWorld(), params);
		GC_TestCheck(m_GCTestJacket && m_GCTestReplacement, "spawn test items");
		if (!m_GCTestJacket || !m_GCTestReplacement || !m_GCTestInventory)
			return "setup failed";
		bool inserted = m_GCTestInventory.TryInsertItemInStorage(m_GCTestJacket, storage);
		return string.Format("insert dispatched=%1", inserted);
	}

	string GC_TestPermissions()
	{
		SCR_CharacterInventoryStorageComponent storage = SCR_CharacterInventoryStorageComponent.Cast(GetControlledEntity().FindComponent(SCR_CharacterInventoryStorageComponent));
		if (!m_GCTestJacket || !m_GCTestInventory || !storage)
			return "setup missing";
		GC_TestCheck(storage.GetClothFromArea(LoadoutJacketArea) == m_GCTestJacket, "actual jacket equipped");
		GC_TestCheck(m_iGCTestAdded == 1, "one native insertion event");
		InventoryItemComponent item = InventoryItemComponent.Cast(m_GCTestJacket.FindComponent(InventoryItemComponent));
		int slot = item.GetParentSlot().GetID();
		GC_TestCheck(!GC_LobbyPlayerController.Cast(this).GC_SetClothingChangesAllowed(0, true), "unauthenticated permission rejected");
		GC_TestCheck(GC_LobbyPlayerController.Cast(this).GC_SetClothingChangesAllowed(GetPlayerId(), false), "authenticated lock");
		GC_TestCheck(!m_GCTestInventory.CanRemoveItemFromStorage(m_GCTestJacket, storage), "locked native remove check");
		GC_TestCheck(!m_GCTestInventory.CanReplaceItem(m_GCTestReplacement, storage, slot), "locked native replace check");
		GC_TestCheck(!m_GCTestInventory.TryRemoveItemFromStorage(m_GCTestJacket, storage), "locked real remove denied");
		GC_TestCheck(!m_GCTestInventory.TryReplaceItem(m_GCTestReplacement, storage, slot), "locked real replacement denied");
		GC_TestCheck(GC_LobbyManager.GetManager().GC_TestAbsentFeature(m_GCTestJacket, m_GCTestReplacement, slot), "native permission with Lobby feature absent");
		GC_TestCheck(m_GCTestInventory.Contains(m_GCTestJacket) && m_iGCTestRemoved == 0, "rejection preserves item and events");
		GC_TestCheck(GC_LobbyPlayerController.Cast(this).GC_SetClothingChangesAllowed(GetPlayerId(), true), "authenticated unlock");
		GC_TestCheck(m_GCTestInventory.CanRemoveItemFromStorage(m_GCTestJacket, storage), "unlocked native remove check");
		GC_TestCheck(m_GCTestInventory.CanReplaceItem(m_GCTestReplacement, storage, slot), "unlocked native replace check");
		GC_TestCheck(!m_GCTestInventory.CanReplaceItem(m_GCTestReplacement, storage, 9999), "invalid native slot rejected");
		bool removed = m_GCTestInventory.TryRemoveItemFromStorage(m_GCTestJacket, storage);
		return string.Format("remove dispatched=%1 checks=%2 failures=%3", removed, m_iGCTestChecks, m_iGCTestFailures);
	}

	string GC_TestShutdownComplete()
	{
		GC_LobbyPlayerController controller = GC_LobbyPlayerController.Cast(this);
		GC_TestCheck(GetGame().GetCallqueue().GetRemainingTime(controller.GC_PresentJipMessage) < 0
			&& GetGame().GetCallqueue().GetRemainingTime(controller.GC_ObserveJip) < 0, "JIP callbacks cancelled after queue tick");
		return string.Format("checks=%1 failures=%2", m_iGCTestChecks, m_iGCTestFailures);
	}

	string GC_TestFinish()
	{
		if (!m_GCTestJacket || !m_GCTestInventory)
			return "setup missing";
		SCR_InventoryStorageManagerComponent inventory = m_GCTestInventory;
		SCR_CharacterInventoryStorageComponent storage = SCR_CharacterInventoryStorageComponent.Cast(GetControlledEntity().FindComponent(SCR_CharacterInventoryStorageComponent));
		GC_TestCheck(!inventory.Contains(m_GCTestJacket) && m_iGCTestRemoved == 1, "one native removal event");
		GC_LobbyPlayerController.Cast(this).GC_UnbindGearAudit();
		GC_LobbyPlayerController.Cast(this).GC_UnbindGearAudit();
		GC_TestCheck(inventory.TryInsertItemInStorage(m_GCTestJacket, storage), "native insert after detach");
		GC_TestCheck(m_iGCTestAdded == 2, "native event preserved while audit detached");
		GC_LobbyPlayerController.Cast(this).GC_BindGearAudit();
		GC_TestCheck(inventory.TryRemoveItemFromStorage(m_GCTestJacket, storage), "native remove after rebind");
		GC_TestCheck(m_iGCTestRemoved == 2, "native removal after audit rebind");
		GC_TestCheck(GC_LobbyPlayerController.Cast(this).GC_SetClothingChangesAllowed(GetPlayerId(), m_bGCTestPermission), "restore permission");
		inventory.m_OnItemAddedInvoker.Remove(GC_TestOnGearItemAdded);
		inventory.m_OnItemRemovedInvoker.Remove(GC_TestOnGearItemRemoved);
		SCR_EntityHelper.DeleteEntityAndChildren(m_GCTestJacket);
		SCR_EntityHelper.DeleteEntityAndChildren(m_GCTestReplacement);
		m_GCTestJacket = null;
		m_GCTestReplacement = null;
		GetGame().GetCallqueue().CallLater(GC_LobbyPlayerController.Cast(this).GC_PresentJipMessage, 60000);
		GetGame().GetCallqueue().CallLater(GC_LobbyPlayerController.Cast(this).GC_ObserveJip, 60000);
		GC_TestCheck(GetGame().GetCallqueue().GetRemainingTime(GC_LobbyPlayerController.Cast(this).GC_PresentJipMessage) >= 0, "queued JIP presentation");
		GC_TestCheck(GetGame().GetCallqueue().GetRemainingTime(GC_LobbyPlayerController.Cast(this).GC_ObserveJip) >= 0, "queued JIP observer");
		GC_LobbyPlayerController.Cast(this).ShutdownLobbyPresentation();
		GC_LobbyPlayerController.Cast(this).ShutdownLobbyPresentation();
		GC_TestCheck(!GetGame().GetInputManager().RemoveActionListener("GC_Jip", EActionTrigger.DOWN, GC_LobbyPlayerController.Cast(this).GC_OnJipPressed), "JIP input listener removed");
		return string.Format("checks=%1 failures=%2", m_iGCTestChecks, m_iGCTestFailures);
	}
}

modded class GC_LobbyManager
{
	bool GC_TestAbsentFeature(IEntity jacket, IEntity replacement, int slot)
	{
		IEntity body = GetGame().GetPlayerController().GetControlledEntity();
		SCR_CharacterInventoryStorageComponent storage = SCR_CharacterInventoryStorageComponent.Cast(body.FindComponent(SCR_CharacterInventoryStorageComponent));
		SCR_InventoryStorageManagerComponent inventory = SCR_InventoryStorageManagerComponent.Cast(body.FindComponent(SCR_InventoryStorageManagerComponent));
		bool previous = m_bShutdown;
		m_bShutdown = true;
		bool passed = !GC_LobbyManager.GetManager() && inventory.CanRemoveItemFromStorage(jacket, storage)
			&& inventory.CanReplaceItem(replacement, storage, slot) && !inventory.CanReplaceItem(replacement, storage, 9999);
		m_bShutdown = previous;
		return passed;
	}

	string GC_RunModdedIsolation(int stage)
	{
		GC_LobbyPlayerController controller = GC_LobbyPlayerController.Cast(GetGame().GetPlayerController());
		if (!controller)
			return "no Lobby player";
		switch (stage)
		{
			case 0: return controller.GC_TestStart();
			case 1: return controller.GC_TestPermissions();
			case 2: return controller.GC_TestFinish();
			case 3: return controller.GC_TestShutdownComplete();
		}
		return "invalid stage";
	}
}
