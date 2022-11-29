using System;
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
	// WidgetRow.ButtonText does A Text.CalcSize to find the size of the button ; that height ends up 22.
	// But when given a width override, the height is just flatly set to 24 (The icon height is 24)
	// This makes buttons inconsistently 2 px different. So just set that 24 to a 22 here.
	[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ButtonRect))]
	public static class ButtonrectHeightFixer
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var inst in instructions)
			{
				if (inst.operand is float f && f == 24f)
					inst.operand = 22f;

				yield return inst;
			}
		}
	}
}
