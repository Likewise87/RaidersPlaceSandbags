using UnityEngine;
using Verse;

namespace SandbagSpawner
{
    /// <summary>ContentFinder textures loaded on the main thread at startup.</summary>
    [StaticConstructorOnStartup]
    internal static class SandbagTextures
    {
        public static readonly Texture2D ViewDetails =
            ContentFinder<Texture2D>.Get("UI/Commands/View_Details", false);
    }
}
