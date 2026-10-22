#ifdef WORKBENCH
// Development-only VM access. Installed into the Workbench profile by runtime setup.
// JsonApiStruct field names are protocol contracts and intentionally use wire names.

class RST_RuntimeRequest : JsonApiStruct
{
	int protocol;
	string action;
	string token;
	string entityName;
	string componentType;
	string method;
	string callCode;
	int offset;
	int limit;

	//------------------------------------------------------------------------------------------------
	void RST_RuntimeRequest()
	{
		RegV("protocol");
		RegV("action");
		RegV("token");
		RegV("entityName");
		RegV("componentType");
		RegV("method");
		RegV("offset");
		RegV("limit");
		RegV("callCode");
	}
}

class RST_RuntimeField : JsonApiStruct
{
	int index;
	string name;
	string type;
	string value;
	string state;

	//------------------------------------------------------------------------------------------------
	void RST_RuntimeField()
	{
		RegV("index");
		RegV("name");
		RegV("type");
		RegV("value");
		RegV("state");
	}
}

class RST_RuntimeResponse : JsonApiStruct
{
	int protocol = 2;
	string adapterBuild = "__RST_ADAPTER_BUILD__";
	string profileDirectory;
	string state;
	string token;
	string world;
	string entityName;
	string componentType;
	string method;
	string error;
	int errorLine;
	string resultJson;
	string entityId;
	string entityType;
	vector position;
	vector angles;
	int total;
	int nextOffset = -1;
	ref array<ref RST_RuntimeField> items = {};

	//------------------------------------------------------------------------------------------------
	void RST_RuntimeResponse()
	{
		RegV("protocol");
		RegV("state");
		RegV("token");
		RegV("world");
		RegV("entityName");
		RegV("adapterBuild");
		RegV("profileDirectory");
		RegV("componentType");
		RegV("method");
		RegV("error");
		RegV("errorLine");
		RegV("entityId");
		RegV("entityType");
		RegV("position");
		RegV("angles");
		RegV("total");
		RegV("nextOffset");
		RegV("items");
		RegV("resultJson");
	}
}

class RST_Runtime : NetApiHandler
{
	// One prepared call, consumed before entering user code. Engine object references
	// are weak: destroyed entities/components must invalidate the prepared call.
	protected static IEntity s_Entity;
	protected static ScriptComponent s_Component;
	protected static BaseWorld s_World;
	protected static string s_sMethod;
	protected static string s_sToken;
	protected static int s_iPreparedAt;
	protected static ref ScriptModule s_CallModule;

	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetRequest()
	{
		return new RST_RuntimeRequest();
	}

	//------------------------------------------------------------------------------------------------
	override JsonApiStruct GetResponse(JsonApiStruct request)
	{
		RST_RuntimeRequest runtimeRequest = RST_RuntimeRequest.Cast(request);
		RST_RuntimeResponse response = new RST_RuntimeResponse();
		Workbench.GetAbsolutePath("$profile:", response.profileDirectory);
		response.token = runtimeRequest.token;

		if (runtimeRequest.protocol != 2)
		{
			response.state = "incompatible_protocol";
			return response;
		}

		response.world = GetGame().GetWorldFile();
		if (!GetGame().InPlayMode())
		{
			Clear();
			response.state = "not_playing";
			return response;
		}

		if (runtimeRequest.action == "status")
		{
			response.state = "playing";
			return response;
		}

		if (runtimeRequest.action == "inspect")
		{
			Inspect(runtimeRequest, response);
			return response;
		}

		if (runtimeRequest.action == "prepare")
		{
			Prepare(runtimeRequest, response);
			return response;
		}

		if (runtimeRequest.action == "invoke")
		{
			Invoke(runtimeRequest, response);
			return response;
		}

		response.state = "invalid_request";
		return response;
	}

	//------------------------------------------------------------------------------------------------
	//! Checks the identifier syntax accepted by the generated call wrapper.
	static bool Identifier(string value)
	{
		if (value.Length() < 1 || value.Length() > 96)
			return false;

		string firstCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
		string remainingCharacters = firstCharacters + "0123456789";
		if (!firstCharacters.Contains(value.Substring(0, 1)))
			return false;

		for (int i = 1; i < value.Length(); i++)
		{
			if (!remainingCharacters.Contains(value.Substring(i, 1)))
				return false;
		}

		return true;
	}

