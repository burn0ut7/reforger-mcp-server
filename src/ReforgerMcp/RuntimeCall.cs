using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ReforgerMcp;

// One typed wrapper around the engine's existing named live-object call. User text
// never becomes a code expression: names/types are checked and values are literals.
internal static class RuntimeCall
{
    internal static string Build(string method, JsonElement[]? arguments, string returnType)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(method, "^[A-Za-z_][A-Za-z0-9_]{0,95}$"))
            throw new ArgumentException("method must be an identifier.");
        static bool Primitive(string? type) => type is "int" or "float" or "bool" or "string" or "vector";
        if (returnType != "void" && !Primitive(returnType)) throw new ArgumentException("returnType must be void, int, float, bool, string or vector.");
        arguments ??= [];
        if (arguments.Length > 9) throw new ArgumentException("The engine call supports at most nine arguments.");
        var declarations = new List<string>();
        var parameters = new List<string>();
        var names = new List<string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];
            if (arg.ValueKind != JsonValueKind.Object || !arg.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String ||
                !Primitive(t.GetString()) || !arg.TryGetProperty("value", out var value) || arg.EnumerateObject().Count() != 2)
                throw new ArgumentException("Each argument must contain exactly type and value; supported types: int, float, bool, string, vector.");
            var type = t.GetString()!;
            var name = "a" + i;
            declarations.Add($" {type} {name} = {Literal(type, value)};");
            parameters.Add(type + " " + name);
            names.Add(name);
        }
        var list = string.Join(", ", names);
        var code = new StringBuilder();
        code.AppendLine("typedef func RST_CallSignature;");
        code.AppendLine($"{returnType} RST_CallSignature({string.Join(", ", parameters)});");
        code.AppendLine("void RST_AccessCheck(__TARGET__ c)");
        code.AppendLine("{");
        foreach (var line in declarations) code.AppendLine(line);
        code.AppendLine(" ScriptInvokerBase<RST_CallSignature> inv;");
        code.AppendLine($" inv.Insert(c.{method});");
        code.AppendLine(returnType == "void" ? $" c.{method}({list});" : $" {returnType} checkedResult = c.{method}({list});");
        code.AppendLine("}");
        code.AppendLine("class RST_CallResult : JsonApiStruct");
        code.AppendLine("{");
        code.AppendLine(" bool called;");
        if (returnType != "void") code.AppendLine($" {returnType} value;");
        code.AppendLine(" override void OnPack()");
        code.AppendLine(" {");
        code.AppendLine("  StoreBoolean(\"called\", called);");
        if (returnType != "void")
        {
            var store = returnType switch { "int" => "Integer", "float" => "Float", "bool" => "Boolean", "string" => "String", "vector" => "Vector", _ => throw new InvalidOperationException() };
            code.AppendLine($"  Store{store}(\"value\", value);");
        }
        code.AppendLine(" }");
        code.AppendLine("}");
        code.AppendLine("string RST_Invoke(ScriptComponent target)");
        code.AppendLine("{");
        foreach (var line in declarations) code.AppendLine(line);
        code.AppendLine($" {(returnType == "void" ? "bool" : returnType)} result;");
        code.AppendLine(" RST_CallResult output = new RST_CallResult();");
        code.AppendLine($" output.called = GetGame().GetScriptModule().Call(target, \"{method}\", false, result{(list.Length == 0 ? "" : ", " + list)});");
        if (returnType != "void") code.AppendLine(" output.value = result;");
        code.AppendLine(" output.Pack();");
        code.AppendLine(" return output.AsString();");
        code.AppendLine("}");
        var source = code.ToString().Replace("\r\n", "\n");
        if (Encoding.UTF8.GetByteCount(source) > 65536) throw new ArgumentException("Combined typed arguments exceed the runtime wrapper size limit.");
        return source;
    }

    private static string Literal(string type, JsonElement value)
    {
        static string Number(JsonElement v)
        {
            if (v.ValueKind != JsonValueKind.Number || !v.TryGetSingle(out var n) || !float.IsFinite(n))
                throw new ArgumentException("Expected a finite single-precision number.");
            return n.ToString("R", CultureInfo.InvariantCulture);
        }
        if (type == "int" && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var integer))
            return integer.ToString(CultureInfo.InvariantCulture);
        if (type == "float") return Number(value);
        if (type == "bool" && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean() ? "true" : "false";
        if (type == "vector" && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 3)
            return "Vector(" + string.Join(", ", value.EnumerateArray().Select(Number)) + ")";
        if (type == "string" && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (Encoding.UTF8.GetByteCount(text) > 4096 || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
                throw new ArgumentException("String arguments are limited to 4096 UTF-8 bytes without unsupported control characters.");
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
        }
        throw new ArgumentException("Argument value does not match its declared type: " + type);
    }
}
