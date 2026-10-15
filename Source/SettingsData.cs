using System.Collections.Generic;
using Verse;

namespace SandbagSpawner
{
    public enum TargetKind : byte { DefName, Tag, Label }

    public class SandbagSettings : ModSettings
    {
        public float CooldownSeconds = 60f;
        public float LifespanSeconds = 120f;
        public bool ApplyToNeutral = true;
        public bool ApplyToAllied = true;
        public List<WeaponFilter> Filters = new List<WeaponFilter>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CooldownSeconds, "CooldownSeconds", 60f);
            Scribe_Values.Look(ref LifespanSeconds, "LifespanSeconds", 120f);
            Scribe_Values.Look(ref ApplyToNeutral, "ApplyToNeutral", true);
            Scribe_Values.Look(ref ApplyToAllied, "ApplyToAllied", true);
            Scribe_Collections.Look(ref Filters, "Filters", LookMode.Deep);

            if (Filters == null) Filters = new List<WeaponFilter>();
        }

        public void ResetToDefault()
        {
            CooldownSeconds = 60f;
            LifespanSeconds = 120f;
            ApplyToNeutral = true;
            ApplyToAllied = true;
            Filters = new List<WeaponFilter>
            {
                new WeaponFilter { Pattern = "Gun_LMG", Kind = TargetKind.DefName },
                new WeaponFilter { Pattern = "LMG", Kind = TargetKind.Label }
            };
        }
    }

    public class WeaponFilter : IExposable
    {
        public string Pattern = "LMG";
        public TargetKind Kind = TargetKind.DefName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Pattern, "Pattern", "LMG");
            Scribe_Values.Look(ref Kind, "Kind", TargetKind.DefName);
        }
    }
}
