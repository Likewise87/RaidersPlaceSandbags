using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SandbagSpawner
{
    public class SandbagMod : Mod
    {
        public static SandbagSettings Settings;

        private Vector2 scroll;
        private bool scrollToBottom;
        private bool generalExpanded = true;
        private bool filtersExpanded = true;

        private const float DeleteIconSize = 24f;
        private const float FilterRowH = 28f;
        private const float KindBtnW = 100f;

        public SandbagMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SandbagSettings>();
            if (Settings.Filters.Count == 0) Settings.ResetToDefault();
        }

        public override string SettingsCategory() => "RaidersPlaceSandbags";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float contentH = CalculateContentHeight();
            var view = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(inRect.height, contentH + 40f));

            if (scrollToBottom)
            {
                scroll.y = Mathf.Max(0f, view.height - inRect.height + 40f);
                scrollToBottom = false;
            }

            Widgets.BeginScrollView(inRect, ref scroll, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            if (SettingsUiUtil.DrawCollapsibleHeader(listing, "General", ref generalExpanded, SettingsUiUtil.SectionHeaderColor,
                "Cooldown, sandbag lifespan, and which NPC faction relations can place sandbags. Player pawns never place sandbags."))
            {
                Settings.CooldownSeconds = SettingsUiUtil.DrawLabeledFloatSlider(
                    listing,
                    "Cooldown to spawn sandbags again",
                    Settings.CooldownSeconds,
                    1f,
                    300f,
                    "The minimum time (in seconds) before an NPC can spawn another set of sandbags.");

                listing.Gap(6f);

                Settings.LifespanSeconds = SettingsUiUtil.DrawLabeledFloatSlider(
                    listing,
                    "Sandbag removal timer",
                    Settings.LifespanSeconds,
                    10f,
                    600f,
                    "How long spawned sandbags remain on the map before being removed.");

                listing.Gap(6f);

                SettingsUiUtil.DrawLabeledCheckbox(
                    listing,
                    "Apply to Neutral too",
                    ref Settings.ApplyToNeutral,
                    "When on, neutral NPC factions can place sandbags. Hostile NPCs always can. Player pawns never can.");

                SettingsUiUtil.DrawLabeledCheckbox(
                    listing,
                    "Apply to Allied too",
                    ref Settings.ApplyToAllied,
                    "When on, allied NPC factions can place sandbags. Hostile NPCs always can. Player pawns never can.");

                listing.Gap(4f);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(listing.GetRect(36f),
                    "NPCs place temporary sandbags when shooting a weapon that matches any filter below (contains match on DefName, Label, or Tag).");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            if (SettingsUiUtil.DrawCollapsibleHeader(
                listing,
                "Weapon filters",
                ref filtersExpanded,
                SettingsUiUtil.SectionHeaderColor,
                "Which weapons trigger sandbag placement. Use the eye icon to preview matching ranged weapons.",
                "Add filter",
                () =>
                {
                    Settings.Filters.Add(new WeaponFilter());
                    filtersExpanded = true;
                    scrollToBottom = true;
                },
                "Reset to defaults",
                () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Are you sure? This will restore cooldown, lifespan, faction toggles, and the default weapon filters.",
                        () =>
                        {
                            Settings.ResetToDefault();
                            generalExpanded = true;
                            filtersExpanded = true;
                            scroll.y = 0f;
                        },
                        true));
                }))
            {
                if (Settings.Filters.Count == 0)
                {
                    listing.Gap(4f);
                    GUI.color = Color.gray;
                    Widgets.Label(listing.GetRect(24f), "No filters. Add a filter or restore defaults.");
                    GUI.color = Color.white;
                }

                int removeAt = -1;
                for (int i = 0; i < Settings.Filters.Count; i++)
                {
                    if (DrawFilterRow(listing, Settings.Filters[i], i))
                        removeAt = i;
                }
                if (removeAt >= 0 && removeAt < Settings.Filters.Count)
                    Settings.Filters.RemoveAt(removeAt);
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private float CalculateContentHeight()
        {
            float h = 8f;
            h += 50f; // general header
            if (generalExpanded) h += 24f + 22f + 6f + 24f + 22f + 6f + 24f + 24f + 40f;
            h += 50f; // filters header
            if (filtersExpanded)
            {
                int n = Settings.Filters?.Count ?? 0;
                if (n == 0) h += 30f;
                else h += n * (FilterRowH + 8f) + 8f;
            }
            return h + 40f;
        }

        /// <returns>True if this filter should be deleted.</returns>
        private bool DrawFilterRow(Listing_Standard listing, WeaponFilter filter, int index)
        {
            if (filter == null) return false;

            listing.Gap(4f);
            Rect row = listing.GetRect(FilterRowH);

            var kindRect = new Rect(row.x, row.y, KindBtnW, row.height);
            if (Widgets.ButtonText(kindRect, filter.Kind.ToString()))
            {
                var opts = new List<FloatMenuOption>();
                foreach (TargetKind k in Enum.GetValues(typeof(TargetKind)))
                {
                    TargetKind captured = k;
                    opts.Add(new FloatMenuOption(captured.ToString(), () => filter.Kind = captured));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            TooltipHandler.TipRegion(kindRect, "Match against weapon DefName, Label, or Tag (contains).");

            float iconsRight = row.xMax;
            float delX = iconsRight - DeleteIconSize;
            float eyeX = delX - 8f - DeleteIconSize;
            float patternRight = eyeX - 8f;

            var patternRect = new Rect(kindRect.xMax + 8f, row.y, Mathf.Max(40f, patternRight - (kindRect.xMax + 8f)), row.height);
            TooltipHandler.TipRegion(patternRect, "Text to match (contains, case-insensitive).");
            filter.Pattern = Widgets.TextField(patternRect, filter.Pattern ?? "");

            float iconY = row.y + (row.height - DeleteIconSize) * 0.5f;
            var eyeRect = new Rect(eyeX, iconY, DeleteIconSize, DeleteIconSize);
            Texture2D viewIcon = SandbagTextures.ViewDetails ?? TexButton.Info;
            if (Widgets.ButtonImage(eyeRect, viewIcon))
            {
                string display = string.IsNullOrWhiteSpace(filter.Pattern)
                    ? $"Filter #{index + 1}"
                    : $"{filter.Kind}: {filter.Pattern}";
                Find.WindowStack.Add(new Window_MatchingWeapons(filter, display));
            }
            TooltipHandler.TipRegion(eyeRect,
                "Show matching weapons: lists ranged weapons from the def database that match this filter's Kind and Pattern.");

            var delRect = new Rect(delX, iconY, DeleteIconSize, DeleteIconSize);
            bool delete = Widgets.ButtonImage(delRect, TexButton.Delete);
            TooltipHandler.TipRegion(delRect, "Delete filter");
            return delete;
        }
    }
}
