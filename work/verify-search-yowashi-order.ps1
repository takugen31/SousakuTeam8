$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$rows = Import-Csv (Join-Path $root 'Assets/Dialogue/CSV/Chapter1/chapter1_search.csv')
$asset = Get-Content (Join-Path $root 'Assets/Dialogue/Data/Chapter1Search.asset') -Raw
$search = Get-Content (Join-Path $root 'Assets/Scripts/Search/Chapter1SearchController.cs') -Raw
$dialogue = Get-Content (Join-Path $root 'Assets/Scripts/Dialogue/Runtime/NovelDialogueController.cs') -Raw
function Assert-Order($condition, $message) { if (-not $condition) { throw $message } }
$first = $rows | Where-Object lineId -eq 'search_bookshelf_001'
$next = $rows | Where-Object lineId -eq $first.nextLineId
Assert-Order ($first.speakerId -eq 'doute' -and $next.lineId -eq 'search_manga_001' -and $next.speakerId -eq 'yowashi') 'CSV order is not Doute then Yowashi'
$block = [regex]::Match($asset,'(?s)- lineId: search_bookshelf_001.*?(?=  - lineId:)').Value
Assert-Order ($block.Contains('speakerId: doute') -and $block.Contains('nextLineId: search_manga_001')) 'Runtime asset differs from CSV'
$bookshelfBlock = [regex]::Match($search,'(?s)new ItemDefinition\(\s*"bookshelf".*?(?=items.Add|private void)').Value
Assert-Order ([regex]::IsMatch($bookshelfBlock,'"search_bookshelf_001",\s*"search_bookshelf_001"') -and -not $bookshelfBlock.Contains('search_manga_001')) 'Bookshelf incorrectly includes the manga animation'
$mangaBlock = [regex]::Match($search,'(?s)new ItemDefinition\(\s*"manga".*?(?=items.Add|private void)').Value
Assert-Order ([regex]::IsMatch($mangaBlock,'"search_bookshelf_001",\s*"search_manga_001"')) 'Manga skips Doute or stops before Yowashi'
$advanceStart = $dialogue.IndexOf('public void Advance()')
$advanceEnd = $dialogue.IndexOf('public bool IsDialoguePlaying', $advanceStart)
$advance = $dialogue.Substring($advanceStart, $advanceEnd-$advanceStart)
Assert-Order ($advance.Contains('if (isTyping)') -and $advance.Contains('CompleteTyping();') -and $advance.IndexOf('CompleteTyping();') -lt $advance.IndexOf('ShowLine(nextLine)')) 'Typing completion input can skip to Yowashi'
Assert-Order ($advance.IndexOf('currentLine.lineId,') -lt $advance.IndexOf('TryResolveNextLine(') -and $advance.Contains('stopAfterLineId,')) 'Bookshelf end-of-range is not checked before following the shared link'
Assert-Order ($dialogue.Contains('autoPlayOnStart = false') -and $dialogue.Contains('if (WasDialogueAdvancePressed())')) 'Manual advance default missing'
'PASS: bookshelf stops after Doute without manga animation; manga waits for Doute advance before Yowashi; runtime/CSV links and end-of-range order agree.'
