using AetherCompass.Common;
using AetherCompass.Common.Attributes;
using AetherCompass.Compasses.Configs;
using AetherCompass.Compasses.Objectives;
using AetherCompass.Game;
using AetherCompass.Game.SeFunctions;
using AetherCompass.UI.Gui;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;

namespace AetherCompass.Compasses;

[CompassType(CompassType.Experimental)]
public class QuestCompass : Compass
{
	public override string CompassName => "Quest Compass";

	public override string Description =>
		"Detecting NPC/objects nearby relevant to your in-progress quests.\n"
		+ "** Currently limited functionality: battle NPCs will not be detected, "
		+ "and the compass sometimes gives inaccurate or, although more rarely, incorrect information.";

	protected override CompassConfig CompassConfig => Plugin.Config.QuestConfig;
	private QuestCompassConfig QuestConfig => (QuestCompassConfig)CompassConfig;

	private static readonly System.Reflection.PropertyInfo?[,] cachedQuestSheetToDoLocMap =
		new System.Reflection.PropertyInfo[24, 8];

	//private static readonly System.Reflection.PropertyInfo?[,] cachedQuestSheetToDoChildLocationMap = new System.Reflection.PropertyInfo[24, 7];
	private readonly Dictionary<uint, (Quest RelatedQuest, bool TodoRevealed)> objQuestMap = [];

	private static readonly System.Numerics.Vector4 infoTextColour = new(.98f, .77f, .35f, 1);
	private static readonly float infoTextShadowLightness = .1f;

	private static readonly int ScreenMarkerQuestNameMaxLength = Language.GetAdjustedTextMaxLength(
		16
	);

	private const uint defaultQuestMarkerIconId = 71223;

	// NPC AnnounceIcon starts from 71200
	// Refer to Excel sheet EventIconType,
	// For types whose IconRange is 6, the 3rd is in-progress and 5th is last seq (checkmark icon),
	// because +0 is the dummy, so 1st icon in the range would start from +1.
	// Each type has available and locked ver, but rn idk how to accurately tell if a quest is avail or locked
	private static uint GetQuestMarkerIconId(
		uint baseIconId,
		byte iconRange,
		bool questLastSeq = false
	) =>
		baseIconId
		+ iconRange switch
		{
			6 => questLastSeq ? 5u : 3u,
			1 => 0,
			_ => 1,
		};

	public override bool IsEnabledInCurrentTerritory()
	{
		var terr = ZoneWatcher.CurrentTerritoryType;
		// 0 is noninstance, 1 is solo instance
		return terr?.ExclusiveType == 0
			|| ((QuestConfig?.EnabledInSoloContents ?? false) && terr?.ExclusiveType == 1);
	}

	public override unsafe bool IsObjective(GameObject* o)
	{
		if (o == null)
			return false;
		var kind = o->ObjectKind;
		var dataId = o->BaseId;
		if (dataId == 0)
			return false;
		// NOTE: already considered hidden quests or those not revealed by Todos when filling up objQuestMap
		// TODO: AreaObject???
		if (
			kind == ObjectKind.EventNpc
			|| kind == ObjectKind.EventObj
			|| kind == ObjectKind.AreaObject
		)
			return objQuestMap.ContainsKey(o->BaseId);
		return false;
	}

	protected override string GetClosestObjectiveDescription(CachedCompassObjective objective) =>
		objective.Name + " (Quest)";

