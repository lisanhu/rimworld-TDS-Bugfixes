using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace TDS_Bug_Fixes
{


	// WidgetRow.Label and TextFieldNumeric apply a gap before and after the rect it uses
	// But instead of Gap() it directly calls IncrementPosition, without checking if startX==curX
	// So labels at the beginning of a widgetRow on somr rect would have a gap applied
	//  (whereas a similar Widgets.Label in the same rect does not)
	// Which is really funny, because caling Gap() first explicitly doesn't create a gap, but label does create a gap

	//  so TL;DR the first IncrementPosition in these methods should be Gap so it doesn't actually gap when curX == 0

	[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.Label))]
	public static class FixWidgetRowLabelGap
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo IncrementPositionInfo = AccessTools.Method(typeof(WidgetRow), nameof(WidgetRow.IncrementPosition));
			MethodInfo GapInfo = AccessTools.Method(typeof(WidgetRow), nameof(WidgetRow.Gap));

			bool first = true;
			foreach (var inst in instructions)
			{
				if (first && inst.Calls(IncrementPositionInfo))
				{
					first = false;
					inst.operand = GapInfo;
				}
				yield return inst;
			}
		}
	}

	/*
	//TextFieldNumeric is generic and crashes rimworld on start
	[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.TextFieldNumeric))]
	public static class FixWidgetRowTextFieldNumericGap
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			=> FixWidgetRowLabelGap.Transpiler(instructions);
	}
	*/
}
