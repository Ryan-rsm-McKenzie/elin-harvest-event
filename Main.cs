#pragma warning disable CA1812 // Avoid uninstantiated internal classes

using BepInEx;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection.Emit;
using System.Linq;

namespace ElinHarvestEvent;

[BepInPlugin("52B586DF-33AF-4BE3-AF95-E0A3B1340148", "ElinHarvestEvent", "1.0")]
internal sealed class Mod : BaseUnityPlugin
{
	private static Harmony? s_harmony;
	private static Mod? s_instance;

	internal static void Error(object data)
	{
		s_instance!.Logger.LogError(data);
	}

#pragma warning disable IDE0051 // Remove unused private members
	private void Start()
	{
		s_instance = this;
		s_harmony = new Harmony("ryan.elinharvestevent");
		s_harmony.PatchAll();
	}
#pragma warning restore IDE0051

	internal static string Detour(QuestHarvest harvest)
	{
		int delivered = harvest.weightDelivered;
		int carrying = 0;
		foreach (var member in EClass.pc.party.members) {
			member.things.Foreach(thing => {
				if (thing.GetBool(CINT.isHarvestQuestCrop)) {
					carrying += thing.SelfWeight * thing.Num;
				}
			});
		}
		if (carrying > 0) {
			string potential = Lang._weight(delivered + carrying);
			return $"{Lang._weight(delivered)} ({potential})";
		} else {
			return Lang._weight(delivered);
		}
	}
}

[HarmonyPatch(typeof(ZoneEventHarvest))]
[HarmonyPatch("get_TextWidgetDate")]
internal sealed class ZoneEventHarvest_TextWidgetDate
{
	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var needle = new CodeMatch[] {
			new(OpCodes.Ldarg_0),
			new(OpCodes.Call, AccessTools.PropertyGetter(
				type: typeof(ZoneEventHarvest),
				name: nameof(ZoneEventHarvest.questHarvest)
			)),
			new(OpCodes.Ldfld, AccessTools.Field(
				type: typeof(QuestHarvest),
				name: nameof(QuestHarvest.weightDelivered)
			)),
			new(OpCodes.Ldc_I4_1),
			new(OpCodes.Ldc_I4_0),
			new(OpCodes.Call, AccessTools.Method(
				type: typeof(Lang),
				name: nameof(Lang._weight),
				parameters: [typeof(int), typeof(bool), typeof(int)]
			)),
		};

		var matcher = new CodeMatcher(instructions);
		matcher.Start().MatchStartForward(needle);
		if (matcher.IsInvalid) {
			Mod.Error("Failed to match ZoneEventHarvest patch");
			return instructions;
		}

		CodeInstruction[] replacement = [
			new(OpCodes.Ldarg_0) { labels = matcher.Labels },
			new(OpCodes.Call, AccessTools.Method(
				type: typeof(ZoneEventHarvest_TextWidgetDate),
				name: nameof(Detour)
			))
		];
		var result = matcher
			.RemoveInstructions(needle.Length)
			.Insert(replacement)
			.Instructions();
		return result;
	}

	private static string Detour(ZoneEventHarvest self) => Mod.Detour(self.questHarvest);
}

[HarmonyPatch(typeof(QuestHarvest))]
[HarmonyPatch(nameof(QuestHarvest.GetTextProgress))]
internal sealed class QuestHarvest_GetTextProgress
{
	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var needle = new CodeMatch[] {
			new(OpCodes.Ldarg_0),
			new(OpCodes.Ldfld, AccessTools.Field(
				type: typeof(QuestHarvest),
				name: nameof(QuestHarvest.weightDelivered)
			)),
			new(OpCodes.Ldc_I4_1),
			new(OpCodes.Ldc_I4_0),
			new(OpCodes.Call, AccessTools.Method(
				type: typeof(Lang),
				name: nameof(Lang._weight),
				parameters: [typeof(int), typeof(bool), typeof(int)]
			)),
		};

		var matcher = new CodeMatcher(instructions);
		matcher.Start().MatchStartForward(needle);
		if (matcher.IsInvalid) {
			Mod.Error("Failed to match QuestHarvest patch");
			return instructions;
		}

		var result = matcher
			.RemoveInstructions(needle.Length)
			.Insert([
				new(OpCodes.Ldarg_0),
				new(OpCodes.Call, AccessTools.Method(
					type: typeof(Mod),
					name: nameof(Mod.Detour)
				))
			])
			.Instructions();
		return result;
	}
}
