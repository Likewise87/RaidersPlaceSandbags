using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SandbagSpawner
{
    /// <summary>Column filter icon + text dropdown (local copy; no Aimbot/WD dependency).</summary>
    [StaticConstructorOnStartup]
    internal static class WeaponTableHeaderFilter
    {
        public const float FilterIconSize = 22f;
        private const float Pad = 10f;
        private const float TitleH = 24f;
        private const float FieldH = 28f;
        private const float ClearBtnH = 28f;
        private const float DefaultDropdownW = 260f;
        private const int DropdownWindowId = 819203472;
        private const string TextControlName = "VanillaSandbagWeaponHeaderFilter_Text";

        private static readonly Texture2D FilterIconTex =
            ContentFinder<Texture2D>.Get("UI/Buttons/OpenSpecificTab", false)
            ?? TexButton.Search
            ?? TexButton.Info;

        private static readonly Color ActiveFilterTint = new Color(0.45f, 0.85f, 1f);
        private static readonly Color IdleFilterTint = new Color(0.85f, 0.85f, 0.85f);

        private static bool dropdownOpen;
        private static Rect dropdownScreenRect;
        private static Action<Rect> dropdownDraw;
        private static bool focusTextNext;
        private static int openedOnFrame = -1;

        public static void CloseDropdown()
        {
            dropdownOpen = false;
            dropdownDraw = null;
            focusTextNext = false;
        }

        public static bool TryCloseOnCancel()
        {
            if (!dropdownOpen) return false;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                CloseDropdown();
                Event.current.Use();
                return true;
            }
            return false;
        }

        public static void DrawDropdownIfOpen()
        {
            if (!dropdownOpen || dropdownDraw == null) return;
            Rect r = dropdownScreenRect;
            Find.WindowStack.ImmediateWindow(
                DropdownWindowId,
                r,
                WindowLayer.Super,
                () => dropdownDraw?.Invoke(r.AtZero()),
                doBackground: true,
                absorbInputAroundWindow: true,
                shadowAlpha: 1f,
                doClickOutsideFunc: () =>
                {
                    if (Time.frameCount <= openedOnFrame) return;
                    CloseDropdown();
                });
        }

        public static void DrawFilterableHeader(
            ref float curX,
            float y,
            float width,
            float height,
            string label,
            bool isSorted,
            bool sortAscending,
            bool filterActive,
            string filterTip,
            Action<Rect> onFilterClick,
            Action onSort)
        {
            Rect headerRect = new Rect(curX, y, width, height);
            const float gap = 2f;
            const float edgePad = 2f;

            float icon = Mathf.Min(FilterIconSize, height - 2f);
            float iconY = y + (height - icon) * 0.5f;

            string arrow = isSorted ? (sortAscending ? " ▲" : " ▼") : "";
            string headerText = label + arrow;
            float maxTextW = Mathf.Max(8f, width - icon - gap - edgePad * 2f);
            float textW = Text.CalcSize(headerText).x;
            if (textW > maxTextW)
            {
                headerText = label.Truncate(Mathf.Max(8f, maxTextW - 16f)) + arrow;
                textW = Mathf.Min(maxTextW, Text.CalcSize(headerText).x);
            }

            float clusterW = textW + gap + icon;
            float clusterX = headerRect.x + edgePad;
            Rect labelRect = new Rect(clusterX, y, textW, height);
            Rect iconRect = new Rect(clusterX + textW + gap, iconY, icon, icon);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, headerText);
            Text.Anchor = TextAnchor.UpperLeft;

            bool overIcon = Mouse.IsOver(iconRect);
            if (onSort != null && Mouse.IsOver(headerRect) && !overIcon)
                Widgets.DrawHighlight(headerRect);
            if (onSort != null && !overIcon && Widgets.ButtonInvisible(headerRect))
                onSort.Invoke();

            if (Mouse.IsOver(iconRect)) Widgets.DrawHighlight(iconRect);
            Color prev = GUI.color;
            GUI.color = filterActive ? ActiveFilterTint : IdleFilterTint;
            if (FilterIconTex != null)
                GUI.DrawTexture(iconRect, FilterIconTex, ScaleMode.ScaleToFit);
            GUI.color = prev;
            if (!filterTip.NullOrEmpty())
                TooltipHandler.TipRegion(iconRect, filterTip);
            if (Widgets.ButtonInvisible(iconRect))
            {
                onFilterClick?.Invoke(iconRect);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            curX += width;
        }

        public static void OpenTextDropdown(
            Rect guiAnchor,
            string title,
            string hint,
            Func<string> get,
            Action<string> onChanged,
            Action onCleared,
            float width = DefaultDropdownW)
        {
            float h = Pad * 2f + TitleH + 12f + FieldH + 6f + ClearBtnH;
            Open(guiAnchor, width, h, inner =>
            {
                float y = DrawDropdownTitle(inner, title);
                Rect field = new Rect(inner.x, y, inner.width, FieldH);
                DrawSearchField(field, hint, get?.Invoke() ?? "", onChanged);
                Rect clear = new Rect(inner.x, field.yMax + 6f, inner.width, ClearBtnH);
                if (Widgets.ButtonText(clear, "Clear"))
                {
                    onCleared?.Invoke();
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            });
        }

        private static float DrawDropdownTitle(Rect inner, string title)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, TitleH), title ?? "");
            Text.Anchor = TextAnchor.UpperLeft;
            float lineY = inner.y + TitleH + 2f;
            Widgets.DrawLineHorizontal(inner.x, lineY, inner.width);
            return lineY + 8f;
        }

        private static Rect GuiRectToScreen(Rect gui)
        {
            Vector2 topLeft = UI.GUIToScreenPoint(new Vector2(gui.x, gui.y));
            Vector2 bottomRight = UI.GUIToScreenPoint(new Vector2(gui.xMax, gui.yMax));
            return Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
        }

        private static void Open(Rect guiAnchor, float width, float height, Action<Rect> draw)
        {
            Rect iconScreen = GuiRectToScreen(guiAnchor);
            float winW = width;
            float winH = height;
            float x = iconScreen.xMax - winW;
            float y = iconScreen.yMax;
            if (x < 8f) x = 8f;
            if (x + winW > UI.screenWidth - 8f)
                x = UI.screenWidth - winW - 8f;
            if (y + winH > UI.screenHeight - 8f)
                y = iconScreen.y - winH;
            if (y < 8f) y = 8f;

            dropdownScreenRect = new Rect(x, y, winW, winH);
            dropdownDraw = win =>
            {
                Rect inner = win.ContractedBy(Pad);
                draw?.Invoke(inner);
            };
            dropdownOpen = true;
            focusTextNext = true;
            openedOnFrame = Time.frameCount;
        }

        private static void DrawSearchField(Rect rect, string hint, string current, Action<string> onChanged)
        {
            string old = current ?? "";
            if (focusTextNext)
            {
                GUI.FocusControl(TextControlName);
                if (Event.current.type == EventType.Repaint)
                    focusTextNext = false;
            }
            GUI.SetNextControlName(TextControlName);
            Text.Font = GameFont.Small;
            string next = Widgets.TextField(rect, old);
            if (string.IsNullOrEmpty(next) && !hint.NullOrEmpty())
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Tiny;
                Widgets.Label(rect, "  " + hint);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            if (next != old)
                onChanged?.Invoke(next);
        }
    }
}
