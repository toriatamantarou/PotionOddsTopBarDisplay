using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace PotionOddsTopBarMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public static void Initialize()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        PotionOddsTopBarDisplay.Install(tree?.Root);
    }
}