	public override DrawAction? CreateDrawDetailsAction(CachedCompassObjective objective)
	{
		if (objective.IsEmpty())
			return null;
		if (!objQuestMap.TryGetValue(objective.DataId, out var mappedInfo))
			return null;
		var questId = mappedInfo.RelatedQuest.QuestID;
		return new(
			() =>
			{
				ImGui.Text($"{objective.Name} {(mappedInfo.TodoRevealed ? "★" : "")}");
				ImGui.BulletText($"Quest: {Language.SanitizeText(GetQuestName(questId))}");
#if DEBUG
				var qItem = GetQuestRow(questId);
				if (qItem != null)
				{
					ImGui.BulletText(
						$"JournalGenre: {qItem.Value.JournalGenre.ValueNullable?.Name ?? string.Empty} #{qItem.Value.JournalGenre.ValueNullable?.RowId}, "
							+ $"Type: {qItem.Value.Type}"
					);
				}
#endif
				ImGui.BulletText(
					$"{CompassUtil.MapCoordToFormattedString(objective.CurrentMapCoord)} (approx.)"
				);
				ImGui.BulletText(
					$"{objective.CompassDirectionFromPlayer},  "
						+ $"{CompassUtil.DistanceToDescriptiveString(objective.Distance3D, false)}"
				);
				ImGui.BulletText(
					CompassUtil.AltitudeDiffToDescriptiveString(objective.AltitudeDiff)
				);
				DrawFlagButton($"##{(long)objective.GameObject}", objective.CurrentMapCoord);
				ImGui.Separator();
			},
			mappedInfo.RelatedQuest.IsPriority
		);
	}

	public override DrawAction? CreateMarkScreenAction(CachedCompassObjective objective)
	{
		if (objective.IsEmpty())
			return null;
		if (!objQuestMap.TryGetValue(objective.DataId, out var mappedInfo))
			return null;
		var qRow = GetQuestRow(mappedInfo.RelatedQuest.QuestID);
		var iconId =
			qRow == null || qRow.Value.EventIconType.ValueNullable == null
				? defaultQuestMarkerIconId
				: GetQuestMarkerIconId(
					qRow.Value.EventIconType.Value.NpcIconAvailable,
					qRow.Value.EventIconType.Value.IconRange,
					mappedInfo.RelatedQuest.QuestSeq == questFinalSeqIdx
				);
		var descr = (mappedInfo.TodoRevealed ? "★ " : "") + $"{objective.Name}";
		if (QuestConfig.ShowQuestName)
		{
			var questName = Language.SanitizeText(GetQuestName(mappedInfo.RelatedQuest.QuestID));
			if (QuestConfig.MarkerTextInOneLine)
			{
				if (questName.Length > ScreenMarkerQuestNameMaxLength)
					questName = questName[..ScreenMarkerQuestNameMaxLength] + "..";
				descr +=
					$" (Quest: {questName}), {CompassUtil.DistanceToDescriptiveString(objective.Distance3D, true)}";
			}
			else
				descr +=
					$", {CompassUtil.DistanceToDescriptiveString(objective.Distance3D, true)}"
					+ $"\n(Quest: {questName})";
		}
		else
			descr += $", {CompassUtil.DistanceToDescriptiveString(objective.Distance3D, true)}";
		return GenerateDefaultScreenMarkerDrawAction(
			objective,
			iconId,
			DefaultMarkerIconSize,
			.9f,
			descr,
			infoTextColour,
			infoTextShadowLightness,
			out _,
			important: objective.Distance3D < 55 || mappedInfo.RelatedQuest.IsPriority
		);
	}

	public override void DrawConfigUiExtra()
	{
		ImGui.BulletText("More options:");
		ImGui.Indent();
		ImGuiEx.Checkbox(
			"Also enable this compass in solo instanced contents",
			ref QuestConfig.EnabledInSoloContents,
			"By default, this compass will not work in any type of instanced contents.\n"
				+ "You can enable it in solo instanced contents if needed."
		);
		//ImGuiEx.Checkbox("Also detect quest related enemies", ref QuestConfig.DetectEnemy,
		//    "By default, this compass will only detect event NPCs or objects, that is, NPCs/Objects that don't fight.\n" +
		//    "You can enable this option to have the compass detect also quest related enemies.");
		ImGuiEx.Checkbox(
			"Don't detect hidden quests",
			ref QuestConfig.HideHidden,
			"Hidden quests are those that you've marked as ignored in Journal.\n"
				+ "If this option is enabled, will not detect NPC/Objects related to these hidden quests."
		);
		ImGuiEx.Checkbox(
			"Detect all NPCs and objects relevant to in-progress quests",
			ref QuestConfig.ShowAllRelated,
			"By default, this compass only detects NPC/objects that are objectives of the quests "
				+ "as shown in the quest Todos and on the Minimap.\n\n"
				+ "If this option is enabled, NPC/objects that are spawned due to the quests will also "
				+ "be detected by this compass, even if they may not be the objectives of the quests.\n"
				+ "Additionally, for quests that require looking for NPC/objects in a certain area, "
				+ "enabling this option may reveal the objectives' locations.\n\n"
				+ "In either case, NPC/objects that are known to be quest objectives will have a \"★\" mark by their names."
		);
		if (MarkScreen)
		{
			ImGui.Checkbox("Show quest name by screen marker", ref QuestConfig.ShowQuestName);
			if (QuestConfig.ShowQuestName)
				ImGuiEx.Checkbox(
					"Show screen marker text in one line",
					ref QuestConfig.MarkerTextInOneLine,
					"Display the whole label text in one line.\n"
						+ "May only display part of the quest name to avoid the text being too long."
				);
		}
		ImGui.Unindent();
	}

