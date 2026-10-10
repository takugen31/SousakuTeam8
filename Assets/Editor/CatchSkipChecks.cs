using System;
using System.Reflection;
using Sousakusai8.MiniGame;
using UnityEngine;

public static class CatchSkipChecks
{
    public static void VerifyAndBuild()
    {
        var phaseField = typeof(CatchMiniGameController).GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (string phase in new[] { "AwaitingInput", "Countdown", "Playing", "GameOver" })
        {
            var root = new GameObject("Catch skip check");
            try
            {
                var game = root.AddComponent<CatchMiniGameController>();
                phaseField.SetValue(game, Enum.Parse(phaseField.FieldType, phase));
                int completed = 0;
                game.RoundEnded += score => { completed++; Assert(score == 0, "Skipping must not invent score."); };
                Assert(!game.IsSuccessfulResult(3000, 3000) && game.IsSuccessfulResult(3001, 3000), "Normal result threshold changed.");
                game.SkipRoundSuccessfully();
                game.SkipRoundSuccessfully();
                bool shouldSkip = phase != "GameOver";
                Assert(completed == (shouldSkip ? 1 : 0), "Skip must complete only once in valid phases.");
                Assert(game.SkippedSuccessfully == shouldSkip && !game.CanSkipRound, "Skip phase is incorrect.");
                Assert(game.IsSuccessfulResult(0, 3000) == shouldSkip, "Skipped result must be success.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        Debug.Log("Catch skip checks passed: pre-start, countdown, playing, completed, duplicate click, success routing.");
        BrowserGameBuild.Build();
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
