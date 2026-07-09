using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace cxve.qap.Patches;

// mainly used to remove code that is causing issues from the game
[HarmonyPatch]
internal class TranspilerPatches
{
    [HarmonyPatch(typeof(SkillMap), nameof(SkillMap.InitializeFromProgressData))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SkipHacker(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        codes.RemoveRange(2, 70);

        return codes.AsEnumerable();
    }



    [HarmonyPatch(typeof(SkillManager), "OnDeleteUpgrade")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SkipMapInitOnDelete(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var index = codes.FindIndex(x => x.LoadsField(AccessTools.Field(typeof(SkillNode), nameof(SkillNode.GUID))));

        codes.RemoveRange(index + 2, 6);

        return codes.AsEnumerable();
    }

    [HarmonyPatch(typeof(SkillManager), nameof(SkillManager.RespecCharacter))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SkipMapInitInRespec(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var index = codes.FindIndex(x => x.Calls(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SetDirty))));

        codes.RemoveRange(index + 1, 5);

        return codes.AsEnumerable();
    }
}
