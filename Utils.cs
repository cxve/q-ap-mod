namespace cxve.qap;

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
