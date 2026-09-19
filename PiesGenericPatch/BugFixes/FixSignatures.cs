using HarmonyLib;
using UniverseLib;

namespace Pies_Generic_Patch.BugFixes
{
    /// <summary>
    /// In vanilla, |citizen.signature| resolves through GetReceiptDifficultyBaseNameInfo, so what a citizen
    /// signs varies with their conscientiousness - the same person is "F. Wellington", "FW", "Ford" or "F".
    /// This was accidently changed when obfuscated sales ledgers were introduced
    /// </summary>
    public class FixSignatures
    {
        [HarmonyPatch(typeof(Strings), nameof(Strings.GetContainedValue))]
        public class Strings_GetContainedValue
        {
            public static bool Prefix(ref string __result, string withinScope, string newValue, object inputObject, Evidence baseEvidence, Strings.LinkSetting linkSetting)
            {
                if (withinScope.ToLower() != "citizen" || newValue.ToLower() != "signature")
                    return true;

                __result = string.Empty;

                var human = inputObject.TryCast<Human>();

                if (human == null)
                    return false;

                var name = human.GetInitialledName();

                if (linkSetting == Strings.LinkSetting.forceLinks && baseEvidence != null)
                {
                    var link = Strings.AddOrGetLink(human.evidenceEntry, baseEvidence.GetMergedDiscoveryLinkKeysFor(human.evidenceEntry, Evidence.DataKey.initialedName));

                    if (link != null)
                        name = $"<link={link.id}>{name}</link>";
                }

                // Vanilla appends a closing font tag even when the citizen has no handwriting preset
                __result = human.handwriting != null ? $"<font=\"{human.handwriting.fontAsset.name}\">{name}</font>" : name;

                return false;
            }
        }
    }
}
