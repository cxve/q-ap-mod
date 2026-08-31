using System.Reflection;
using System.Text.RegularExpressions;

namespace cxve.qap;

internal static class Util {
    internal static string BuildAPIcon(int x = 0, int y = 0) => 
        $"<color=#a0c4ff><voffset={y-4}><pos={x-7}>•</color>" +
        $"<color=#fdffb6><voffset={y+4}><pos={x-7}>•</color>" +
        $"<color=#ffadad><voffset={y+8}><pos={x}>•</color>" + 
        $"<color=#caffbf><voffset={y+4}><pos={x+7}>•</color>" +
        $"<color=#bdb2ff><voffset={y-4}><pos={x+7}>•</color>" +
        $"<color=#ffd6a5><voffset={y-8}><pos={x}>•</color>";

    internal static string BuildAPIconSmall(int x = 0, int y = 0) => 
        $"<color=#a0c4ff><voffset={y-3}><pos={x-6}>•</color>" +
        $"<color=#fdffb6><voffset={y+3}><pos={x-6}>•</color>" +
        $"<color=#ffadad><voffset={y+7}><pos={x}>•</color>" + 
        $"<color=#caffbf><voffset={y+3}><pos={x+6}>•</color>" +
        $"<color=#bdb2ff><voffset={y-3}><pos={x+6}>•</color>" +
        $"<color=#ffd6a5><voffset={y-7}><pos={x}>•</color>";
}

internal static class SkillNodeExtensions
{
    // the game has a similar feature, but the original char is incorrect
    public static SaveManager.SerializableSkillNode Serialize(this SkillNode me) => new()
    {
        autoBuyLevel = 0,
        gridPosition = me.gridPosition,
        guid = me.GUID,
        isInventory = me.isInventory,
        level = me.level,
        name = me.name,
        originalChar = me.map.character
    };
}

/// source: https://stackoverflow.com/questions/95910/find-a-private-field-with-reflection/46488844#46488844
internal static class ReflectionExtensions {
    public static T GetFieldValue<T>(this object obj, string name) {
        var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        var field = obj.GetType().GetField(name, bindingFlags);
        return (T)field?.GetValue(obj);
    }
}

internal static class StringExtensions {
    public static readonly Regex regexUserInput = new("[<>\\[\\]]");
    public static string Sanitize(this string input) => regexUserInput.Replace(input, "");
}