#ifdef WORKBENCH
//! Result of one synchronous editor action. A failed rollback requires inspection.
class RST_EntityReferenceEditResult
{
	string m_sState = "refused";
	string m_sName;
	string m_sReason;
}

//! Owning transaction for a resolved scalar entity-reference destination.
//! Callers resolve fresh source identities and provider eligibility before entry.
class RST_EntityReferenceEdit
{
	//------------------------------------------------------------------------------------------------
	static BaseContainer Resolve(IEntitySource root, array<ref ContainerIdPathEntry> path)
	{
		BaseContainer container = root;
		if (!path)
			return container;
		foreach (ContainerIdPathEntry entry : path)
		{
			if (!container || !entry || entry.PropertyName.IsEmpty())
				return null;
			if (entry.Index < -1)
				return null;
			if (entry.Index < 0)
				container = container.GetObject(entry.PropertyName);
			else
			{
				BaseContainerList objects = container.GetObjectArray(entry.PropertyName);
				if (!objects || entry.Index >= objects.Count())
					return null;
				container = objects.Get(entry.Index);
			}
		}
		return container;
	}

	//------------------------------------------------------------------------------------------------
	protected static bool Matches(IEntitySource root, array<ref ContainerIdPathEntry> path, string expectedClass, string key, string value)
	{
		BaseContainer container = Resolve(root, path);
		string observed;
		return container && container.GetClassName() == expectedClass && container.Get(key, observed) && observed == value;
	}

	//------------------------------------------------------------------------------------------------
	static bool MatchesArray(BaseContainer container, string expectedClass, string key, array<string> expected)
	{
		array<string> observed;
		if (!container || container.GetClassName() != expectedClass || !expected || !container.Get(key, observed) || !observed || observed.Count() != expected.Count())
			return false;
		for (int i; i < expected.Count(); i++)
		{
			if (observed[i] != expected[i])
				return false;
		}
		return true;
	}

	//! Read-only naming decision, shared by preparation and the owning transaction.
	//------------------------------------------------------------------------------------------------
	static string ProposeName(WorldEditorAPI api, IEntitySource entity, out string reason)
	{
		reason = "Entity source unavailable";
		if (!api || !api.GetWorld() || !entity || api.FindEntityByID(entity.GetID()) != entity)
			return "";
		IEntity live = api.SourceToEntity(entity);
		if (!live)
			return "";
		string name = entity.GetName();
		if (name.IsEmpty())
		{
			reason = "Unnamed entity layer is not editable";
			if (entity.GetSubScene() != api.GetCurrentSubScene() || api.IsEntityLayerLockedHierarchy(entity.GetSubScene(), entity.GetLayerID()))
				return "";
			name = api.GenerateDefaultEntityName(entity);
			reason = "Cannot generate an unused entity name";
			if (name.IsEmpty() || api.FindEntityByName(name) || api.GetWorld().FindEntityByName(name))
				return "";
		}
		else if (live.GetName() != name || api.FindEntityByName(name) != entity || api.GetWorld().FindEntityByName(name) != live)
		{
			reason = "Existing entity name does not resolve uniquely";
			return "";
		}
		reason = "";
		return name;
	}

