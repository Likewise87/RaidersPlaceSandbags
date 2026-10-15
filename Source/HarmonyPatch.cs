using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace SandbagSpawner
{
    [StaticConstructorOnStartup]
    public static class Patch_Verb_Shoot_SandbagLogic
    {
        private static readonly Dictionary<int, int> cooldownCache = new Dictionary<int, int>();
        private static readonly Dictionary<Thing, int> cleanupQueue = new Dictionary<Thing, int>();
        private static readonly List<Thing> removalList = new List<Thing>();
        private const int CleanupInterval = 600;

        static Patch_Verb_Shoot_SandbagLogic()
        {
            var harmony = new Harmony("TSA.RaidersPlaceSandbags");

            // Vanilla: Verb_Shoot.TryCastShot (not CE TryCastNextBurstShot)
            var targetMethod = AccessTools.Method(typeof(Verb_Shoot), "TryCastShot");

            if (targetMethod != null)
            {
                harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(Patch_Verb_Shoot_SandbagLogic), nameof(UniversalPostfix)));
            }

            harmony.Patch(AccessTools.Method(typeof(Map), nameof(Map.MapPostTick)),
                postfix: new HarmonyMethod(typeof(Patch_Verb_Shoot_SandbagLogic), nameof(OptimizedTickCleanup)));

            harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.DeinitAndRemoveMap)),
                postfix: new HarmonyMethod(typeof(Patch_Verb_Shoot_SandbagLogic), nameof(ClearCachesForMap)));
        }

        /// <summary>
        /// Prune queue for the map being removed only — do not wipe other maps' sandbags.
        /// </summary>
        public static void ClearCachesForMap(Map map)
        {
            removalList.Clear();
            foreach (var kvp in cleanupQueue)
            {
                var t = kvp.Key;
                if (t == null || !t.Spawned || t.Map == map)
                    removalList.Add(t);
            }
            foreach (var item in removalList)
                cleanupQueue.Remove(item);

            cooldownCache.Clear();
            removalList.Clear();
        }

        public static void OptimizedTickCleanup(Map __instance)
        {
            if (Find.TickManager.TicksGame % CleanupInterval != 0 || cleanupQueue.Count == 0) return;
            int currentTick = Find.TickManager.TicksGame;
            removalList.Clear();

            foreach (var kvp in cleanupQueue)
            {
                var t = kvp.Key;
                if (t == null || !t.Spawned)
                {
                    removalList.Add(t);
                    continue;
                }

                if (currentTick < kvp.Value)
                    continue;

                // Only destroy/dequeue for this map; leave other maps' expired entries alone.
                if (t.Map != __instance)
                    continue;

                t.Destroy(DestroyMode.Vanish);
                removalList.Add(t);
            }

            for (int i = 0; i < removalList.Count; i++)
                cleanupQueue.Remove(removalList[i]);
        }

        public static void UniversalPostfix(Verb __instance, bool __result)
        {
            if (!__result) return;

            Pawn pawn = __instance.CasterPawn;
            // NPC factions only (never player). Hostile always; Neutral/Allied via settings.
            if (pawn == null || pawn.Faction == null || !pawn.Spawned) return;
            if (pawn.Faction.IsPlayer) return;
            if (!IsAllowedFactionRelation(pawn.Faction)) return;

            var weapon = __instance.EquipmentSource;
            if (weapon == null || !IsAllowedWeapon(weapon)) return;

            int cooldownTicks = Mathf.RoundToInt(SandbagMod.Settings.CooldownSeconds * 60f);
            int ticks = Find.TickManager.TicksGame;

            if (cooldownCache.TryGetValue(pawn.thingIDNumber, out int lastTick) && ticks < lastTick + cooldownTicks)
                return;

            if (SpawnSandbags(pawn, __instance.CurrentTarget.Cell))
                cooldownCache[pawn.thingIDNumber] = ticks;
        }

        private static bool IsAllowedFactionRelation(Faction faction)
        {
            var settings = SandbagMod.Settings;
            switch (faction.RelationKindWith(Faction.OfPlayer))
            {
                case FactionRelationKind.Hostile:
                    return true;
                case FactionRelationKind.Neutral:
                    return settings == null || settings.ApplyToNeutral;
                case FactionRelationKind.Ally:
                    return settings == null || settings.ApplyToAllied;
                default:
                    return false;
            }
        }

        private static bool IsAllowedWeapon(ThingWithComps weapon)
        {
            return WeaponMatchHelper.IsAllowedWeapon(weapon, SandbagMod.Settings?.Filters);
        }

        /// <summary>
        /// All-or-nothing: if any cell is invalid, spawn nothing (do not port CE partial spawn).
        /// </summary>
        private static bool SpawnSandbags(Pawn pawn, IntVec3 targetCell)
        {
            Map map = pawn.Map;
            IntVec3 shooterPos = pawn.Position;
            Vector3 dirVec = (targetCell - shooterPos).ToVector3();
            if (dirVec.magnitude < 1.5f) return false;

            dirVec.Normalize();
            IntVec3 forward = new IntVec3(Mathf.RoundToInt(dirVec.x), 0, Mathf.RoundToInt(dirVec.z));
            if (forward == IntVec3.Zero) return false;

            IntVec3 sideOffset = new IntVec3(-forward.z, 0, forward.x);
            IntVec3[] positions = { shooterPos + forward, shooterPos + forward + sideOffset, shooterPos + forward - sideOffset };

            for (int i = 0; i < 3; i++)
            {
                IntVec3 pos = positions[i];
                if (!pos.InBounds(map) || pos.GetEdifice(map) != null || pos.GetFirstItem(map) != null) return false;
                if (pos.GetThingList(map).Any(t => t is Pawn)) return false;

                TerrainDef terrain = pos.GetTerrain(map);
                if (ThingDefOf.Sandbags.terrainAffordanceNeeded != null && (terrain.affordances == null || !terrain.affordances.Contains(ThingDefOf.Sandbags.terrainAffordanceNeeded)))
                    return false;
            }

            int deathTick = Find.TickManager.TicksGame + Mathf.RoundToInt(SandbagMod.Settings.LifespanSeconds * 60f);

            for (int i = 0; i < 3; i++)
            {
                Thing sandbag = ThingMaker.MakeThing(ThingDefOf.Sandbags, ThingDefOf.Cloth);
                GenSpawn.Spawn(sandbag, positions[i], map);
                sandbag.SetFaction(pawn.Faction);
                cleanupQueue[sandbag] = deathTick;
                FleckMaker.ThrowDustPuff(positions[i].ToVector3Shifted(), map, 1.2f);
            }
            return true;
        }
    }
}