	//------------------------------------------------------------------------------------------------
	//! Releases the prepared call without retaining engine objects.
	static void Clear()
	{
		s_Entity = null;
		s_Component = null;
		s_World = null;
		s_sMethod = "";
		s_sToken = "";
		s_CallModule = null;
	}

	//------------------------------------------------------------------------------------------------
	static void Inspect(RST_RuntimeRequest runtimeRequest, RST_RuntimeResponse response)
	{
		if (runtimeRequest.entityName.IsEmpty() || runtimeRequest.entityName.Length() > 240
			|| runtimeRequest.offset < 0 || runtimeRequest.offset > 2147483583
			|| runtimeRequest.limit < 1 || runtimeRequest.limit > 64)
		{
			response.state = "invalid_request";
			return;
		}

		IEntity entity = GetGame().GetWorld().FindEntityByName(runtimeRequest.entityName);
		if (!entity)
		{
			response.state = "entity_missing";
			return;
		}

		response.entityName = entity.GetName();
		response.entityId = entity.GetID().ToString();
		response.entityType = entity.Type().ToString();
		response.position = entity.GetOrigin();
		response.angles = entity.GetAngles();

		typename componentType = ScriptComponent;
		if (!runtimeRequest.componentType.IsEmpty())
		{
			if (!Identifier(runtimeRequest.componentType))
			{
				response.state = "invalid_request";
				return;
			}

			componentType = runtimeRequest.componentType.ToType();
			if (!componentType || !componentType.IsInherited(ScriptComponent))
			{
				response.state = "unsupported_component";
				return;
			}
		}

		array<Managed> components = {};
		entity.FindComponents(componentType, components);

		bool inspectFields = !runtimeRequest.componentType.IsEmpty();
		if (inspectFields && components.Count() != 1)
		{
			response.state = "component_missing_or_ambiguous";
			return;
		}

		ScriptComponent target;
		typename targetType;
		response.total = components.Count();
		if (inspectFields)
		{
			target = ScriptComponent.Cast(components[0]);
			targetType = target.Type();
			response.componentType = targetType.ToString();
			response.total = targetType.GetVariableCount();
		}

		int endOffset = Math.Min(runtimeRequest.offset + runtimeRequest.limit, response.total);
		for (int i = runtimeRequest.offset; i < endOffset; i++)
		{
			RST_RuntimeField item = new RST_RuntimeField();
			item.index = i;
			response.items.Insert(item);

			if (!inspectFields)
			{
				item.type = components[i].Type().ToString();
				item.state = "observed";
				continue;
			}

			ReadField(target, targetType, i, item);
		}

		if (endOffset < response.total)
			response.nextOffset = endOffset;

		response.state = "observed";
	}

	//------------------------------------------------------------------------------------------------
	//! Reads primitive fields only; unsupported values remain explicit in the response.
	protected static void ReadField(ScriptComponent target, typename targetType, int fieldIndex, RST_RuntimeField item)
	{
		item.name = targetType.GetVariableName(fieldIndex);
		typename fieldType = targetType.GetVariableType(fieldIndex);
		item.type = fieldType.ToString();

		bool wasRead;
		if (fieldType == int)
		{
			int intValue;
			wasRead = targetType.GetVariableValue(target, fieldIndex, intValue);
			item.value = intValue.ToString();
		}
		else if (fieldType == float)
		{
			float floatValue;
			wasRead = targetType.GetVariableValue(target, fieldIndex, floatValue);
			item.value = floatValue.ToString();
		}
		else if (fieldType == bool)
		{
			bool boolValue;
			wasRead = targetType.GetVariableValue(target, fieldIndex, boolValue);
			item.value = boolValue.ToString();
		}
		else if (fieldType == string)
		{
			string stringValue;
			wasRead = targetType.GetVariableValue(target, fieldIndex, stringValue);
			item.value = stringValue;
		}
		else if (fieldType == vector)
		{
			vector vectorValue;
			wasRead = targetType.GetVariableValue(target, fieldIndex, vectorValue);
			item.value = vectorValue.ToString();
		}
		else
		{
			item.state = "unsupported_type";
			return;
		}

		item.state = "read_failed";
		if (wasRead)
			item.state = "observed";

		if (item.value.Length() > 512)
		{
			item.value = item.value.Substring(0, 512);
			item.state = "truncated";
		}
	}

