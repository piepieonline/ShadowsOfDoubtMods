using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                Toolbox.Instance.scopeDictionary["citizen"].containedScopes.Add(new DDSScope.ContainedScope() { name = "currentgroup", type = Toolbox.Instance.scopeDictionary["group"] });
                GameplayControls.Instance.humanScope.containedScopes.Add(new DDSScope.ContainedScope() { name = "currentgroup", type = Toolbox.Instance.scopeDictionary["group"] });
                Toolbox.Instance.scopeDictionary["group"].containedValues.Add("membercount");
                Toolbox.Instance.scopeDictionary["group"].containedValues.Add("type");
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
