using System.IO;
using TeamRoulette.Room3;
using UnityEditor.Build;
using UnityEngine;

public sealed class PorchCreditsBuildProcessor : BuildPlayerProcessor
{
    public override void PrepareForBuild(BuildPlayerContext context)
    {
        string source = Path.Combine(Application.dataPath, PorchGameController.CreditsSourcePath);
        context.AddAdditionalPathToStreamingAssets(source, PorchGameController.CreditsFileName);
    }
}
