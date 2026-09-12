using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniverseLib;
using Il2CppType = Il2CppInterop.Runtime.Il2CppType;

namespace DialogAdditions
{
    internal class ModifyDDSScopes
    {
        public static GroupsController.SocialGroup GroupToSpeakAbout = null;

        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                var scopes = Toolbox.Instance.resourcesCache[Il2CppType.Of<DDSScope>()];
                var citizenScope = scopes["citizen"].TryCast<DDSScope>();
                var groupScope = scopes["group"].TryCast<DDSScope>();
                citizenScope.containedScopes.Add(new DDSScope.ContainedScope() { name = "currentgroup", type = groupScope });
                GameplayControls.Instance.humanScope.containedScopes.Add(new DDSScope.ContainedScope() { name = "currentgroup", type = groupScope });
                groupScope.containedValues.Add("membercount");
                groupScope.containedValues.Add("type");
            }
        }

        // TODO: Not sure we can get the specific group we are talking about here at all..
        [HarmonyPatch(typeof(Strings), nameof(Strings.GetContainedValue))]
        class Strings_GetContainedValue
        {
            static bool Prefix(ref string __result, object baseObject, string withinScope, string newValue, object inputObject, object additionalObject)
            {
                string lowerValue = newValue.ToLower();

                if (withinScope == "group")
                {
                    if (lowerValue == "membercount")
                    {
                        try
                        {
                            GroupsController.SocialGroup socialGroup = ((dynamic)inputObject).Cast<GroupsController.SocialGroup>();
                            __result = $"{socialGroup.members.Count}";
                            return false;
                        }
                        catch { }
                    }
                    else if (lowerValue == "type")
                    {
                        try
                        {
                            GroupsController.SocialGroup socialGroup = ((dynamic)inputObject).Cast<GroupsController.SocialGroup>();
                            __result = Strings.Get("misc", socialGroup.preset);
                            return false;
                        }
                        catch { }
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Strings), nameof(Strings.GetScopeObject))]
        class Strings_GetScopeObject
        {
            static bool Prefix(ref object __result, object inputObject, string withinScope, string newType)
            {
                withinScope = withinScope.ToLower();
                newType = newType.ToLower();

                if (withinScope == "citizen")
                {
                    if (newType == "currentgroup")
                    {
                        try
                        {
                            __result = GroupToSpeakAbout;
                            return false;
                        }
                        catch { }
                    }
                }
                return true;
            }
        }
    }
}