	//------------------------------------------------------------------------------------------------
	static RST_EntityReferenceEditResult CommitScalar(WorldEditorAPI api, IEntitySource entity, IEntitySource destination,
		array<ref ContainerIdPathEntry> path, string expectedClass, string key, string expectedValue)
	{
		RST_EntityReferenceEditResult result = new RST_EntityReferenceEditResult();
		result.m_sReason = "Editor or source unavailable";
		if (!api || !api.IsEditMode() || api.IsGameMode() || api.IsDoingEditAction() || !api.GetWorld() || !entity || !destination)
			return result;
		if (api.FindEntityByID(entity.GetID()) != entity || api.FindEntityByID(destination.GetID()) != destination)
			return result;
		IEntity live = api.SourceToEntity(entity);
		if (!live)
			return result;
		BaseContainer container = Resolve(destination, path);
		result.m_sReason = "Destination changed";
		if (!container || !Matches(destination, path, expectedClass, key, expectedValue))
			return result;
		result.m_sReason = "Destination layer is not editable";
		if (destination.GetSubScene() != api.GetCurrentSubScene() || api.IsEntityLayerLockedHierarchy(destination.GetSubScene(), destination.GetLayerID()))
			return result;

		string oldName = entity.GetName();
		string name = ProposeName(api, entity, result.m_sReason);
		if (name.IsEmpty())
			return result;
		result.m_sName = name;
		if (name == expectedValue && !oldName.IsEmpty())
		{
			result.m_sState = "unchanged";
			result.m_sReason = "";
			return result;
		}

		bool wasLocal = container.IsVariableSetDirectly(key);
		EntityID entityId = entity.GetID();
		EntityID destinationId = destination.GetID();
		result.m_sReason = "Cannot begin entity-reference action";
		if (!api.BeginEntityAction("Select entity reference"))
			return result;

		bool written = api.SetVariableValue(destination, path, key, name);
		entity = api.FindEntityByID(entityId);
		destination = api.FindEntityByID(destinationId);
		written = written && entity && destination && Matches(destination, path, expectedClass, key, name);
		if (written && oldName.IsEmpty())
			written = api.RenameEntity(entity, name);
		entity = api.FindEntityByID(entityId);
		destination = api.FindEntityByID(destinationId);
		live = null;
		if (entity)
			live = api.SourceToEntity(entity);
		written = written && entity && destination && live && entity.GetName() == name && live.GetName() == name;
		written = written && api.FindEntityByName(name) == entity && api.GetWorld().FindEntityByName(name) == live;
		written = written && Matches(destination, path, expectedClass, key, name);
		if (written)
		{
			result.m_sState = "committed";
			result.m_sReason = "";
		}
		else
		{
			// Restore local presence as well as the effective text. An inherited
			// value must not become a new local override after a rejected edit.
			bool restored;
			if (destination && wasLocal)
				restored = api.SetVariableValue(destination, path, key, expectedValue);
			else if (destination)
				restored = api.ClearVariableValue(destination, path, key);
			entity = api.FindEntityByID(entityId);
			if (entity && entity.GetName() != oldName)
				restored = api.RenameEntity(entity, oldName) && restored;
			entity = api.FindEntityByID(entityId);
			destination = api.FindEntityByID(destinationId);
			container = Resolve(destination, path);
			live = null;
			if (entity)
				live = api.SourceToEntity(entity);
			restored = restored && container && container.IsVariableSetDirectly(key) == wasLocal;
			restored = restored && entity && live && Matches(destination, path, expectedClass, key, expectedValue) && entity.GetName() == oldName && live.GetName() == oldName;
			result.m_sState = "rollback_failed";
			if (restored)
				result.m_sState = "rolled_back";
			result.m_sReason = "Entity-reference write did not verify";
		}
		if (!api.EndEntityAction())
		{
			result.m_sState = "finalization_failed";
			result.m_sReason = "Entity action did not close successfully; inspect before retrying";
			return result;
		}
		entity = api.FindEntityByID(entityId);
		destination = api.FindEntityByID(destinationId);
		live = null;
		if (entity)
			live = api.SourceToEntity(entity);
		container = Resolve(destination, path);
		bool finalMatches = entity && destination && live && container;
		if (written)
		{
			finalMatches = finalMatches && Matches(destination, path, expectedClass, key, name);
			finalMatches = finalMatches && entity.GetName() == name && live.GetName() == name;
			finalMatches = finalMatches && api.FindEntityByName(name) == entity && api.GetWorld().FindEntityByName(name) == live;
		}
		else
		{
			finalMatches = finalMatches && Matches(destination, path, expectedClass, key, expectedValue);
			finalMatches = finalMatches && container.IsVariableSetDirectly(key) == wasLocal;
			finalMatches = finalMatches && entity.GetName() == oldName && live.GetName() == oldName;
		}
		if (!finalMatches)
		{
			result.m_sState = "finalization_failed";
			result.m_sReason = "Final source state did not verify; inspect before retrying";
		}
		return result;
	}
}
//! Explicit write request; discovery never invokes this handler.
class RST_EntityReferenceCommitRequest : JsonApiStruct
{
	string operation;
	string adapterBuild;
	string token;
	string world;
	string provider;
	int destinationHi;
	int destinationLo;
	int entityHi;
	int entityLo;
	string entityName;
	string expectedClass;
	string key;
	string expectedValue;
	int arrayIndex = -2;
	ref array<string> expectedArray = {};
	ref array<string> pathKeys = {};
	ref array<int> pathIndices = {};
	ref array<string> pathClasses = {};
	void RST_EntityReferenceCommitRequest()
	{
		RegV("operation");
		RegV("adapterBuild");
		RegV("token");
		RegV("world");
		RegV("provider");
		RegV("destinationHi");
		RegV("destinationLo");
		RegV("entityHi");
		RegV("entityLo");
		RegV("entityName");
		RegV("expectedClass");
		RegV("key");
		RegV("expectedValue");
		RegV("arrayIndex");
		RegV("expectedArray");
		RegV("pathKeys");
		RegV("pathIndices");
		RegV("pathClasses");
	}
}

