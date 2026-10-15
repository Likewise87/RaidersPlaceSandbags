using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SandbagSpawner
{
    /// <summary>
    /// Preview which ranged weapon defs match a weapon filter's Kind/Pattern (contains).
    /// Local copy of Aimbot matching-weapons UI; no Aimbot dependency.
    /// </summary>
    public class Window_MatchingWeapons : Window
    {
        private const float RowHeightMin = 40f;
        private const float RowHeightMax = 120f;
        private const float HeaderHeight = 28f;
        private const float TitleHeight = 32f;
        private const float SubtitleHeight = 22f;
        private const float ColIcon = 48f;
        private const float ColLabel = 220f;
        private const float ColDefName = 240f;
        private const float ColTagsMin = 280f;
        private const float TagsPad = 4f;

        private readonly WeaponFilter filter;
        private readonly string filterDisplayName;
        private List<ThingDef> rows = new List<ThingDef>();
        private Vector2 scrollPos;
        private string sortColumn = "Label";
        private bool sortAscending = true;
        private string filterLabel = "";
        private string filterDefName = "";
        private string filterTags = "";
        private float lastScrollViewportHeight = 400f;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1100f, UI.screenWidth * 0.9f),
            Mathf.Min(720f, UI.screenHeight * 0.85f));

        public Window_MatchingWeapons(WeaponFilter filter, string filterDisplayName)
        {
            this.filter = filter;
            this.filterDisplayName = string.IsNullOrWhiteSpace(filterDisplayName) ? "Filter" : filterDisplayName;
            doCloseX = true;
            closeOnCancel = true;
            draggable = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            RebuildRows();
        }

        public override void PreClose()
        {
            WeaponTableHeaderFilter.CloseDropdown();
            base.PreClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (WeaponTableHeaderFilter.TryCloseOnCancel())
                return;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, TitleHeight),
                $"Matching weapons: {filterDisplayName}");

            var visible = GetVisibleRows();
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            string pattern = filter?.Pattern ?? "";
            string subtitle;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                subtitle = "No pattern set. Empty filters never match at runtime.";
            }
            else if (HasAnyColumnFilter())
            {
                subtitle = $"{filter!.Kind} / Contains / \"{pattern}\"  ·  {visible.Count} / {rows.Count} weapon(s)";
            }
            else
            {
                subtitle = $"{filter!.Kind} / Contains / \"{pattern}\"  ·  {rows.Count} weapon(s)";
            }
            Widgets.Label(new Rect(0f, TitleHeight, inRect.width, SubtitleHeight), subtitle);
            GUI.color = Color.white;

            float headerTop = TitleHeight + SubtitleHeight + 8f;
            float listTop = headerTop + HeaderHeight + 4f;
            float tableHeight = inRect.height - listTop - 8f;

            float tableWidth = Mathf.Max(inRect.width - 16f, ColIcon + ColLabel + ColDefName + ColTagsMin);
            DrawTableHeader(new Rect(0f, headerTop, tableWidth, HeaderHeight));
            Widgets.DrawLineHorizontal(0f, headerTop + HeaderHeight, inRect.width);

            float tagsColW = Mathf.Max(ColTagsMin, tableWidth - (ColIcon + ColLabel + ColDefName));
            float totalHeight = 8f;
            for (int i = 0; i < visible.Count; i++)
                totalHeight += RowHeightFor(visible[i], tagsColW);

            Rect viewRect = new Rect(0f, 0f, tableWidth, Mathf.Max(totalHeight, tableHeight));
            Rect scrollOuter = new Rect(0f, listTop, inRect.width, tableHeight);
            lastScrollViewportHeight = scrollOuter.height;

            Widgets.BeginScrollView(scrollOuter, ref scrollPos, viewRect);
            float y = 0f;
            for (int i = 0; i < visible.Count; i++)
            {
                float rowH = RowHeightFor(visible[i], tagsColW);
                DrawRow(0f, y, tableWidth, rowH, tagsColW, visible[i], i % 2 == 0);
                y += rowH;
            }
            Widgets.EndScrollView();

            if (visible.Count == 0)
            {
                Text.Font = GameFont.Small;
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                string emptyMsg;
                if (string.IsNullOrWhiteSpace(filter?.Pattern))
                    emptyMsg = "Enter a pattern on the filter to preview matches.";
                else if (HasAnyColumnFilter())
                    emptyMsg = "No weapons match the current column filters.";
                else
                    emptyMsg = "No ranged weapons match this filter.";
                Widgets.Label(scrollOuter, emptyMsg);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            WeaponTableHeaderFilter.DrawDropdownIfOpen();
        }

        private bool HasAnyColumnFilter() =>
            !string.IsNullOrWhiteSpace(filterLabel)
            || !string.IsNullOrWhiteSpace(filterDefName)
            || !string.IsNullOrWhiteSpace(filterTags);

        private void RebuildRows()
        {
            rows = WeaponMatchHelper.CollectMatchingWeaponDefs(filter);
            ApplySort();
        }

        private List<ThingDef> GetVisibleRows()
        {
            string qLabel = string.IsNullOrWhiteSpace(filterLabel) ? null : filterLabel.Trim().ToLowerInvariant();
            string qDef = string.IsNullOrWhiteSpace(filterDefName) ? null : filterDefName.Trim().ToLowerInvariant();
            string qTags = string.IsNullOrWhiteSpace(filterTags) ? null : filterTags.Trim().ToLowerInvariant();
            if (qLabel == null && qDef == null && qTags == null) return rows;

            return rows.Where(d =>
            {
                if (qLabel != null)
                {
                    string label = (d.label ?? "").ToLowerInvariant();
                    string cap = d.LabelCap.ToString().ToLowerInvariant();
                    if (!label.Contains(qLabel) && !cap.Contains(qLabel)) return false;
                }
                if (qDef != null && !(d.defName ?? "").ToLowerInvariant().Contains(qDef))
                    return false;
                if (qTags != null)
                {
                    if (d.weaponTags == null || d.weaponTags.Count == 0)
                    {
                        if (!"(none)".Contains(qTags)) return false;
                    }
                    else
                    {
                        bool any = false;
                        for (int i = 0; i < d.weaponTags.Count; i++)
                        {
                            if ((d.weaponTags[i] ?? "").ToLowerInvariant().Contains(qTags))
                            {
                                any = true;
                                break;
                            }
                        }
                        if (!any) return false;
                    }
                }
                return true;
            }).ToList();
        }

        private void ApplySort()
        {
            int dir = sortAscending ? 1 : -1;
            rows.Sort((a, b) =>
            {
                int cmp;
                switch (sortColumn)
                {
                    case "DefName":
                        cmp = string.Compare(a.defName, b.defName, StringComparison.OrdinalIgnoreCase);
                        break;
                    case "Tags":
                        cmp = string.Compare(FormatTags(a), FormatTags(b), StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        cmp = string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
                        break;
                }
                return cmp * dir;
            });
        }

        private void SetSort(string col)
        {
            if (sortColumn == col) sortAscending = !sortAscending;
            else { sortColumn = col; sortAscending = true; }
            ApplySort();
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        private void DrawTableHeader(Rect hRect)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            float curX = hRect.x;
            curX += ColIcon;

            WeaponTableHeaderFilter.DrawFilterableHeader(
                ref curX, hRect.y, ColLabel, HeaderHeight,
                "Label",
                sortColumn == "Label", sortAscending,
                !string.IsNullOrWhiteSpace(filterLabel),
                "Filter by label",
                icon => WeaponTableHeaderFilter.OpenTextDropdown(
                    icon,
                    "Filter by label",
                    "Contains...",
                    () => filterLabel,
                    v => filterLabel = v ?? "",
                    () => filterLabel = ""),
                () => SetSort("Label"));

            WeaponTableHeaderFilter.DrawFilterableHeader(
                ref curX, hRect.y, ColDefName, HeaderHeight,
                "defName",
                sortColumn == "DefName", sortAscending,
                !string.IsNullOrWhiteSpace(filterDefName),
                "Filter by defName",
                icon => WeaponTableHeaderFilter.OpenTextDropdown(
                    icon,
                    "Filter by defName",
                    "Contains...",
                    () => filterDefName,
                    v => filterDefName = v ?? "",
                    () => filterDefName = ""),
                () => SetSort("DefName"));

            float tagsW = Mathf.Max(ColTagsMin, hRect.width - (ColIcon + ColLabel + ColDefName));
            WeaponTableHeaderFilter.DrawFilterableHeader(
                ref curX, hRect.y, tagsW, HeaderHeight,
                "Tags",
                sortColumn == "Tags", sortAscending,
                !string.IsNullOrWhiteSpace(filterTags),
                "Filter by tag",
                icon => WeaponTableHeaderFilter.OpenTextDropdown(
                    icon,
                    "Filter by tag",
                    "Contains...",
                    () => filterTags,
                    v => filterTags = v ?? "",
                    () => filterTags = ""),
                () => SetSort("Tags"));

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private float RowHeightFor(ThingDef def, float tagsColW)
        {
            string tags = FormatTags(def);
            Text.Font = GameFont.Tiny;
            float textH = Text.CalcHeight(tags, Mathf.Max(40f, tagsColW - TagsPad * 2f));
            Text.Font = GameFont.Small;
            return Mathf.Clamp(textH + TagsPad * 2f, RowHeightMin, RowHeightMax);
        }

        private void DrawRow(float x, float y, float width, float rowH, float tagsColW, ThingDef def, bool zebra)
        {
            float visibleY = scrollPos.y - rowH;
            float visibleYMax = scrollPos.y + lastScrollViewportHeight;
            if (y < visibleY || y >= visibleYMax)
                return;

            Rect row = new Rect(x, y, width, rowH);
            if (zebra) Widgets.DrawHighlight(row);
            if (Mouse.IsOver(row)) Widgets.DrawLightHighlight(row);

            float curX = x;

            Rect iconCell = new Rect(curX, y, ColIcon, rowH);
            float iconSize = 32f;
            Rect iconRect = new Rect(
                iconCell.x + (ColIcon - iconSize) * 0.5f,
                y + (rowH - iconSize) * 0.5f,
                iconSize,
                iconSize);
            Texture2D tex = def.uiIcon;
            if (tex != null)
                GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
            else
                Widgets.DrawBoxSolid(iconRect, new Color(0.3f, 0.3f, 0.35f, 1f));
            if (Widgets.ButtonInvisible(iconCell))
                Find.WindowStack.Add(new Dialog_InfoCard(def));
            curX += ColIcon;

            Rect labelRect = new Rect(curX, y, ColLabel, rowH);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string labelCap = def.LabelCap.ToString();
            if (string.IsNullOrEmpty(labelCap)) labelCap = def.label ?? def.defName ?? "";
            Widgets.Label(labelRect, labelCap.Truncate(ColLabel - 4f));
            TooltipHandler.TipRegion(labelRect, labelCap);
            if (Widgets.ButtonInvisible(labelRect))
                Find.WindowStack.Add(new Dialog_InfoCard(def));
            curX += ColLabel;

            Rect defRect = new Rect(curX, y, ColDefName, rowH);
            Widgets.Label(defRect, (def.defName ?? "").Truncate(ColDefName - 4f));
            TooltipHandler.TipRegion(defRect, def.defName);
            curX += ColDefName;

            Rect tagsRect = new Rect(curX + TagsPad, y + TagsPad, tagsColW - TagsPad * 2f, rowH - TagsPad * 2f);
            string tags = FormatTags(def);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            bool wordWrapPrev = Text.WordWrap;
            Text.WordWrap = true;
            Widgets.Label(tagsRect, tags);
            Text.WordWrap = wordWrapPrev;
            Text.Font = GameFont.Small;
            if (!string.IsNullOrEmpty(tags))
                TooltipHandler.TipRegion(tagsRect, tags);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static string FormatTags(ThingDef def)
        {
            if (def?.weaponTags == null || def.weaponTags.Count == 0) return "(none)";
            var sb = new StringBuilder();
            for (int i = 0; i < def.weaponTags.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(def.weaponTags[i] ?? "");
            }
            return sb.ToString();
        }
    }
}
