$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$out = Join-Path $PSScriptRoot '..\editor-subasm-methods.txt'
$types = $asm.GetTypes() | Where-Object { $_.Name -like '*Subassembly*' } | ForEach-Object { $_.FullName }
$types | Out-File $out -Encoding utf8
"Found $($types.Count) types" | Add-Content $out
