using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;

public class SolitaireCruncherAppPrefab : CruncherAppContent
{
    // This one seems to be retired?
    public override void Setup(ComputerController cc)
    {
        base.controller = cc;
        DoSetup();
    }

    public override void OnSetup()
    {
        DoSetup();
    }

    private void DoSetup()
    {
        GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.name == "Exit").FirstOrDefault().onClick.AddListener(() => controller.OnAppExit());
    }

    public override void PrintButton()
    {
    }
}
