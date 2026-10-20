$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($file in @('Chapter1SearchController.cs','SearchSceneController.cs')) {
    $source=Get-Content (Join-Path $root "Assets/Scripts/Search/$file") -Raw
    if(-not $source.Contains('searchHeader = header;')){throw "$file does not retain header root"}
    if(-not [regex]::IsMatch($source,'isDialogueOpen = true;\s*if \(searchHeader != null\) searchHeader.SetActive\(false\);')){throw "$file shows header during dialogue"}
    if(-not [regex]::IsMatch($source,'currentRoom = room;\s*if \(searchHeader != null\) searchHeader.SetActive\(true\);')){throw "$file does not restore header on exploration return"}
    if(-not [regex]::IsMatch($source,'isCompleting = true;\s*if \(searchHeader != null\) searchHeader.SetActive\(false\);')){throw "$file flashes header during final transition"}
}
'PASS: both exploration headers hide before dialogue, remain hidden through completion fade and return with exploration.'
