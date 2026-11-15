namespace SableLatch.Core;

// Variables are transported as data, never substituted into source code.
public static class ScriptBindings
{
    public static bool IsValueName(string name) => name.StartsWith("value", StringComparison.Ordinal) && int.TryParse(name.AsSpan(5), out var n) && n is >= 1 and <= 20 && name == "value" + n;
    public static Dictionary<string, string> For(VaultItem item)
    {
        var values = new Dictionary<string, string> { ["name"] = item.Label };
        if (item.ContentType is "sable.secret" or "sable.text") values["value1"] = item.Field("text");
        else if (item.ContentType == "sable.login") { values["value1"] = item.Field("username"); values["value2"] = item.Field("password"); }
        foreach (var pair in item.ScriptValues)
        {
            if (!IsValueName(pair.Key) || values.ContainsKey(pair.Key)) throw new InvalidDataException("A script field name is invalid or already used.");
            values.Add(pair.Key, pair.Value);
        }
        return values;
    }
}
