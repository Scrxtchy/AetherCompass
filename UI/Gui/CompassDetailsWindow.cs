using AetherCompass.Common;
using AetherCompass.Compasses;
using AetherCompass.Game;
using Dalamud.Bindings.ImGui;

namespace AetherCompass.UI.Gui;

public class CompassDetailsWindow
{
	private readonly Dictionary<Compass, ActionQueue> drawActions = [];

	public bool RegisterCompass(Compass c) => drawActions.TryAdd(c, new(80));

	public bool UnregisterCompass(Compass c) => drawActions.Remove(c);

	public bool AddDrawAction(Compass c, Action? a, bool important)
	{
		if (a == null)
			return false;
		if (!drawActions.TryGetValue(c, out var queue))
			return false;
		return queue.QueueAction(a, important);
	}

	public bool AddDrawAction(Compass c, DrawAction? a)
	{
		if (a == null)
			return false;
		if (!drawActions.TryGetValue(c, out var queue))
			return false;
		return queue.QueueAction(a);
	}

	public void Draw()
	{
		var map = ZoneWatcher.CurrentMap;
		if (map == null)
			return;

		if (ImGui.Begin("AetherCompass: Detected Objects' Details"))
		{
			var regionName = CompassUtil.GetPlaceNameToString(map.Value.PlaceNameRegion.RowId);
			var placeName = CompassUtil.GetPlaceNameToString(map.Value.PlaceName.RowId);
			var subName = CompassUtil.GetPlaceNameToString(map.Value.PlaceNameSub.RowId);
			var mapName = regionName;
			if (!string.IsNullOrEmpty(mapName) && !string.IsNullOrEmpty(placeName))
				mapName += " > " + placeName;
			else if (!string.IsNullOrEmpty(placeName))
				mapName = placeName;
			if (!string.IsNullOrEmpty(mapName) && !string.IsNullOrEmpty(subName))
				mapName += " > " + subName;

			if (ImGui.BeginTable("##Tbl_DetailWindowMapInfo", 2, ImGuiTableFlags.SizingStretchProp))
			{
				ImGui.TableSetupColumn("##MapInfo", ImGuiTableColumnFlags.WidthStretch);
				ImGui.TableSetupColumn("##ConfigButton", ImGuiTableColumnFlags.WidthFixed);
				ImGui.TableNextColumn();
				ImGuiEx.IconTextMapMarker(true);
				ImGui.TextWrapped($"{mapName}");
				ImGui.TableNextColumn();
				if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.Cog, 0, "Open Config"))
					Plugin.OpenConfig(true);
				ImGui.EndTable();
			}

#if DEBUG
			ImGui.BulletText($"TerritoryType={ZoneWatcher.CurrentTerritoryType?.RowId ?? 0}");
			ImGui.BulletText(
				$"Map data: RowId={map.Value.RowId}, SizeFactor={map.Value.SizeFactor}, "
					+ $"OffsetX={map.Value.OffsetX}, OffsetY={map.Value.OffsetY}, OffsetZ={CompassUtil.GetCurrentTerritoryZOffset()}"
			);
			ImGui.BulletText(
				$"Main Viewport: pos={Dalamud.Interface.Utility.ImGuiHelpers.MainViewport.Pos}, "
					+ $"size={Dalamud.Interface.Utility.ImGuiHelpers.MainViewport.Size}, dpi={Dalamud.Interface.Utility.ImGuiHelpers.MainViewport.DpiScale}"
			);
#endif
			if (ImGui.BeginTabBar("CompassesTabBar", ImGuiTabBarFlags.Reorderable))
			{
				foreach (var c in drawActions.Keys)
				{
					if (c.CompassEnabled && c.ShowDetail)
					{
						var name = c.GetType().Name;
						name = name[..^"Compass".Length];
						if (ImGui.BeginTabItem(name))
						{
							drawActions[c].DoAll();
							ImGui.EndTabItem();
						}
					}
				}
				ImGui.EndTabBar();
			}
		}
		ImGui.End();
	}

	public void Clear()
	{
		foreach (var q in drawActions.Values)
			q.Clear();
	}
}
