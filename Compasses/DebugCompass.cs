using AetherCompass.Common;
using AetherCompass.Common.Attributes;
using AetherCompass.Compasses.Objectives;
using AetherCompass.Game;
using AetherCompass.UI.Gui;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;

namespace AetherCompass.Compasses;

[CompassType(CompassType.Debug)]
public class DebugCompass : Compass
{
	public override string CompassName => "Debug Compass";
	public override string Description => "For Debug";

	protected override CompassConfig CompassConfig => Plugin.Config.DebugConfig;

	private const uint markerIconId = IconManager.DefaultMarkerIconId;

	public override bool IsEnabledInCurrentTerritory() =>
		ZoneWatcher.CurrentTerritoryType?.RowId != 0;

	public override unsafe bool IsObjective(GameObject* o) =>
		o != null
		&& (
#if DEBUG
			Plugin.Config.DebugTestAllGameObjects
			||
#endif
			o->EntityId == GameObjects.LocalPlayer->EntityId
			|| o->ObjectKind == ObjectKind.EventObj
			|| o->ObjectKind == ObjectKind.EventNpc
			|| o->ObjectKind == ObjectKind.GatheringPoint
			|| o->ObjectKind == ObjectKind.Aetheryte
			|| o->ObjectKind == ObjectKind.AreaObject
			|| o->ObjectKind == ObjectKind.CardStand
			|| o->ObjectKind == ObjectKind.ReactionEventObject
			|| o->ObjectKind == ObjectKind.BattleNpc
		);

	protected override unsafe CachedCompassObjective CreateCompassObjective(GameObject* obj) =>
		new DebugCachedCompassObjective(obj);

	protected override unsafe CachedCompassObjective CreateCompassObjective(
		UI3DModule.ObjectInfo* info
	) => new DebugCachedCompassObjective(info);

	protected override string GetClosestObjectiveDescription(CachedCompassObjective _) =>
		"Debug Obj";

	public override unsafe DrawAction? CreateDrawDetailsAction(CachedCompassObjective objective)
	{
		if (objective.IsEmpty() || objective is not DebugCachedCompassObjective debugObjective)
			return null;
		return new(() =>
		{
			ImGui.Text($"Object: {debugObjective.Name}");
			ImGui.BulletText(
				$"ObjectId: {debugObjective.GameObjectId.ObjectId}, type {debugObjective.GameObjectId.Type}"
			);
			ImGui.BulletText($"ObjectKind: {debugObjective.ObjectKind}");
			ImGui.BulletText($"NpcId: {debugObjective.NpcId} DataId: {debugObjective.DataId}");
			ImGui.BulletText($"2D-Distance: {debugObjective.Distance2D:0.0}");
			ImGui.BulletText($"Height diff: {debugObjective.AltitudeDiff:0.0}");
			ImGui.BulletText($"3D-Distance: {debugObjective.Distance3D:0.0}");
			ImGui.BulletText(
				$"Direction: {debugObjective.CompassDirectionFromPlayer}, {debugObjective.RotationFromPlayer:0.00}"
			);
			ImGui.BulletText($"Position: {debugObjective.Position}");
			ImGui.BulletText(
				$"MapCoord: {CompassUtil.MapCoordToFormattedString(debugObjective.CurrentMapCoord)}"
			);
			ImGui.BulletText($"Normalised Nameplate Pos: {objective.NormalisedNameplatePos}");

			var o = (GameObject*)objective.GameObject.ToPointer();
			var ischar = CompassUtil.IsCharacter(o);
			ImGui.BulletText($"Is character: {ischar}");
			if (ischar)
			{
				ImGui.BulletText($"Level: {CompassUtil.GetCharacterLevel(o)}");
				ImGui.BulletText($"Alive: {CompassUtil.IsCharacterAlive(o)}");
				ImGui.BulletText($"IsHostile: {CompassUtil.IsHostileCharacter(o)}");
			}

			DrawFlagButton(
				((long)debugObjective.GameObject).ToString(),
				debugObjective.CurrentMapCoord
			);

			ImGui.NewLine();
		});
	}

	public override DrawAction? CreateMarkScreenAction(CachedCompassObjective objective)
	{
		if (objective.IsEmpty())
			return null;
		// These are already handled by the Draw...Default method,
		// here is just for debug record
		UiHelper.WorldToScreenPos(objective.Position, out var screenPos);
		screenPos.Y -= ImGui.GetMainViewport().Size.Y / 50; // slightly raise it up from hitbox screen pos

		var info =
			$"name={objective.Name}, "
			+ $"worldPos=<{objective.Position.X:0.00}, {objective.Position.Y:0.00}, {objective.Position.Z:0.00}, "
			+ $"dist={objective.Distance3D:0.0}\n"
			+ $"sPosUnfixed=<{screenPos.X:0.0}, {screenPos.Y:0.0}>, "
			+
			// $"raw=<{pCoordsRaw.X:0.0}, {pCoordsRaw.Y:0.0}, {pCoordsRaw.Z:0.0}>\n" +
			$"npPos=<{objective.NormalisedNameplatePos.X:0.0}, {objective.NormalisedNameplatePos.Y:0.0}, {objective.NormalisedNameplatePos.Z:0.0}>";
		return GenerateDefaultScreenMarkerDrawAction(
			objective,
			markerIconId,
			DefaultMarkerIconSize,
			.9f,
			info,
			new(1, 1, 1, 1),
			0,
			out _
		);
	}
}
