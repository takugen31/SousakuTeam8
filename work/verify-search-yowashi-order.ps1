$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$rows = Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter1/chapter1_search.csv')
$asset = Get-Content (Join-Path $root 'Assets/Dialogue/Data/Chapter1Search.asset') -Raw
$search = Get-Content (Join-Path $root 'Assets/Scripts/Search/Chapter1SearchController.cs') -Raw
$dialogue = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueControllerSO.cs') -Raw
function Assert-Order($condition, $message) { if (-not $condition) { throw $message } }
$first = $rows | Where-Object lineId -eq 'search_bookshelf_001'
$next = $rows | Where-Object lineId -eq $first.nextLineId
Assert-Order ($first.speakerId -eq 'doute' -and $next.lineId -eq 'search_manga_001' -and $next.speakerId -eq 'yowashi') 'CSV order is not Doute then Yowashi'
$block = [regex]::Match($asset,'(?s)- lineId: search_bookshelf_001.*?(?=  - lineId:)').Value
Assert-Order ($block.Contains('speakerId: doute') -and $block.Contains('nextLineId: search_manga_001')) 'Runtime asset differs from CSV'
foreach ($item in @('bookshelf','manga')) {
    $itemBlock = [regex]::Match($search,'(?s)new ItemDefinition\(\s*"'+$item+'".*?(?=items.Add|private void)').Value
    Assert-Order ($itemBlock.Contains('"search_bookshelf_001",') -and $itemBlock.Contains('"search_manga_001"')) "$item skips the Doute page"
}
$advanceStart = $dialogue.IndexOf('public void Advance()')
$advanceEnd = $dialogue.IndexOf('public bool IsDialoguePlaying', $advanceStart)
$advance = $dialogue.Substring($advanceStart, $advanceEnd-$advanceStart)
Assert-Order ($advance.Contains('if (isTyping)') -and $advance.Contains('CompleteTyping();') -and $advance.IndexOf('CompleteTyping();') -lt $advance.IndexOf('ShowLine(nextLine)')) 'Typing completion input can skip to Yowashi'
Assert-Order ($dialogue.Contains('autoPlayOnStart = false') -and $dialogue.Contains('if (WasDialogueAdvancePressed())')) 'Manual advance default missing'
'PASS: bookshelf and manga begin with existing Doute text, runtime/CSV links agree, Yowashi follows page advance rather than item-open, typing completion stays on the current page.'
