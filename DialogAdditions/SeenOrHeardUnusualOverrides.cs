using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DialogAdditions
{
    internal class SeenOrHeardUnusualOverrides
    {
        [HarmonyPatch(typeof(DialogController), nameof(DialogController.SeenOrHeardUnusual))]
        class DialogController_SeenOrHeardUnusual
        {
            public static bool Prefix(Citizen saysTo)
            {
                float num = float.NegativeInfinity;
                Human.Sighting sighting = null;
                Human seenHuman = null;
                foreach (var lastSighting in saysTo.lastSightings)
                {
                    if (lastSighting.Value.poi && !lastSighting.Value.phone && (double)lastSighting.Value.time > (double)num)
                    {
                        num = lastSighting.Value.time;
                        seenHuman = lastSighting.Key;
                        sighting = lastSighting.Value;
                    }
                }

                // Sighting.Sound (0 none, 1 gunshot, 2 scream)
                if (sighting != null && sighting.sound == 0)
                {
                    if(saysTo.FindAcquaintanceExists(seenHuman, out var returnAcq))
                    {
                        // TODO: Config value, find a good value, dunno
                        if(returnAcq.like > DialogAdditionPlugin.SeenUnusualLikeBlock.Value)
                        {
                            // Rude no
                            saysTo.speechController.Speak("b8591738-092c-4de6-992e-33a44d6ab43f");
                            return false;
                        }
                        else
                        {
                            // I saw |receiver.casualname| acting a bit strange actually...
                            saysTo.speechController.Speak("51421182-3138-47a6-a8cf-3991990e9815");
                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }
}
