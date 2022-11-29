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
	[HarmonyPatch(typeof(Window), nameof(Window.InnerWindowOnGUI))]
	public static class FixWindowDragInsteadOfReorderable
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			FieldInfo draggableInfo = AccessTools.Field(typeof(Window), nameof(Window.draggable));
			foreach (var inst in instructions)
			{
				if (inst.LoadsField(draggableInfo))
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FixWindowDragInsteadOfReorderable), nameof(DraggableAndNotReordering)));
				}
				else
					yield return inst;
			}
		}

		public static bool DraggableAndNotReordering(Window window)
		{
			return window.draggable && ReorderableWidget.draggingReorderable == -1;
		}
	}


	/*
	 * Fix bug where a draggable window preventing reorderable widgets from reordering
	 * The Event.current.mousePosition of the reorderable was always adjusted to have not moved
	 * Because GUI.DragWindow would adjust it to anchor to the cursor
	 * Simply fix: don't GUI.DragWindow if a reoderable rect has been ReorderableWidget.clicked
	 * 
	[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.Reorderable))]
	public static class NewFeature
	{
		//public static bool Reorderable(int groupID, Rect rect, bool useRightButton = false, bool highlightDragged = true)
		public static void Prefix(int groupID)
		{
			if(Event.current.type == EventType.Repaint)
				Log.Message($"if (ReorderableWidget.draggingReorderable({ReorderableWidget.draggingReorderable}) != -1 && ReorderableWidget.dragBegun({ReorderableWidget.dragBegun}) || (Vector2.Distance(clickedAt({ReorderableWidget.clickedAt}), Event.current.mousePosition({Event.current.mousePosition}) > 5f && ReorderableWidget.groupClicked({ReorderableWidget.groupClicked}) == groupID({groupID})");

		}
	}

	[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.ReorderableWidgetOnGUI_AfterWindowStack))]
	public static class NewFeatureReorderableWidgetOnGUI_AfterWindowStack
	{
		public static void Prefix()
		{
			if (Event.current.type == EventType.Repaint && ReorderableWidget.clicked)
				Log.Message($"ReorderableWidgetOnGUI_AfterWindowStack Repaint : ReorderableWidget.clicked = {ReorderableWidget.clicked}");
		}

		public static void Postfix()
		{
			Log.Message($"ReorderableWidgetOnGUI_AfterWindowStack Post: ReorderableWidget.draggingReorderable = {ReorderableWidget.draggingReorderable}, ReorderableWidget.groupClicked={ReorderableWidget.groupClicked}, ReorderableWidget.lastInsertNear = {ReorderableWidget.lastInsertNear}, ReorderableWidget.hoveredGroup = {ReorderableWidget.hoveredGroup}");

		}
	}

	[HarmonyPatch(typeof(ReorderableWidget), nameof(ReorderableWidget.Reorderable))]
	public static class NewFeaturez
	{
		//	public static bool Reorderable(int groupID, Rect rect, bool useRightButton = false, bool highlightDragged = true)

		public static void Postfix(int groupID, Rect rect)
		{
			if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
				Log.Message($"Reorderable ReorderableWidget.clicked = {ReorderableWidget.clicked} : {groupID} / {rect}");
		}
	}

	[HarmonyPatch(typeof(Window), nameof(Window.InnerWindowOnGUI))]
	public static class Logevent
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo DragWindowInfo = AccessTools.Method(typeof(GUI), nameof(GUI.DragWindow));

			foreach (var inst in instructions)
			{
				if (inst.Calls(DragWindowInfo))
				{
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Logevent), nameof(LogEvent1)));

					yield return inst;

					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Logevent), nameof(LogEvent2)));

				}
				else
					yield return inst;
			}
		}
		public static void LogEvent1()
		{
			if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
				Log.Message($"Event b4 is {Event.current} :: ReorderableWidget.clicked = {ReorderableWidget.clicked}");
		}
		public static void LogEvent2()
		{
			if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
				Log.Message($"Event af is {Event.current} :: ReorderableWidget.clicked = {ReorderableWidget.clicked}");
		}
	}
	*/
}
