#ifdef WORKBENCH
// Editor-only provider bridge. Providers live in their owning addon and expose
// GetChoices(api) for strings or GetChoiceEntities(api) for source identities.
// Neither discovery route performs gameplay or authoring writes.
class RST_EditorChoicesRequest : JsonApiStruct
{
	string provider;
	string token;
	string kind;
	void RST_EditorChoicesRequest()
	{
		RegV("provider");
		RegV("token");
		RegV("kind");
	}
}

class RST_EditorChoicesResponse : JsonApiStruct
{
	string kind;
	string adapterBuild = "__RST_ADAPTER_BUILD__";
	string token;
	string state;
	string world;
	ref array<string> values = {};
	// Parallel arrays preserve identity independently of display labels and names.
	ref array<string> entityIds = {};
	ref array<string> entityNames = {};
	ref array<string> entityLabels = {};
	void RST_EditorChoicesResponse()
	{
		RegV("kind");
		RegV("adapterBuild");
		RegV("token");
		RegV("state");
		RegV("world");
		RegV("values");
		RegV("entityIds");
		RegV("entityNames");
		RegV("entityLabels");
	}
}

class RST_EditorChoices : NetApiHandler
{
	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetRequest()
	{
		return new RST_EditorChoicesRequest();
	}

	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetResponse(JsonApiStruct request)
	{
		RST_EditorChoicesRequest input = RST_EditorChoicesRequest.Cast(request);
		RST_EditorChoicesResponse response = new RST_EditorChoicesResponse();
		response.token = input.token;
		response.state = "world_unavailable";
		WorldEditor editor = Workbench.GetModule(WorldEditor);
		if (!editor || !GetGame() || GetGame().InPlayMode())
			return response;

		WorldEditorAPI api = editor.GetApi();
		if (!api || !api.GetWorld())
			return response;
		api.GetWorldPath(response.world);
		response.state = "provider_unavailable";
		typename providerType = input.provider.ToType();
		if (!providerType)
			return response;
		ref Managed instance = providerType.Spawn();
		if (!instance)
			return response;

		if (input.kind == "entities")
		{
			response.kind = "entities";
			array<IEntitySource> sources;
			if (!GetGame().GetScriptModule().Call(instance, "GetChoiceEntities", false, sources, api) || !sources)
				return response;
			foreach (IEntitySource source : sources)
			{
				if (!source || api.FindEntityByID(source.GetID()) != source)
					continue;
				IEntity entity = api.SourceToEntity(source);
				if (!entity)
					continue;

				string name = source.GetName();
				// Never advertise a nonempty name unless both owners resolve it.
				if (!name.IsEmpty() && (entity.GetName() != name || api.FindEntityByName(name) != source || api.GetWorld().FindEntityByName(name) != entity))
					continue;
				response.entityIds.Insert(source.GetID().ToString());
				response.entityNames.Insert(name);
				response.entityLabels.Insert(api.GetEntityNiceName(source));
			}
			response.state = "ready";
			return response;
		}
		if (!input.kind.IsEmpty())
			return response;

		array<string> values;
		if (!GetGame().GetScriptModule().Call(instance, "GetChoices", false, values, api) || !values)
			return response;
		response.values = values;
		response.state = "ready";
		return response;
	}
}
#endif
