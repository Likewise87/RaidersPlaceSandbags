using System;
using UnityEngine;
using Verse;

namespace SandbagSpawner
{
    /// <summary>Aimbot-style settings chrome (local copy; no Aimbot dependency).</summary>
    public static class SettingsUiUtil
    {
        public static readonly Color SectionHeaderColor = new Color(0.55f, 0.85f, 1f);

        public static bool DrawCollapsibleHeader(Listing_Standard l, string label, ref bool expanded, Color? color = null, string? tip = null)
        {
            return DrawCollapsibleHeader(l, label, ref expanded, color, tip, null, null, null, null);
        }

        /// <summary>
        /// Collapsible section header. Optional trailing buttons are drawn on the right
        /// (secondary then red primary, e.g. Add filter + Reset to defaults).
        /// </summary>
        public static bool DrawCollapsibleHeader(
            Listing_Standard l,
            string label,
            ref bool expanded,
            Color? color,
            string? tip,
            string? secondaryRightLabel,
            Action? onSecondaryRight,
            string? redRightLabel,
            Action? onRedRight)
        {
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;

            l.Gap(8f);
            l.GapLine(6f);
            l.Gap(4f);
            Rect r = l.GetRect(26f);

            bool hasSecondary = !secondaryRightLabel.NullOrEmpty() && onSecondaryRight != null;
            bool hasRed = !redRightLabel.NullOrEmpty() && onRedRight != null;

            float btnH = r.height;
            float redW = hasRed ? 150f : 0f;
            float secondaryW = hasSecondary ? 100f : 0f;
            float gap = 8f;
            float rightBudget = 0f;
            if (hasRed) rightBudget += redW;
            if (hasSecondary) rightBudget += (rightBudget > 0f ? gap : 0f) + secondaryW;

            float labelMax = r.width - (rightBudget > 0f ? rightBudget + gap : 0f);
            Rect labelRect = new Rect(r.x, r.y, Mathf.Max(80f, labelMax), r.height);

            Widgets.DrawHighlightIfMouseover(labelRect);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(labelRect, tip);
            if (Widgets.ButtonInvisible(labelRect)) expanded = !expanded;

            Color c = color ?? Color.white;
            string arrow = expanded ? "▼" : "▶";
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, $"<b><color=#{ColorUtility.ToHtmlStringRGBA(c)}>{arrow}  {label}</color></b>");
            Text.Anchor = TextAnchor.UpperLeft;

            float x = r.xMax;
            if (hasRed)
            {
                x -= redW;
                DrawRedButton(new Rect(x, r.y, redW, btnH), redRightLabel!, onRedRight!);
                x -= gap;
            }
            if (hasSecondary)
            {
                x -= secondaryW;
                if (Widgets.ButtonText(new Rect(x, r.y, secondaryW, btnH), secondaryRightLabel))
                    onSecondaryRight!();
            }

            l.Gap(4f);

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            return expanded;
        }

        public static void DrawRedButton(Rect rect, string label, Action onClick)
        {
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(rect, label)) onClick();
            GUI.color = prev;
        }

        public static void DrawLabeledCheckbox(Listing_Standard l, string label, ref bool value, string? tip = null)
        {
            Rect row = l.GetRect(24f);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(row, tip);
            Widgets.Label(row, label);
            Rect checkRect = new Rect(row.xMax - 45f, row.y, 24f, 24f);
            Widgets.Checkbox(checkRect.position, ref value);
        }

        /// <summary>Label + cyan value on one row, then a full-width slider.</summary>
        public static float DrawLabeledFloatSlider(Listing_Standard l, string label, float value, float min, float max, string? tip = null, string unit = "s")
        {
            Rect labelRect = l.GetRect(24f);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(labelRect, tip);
            Widgets.Label(labelRect, $"{label}: {value.ToString("0").Colorize(Color.cyan)}{unit}");
            return l.Slider(value, min, max);
        }
    }
}