class RST_EntityReferenceCommitResponse : JsonApiStruct
{
	string adapterBuild = "__RST_ADAPTER_BUILD__";
	string token;
	string state = "refused";
	string name;
	string reason;
	void RST_EntityReferenceCommitResponse()
	{
		RegV("adapterBuild");
		RegV("token");
		RegV("state");
		RegV("name");
		RegV("reason");
	}
}

//! Process-local prepared edit. No caller-owned request or array is retained.
class RST_PreparedEntityReference
{
	string m_sToken;
	string m_sProvider;
	string m_sWorldPath;
	string m_sEntityName;
	string m_sExpectedClass;
	string m_sKey;
	string m_sExpectedValue;
	string m_sProposedName;
	int m_iArrayIndex = -1;
	ref array<string> m_aExpectedArray = {};
	BaseWorld m_World;
	IEntitySource m_Entity;
	IEntitySource m_Destination;
	EntityID m_EntityId;
	EntityID m_DestinationId;
	ref array<ref ContainerIdPathEntry> m_aPath = {};
	ref array<BaseContainer> m_aContainers = {};
}

//! Prepare is read-only. Commit consumes the prepared edit exactly once.
//! Native callers must recapture their destination after prepare and refuse a
//! changed property binding before sending commit; no UI is retained here.
class RST_EntityReferenceCommit : NetApiHandler
{
	// A response loss is not permission to replay a write. Keep recent receipts
	// across handler instances; clients must inspect an uncertain result.
	protected static ref array<string> s_aReceipts = {};
	protected static ref RST_PreparedEntityReference s_Prepared;

	//------------------------------------------------------------------------------------------------
	protected bool IsEligible(WorldEditorAPI api, string providerName, IEntitySource entity)
	{
		typename providerType = providerName.ToType();
		if (!providerType)
			return false;
		ref Managed provider = providerType.Spawn();
		array<IEntitySource> choices;
		return provider && GetGame().GetScriptModule().Call(provider, "GetChoiceEntities", false, choices, api) && choices && choices.Contains(entity);
	}

