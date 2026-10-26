#ifdef WORKBENCH
// Editor-only provider bridge. Providers live in their owning addon and expose
// array<string> GetChoices(WorldEditorAPI api). No gameplay or authoring writes.
class RST_EditorChoicesRequest : JsonApiStruct
{
	string provider;
	string token;
	void RST_EditorChoicesRequest()
	{
		RegV("provider");
		RegV("token");
	}
}

class RST_EditorChoicesResponse : JsonApiStruct
{
	string adapterBuild = "__RST_ADAPTER_BUILD__";
	string token;
	string state;
	string world;
	ref array<string> values = {};
	void RST_EditorChoicesResponse()
	{
		RegV("adapterBuild");
		RegV("token");
		RegV("state");
		RegV("world");
		RegV("values");
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

		array<string> values;
		if (!GetGame().GetScriptModule().Call(instance, "GetChoices", false, values, api) || !values)
			return response;
		response.values = values;
		response.state = "ready";
		return response;
	}
}
#endif
