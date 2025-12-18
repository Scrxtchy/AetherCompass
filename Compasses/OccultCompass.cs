using AetherCompass.Common;
using AetherCompass.Common.Attributes;
using AetherCompass.Compasses.Objectives;
using AetherCompass.Game;
using AetherCompass.UI.Gui;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Dalamud.Bindings.ImGui;

namespace AetherCompass.Compasses;

[CompassType(CompassType.Standard)]
public class OccultCompass : Compass
{
	public override string CompassName => "Occult Crescent Compass";
	public override string Description => "Occult Crescent Compass";

	protected override CompassConfig CompassConfig => Plugin.Config.EurekanConfig;

	private static System.Numerics.Vector4 infoTextColour = new(.8f, .95f, .75f, 1);
	private const float infoTextShadowLightness = .1f;

	private static readonly System.Numerics.Vector2 elementalMarkerIconSize = new(25, 25);

	public override bool IsEnabledInCurrentTerritory() =>
		ZoneWatcher.CurrentTerritoryType?.TerritoryIntendedUse.ValueNullable?.RowId == 61;

	protected override string GetClosestObjectiveDescription(CachedCompassObjective objective) =>
		objective.Name;

	public override unsafe bool IsObjective(GameObject* o) =>
		o != null
		&& ((o->ObjectKind == ObjectKind.Treasure)
		&& CompassUtil.GetName(o) == "Treasure Coffer")
		|| ((o->ObjectKind == ObjectKind.EventNpc)
		&& CompassUtil.GetName(o) == "Destination");

	public override DrawAction? CreateDrawDetailsAction(CachedCompassObjective objective)
	{
		if (objective.IsEmpty())
			return null;
		return new(() =>
		{
			ImGui.Text($"{objective.Name}");
			ImGui.BulletText(
				$"{CompassUtil.MapCoordToFormattedString(objective.CurrentMapCoord)} (approx.)"
			);
			ImGui.BulletText(
				$"{objective.CompassDirectionFromPlayer},  "
					+ $"{CompassUtil.DistanceToDescriptiveString(objective.Distance3D, false)}"
			);
			ImGui.BulletText(CompassUtil.AltitudeDiffToDescriptiveString(objective.AltitudeDiff));
			DrawFlagButton($"{(long)objective.GameObject}", objective.CurrentMapCoord);
			ImGui.Separator();
		});
	}

	public override DrawAction? CreateMarkScreenAction(CachedCompassObjective objective)
	{
		if (objective.IsEmpty())
			return null;
		return GenerateDefaultScreenMarkerDrawAction(
			objective,
			IconManager.DefaultMarkerIconId,
			DefaultMarkerIconSize,
			.9f,
			$"{objective.Name}, {CompassUtil.DistanceToDescriptiveString(objective.Distance3D, true)}",
			infoTextColour,
			infoTextShadowLightness,
			out _
		);
	}

	private static bool IsEurekanElementalName(string? name)
	{
		if (name == null)
			return false;
		name = name.ToLower();
		return name == "Treasure Coffer";
	}
}
