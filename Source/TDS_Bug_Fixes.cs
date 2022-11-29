using System.Reflection;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace TDS_Bug_Fixes
{
	public class Mod : Verse.Mod
	{
		public static Settings settings;
		public Mod(ModContentPack content) : base(content)
		{
			// initialize settings
			settings = GetSettings<Settings>();

#if DEBUG
			Harmony.DEBUG = true;
#endif

			Harmony harmony = new Harmony("Uuugggg.rimworld.TDS_Bug_Fixes.main");	
			harmony.PatchAll();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
			settings.DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "TDS Bug Fixes";
		}
	}
}