using UnityEngine;

namespace Sousakusai8.MiniGame
{
    /// <summary>
    /// The prototype is intentionally self-starting so the empty SampleScene can be
    /// played without any manual scene setup.
    /// </summary>
    public static class CatchMiniGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            if (Object.FindFirstObjectByType<CatchMiniGameController>() != null)
            {
                return;
            }

            var root = new GameObject("Catch Mini Game");
            root.AddComponent<CatchMiniGameController>();
        }
    }
}
