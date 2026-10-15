using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SandbagSpawner
{
    /// <summary>Shared weapon-filter matching for runtime spawn + settings preview.</summary>
    public static class WeaponMatchHelper
    {
        public static bool FilterMatchesDef(WeaponFilter filter, ThingDef def)
        {
            if (filter == null || def == null || string.IsNullOrEmpty(filter.Pattern)) return false;

            string pattern = filter.Pattern;
            if (filter.Kind == TargetKind.DefName)
                return def.defName != null && def.defName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
            if (filter.Kind == TargetKind.Label)
                return def.label != null && def.label.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
            if (filter.Kind == TargetKind.Tag)
                return def.weaponTags != null && def.weaponTags.Any(t => t != null && t.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
            return false;
        }

        public static bool IsAllowedWeapon(ThingWithComps weapon, List<WeaponFilter> filters)
        {
            if (weapon?.def == null || filters == null || filters.Count == 0) return false;
            for (int i = 0; i < filters.Count; i++)
            {
                if (FilterMatchesDef(filters[i], weapon.def)) return true;
            }
            return false;
        }

        public static bool LooksLikeRangedWeaponDef(ThingDef def)
        {
            if (def == null) return false;
            try
            {
                if (def.IsRangedWeapon) return true;
            }
            catch { /* forks */ }

            if (def.Verbs == null) return false;
            foreach (var vp in def.Verbs)
            {
                if (vp != null && !vp.IsMeleeAttack && vp.range > 1f) return true;
            }
            return false;
        }

        /// <summary>All ranged weapon defs that match this filter's Kind/Pattern (contains).</summary>
        public static List<ThingDef> CollectMatchingWeaponDefs(WeaponFilter filter)
        {
            var result = new List<ThingDef>();
            if (filter == null || string.IsNullOrWhiteSpace(filter.Pattern)) return result;

            var all = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (!LooksLikeRangedWeaponDef(def)) continue;
                if (!FilterMatchesDef(filter, def)) continue;
                result.Add(def);
            }

            result.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }
}
