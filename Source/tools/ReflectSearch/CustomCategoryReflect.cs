using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class CustomCategoryReflect
{
    static void Main()
    {
        var asm = AssemblyDefinition.ReadAssembly(
            @"F:\SteamLibrary\steamapps\common\Kerbal Space Program\KSP_x64_Data\Managed\Assembly-CSharp.dll");

        var pc = asm.MainModule.Types.First(t => t.Name == "PartCategorizer");
        foreach (string name in new[]
        {
            "LoadCustomPartCategories",
            "SaveCustomPartCategories",
            "LoadCustomSubassemblyCategories",
            "SaveCustomSubassemblyCategories",
            "AddCustomCategory",
            "AddPartToCategory",
            "AddSubassemblyToSelectedCategory"
        })
        {
            DumpMethod(pc, name);
        }

        var cat = pc.NestedTypes.First(t => t.Name == "Category");
        foreach (string name in new[]
        {
            "AddCustomCategory",
            "AddCustomCategorySubassembly",
            "AcceptNewSubcategory",
            "AcceptNewSubcategorySubassembly",
            "CompileExclusionFilter",
            "CompileExclusionFilterSubassembly",
            "AddPart",
            "AddSubassembly",
            "DeleteCategory",
            "DeleteSubcategory",
            "OnTrueSUB",
            "OnTrueCATEGORY"
        })
        {
            DumpMethod(cat, name);
        }

        foreach (string name in new[] { "SetAdvancedMode", "SelectedSubassemblyIsCustom" })
        {
            DumpMethod(pc, name);
        }
    }

    static void DumpMethod(TypeDefinition type, string name)
    {
        var method = type.Methods.FirstOrDefault(x => x.Name == name);
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
            else if ((ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Stfld)
                && ins.Operand is FieldReference fr)
            {
                extra = " " + fr.Name;
            }

            Console.WriteLine("  " + ins.OpCode + extra);
        }
    }
}
