using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class SubassemblyReflect
{
    static void Main()
    {
        var asm = AssemblyDefinition.ReadAssembly(
            @"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll");

        var pc = asm.MainModule.Types.FirstOrDefault(x => x.Name == "PartCategorizer");
        var setup = pc.Methods.FirstOrDefault(m => m.Name == "Setup");
        Console.WriteLine("=== Setup strings and subassembly-related ===");
        foreach (var ins in setup.Body.Instructions)
        {
            if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s
                && (s.IndexOf("sub", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.IndexOf("assembl", StringComparison.OrdinalIgnoreCase) >= 0
                    || s == "stockIcon_subassemblies"))
            {
                Console.WriteLine("  STR: " + s);
            }
            if (ins.OpCode == OpCodes.Stfld && ins.Operand is FieldReference fr
                && (fr.Name.Contains("subassembl") || fr.Name == "filterSubassemblies"))
            {
                Console.WriteLine("  STFLD: " + fr.Name);
            }
            if (ins.OpCode == OpCodes.Ldc_I4 && ins.Operand is int iv && (iv == 1 || iv == 4 || iv == 5))
            {
                // display type constants near subassembly setup
            }
        }

        DumpMethods(asm, "EditorPartListFilter`1", "FilterList");
    }

    static void DumpMethods(AssemblyDefinition asm, string typeName, string methodName)
    {
        var t = asm.MainModule.Types.FirstOrDefault(x => x.Name == typeName);
        if (t == null) return;
        foreach (var m in t.Methods.Where(x => x.Name == methodName))
        {
            Console.WriteLine("\n=== " + typeName + "." + methodName + " ===");
            int c = 0;
            foreach (var ins in m.Body.Instructions)
            {
                string extra = "";
                if (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt)
                {
                    if (ins.Operand is MethodReference mr) extra = " " + mr.DeclaringType.Name + "." + mr.Name;
                }
                Console.WriteLine("  " + ins.OpCode + extra);
                if (++c > 30) break;
            }
        }
    }
}