	//------------------------------------------------------------------------------------------------
	protected static void Prepare(RST_RuntimeRequest runtimeRequest, RST_RuntimeResponse response)
	{
		Clear();
		if (runtimeRequest.entityName.IsEmpty() || runtimeRequest.entityName.Length() > 240
			|| !Identifier(runtimeRequest.componentType)
			|| !Identifier(runtimeRequest.method) || !Identifier(runtimeRequest.token))
		{
			response.state = "invalid_request";
			return;
		}

		IEntity entity = GetGame().GetWorld().FindEntityByName(runtimeRequest.entityName);
		if (!entity)
		{
			response.state = "entity_missing";
			return;
		}

		typename componentType = runtimeRequest.componentType.ToType();
		if (!componentType || !componentType.IsInherited(ScriptComponent))
		{
			response.state = "unsupported_component";
			return;
		}

		array<Managed> components = {};
		entity.FindComponents(componentType, components);

		if (components.Count() != 1)
		{
			response.state = "component_missing_or_ambiguous";
			return;
		}

		ScriptComponent component = ScriptComponent.Cast(components[0]);
		string dispatchType = component.Type().ToString();
		if (!Identifier(dispatchType))
		{
			response.state = "unsupported_component";
			return;
		}

		// C# supplies checked primitive literals and one wrapper around the owning
		// game module's named call. Compilation checks public access and signature.
		if (runtimeRequest.callCode.IsEmpty() || runtimeRequest.callCode.Length() > 65536)
		{
			response.state = "invalid_request";
			return;
		}

		string code = runtimeRequest.callCode;
		code.Replace("\nvoid RST_AccessCheck(__TARGET__ c)\n",
			"\nvoid RST_AccessCheck(" + dispatchType + " c)\n");
		ScriptModule module = ScriptModule.CompileScript(GetGame().GetScriptModule(), code,
			response.error, response.errorLine);
		if (!module)
		{
			response.state = "compile_rejected";
			return;
		}

		s_CallModule = module;
		s_Entity = entity;
		s_Component = component;
		s_World = GetGame().GetWorld();
		s_sMethod = runtimeRequest.method;
		s_sToken = runtimeRequest.token;
		s_iPreparedAt = System.GetTickCount();

		response.entityName = entity.GetName();
		response.componentType = component.Type().ToString();
		response.method = runtimeRequest.method;
		response.state = "prepared";
	}

	//------------------------------------------------------------------------------------------------
	protected static void Invoke(RST_RuntimeRequest runtimeRequest, RST_RuntimeResponse response)
	{
		if (s_sToken == "" || runtimeRequest.token != s_sToken)
		{
			response.state = "stale_target";
			return;
		}

		if (System.GetTickCount(s_iPreparedAt) > 60000
			|| !s_Entity || !s_Component || s_World != GetGame().GetWorld()
			|| s_Entity.GetWorld() != s_World
			|| s_World.FindEntityByID(s_Entity.GetID()) != s_Entity
			|| s_Component.GetOwner() != s_Entity)
		{
			Clear();
			response.state = "stale_target";
			return;
		}

		ScriptComponent target = s_Component;
		ScriptModule callModule = s_CallModule;
		string invokeMethod = s_sMethod;
		response.entityName = s_Entity.GetName();
		response.componentType = target.Type().ToString();
		response.method = invokeMethod;

		// Consume before entering user code so reentrant calls cannot reuse this token.
		Clear();

		Print("RST_MCP_BEGIN " + runtimeRequest.token);
		bool called;
		if (callModule)
			called = callModule.Call(null, "RST_Invoke", false, response.resultJson, target);

		response.state = "call_failed";
		if (called)
			response.state = "invoked";

		Print("RST_MCP_END " + runtimeRequest.token + " " + response.state);
	}
}
#endif
