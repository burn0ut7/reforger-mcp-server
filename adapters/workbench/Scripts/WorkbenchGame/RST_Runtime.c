#ifdef WORKBENCH
// Development-only VM access. Installed into the Workbench profile by runtime setup.
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
 void RST_RuntimeRequest()
 {
  RegV("protocol"); RegV("action"); RegV("token"); RegV("entityName"); RegV("componentType"); RegV("method");
  RegV("offset"); RegV("limit");
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
 void RST_RuntimeField()
 {
  RegV("index"); RegV("name"); RegV("type"); RegV("value"); RegV("state");
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
 void RST_RuntimeResponse()
 {
  RegV("protocol"); RegV("state"); RegV("token"); RegV("world"); RegV("entityName");
		RegV("adapterBuild");
		RegV("profileDirectory");
  RegV("componentType"); RegV("method"); RegV("error"); RegV("errorLine");
  RegV("entityId"); RegV("entityType"); RegV("position"); RegV("angles"); RegV("total"); RegV("nextOffset"); RegV("items");
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
 protected static string s_Method;
 protected static string s_Token;
 protected static int s_PreparedAt;
 protected static ref ScriptModule s_CallModule;

 static bool Identifier(string value)
 {
  if (value.Length() < 1 || value.Length() > 96) return false;
  string first = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
  string rest = first + "0123456789";
  if (!first.Contains(value.Substring(0, 1))) return false;
  for (int i = 1; i < value.Length(); i++)
   if (!rest.Contains(value.Substring(i, 1))) return false;
  return true;
 }
 static void Clear()
 {
  s_Entity = null; s_Component = null; s_World = null; s_Method = ""; s_Token = ""; s_CallModule = null;
 }
 static void Inspect(RST_RuntimeRequest req, RST_RuntimeResponse r)
 {
  if (req.entityName.IsEmpty() || req.entityName.Length() > 240 || req.offset < 0 || req.offset > 2147483583 || req.limit < 1 || req.limit > 64)
  { r.state = "invalid_request"; return; }
  IEntity entity = GetGame().GetWorld().FindEntityByName(req.entityName);
  if (!entity) { r.state = "entity_missing"; return; }
  r.entityName = entity.GetName(); r.entityId = entity.GetID().ToString(); r.entityType = entity.Type().ToString();
  r.position = entity.GetOrigin(); r.angles = entity.GetAngles();
  typename componentType = ScriptComponent;
  if (!req.componentType.IsEmpty())
  {
   if (!Identifier(req.componentType)) { r.state = "invalid_request"; return; }
   componentType = req.componentType.ToType();
   if (!componentType || !componentType.IsInherited(ScriptComponent)) { r.state = "unsupported_component"; return; }
  }
  array<Managed> components = {};
  entity.FindComponents(componentType, components);
  bool fields = !req.componentType.IsEmpty();
  if (fields && components.Count() != 1) { r.state = "component_missing_or_ambiguous"; return; }
  ScriptComponent target;
  typename actualType;
  r.total = components.Count();
  if (fields)
  {
   target = ScriptComponent.Cast(components[0]); actualType = target.Type();
   r.componentType = actualType.ToString(); r.total = actualType.GetVariableCount();
  }
  int end = Math.Min(req.offset + req.limit, r.total);
  for (int i = req.offset; i < end; i++)
  {
   RST_RuntimeField item = new RST_RuntimeField(); item.index = i;
   r.items.Insert(item);
   if (!fields) { item.type = components[i].Type().ToString(); item.state = "observed"; continue; }
   item.name = actualType.GetVariableName(i);
   typename fieldType = actualType.GetVariableType(i); item.type = fieldType.ToString();
   bool read;
   if (fieldType == int) { int iv; read = actualType.GetVariableValue(target, i, iv); item.value = iv.ToString(); }
   else if (fieldType == float) { float fv; read = actualType.GetVariableValue(target, i, fv); item.value = fv.ToString(); }
   else if (fieldType == bool) { bool bv; read = actualType.GetVariableValue(target, i, bv); item.value = bv.ToString(); }
   else if (fieldType == string) { string sv; read = actualType.GetVariableValue(target, i, sv); item.value = sv; }
   else if (fieldType == vector) { vector vv; read = actualType.GetVariableValue(target, i, vv); item.value = vv.ToString(); }
   else { item.state = "unsupported_type"; continue; }
   item.state = "read_failed";
   if (read) item.state = "observed";
   if (item.value.Length() > 512) { item.value = item.value.Substring(0, 512); item.state = "truncated"; }
  }
  if (end < r.total) r.nextOffset = end;
  r.state = "observed";
 }
 override JsonApiStruct GetRequest() { return new RST_RuntimeRequest(); }
 override JsonApiStruct GetResponse(JsonApiStruct request)
 {
  RST_RuntimeRequest req = RST_RuntimeRequest.Cast(request);
  RST_RuntimeResponse r = new RST_RuntimeResponse();
		Workbench.GetAbsolutePath("$profile:", r.profileDirectory);
  r.token = req.token;
  if (req.protocol != 2) { r.state = "incompatible_protocol"; return r; }
  r.world = GetGame().GetWorldFile();
  if (!GetGame().InPlayMode()) { Clear(); r.state = "not_playing"; return r; }
  if (req.action == "status") { r.state = "playing"; return r; }
  if (req.action == "inspect") { Inspect(req, r); return r; }
  if (req.action == "prepare")
  {
   Clear();
   if (req.entityName.IsEmpty() || req.entityName.Length() > 240 || !Identifier(req.componentType) || !Identifier(req.method) || !Identifier(req.token))
   { r.state = "invalid_request"; return r; }
   IEntity entity = GetGame().GetWorld().FindEntityByName(req.entityName);
   if (!entity) { r.state = "entity_missing"; return r; }
   typename componentType = req.componentType.ToType();
   if (!componentType || !componentType.IsInherited(ScriptComponent)) { r.state = "unsupported_component"; return r; }
   array<Managed> components = {};
   entity.FindComponents(componentType, components);
   if (components.Count() != 1) { r.state = "component_missing_or_ambiguous"; return r; }
   ScriptComponent component = ScriptComponent.Cast(components[0]);
   string dispatchType = component.Type().ToString();
   if (!Identifier(dispatchType)) { r.state = "unsupported_component"; return r; }
   // C# supplies checked primitive literals and one wrapper around the owning
   // game module's named call. Compilation checks public access and signature.
   if (req.callCode.IsEmpty() || req.callCode.Length() > 65536) { r.state = "invalid_request"; return r; }
   string code = req.callCode;
   code.Replace("\nvoid RST_AccessCheck(__TARGET__ c)\n", "\nvoid RST_AccessCheck(" + dispatchType + " c)\n");
   ScriptModule module = ScriptModule.CompileScript(GetGame().GetScriptModule(), code, r.error, r.errorLine);
   if (!module) { r.state = "compile_rejected"; return r; }
   s_CallModule = module;
   s_Entity = entity; s_Component = component; s_World = GetGame().GetWorld();
   s_Method = req.method; s_Token = req.token; s_PreparedAt = System.GetTickCount();
   r.entityName = entity.GetName(); r.componentType = component.Type().ToString(); r.method = req.method;
   r.state = "prepared";
   return r;
  }
  if (req.action != "invoke") { r.state = "invalid_request"; return r; }
  if (s_Token == "" || req.token != s_Token) { r.state = "stale_target"; return r; }
  if (System.GetTickCount(s_PreparedAt) > 60000 || !s_Entity || !s_Component || s_World != GetGame().GetWorld()
   || s_Entity.GetWorld() != s_World || s_World.FindEntityByID(s_Entity.GetID()) != s_Entity || s_Component.GetOwner() != s_Entity)
  { Clear(); r.state = "stale_target"; return r; }
  ScriptComponent target = s_Component;
  ScriptModule callModule = s_CallModule;
  string invokeMethod = s_Method;
  r.entityName = s_Entity.GetName(); r.componentType = target.Type().ToString(); r.method = invokeMethod;
  Clear();
  Print("RST_MCP_BEGIN " + req.token);
  bool called;
  if (callModule) called = callModule.Call(null, "RST_Invoke", false, r.resultJson, target);
  r.state = "call_failed";
  if (called) r.state = "invoked";
  Print("RST_MCP_END " + req.token + " " + r.state);
  return r;
 }
}
#endif
