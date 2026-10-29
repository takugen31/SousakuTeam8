$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Check($condition, $message) { if (-not $condition) { throw $message } }
$rows = @(Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Yowashi_Consultation.csv'))
$entry = $rows | Where-Object lineId -eq 'chapter_yowashi_consultation_001'
Check ([string]::IsNullOrWhiteSpace($entry.text) -and [string]::IsNullOrWhiteSpace($entry.nextLineId)) 'Entry still renders dialogue or advances directly'
Check ($entry.choice1Text -eq '一回状況を整理しよう' -and $entry.choice1NextLineId -eq 'chapter_yowashi_consultation_002') 'Single choice label or target incorrect'
Check ([string]::IsNullOrEmpty($entry.choice2Text) -and [string]::IsNullOrEmpty($entry.choice3Text)) 'Extra choice added'
Check (($rows | Where-Object lineId -eq $entry.choice1NextLineId).speakerId -eq 'doute') 'Doute response missing'
$asset = Get-Content (Join-Path $root 'Assets/Dialogue/Data/Chapter_Yowashi_Consultation.asset') -Raw
$block = [regex]::Match($asset, '(?s)  - lineId: chapter_yowashi_consultation_001\r?\n.*?(?=\r?\n  - lineId:|\z)').Value
Check ($block.Contains('- text: "一回状況を整理しよう"') -and $block.Contains('nextLineId: chapter_yowashi_consultation_002')) 'Runtime choice differs'
$source = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
Check ($source.Contains('currentLine.HasChoices && string.IsNullOrWhiteSpace(text)') -and $source.Contains('ShowChoiceOnlyWindow();')) 'Choice-only entry not immediate'
Check ($source.Contains('choiceOnlyWindowGroup.alpha = 0f') -and $source.Contains('choiceOnlyWindowGroup.blocksRaycasts = false') -and $source.Contains('namePlate.SetActive(false)')) 'Text panel/name still visible'
Check ($source.Contains('panel != dialogueRoot?.transform') -and $source.Contains('!choiceOptionsRoot.IsChildOf(panel)')) 'Choice/portraits may be hidden with dialogue'
Check ($source.Contains('choiceOnlyWindowGroup.alpha = choiceOnlyOriginalAlpha') -and $source.Contains('choiceOnlyWindowGroup.blocksRaycasts = choiceOnlyOriginalRaycasts')) 'Following dialogue is not restored'
Check ($source.Contains('StartCoroutine(EnableChoiceButtonsAfterInputRelease())')) 'Entry click can accidentally select choice'
'PASS: one explicit click choice, empty dialogue body, correct Doute response, panel/name hidden without hiding portraits/choices, presentation restored and input-release guard preserved.'