	public override void ProcessOnLoopStart()
	{
		objQuestMap.Clear();
		ProcessQuestData();

		base.ProcessOnLoopStart();

		// TODO: may mark those location range in quest ToDOs if somehow we can know which ToDos are already done;
		// we can find those locations easily from Quest sheet but only if we can know which ToDo are done!
		// (If we can know that, we can also detect relevant BNpcs as well..)
	}

	private static uint QuestIdToQuestRowId(ushort questId) => questId + (uint)65536;

	private static ushort QuestRowIdToQuestId(uint questRowId)
	{
		if (questRowId <= 65536)
			return 0;
		var id = questRowId - 65536;
		if (id <= ushort.MaxValue)
			return (ushort)id;
		return 0;
	}

	private static readonly ExcelSheet<Sheets.Quest>? QuestSheet =
		Plugin.DataManager.GetExcelSheet<Sheets.Quest>();

	private static readonly ExcelSheet<Sheets.EObj>? EObjSheet =
		Plugin.DataManager.GetExcelSheet<Sheets.EObj>();

	private static readonly ExcelSheet<Sheets.ENpcBase>? ENpcSheet =
		Plugin.DataManager.GetExcelSheet<Sheets.ENpcBase>();

	private static readonly ExcelSheet<Sheets.Level>? LevelSheet =
		Plugin.DataManager.GetExcelSheet<Sheets.Level>();

	private static Sheets.Quest? GetQuestRow(ushort questId) =>
		QuestSheet?.GetRow(QuestIdToQuestRowId(questId));

	private static string GetQuestName(ushort questId) =>
		GetQuestRow(questId)?.Name.ExtractText() ?? string.Empty;

	// ActorSpawn, ActorDespawn, Listener, etc.
	private const int questSheetActorArrayLength = 64;

	private const int questSheetToDoArrayLength = 24;
	private const int questSheetToDoChildMaxCount = 7;
	private const byte questFinalSeqIdx = byte.MaxValue;

