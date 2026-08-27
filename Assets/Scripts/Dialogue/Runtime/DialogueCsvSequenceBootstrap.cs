using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class DialogueCsvSequenceBootstrap : MonoBehaviour
{
    [SerializeField] private NovelDialogueController dialogueController;
    [SerializeField] private TextAsset[] scenarioCsvs;
    [SerializeField] private Sprite backgroundForPathRows;

    private readonly List<DialogueScenarioSO> runtimeScenarios = new();

    private void Awake()
    {
        if (dialogueController == null)
        {
            dialogueController = GetComponent<NovelDialogueController>();
        }

        if (dialogueController == null || scenarioCsvs == null || scenarioCsvs.Length == 0)
        {
            Debug.LogError("Dialogue CSVシーケンスの設定が不足しています。", this);
            enabled = false;
            return;
        }

        foreach (TextAsset csv in scenarioCsvs)
        {
            if (csv != null)
            {
                runtimeScenarios.Add(DialogueRuntimeCsv.CreateScenario(csv, backgroundForPathRows));
            }
        }

        if (runtimeScenarios.Count == 0)
        {
            Debug.LogError("読み込み可能なDialogue CSVがありません。", this);
            enabled = false;
            return;
        }

        dialogueController.ConfigureScenarioSequence(runtimeScenarios);
    }

    private void OnDestroy()
    {
        foreach (DialogueScenarioSO scenario in runtimeScenarios)
        {
            if (scenario != null)
            {
                Destroy(scenario);
            }
        }

        runtimeScenarios.Clear();
    }
}
