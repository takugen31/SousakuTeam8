$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Check($condition, $message) { if (-not $condition) { throw $message } }
$row = Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter_Kayo/Chapter_Kayo_3.csv') | Where-Object lineId -eq 'chapter_kayo_3_008'
Check ([string]::IsNullOrEmpty($row.text)) 'Update message still displayed'
Check ($row.choice1Text -eq 'カヨのドジを治すべきか否か' -and $row.choice1NextLineId -eq 'chapter_kayo_3_009') 'Consultation choice lost'
$asset = Get-Content (Join-Path $root 'Assets/Dialogue/Data/Kayo/Chapter_Kayo_3.asset') -Raw
Check ([regex]::IsMatch($asset, 'lineId: chapter_kayo_3_008\s+speakerId:\s+expressionId:\s+text:\s+background:')) 'Runtime asset message not removed'
$controller = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
Check ($controller.Contains('line?.lineId == "chapter_kayo_3_008"') -and $controller.Contains('ArchiveManager.Unlock("chapter_kayo.profile")') -and $controller.Contains('ArchiveManager.Replace("chapter_kayo.floor_dent", "chapter_kayo.clumsiness")')) 'Update cue missing'
$database = Get-Content (Join-Path $root 'Assets/Resources/Archive/ArchiveDatabase.asset') -Raw
foreach ($id in @('chapter_kayo.profile', 'chapter_kayo.floor_dent', 'chapter_kayo.clumsiness')) { Check ($database.Contains("id: $id")) "Unknown archive id $id" }
$manager = Get-Content (Join-Path $root 'Assets/Scripts/Archive/ArchiveManager.cs') -Raw
Check ($manager.Contains('manager.unlockedIds.Remove(previousId)') -and $manager.Contains('manager.UnlockEntry(replacementId)') -and $manager.Contains('menu.ShowUnlockNotification(entry.Title)')) 'Replacement or shared notification path missing'
$ui = Get-Content (Join-Path $root 'Assets/Scripts/Archive/ArchiveMenuUI.cs') -Raw
Check ($ui.Contains('pendingNotifications.Enqueue(entryTitle)') -and $ui.Contains('yield return ShowNotificationRoutine(pendingNotifications.Dequeue())')) 'Multiple updates overwrite notifications'
'PASS: update message removed in CSV/runtime asset, consultation choice preserved, two valid archive updates, old floor entry replaced, shared right-top notifications queued.'