	//------------------------------------------------------------------------------------------------
	protected RST_EntityReferenceCommitResponse Commit(WorldEditorAPI api, string token, RST_EntityReferenceCommitResponse response)
	{
		response.reason = "Prepared edit is absent, superseded or already consumed";
		if (!s_Prepared || s_Prepared.m_sToken != token)
			return response;
		ref RST_PreparedEntityReference prepared = s_Prepared;
		s_Prepared = null;
		// Array preparations cannot enter the scalar setter. Typed array commit
		// is enabled only once its native-owner readiness contract is connected.
		response.reason = "Prepared edit requires the typed array command owner";
		if (prepared.m_iArrayIndex >= 0)
			return response;
		response.reason = "Prepared world or source changed";
		string world;
		api.GetWorldPath(world);
		if (api.GetWorld() != prepared.m_World || world != prepared.m_sWorldPath)
			return response;
		IEntitySource destination = api.FindEntityByID(prepared.m_DestinationId);
		IEntitySource entity = api.FindEntityByID(prepared.m_EntityId);
		if (!destination || !entity || destination != prepared.m_Destination || entity != prepared.m_Entity || entity.GetName() != prepared.m_sEntityName)
			return response;
		response.reason = "Entity is no longer an eligible choice";
		if (!IsEligible(api, prepared.m_sProvider, entity))
			return response;
		response.reason = "Prepared destination object changed";
		array<ref ContainerIdPathEntry> prefix = {};
		for (int i; i < prepared.m_aPath.Count(); i++)
		{
			prefix.Insert(prepared.m_aPath[i]);
			BaseContainer current = RST_EntityReferenceEdit.Resolve(destination, prefix);
			if (!current || current != prepared.m_aContainers[i])
				return response;
		}
		RST_EntityReferenceEditResult result = RST_EntityReferenceEdit.CommitScalar(api, entity, destination,
			prepared.m_aPath, prepared.m_sExpectedClass, prepared.m_sKey, prepared.m_sExpectedValue);
		response.state = result.m_sState;
		response.name = result.m_sName;
		response.reason = result.m_sReason;
		return response;
	}

	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetRequest()
	{
		return new RST_EntityReferenceCommitRequest();
	}

	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetResponse(JsonApiStruct request)
	{
		RST_EntityReferenceCommitRequest input = RST_EntityReferenceCommitRequest.Cast(request);
		RST_EntityReferenceCommitResponse response = new RST_EntityReferenceCommitResponse();
		if (!input)
			return response;
		response.token = input.token;
		response.reason = "Invalid or incompatible reference edit request";
		if ((input.operation != "prepare_scalar" && input.operation != "prepare_array" && input.operation != "commit_scalar") || input.adapterBuild != response.adapterBuild)
			return response;
		if (input.token.IsEmpty() || input.token.Length() > 64)
			return response;

		response.reason = "Editor world unavailable or changed";
		WorldEditor editor = Workbench.GetModule(WorldEditor);
		if (!editor || !GetGame() || GetGame().InPlayMode())
			return response;
		WorldEditorAPI api = editor.GetApi();
		if (!api || !api.GetWorld() || !api.IsEditMode() || api.IsGameMode() || api.IsDoingEditAction())
			return response;
		if (input.operation == "commit_scalar")
			return Commit(api, input.token, response);

		bool isArray = input.operation == "prepare_array";
		response.reason = "Invalid reference preparation";
		if ((!isArray && input.arrayIndex != -1) || input.key.IsEmpty() || input.expectedClass.IsEmpty() || !input.pathKeys || !input.pathIndices || !input.pathClasses)
			return response;
		if (isArray && (!input.expectedArray || input.expectedArray.Count() > 4096 || input.arrayIndex < 0 || input.arrayIndex >= input.expectedArray.Count()))
			return response;
		if (isArray && input.expectedArray[input.arrayIndex] != input.expectedValue)
			return response;
		int count = input.pathKeys.Count();
		if (count > 64 || input.pathIndices.Count() != count || input.pathClasses.Count() != count)
			return response;
		response.reason = "Receipt already used; inspect the prior edit before retrying";
		if (s_aReceipts.Contains(input.token))
			return response;
		string world;
		api.GetWorldPath(world);
		if (world.IsEmpty() || world != input.world)
			return response;

		IEntitySource destination = api.FindEntityByID(EntityID.FromInt(input.destinationHi, input.destinationLo));
		IEntitySource entity = api.FindEntityByID(EntityID.FromInt(input.entityHi, input.entityLo));
		response.reason = "Source identity or entity name changed";
		if (!destination || !entity || entity.GetName() != input.entityName)
			return response;
		response.reason = "Entity is no longer an eligible choice";
		if (!IsEligible(api, input.provider, entity))
			return response;

		response.reason = "Destination object path changed";
		ref RST_PreparedEntityReference prepared = new RST_PreparedEntityReference();
		BaseContainer container = destination;
		for (int i; i < count; i++)
		{
			if (input.pathKeys[i].IsEmpty() || input.pathIndices[i] < -1)
				return response;
			prepared.m_aPath.Insert(new ContainerIdPathEntry(input.pathKeys[i], input.pathIndices[i]));
			container = RST_EntityReferenceEdit.Resolve(destination, prepared.m_aPath);
			if (!container || container.GetClassName() != input.pathClasses[i])
				return response;
			prepared.m_aContainers.Insert(container);
		}
		string observed;
		if (isArray)
		{
			response.reason = "Array contents or local presence changed";
			if (!RST_EntityReferenceEdit.MatchesArray(container, input.expectedClass, input.key, input.expectedArray) || !container.IsVariableSetDirectly(input.key))
				return response;
			response.reason = "Destination layer is not editable";
			if (destination.GetSubScene() != api.GetCurrentSubScene() || api.IsEntityLayerLockedHierarchy(destination.GetSubScene(), destination.GetLayerID()))
				return response;
			prepared.m_sProposedName = RST_EntityReferenceEdit.ProposeName(api, entity, response.reason);
			if (prepared.m_sProposedName.IsEmpty())
				return response;
			prepared.m_iArrayIndex = input.arrayIndex;
			foreach (string value : input.expectedArray)
				prepared.m_aExpectedArray.Insert(value);
		}
		else if (!container || container.GetClassName() != input.expectedClass || !container.Get(input.key, observed) || observed != input.expectedValue)
			return response;

		s_aReceipts.Insert(input.token);
		if (s_aReceipts.Count() > 128)
			s_aReceipts.Remove(0);
		prepared.m_sToken = input.token;
		prepared.m_sProvider = input.provider;
		prepared.m_sWorldPath = world;
		prepared.m_sEntityName = input.entityName;
		prepared.m_sExpectedClass = input.expectedClass;
		prepared.m_sKey = input.key;
		prepared.m_sExpectedValue = input.expectedValue;
		prepared.m_World = api.GetWorld();
		prepared.m_Entity = entity;
		prepared.m_Destination = destination;
		prepared.m_EntityId = entity.GetID();
		prepared.m_DestinationId = destination.GetID();
		s_Prepared = prepared;
		response.state = "prepared";
		response.name = prepared.m_sProposedName;
		response.reason = "";
		return response;
	}
}
//! Direct string-array assignment. No serialized array text or custom undo owner.
class RST_StringArrayEditRequest : JsonApiStruct
{
	string adapterBuild;
	string token;
	string world;
	int destinationHi;
	int destinationLo;
	string expectedClass;
	string key;
	int arrayIndex = -1;
	string value;
	ref array<string> expectedArray = {};
	ref array<string> pathKeys = {};
	ref array<int> pathIndices = {};
	ref array<string> pathClasses = {};
	void RST_StringArrayEditRequest()
	{
		RegV("adapterBuild");
		RegV("token");
		RegV("world");
		RegV("destinationHi");
		RegV("destinationLo");
		RegV("expectedClass");
		RegV("key");
		RegV("arrayIndex");
		RegV("value");
		RegV("expectedArray");
		RegV("pathKeys");
		RegV("pathIndices");
		RegV("pathClasses");
	}
}

