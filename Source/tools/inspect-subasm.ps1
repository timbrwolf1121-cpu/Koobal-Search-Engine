$asm = [Reflection.Assembly]::LoadFrom('F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll')
$pc = $asm.GetType('KSP.UI.Screens.PartCategorizer')
$cat = $pc.GetNestedType('Category',[Reflection.BindingFlags]'NonPublic,Public')

function Dump-MethodStrings($type, $methodName) {
    $m = $type.GetMethod($methodName, [Reflection.BindingFlags]'Instance,NonPublic,Public')
    if (-not $m) { Write-Output "MISSING $methodName"; return }
    $body = $m.GetMethodBody().GetILAsByteArray()
    $module = $m.Module
    $reader = New-Object System.Reflection.Emit.OpCodes
    # Use Mono Cecil via inline - fallback: grep method for field names in disassembly from ildasm
    Write-Output "=== $methodName ($($m.GetParameters().Count) params) ==="
    Write-Output $m.ToString()
}

Dump-MethodStrings $pc 'LoadCustomSubassemblyCategories'
Dump-MethodStrings $cat 'AddCustomCategorySubassembly'
Dump-MethodStrings $cat 'AcceptNewSubcategorySubassembly'
Dump-MethodStrings $cat 'OnTrueSUB'
Dump-MethodStrings $cat 'OnTrueCATEGORY'

# Use ildasm if available
$ildasm = @(
    "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\ildasm.exe",
    "${env:ProgramFiles}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\ildasm.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($ildasm) {
    $out = Join-Path $PSScriptRoot 'ildasm-category.txt'
    & $ildasm /TEXT /OUT=$out 'F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll'
    Write-Output "Wrote $out"
    Select-String -Path $out -Pattern 'LoadCustomSubassemblyCategories|AddCustomCategorySubassembly|subassembliesAll|subassemblies[^A]' | Select-Object -First 40 | ForEach-Object { $_.Line }
}
