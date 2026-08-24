using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal sealed class UnstayedMastMarker : MonoBehaviour
    {
        internal UnstayedMastSourceIdentity Identity;
    }

    internal sealed class EmptyRestrictedPartMarker : MonoBehaviour
    {
    }

    internal sealed class UnstayedBoatMarker : MonoBehaviour
    {
        private void OnDestroy()
        {
            BoatCustomParts parts = GetComponent<BoatCustomParts>();
            if (parts != null)
            {
                UnstayedBoatRegistry.Unregister(parts);
            }
        }
    }

    internal enum RestrictedPartKind
    {
        Rigging,
        RiggingAccessory,
        CrowsNest
    }

    internal sealed class RestrictedPartSelection
    {
        internal BoatPart Part;
        internal List<BoatPart> OwningMastParts;
        internal BoatPartOption EmptyOption;
        internal List<BoatPartOption> NonEmptyOptions;
        internal RestrictedPartKind Kind;
    }

    internal sealed class UnstayedMastProfile
    {
        internal BoatPart MastPart;
        internal BoatPartOption UnstayedOption;
        internal UnstayedMastMarker Marker;
        internal bool UsesFixedVanillaIndex;
        internal List<RestrictedPartSelection> RestrictedSelections;
    }

    internal sealed class UnstayedBoatProfile
    {
        internal BoatCustomParts Parts;
        internal BoatRefs Refs;
        internal int SceneIndex;
        internal List<UnstayedMastProfile> Masts;
        internal List<int> RetiredMastIndices;
    }

    internal static class UnstayedBoatRegistry
    {
        private static readonly Dictionary<BoatCustomParts, UnstayedBoatProfile>
            Profiles = new Dictionary<BoatCustomParts, UnstayedBoatProfile>();

        internal static void Register(UnstayedBoatProfile profile)
        {
            Profiles[profile.Parts] = profile;
        }

        internal static bool TryGet(
            BoatCustomParts parts,
            out UnstayedBoatProfile profile)
        {
            if (parts == null)
            {
                profile = null;
                return false;
            }

            return Profiles.TryGetValue(parts, out profile);
        }

        internal static void Unregister(BoatCustomParts parts)
        {
            Profiles.Remove(parts);
        }

        internal static List<UnstayedBoatProfile> GetProfiles()
        {
            return new List<UnstayedBoatProfile>(Profiles.Values);
        }

        internal static void Clear()
        {
            Profiles.Clear();
        }
    }
}