class RST_StringArrayEdit : NetApiHandler
{
	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetRequest()
	{
		return new RST_StringArrayEditRequest();
	}

	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetResponse(JsonApiStruct request)
	{
		RST_StringArrayEditRequest input = RST_StringArrayEditRequest.Cast(request);
		RST_EntityReferenceCommitResponse response = new RST_EntityReferenceCommitResponse();
		response.reason = "Invalid string-array request";
		if (!input)
			return response;
		response.token = input.token;
		if (input.adapterBuild != response.adapterBuild || input.token.IsEmpty() || input.token.Length() > 64 || input.key.IsEmpty())
			return response;
		if (!input.expectedArray || input.expectedArray.Count() > 4096 || input.arrayIndex < 0 || input.arrayIndex >= input.expectedArray.Count())
			return response;
		if (!input.pathKeys || !input.pathIndices || !input.pathClasses || input.pathKeys.Count() > 64 || input.pathKeys.Count() != input.pathIndices.Count() || input.pathKeys.Count() != input.pathClasses.Count())
			return response;
		WorldEditor editor = Workbench.GetModule(WorldEditor);
		response.reason = "Editor world unavailable";
		if (!editor || !GetGame() || GetGame().InPlayMode())
			return response;
		WorldEditorAPI api = editor.GetApi();
		if (!api || !api.GetWorld() || !api.IsEditMode() || api.IsGameMode() || api.IsDoingEditAction())
			return response;
		string world;
		api.GetWorldPath(world);
		if (world.IsEmpty() || world != input.world)
			return response;
		IEntitySource destination = api.FindEntityByID(EntityID.FromInt(input.destinationHi, input.destinationLo));
		response.reason = "Destination unavailable or not editable";
		if (!destination || destination.GetSubScene() != api.GetCurrentSubScene() || api.IsEntityLayerLockedHierarchy(destination.GetSubScene(), destination.GetLayerID()))
			return response;
		array<ref ContainerIdPathEntry> path = {};
		BaseContainer container = destination;
		for (int i; i < input.pathKeys.Count(); i++)
		{
			if (input.pathKeys[i].IsEmpty() || input.pathIndices[i] < -1)
				return response;
			path.Insert(new ContainerIdPathEntry(input.pathKeys[i], input.pathIndices[i]));
			container = RST_EntityReferenceEdit.Resolve(destination, path);
			if (!container || container.GetClassName() != input.pathClasses[i])
				return response;
		}
		response.reason = "Array changed before assignment";
		if (!RST_EntityReferenceEdit.MatchesArray(container, input.expectedClass, input.key, input.expectedArray))
			return response;
		response.name = input.value;
		if (input.expectedArray[input.arrayIndex] == input.value)
		{
			response.state = "unchanged";
			response.reason = "";
			return response;
		}
		array<string> values = {};
		foreach (string value : input.expectedArray)
			values.Insert(value);
		values[input.arrayIndex] = input.value;
		response.reason = "Cannot begin string edit";
		if (!api.BeginEntityAction("Set reference string"))
			return response;
		if (!api.BeginEditSequence(destination))
		{
			api.EndEntityAction();
			return response;
		}
		// Register the field change with the editor so its document is dirty.
		// Defer entity reconstruction until the complete typed value is assigned;
		// the temporary empty value must never be consumed by a rebuilt entity.
		IEntitySource editSource = destination;
		bool registered = api.SetVariableValue(destination, path, input.key, "");
		destination = api.FindEntityByID(EntityID.FromInt(input.destinationHi, input.destinationLo));
		// The owning editor setter creates instance overrides. Never directly
		// assign into an ancestor if any part of that override is still shared.
		container = destination;
		array<ref ContainerIdPathEntry> localPath = {};
		foreach (ContainerIdPathEntry entry : path)
		{
			if (!container || !container.IsVariableSetDirectly(entry.PropertyName))
			{
				container = null;
				break;
			}
			localPath.Insert(entry);
			container = RST_EntityReferenceEdit.Resolve(destination, localPath);
		}
		bool writable = container && container.GetClassName() == input.expectedClass && container.IsVariableSetDirectly(input.key);
		if (writable && registered)
			container.Set(input.key, values);
		bool written = registered && RST_EntityReferenceEdit.MatchesArray(container, input.expectedClass, input.key, values);
		if (!written && writable)
			container.Set(input.key, input.expectedArray);
		bool rebuilt = api.EndEditSequence(editSource);
		bool ended = api.EndEntityAction();
		api.UpdateSelectionGui();
		container = RST_EntityReferenceEdit.Resolve(api.FindEntityByID(EntityID.FromInt(input.destinationHi, input.destinationLo)), path);
		response.state = "finalization_failed";
		response.reason = "String assignment did not verify; inspect before retrying";
		if (written && rebuilt && ended && RST_EntityReferenceEdit.MatchesArray(container, input.expectedClass, input.key, values))
		{
			response.state = "committed";
			response.reason = "";
		}
		return response;
	}
}
#endif