	private unsafe void ProcessQuestData()
	{
		var questlist = Quests.GetQuestListArray();
		if (questlist == null)
		{
			LogError("Failed to get QuestListArray");
			return;
		}

		static bool ShouldExitActorArrayLoop(Sheets.Quest q, int idx) =>
			(
				idx >= 0
				&& idx < questSheetActorArrayLength / 2
				&& q.QuestListenerParams[idx].QuestUInt8A == 0
			);
		// TODO complete API11/7.1 update
		//|| (idx >= questSheetActorArrayLength / 2 && idx < questSheetActorArrayLength
		//    && q.QuestListenerParams[idx - questSheetActorArrayLength / 2].QuestUInt8B == 0);

		for (var i = 0; i < Quests.QuestListArrayLength; i++)
		{
			var quest = questlist[i];
			if (quest.QuestID == 0)
				continue;
			if (quest.IsHidden && QuestConfig.HideHidden)
				continue;
			var questRow = GetQuestRow(quest.QuestID);
			if (questRow == null)
				continue;

			// ToDos: find out objective gameobjs revealed by ToDos, if any
			HashSet<uint> todoRevealedObjs = [];
			for (var j = 0; j < questSheetToDoArrayLength; j++)
			{
				// NOTE: ignore Level location for now,
				// because we cant tell if the ToDos are completed or not when there are multiple Todos
				if (questRow.Value.TodoParams[j].ToDoCompleteSeq == quest.QuestSeq)
				{
					//var mainLoc = questRow.ToDoMainLocation[j].Value;
					//if (mainLoc != null && mainLoc.Object != 0)
					//    todoRevealedObjs.Add(mainLoc.Object);
					//for (int k = 0; k < questSheetToDoChildMaxCount; k++)
					//{
					//    var childLocRowId = GetQuestToDoChildLocationRowId(questRow, j, k);
					//    if (childLocRowId != 0)
					//    {
					//        var childLoc = LevelSheet?.GetRow(childLocRowId);
					//        if (childLoc != null && childLoc.Object != 0)
					//            todoRevealedObjs.Add(childLoc.Object);
					//    }
					//}

					// Main + Child
					for (var k = 0; k < questSheetToDoChildMaxCount + 1; k++)
					{
						var todoLocRowId = GetQuestToDoLocRowId(questRow, j, k);
						if (todoLocRowId > 0)
						{
							var todoLoc = LevelSheet?.GetRow(todoLocRowId);
							if (todoLoc != null && todoLoc.Value.Object.RowId != 0)
								todoRevealedObjs.Add(todoLoc.Value.Object.RowId);
						}
					}
				}
				if (questRow.Value.TodoParams[j].ToDoCompleteSeq == questFinalSeqIdx)
					break;
			}

			// Actor related arrays: find out related gameobjs
			List<uint> objsThisSeq = []; // objectives (usually listeners) that will be deactivated when this Seq completes
			for (var j = 0; j < questSheetActorArrayLength; j++)
			{
				if (ShouldExitActorArrayLoop(questRow.Value, j))
					break;
				var listener = questRow.Value.QuestListenerParams[j].Listener;
				// Track ConditionValue if ConditionType non-zero instead of listener itself;
				// this usually happens with BNpc etc. which we don't consider yet, but anyway
				var objToTrack =
					questRow.Value.QuestListenerParams[j].ConditionType > 0
						? questRow.Value.QuestListenerParams[j].ConditionValue
						: listener;
				var todoRevealed = todoRevealedObjs.Contains(objToTrack);
				// Skip those not revealed by ToDos if option not enabled
				if (!QuestConfig.ShowAllRelated && !todoRevealed)
					continue;
				if (questRow.Value.QuestListenerParams[j].ActorSpawnSeq == 0)
					continue; // Invalid? usually won't have this
				if (questRow.Value.QuestListenerParams[j].ActorSpawnSeq > quest.QuestSeq)
					continue; // Not spawn/active yet
				if (
					questRow.Value.QuestListenerParams[j].ActorDespawnSeq
					< questSheetToDoArrayLength
				)
				{
					// I think ActorDespawnSeq corresponds to ToDo idx, not Seq
					var despawnSeq = questRow
						.Value
						.TodoParams[questRow.Value.QuestListenerParams[j].ActorDespawnSeq]
						.ToDoCompleteSeq;
					if (despawnSeq < quest.QuestSeq)
						continue; // Despawned/deactivated when previous Seq ends
					// TODO: should we also check if it's spawned at start of this Seq?
					if (despawnSeq == quest.QuestSeq)
						objsThisSeq.Add(objToTrack);
				}
				if (!objQuestMap.ContainsKey(objToTrack) || quest.IsPriority)
					objQuestMap[objToTrack] = (quest, todoRevealed);
			}
			// Filter out already interacted ones
			if (quest.ObjectiveObjectsInteractedFlags != 0)
			{
				for (var k = 0; k < objsThisSeq.Count; k++)
					if (quest.IsObjectiveInteracted(k))
						objQuestMap.Remove(objsThisSeq[k]);
			}
		}
	}

