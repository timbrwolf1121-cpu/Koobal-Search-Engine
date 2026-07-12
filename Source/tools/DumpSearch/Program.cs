using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private static void Main()
    {
        const string asmPath = @"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll";
        var asm = AssemblyDefinition.ReadAssembly(asmPath);
        var pc = asm.MainModule.Types.First(t => t.Name == "PartCategorizer");
        var bpc = asm.MainModule.Types.First(t => t.Name == "BasePartCategorizer");

        Dump(bpc, "SearchField_OnValueChange");
        Dump(bpc, "SearchStart");
        Dump(bpc, "Setup");
        Dump(pc, "Setup");
        Dump(pc, "Update");
        DumpNested(pc, "<Start>d__86", "MoveNext");
        DumpNested(pc, "<SearchRoutine>d__99", "MoveNext");
        DumpNested(pc, "<UpdateDaemon>d__156", "MoveNext");
        Dump(pc, "SearchRoutine");
        Dump(pc, "SearchStop");
        Dump(pc, "SearchFilterResult");
    }

    private static void DumpNested(TypeDefinition parent, string nestedName, string name)
    {
        var nested = parent.NestedTypes.FirstOrDefault(t => t.Name == nestedName);
        if (nested == null)
        {
            Console.WriteLine("\n=== " + parent.Name + "+" + nestedName + " (missing) ===");
            return;
        }

        Dump(nested, name);
    }

    private static void Dump(TypeDefinition type, string name)
    {
        var method = type.Methods.FirstOrDefault(m => m.Name == name);
        Console.WriteLine("\n=== " + type.Name + "." + name + " ===");
        if (method?.Body == null)
        {
            Console.WriteLine("  (missing)");
            return;
        }

        foreach (Instruction ins in method.Body.Instructions)
        {
            string extra = string.Empty;
            if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s)
            {
                extra = " \"" + s + "\"";
            }
            else if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt)
                && ins.Operand is MethodReference mr)
            {
                extra = " " + mr.DeclaringType.Name + "." + mr.Name;
            }
            else if ((ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Stfld || ins.OpCode == OpCodes.Ldflda)
                && ins.Operand is FieldReference fr)
            {
                extra = " " + fr.Name;
            }

            Console.WriteLine("  " + ins.OpCode + extra);
        }
    }
}
