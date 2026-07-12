using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program
{
    static void Main()
    {
        var asm = AssemblyDefinition.ReadAssembly(
            @"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll");

        DumpMethod(asm, "EditorPartListFilter`1", "FilterList");
        DumpMethod(asm, "EditorPartList", "RefreshSubassemblies");
        DumpMethod(asm, "EditorPartList", "RefreshSubassemblyList");
        DumpMethod(asm, "EditorPartListFilterList`1", "FilterList");
    }

    static void DumpMethod(AssemblyDefinition asm, string typeName, string methodName)
    {
        var type = asm.MainModule.Types.FirstOrDefault(t => t.Name == typeName);
        if (type == null)
        {
            System.Console.WriteLine("Missing type " + typeName);
            return;
        }

        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        System.Console.WriteLine("\n=== " + typeName + "." + methodName + " ===");
        if (method?.Body == null)
        {
            System.Console.WriteLine("  (missing)");
            return;
        }

        foreach (var ins in method.Body.Instructions)
        {
            string extra = string.Empty;
            if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s)
            {
                extra = " \"" + s + "\"";
            }
            else if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt)
                && ins.Operand is MethodReference cm)
            {
                extra = " " + cm.DeclaringType.Name + "." + cm.Name;
            }
            else if ((ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Ldsfld)
                && ins.Operand is FieldReference fr)
            {
                extra = " " + fr.Name;
            }

            System.Console.WriteLine("  " + ins.OpCode + extra);
        }
    }
}
