$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$t = $asm.GetType('KSP.UI.Screens.EditorSubassemblyItem')
$out = Join-Path $PSScriptRoot '..\editor-subasm-methods.txt'
if (-not $t) {
    "TYPE NOT FOUND" | Out-File $out -Encoding utf8
    exit 1
}
$t.GetMethods([Reflection.BindingFlags]'Instance,Public,NonPublic,DeclaredOnly') |
    ForEach-Object { $_.ToString() } |
    Sort-Object |
    Out-File $out -Encoding utf8