	// The uint stored in the cell is the corresponding row id in Level sheet
	private static uint GetQuestToDoLocRowId(Sheets.Quest? questRow, int row, int col)
	{
		if (
			row < 0
			|| row >= questSheetToDoArrayLength
			|| col < 0
			|| col >= questSheetToDoChildMaxCount + 1
			|| questRow == null
		)
			return 0;
		var val = cachedQuestSheetToDoLocMap[row, col]?.GetValue(questRow);
		// First 7 cells (from col 1221) is put in an array accessed via Unknown1221
		if (val == null)
			return 0;
		if (row <= 7 && col == 0)
			return (val as uint[])?[row] ?? 0;
		else
			return (uint)val;
	}

	//// ToDoChildLocation is 24 * 7 array containing row id of corresponding Level
	//// In sheet first col of every row get listed first, then 2nd col and so on.
	//// (So together with ToDoMainLocation, we can have at most 24 ToDos for a quest and each ToDo can have at most 8 Levels.)
	//public static uint GetQuestToDoChildLocationRowId(Sheets.Quest? questRow, int row, int col)
	//{
	//    if (row < 0 || row >= questSheetToDoArrayLength
	//        || col < 0 || col >= questSheetToDoChildMaxCount || questRow == null) return 0;
	//    var val = cachedQuestSheetToDoChildLocationMap[row, col]?.GetValue(questRow);
	//    if (val == null) return 0;
	//    if (row <= 6 && col == 0) // covered by uint array at col 1245
	//        return (val as uint[])?[row] ?? 0;
	//    else return (uint)val;
	//}

	//public static uint GetQuestToDoChildLocationRowId(uint questRowId, int row, int col)
	//    => GetQuestToDoChildLocationRowId(QuestSheet?.GetRow(questRowId), row, col);

	// (0,0) is at col 1221. These first 24 cells used to be
	//  represented as one array called ToDoMainlocation
	// The rest used to be a 24x7 array of ToDoChildLocation;
	//  still row first (traverse through every row of one column then the next)
	//
	// Currently: Unknown1221 -> col 1221 ~ 1228
	// From Unknown1229 single cell, until Unknown1412 (incl.)
	private static void InitQuestSheetToDoLocMap()
	{
		var questSheetType = typeof(Sheets.Quest);
		var propIdx0 = 1221;
		for (var i = 0; i < questSheetToDoArrayLength; i++)
		{
			// Main
			cachedQuestSheetToDoLocMap[i, 0] =
				i <= 7
					? questSheetType.GetProperty($"Unknown{propIdx0}")
					: questSheetType.GetProperty($"Unknown{propIdx0 + i}");
			// Child
			for (var j = 0; j < questSheetToDoChildMaxCount; j++)
				cachedQuestSheetToDoLocMap[i, j] = questSheetType.GetProperty(
					$"Unknown{propIdx0 + j * 24 + i}"
				);
		}
	}

	//// (0, 1) is at col 1245. Row first --- col 1246 is (1,1) etc.
	//// Used to be a 24x7 unit array in Lumina but now merged into one array with the previous ToDoMainLocation array
	//// So the Child part starts from the second columns of this big array
	//private static void InitQuestSheetToDoChildLocationMap()
	//{
	//    var questSheetType = typeof(Sheets.Quest);
	//    int propIdx0 = 1245;
	//    for (int i = 0; i < questSheetToDoArrayLength; i++)
	//    {
	//        for (int j = 0; j < questSheetToDoChildMaxCount; j++)
	//        {
	//            if (i <= 6 && j == 0)
	//                // From uint array at col 1245
	//                cachedQuestSheetToDoChildLocationMap[i, j] = questSheetType.GetProperty($"Unknown{propIdx0}");
	//            else
	//            {
	//                int propIdx = propIdx0 + j * questSheetToDoArrayLength + i;
	//                cachedQuestSheetToDoChildLocationMap[i, j] = questSheetType.GetProperty($"Unknown{propIdx}");
	//            }
	//        }
	//    }
	//}

	public QuestCompass()
	{
		InitQuestSheetToDoLocMap();
		//InitQuestSheetToDoChildLocationMap();
	}
}
