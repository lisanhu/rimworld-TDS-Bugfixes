using System;
using System.Reflection;
using System.Reflection.Emit;
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
	// It turns out that reorderable widgets, with multi-groups, and dragging onto an empty group, doesn't work in the game UI
	// This is 100% because of the COLONIST BAR at the top center of the screen.
	// For some reason its rect is the entire screen. This rect is only used for multi-group reordering which it doesn't even do.
	// The code would find what group's rect is "hovered" over to drop onto that group.
	// That works fine in the modlist, because there's no colonist bar, so you can drop onto an empty modlist and it'll handle that
	// But when there's a goddamn colonist bar drawing after all your UI that says it takes up the entire screen,
	// it hijacks all that and says I'M ON TOP, I GET THE REORDER DROP
	// But then later it goes "oh wait, I'm not in your group, nevermind" and so nothing happens.
	// All the code needs to do is NOT set the hoveredGroup is that group is not inthe multigroup for the dragged reorderable.

	// SECOND. To support nested reordering areas. It has to do a few more checks about those rects. Which are done in the below patch



	// Simply move hoveredGroup = j into the if block which checks if we care about j, isn't that smart
	// Secondly, add support for overlapping reorder rects.
	// To support nested rects, also check if the new hoveredRect is inside the old one,
	// and the "nearest" reorderable is not within the rect.
	[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.ReorderableWidgetOnGUI_AfterWindowStack))]
	public static class FixReorderableHoveredGroup
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			//1. check if group absRect contains point, and is a better group to use...
			MethodInfo ContainsInfo = AccessTools.Method(typeof(Rect), nameof(Rect.Contains), new Type[] { typeof(Vector2) });

			//2. save hoveredGroup if it passes more checks
			FieldInfo hoveredGroupInfo = AccessTools.Field(typeof(ReorderableWidget), nameof(ReorderableWidget.hoveredGroup));
			FieldInfo lastInsertNearLeftInfo = AccessTools.Field(typeof(ReorderableWidget), nameof(ReorderableWidget.lastInsertNearLeft));

			//3. Find better insert point instead of at the end 
			MethodInfo FindLastReorderableIndexWithinGroupInfo = AccessTools.Method(typeof(ReorderableWidget), nameof(ReorderableWidget.FindLastReorderableIndexWithinGroup));


			List<CodeInstruction> instList = instructions.ToList();
			int hoverGroupIndex = -1;
			for (int i = 0; i < instList.Count; i++)
			{
				// 1. Check if hoveredRect contains new hoveredRect
				if (instList[i].Calls(ContainsInfo))
				{
					for (int j = i + 1; j < instList.Count; j++)
					{
						if (instList[j].StoresField(hoveredGroupInfo))
						{
							yield return instList[j - 1]; //j index of hoveredGroup
							break;
						}
					}
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FixReorderableHoveredGroup), nameof(RectContainsMouseAndInsideOld)));
				}

				// 2.a. Move 'hoveredGroup = j' inside the if
				// If it's not hoveredGroup = -1, it's gonna be hoveredGroup = j (local int)
				else if (i < instList.Count - 1 && instList[i + 1].StoresField(hoveredGroupInfo) && instList[i].opcode != OpCodes.Ldc_I4_M1)
				{
					hoverGroupIndex = i;
					i++;  //skip 'j' and next 'hoveredGroup = '
				}

				//3. find closest reorderable (again, after it was cleared when finding a parent group rect), not just the last index.
				else if (instList[i].Calls(FindLastReorderableIndexWithinGroupInfo))
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FixReorderableHoveredGroup), nameof(FindCurrentInsertNearWithinGroup)));
				}

				else if (instList[i].StoresField(lastInsertNearLeftInfo))
				{
					// 3.b. Don't set lastInsertNearLeft, FindCurrentInsertNearWithinGroup just did
					yield return new CodeInstruction(OpCodes.Pop);

					// 2.b. Do 'hoveredGroup = j' only after 'lastInsertNearLeft = ...'
					yield return instList[hoverGroupIndex]; // load j
					yield return instList[hoverGroupIndex + 1]; // set static hoveredGroup = j
				}
				else
					yield return instList[i];
			}
		}

		public static bool RectContainsMouseAndInsideOld(ref Rect checkIfHoveredRect, Vector2 mousePos, int hoveredIndex) =>
			// Vanilla check "Is the mouse over this group"
			checkIfHoveredRect.Contains(mousePos)
				&&
			// Also Check that the closest reorderable row is in another group
			(ReorderableWidget.lastInsertNear == -1 ||
			ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear].groupID != hoveredIndex &&
			(ReorderableWidget.groups[ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear].groupID].absRect.Contains(checkIfHoveredRect.ContractedBy(1))
			|| !checkIfHoveredRect.Contains(ReorderableWidget.groups[ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear].groupID].absRect.ContractedBy(1))))
				&&
			// Also Check that this rect is within the already hovered rect, if it exists.
			(ReorderableWidget.hoveredGroup == -1 ||
			ReorderableWidget.groups[ReorderableWidget.hoveredGroup].absRect.Contains(checkIfHoveredRect.ContractedBy(1)));



		public static bool Contains(this Rect self, Rect rect) =>
			self.Contains(rect.min) && self.Contains(rect.max);



		//copy/paste of CurrentInsertNear that uses same group only
		private static int FindCurrentInsertNearWithinGroup(int groupID) // out bool toTheLeft) or just refer to the static field ...
		{
			ReorderableWidget.lastInsertNearLeft = true;

			int nearestID = -1;
			for (int i = 0; i < ReorderableWidget.reorderables.Count; i++)
			{
				ReorderableWidget.ReorderableInstance reorderableInstance = ReorderableWidget.reorderables[i];
				if ((reorderableInstance.groupID == groupID) && (nearestID == -1 || Event.current.mousePosition.DistanceToRect(reorderableInstance.absRect) < Event.current.mousePosition.DistanceToRect(ReorderableWidget.reorderables[nearestID].absRect)))
				{
					nearestID = i;
				}
			}
			if (nearestID >= 0)
			{
				ReorderableWidget.ReorderableInstance reorderableInstance2 = ReorderableWidget.reorderables[nearestID];
				if (ReorderableWidget.groups[reorderableInstance2.groupID].direction == ReorderableDirection.Horizontal)
				{
					ReorderableWidget.lastInsertNearLeft = Event.current.mousePosition.x < reorderableInstance2.absRect.center.x;
				}
				else
				{
					ReorderableWidget.lastInsertNearLeft = Event.current.mousePosition.y < reorderableInstance2.absRect.center.y;
				}
			}
			return nearestID;
		}
	}


	// For good measure while I'm here, fix the absRect in ReorderableWidget.NewGroup which threw out the rect position and used zero.
	// Also, set the hovered DrawLine to use the rect as well! Geez.
	[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.NewGroup))]
	public static class FixNewGroupAbsRect
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo VectorZeroInfo = AccessTools.PropertyGetter(typeof(Vector2), nameof(Vector2.zero));
			MethodInfo DrawLineInfo = AccessTools.Method(typeof(ReorderableWidget), nameof(ReorderableWidget.DrawLine));

			foreach (var inst in instructions)
			{
				if (inst.Calls(VectorZeroInfo))
				{
					yield return new CodeInstruction(OpCodes.Ldarga_S, 2); // ref rect, as this
					yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rect), nameof(Rect.position))); // rect.position
				}
				else if (inst.Calls(DrawLineInfo))
				{
					yield return new CodeInstruction(OpCodes.Pop);
					yield return new CodeInstruction(OpCodes.Ldarg_S, 2);
					yield return inst;
				}
				else
					yield return inst;
			}
		}
	}

	// OF COURSE with the above patch, the vanilla code passes the wrong rect into NewGroup. So let's fix that.
	// The DoModList calls BeginScroll then immediately NewGroup, so the GUI coords were at 0,0. 
	// But the rect passed in could be elsewhere. Just nudge that rect to 0,0 for the newgroup.
	[HarmonyPatch(typeof(Page_ModsConfig), nameof(Page_ModsConfig.DoModList))]
	public static class FixModsConfigRect
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			Transpilers.MethodReplacer(instructions,
				AccessTools.Method(typeof(ReorderableWidget), nameof(ReorderableWidget.NewGroup)),
				AccessTools.Method(typeof(FixModsConfigRect), nameof(NewGroupWithRectAtZero)));


		public static int NewGroupWithRectAtZero(Action<int, int> reorderedAction, ReorderableDirection direction, Rect rect, float drawLineExactlyBetween_space = -1f, Action<int, Vector2> extraDraggedItemOnGUI = null, bool playSoundOnStartReorder = true) =>
			ReorderableWidget.NewGroup(reorderedAction, direction, rect.AtZero(), drawLineExactlyBetween_space, extraDraggedItemOnGUI, playSoundOnStartReorder);
	}

	// There was a problem with dropping into the end of a list when a nested group was the last item.
	// It would either snap to the nested items and not insert into the parent list,
	// or at least the drawn line to insert into the parent list was below the parent line, but above the nested children
	// The solution was to have the parent reorder rect extend around and below the children
	// That required two conflicting things:
	// If the Reorderable call for the parent came after the children, it would be dragged instead of the children, because code simply takes the last reorder rect
	// So the parent Reorderable call would have to come first, so the children's rect's would come later they could be dragged.
	// But now UI in the children would be active while dragging, or, the drag operation would cover up the UI...
	//  (e.g. a 1-10 slider GUI would both drag the slider and drag the item, so that'd be weird)
	// So the parent Reorderable call would have to come after the children, so that events in the children's UI would catch and use the event instead of dragging it.
	// So the real solution is to not count the reorderect as dragged if there's already a dragged rect that is smaller than the new one.


	// don't drag reorderable when moving floatslider (reorderable moved before drawing?)
	// - transpile Reorderable IsOver(rect) to check if groupClicked != -1 && clickedInRect.Contains(rect.contractedby(1))

	[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.Reorderable))]
	public static class FixNestedDrag
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo IsOverInfo = AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver));

			foreach (var inst in instructions)
			{
				yield return inst;

				if (inst.Calls(IsOverInfo))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_1);//Rect rect
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FixNestedDrag), nameof(InsideClickedGroup)));//InsideClickedGroup(rect)
					yield return new CodeInstruction(OpCodes.And);//IsOver(rect) && InsideClickedGroup(rect)
				}

			}
		}

		//public static bool IsOver(Rect rect)
		public static bool InsideClickedGroup(Rect reorderRect) =>
			!ReorderableWidget.clicked ||
			ReorderableWidget.clickedInRect.Contains(reorderRect.ContractedBy(1));
	}

	/*
	//[HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.Visible), MethodType.Getter)]
	public static class NoBar
	{
		public static void Postfix(ref bool __result)
		{
			__result = false;
		}
	}

	[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.ReorderableWidgetOnGUI_AfterWindowStack))]
	public static class HijackAfterWindowStack
	{
		public static bool Prefix()
		{
			if (Event.current.rawType == EventType.MouseUp)
			{
				ReorderableWidget.released = true;
			}
			if (Event.current.type != EventType.Repaint)
			{
				return false;
			}
			if (ReorderableWidget.clicked)
			{
				ReorderableWidget.StopDragging();
				for (int i = 0; i < ReorderableWidget.reorderables.Count; i++)
				{
					Log.Message($"if (ReorderableWidget.reorderables[{i}].groupID  ({ ReorderableWidget.reorderables[i].groupID}) == { ReorderableWidget.groupClicked} && { ReorderableWidget.reorderables[i].rect} == {ReorderableWidget.clickedInRect}))");
					if (ReorderableWidget.reorderables[i].groupID == ReorderableWidget.groupClicked && ReorderableWidget.reorderables[i].rect == ReorderableWidget.clickedInRect)
					{
						ReorderableWidget.draggingReorderable = i;
						ReorderableWidget.dragStartPos = Event.current.mousePosition;
						break;
					}
				}
				ReorderableWidget.clicked = false;
			}
			if (ReorderableWidget.draggingReorderable >= ReorderableWidget.reorderables.Count)
			{
				ReorderableWidget.StopDragging();
			}
			if (ReorderableWidget.reorderables.Count != ReorderableWidget.lastFrameReorderableCount)
			{	
				ReorderableWidget.StopDragging();
			}
			ReorderableWidget.lastInsertNear = ReorderableWidget.CurrentInsertNear(out ReorderableWidget.lastInsertNearLeft);
			if (ReorderableWidget.lastInsertNear >= 0)
				Log.Message($"STARTING with lastInsertNear = {ReorderableWidget.lastInsertNear} ; lastInsertNearLeft = {ReorderableWidget.lastInsertNearLeft} ; hoveredGroup = {ReorderableWidget.hoveredGroup} ; {(ReorderableWidget.hoveredGroup >= 0 ? ReorderableWidget.groups[ReorderableWidget.hoveredGroup].absRect:null)}");
			ReorderableWidget.hoveredGroup = -1;
			for (int j = 0; j < ReorderableWidget.groups.Count; j++)
			{
				// Original if statement didn't handle nested rects.
				// if (ReorderableWidget.groups[j].absRect.Contains(Event.current.mousePosition))

				if (ReorderableWidget.lastInsertNear >= 0)
					Log.Message($@"
					if ({ReorderableWidget.groups[j].absRect}.Contains({Event.current.mousePosition})
					&&
					({ReorderableWidget.lastInsertNear} == -1 ||
					!{ReorderableWidget.groups[j].absRect}.Contains({ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear<0?0: ReorderableWidget.lastInsertNear].absRect}))
					&&
					({ReorderableWidget.hoveredGroup} == -1 ||
					{ReorderableWidget.groups[ReorderableWidget.hoveredGroup<0?0: ReorderableWidget.hoveredGroup].absRect}.Contains({ReorderableWidget.groups[j].absRect}))");

				if (ReorderableWidget.groups[j].absRect.Contains(Event.current.mousePosition)
					&&
					(ReorderableWidget.lastInsertNear == -1 ||
					!ReorderableWidget.groups[j].absRect.Contains(ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear].absRect.ContractedBy(1))) 
					&&
					(ReorderableWidget.hoveredGroup == -1 ||
					ReorderableWidget.groups[ReorderableWidget.hoveredGroup].absRect.Contains(ReorderableWidget.groups[j].absRect))
				)
				{
					// Original if block directly set hoveredGroup without checking if it was in the multigroup
					// ReorderableWidget.hoveredGroup = j;
					if (ReorderableWidget.lastInsertNear >= 0)
						Log.Message($" Could be {j} : {ReorderableWidget.groups[j].absRect}");
					if (ReorderableWidget.lastInsertNear >= 0 && ReorderableWidget.AreInMultiGroup(j, ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear].groupID) && ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear].groupID != j)
					{
						ReorderableWidget.lastInsertNear = ReorderableWidget.FindLastReorderableIndexWithinGroup(j);
						ReorderableWidget.lastInsertNearLeft = ReorderableWidget.lastInsertNear < 0;
						ReorderableWidget.hoveredGroup = j; // < - this right here fixes it
						Log.Message($"  SETTING lastInsertNear = {ReorderableWidget.lastInsertNear}; lastInsertNearLeft = {ReorderableWidget.lastInsertNearLeft} ; hoveredGroup = {ReorderableWidget.hoveredGroup} ; {ReorderableWidget.groups[ReorderableWidget.hoveredGroup].absRect}");
					}
				}
			}
			if (ReorderableWidget.released)
			{
				ReorderableWidget.released = false;
				if (ReorderableWidget.dragBegun && ReorderableWidget.draggingReorderable >= 0)
				{
					Log.Message($"RELEASED! : hoveredGroup = {ReorderableWidget.hoveredGroup} lastInsertNear = {ReorderableWidget.lastInsertNear}; lastInsertNearLeft = {ReorderableWidget.lastInsertNearLeft}");
					int fromIndex = ReorderableWidget.GetIndexWithinGroup(ReorderableWidget.draggingReorderable);
					int fromID = ReorderableWidget.reorderables[ReorderableWidget.draggingReorderable].groupID;
					int toIndex = ((ReorderableWidget.lastInsertNear == ReorderableWidget.draggingReorderable) ? fromIndex : ((!ReorderableWidget.lastInsertNearLeft) ? (ReorderableWidget.GetIndexWithinGroup(ReorderableWidget.lastInsertNear) + 1) : ReorderableWidget.GetIndexWithinGroup(ReorderableWidget.lastInsertNear)));
					int toID = -1;
					Log.Message($" Thinking ({fromIndex}, {fromID}, {toIndex}, {toID})");
					if (ReorderableWidget.lastInsertNear >= 0)
					{
						toID = ReorderableWidget.reorderables[ReorderableWidget.lastInsertNear].groupID;
						Log.Message($"  toID = {toID}");
					}
					if (ReorderableWidget.AreInMultiGroup(fromID, ReorderableWidget.hoveredGroup) && ReorderableWidget.hoveredGroup >= 0 && ReorderableWidget.hoveredGroup != toID)
					{
						toID = ReorderableWidget.hoveredGroup;
						toIndex = ReorderableWidget.GetIndexWithinGroup(ReorderableWidget.FindLastReorderableIndexWithinGroup(toID)) + 1;
						Log.Message($"  toID = {toID}; toIndex = {toIndex}");
					}
					if (ReorderableWidget.AreInMultiGroup(fromID, toID))
					{
						Log.Message($" Doing it for {fromID}: ({fromIndex}, {fromID}, {toIndex}, {toID}");
						ReorderableWidget.GetMultiGroupByGroupID(fromID).Value.reorderedAction(fromIndex, fromID, toIndex, toID);
						SoundDefOf.DropElement.PlayOneShotOnCamera();
					}
					else if (toIndex >= 0 && toIndex != fromIndex && toIndex != fromIndex + 1)
					{
						SoundDefOf.DropElement.PlayOneShotOnCamera();
						try
						{
							Log.Message($"Doing it for {ReorderableWidget.draggingReorderable}: ({fromIndex}, {toIndex})");
							ReorderableWidget.groups[ReorderableWidget.reorderables[ReorderableWidget.draggingReorderable].groupID].reorderedAction(fromIndex, toIndex);
						}
						catch (Exception ex)
						{
							Log.Error("Could not reorder elements (from " + fromIndex + " to " + toIndex + "): " + ex);
						}
					}
				}
				ReorderableWidget.StopDragging();
			}
			ReorderableWidget.lastFrameReorderableCount = ReorderableWidget.reorderables.Count;
			ReorderableWidget.multiGroups.Clear();
			ReorderableWidget.groups.Clear();
			ReorderableWidget.reorderables.Clear();



			return false;
		}
	}
	*/
}
