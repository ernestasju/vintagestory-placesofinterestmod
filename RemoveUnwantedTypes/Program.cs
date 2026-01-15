using Mono.Cecil;

if (args is [string assemblyPath])
{
    using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadWrite = true, });
    var type = assembly.MainModule.GetType("PlacesOfInterestMod.LocalizedTextsGenerator");
    assembly.MainModule.Types.Remove(type);
    assembly.Write();
}
else
{
    Console.WriteLine("USAGE: RemoveUnwantedTypes.exe <path-to-assembly>");
    Environment.Exit(1);
}
