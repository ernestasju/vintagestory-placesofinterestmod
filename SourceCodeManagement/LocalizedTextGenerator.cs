using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SourceCodeManagement;

[Generator(LanguageNames.CSharp)]
public class LocalizedTextGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // NOTE: Consuming project must mark localization files as AdditionalFiles in the .csproj:
        // NOTE: <ItemGroup>
        // NOTE:   <AdditionalFiles Include=" ... \assets\*\lang\*.json" />
        // NOTE: </ItemGroup>
        IncrementalValueProvider<Dictionary<string, LocalizationFile>> localizationFiles = context
            .AdditionalTextsProvider
            .Where(static value => Regex.IsMatch(Path.GetFileName(value.Path), "[a-z]{2}.json$", RegexOptions.IgnoreCase))
            .Select(static (value, token) => new { value.Path, Content = value.GetText(token)!.ToString(), })
            .Select(static LocalizationFile (value, _) =>
            {
                if (JsonSerializer.Deserialize<JsonObject>(value.Content) is not JsonObject jsonObject)
                {
                    return new LocalizationFile()
                    {
                        Path = value.Path,
                        MissingPath = false,
                        Keys = [],
                    };
                }

                return new LocalizationFile()
                {
                    Path = value.Path,
                    MissingPath = false,
                    Keys = jsonObject.Select(kv => kv.Key).ToArray(),
                };
            })
            .Collect()
            .Select((x, c) => x.ToDictionary(x => x.Path));

        IncrementalValuesProvider<LocalizationKeysClass> localizationKeysClasses = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                "SourceCodeManagement.LocalizedTextsAttribute",
                (x, c) => true,
                (x, c) => new LocalizationKeysClass()
                {
                    Namespace = x.TargetSymbol.CalculateFullNamespaceName(),
                    ClassName = x.TargetSymbol.Name,
                    Paths = x.Attributes
                        .Select(a => a.ConstructorArguments.First().Value!.ToString()!)
                        .ToArray(),
                });

        var generatedLocalizationKeysClasses = localizationKeysClasses
            .Combine(localizationFiles)
            .Select((x, c) =>
            {
                IEnumerable<LocalizationFile> localizationFiles = x.Left.Paths.Select(
                    y =>
                    {
                        if (!x.Right.TryGetValue(y, out var v))
                        {
                            return new LocalizationFile()
                            {
                                Path = y,
                                MissingPath = true,
                                Keys = [],
                            };
                        }
                        
                        return v;
                    });

                return new GeneratedLocalizationKeysClass()
                {
                    Namespace = x.Left.Namespace,
                    ClassName = x.Left.ClassName,
                    LocalizationFiles = localizationFiles,
                };
            });

        context.RegisterSourceOutput(
            generatedLocalizationKeysClasses,
            (x, c) =>
            {
                StringBuilder source = new();
                source.AppendLine("namespace " + c.Namespace);
                source.AppendLine("{");
                source.AppendLine("    partial class " + c.ClassName);
                source.AppendLine("    {");
                foreach (LocalizationFile localizationsFile in c.LocalizationFiles)
                {
                    source.AppendLine("        // " + localizationsFile.Path);
                    if (localizationsFile.MissingPath)
                    {
                        source.AppendLine("        // Missing File");
                        continue;
                    }

                    string prefix = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(localizationsFile.Path)));

                    foreach (string key in localizationsFile.Keys)
                    {
                        string literal = SyntaxFactory.Literal(prefix + ":" + key).ToFullString();
                        source.AppendLine($"        public const string {key} = {literal};");
                    }
                }
                source.AppendLine("    }");
                source.AppendLine("}");

                x.AddSource(c.Namespace + "." + c.ClassName + ".g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
            });

        context.RegisterPostInitializationOutput(static ctx =>
        {
            StringBuilder source = new();
            source.AppendLine("namespace SourceCodeManagement");
            source.AppendLine("{");
            source.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = true)]");
            source.AppendLine("    public sealed class LocalizedTextsAttribute : global::System.Attribute");
            source.AppendLine("    {");
            source.AppendLine("        public LocalizedTextsAttribute(string path)");
            source.AppendLine("        {");
            source.AppendLine("            Path = path;");
            source.AppendLine("        }");
            source.AppendLine("");
            source.AppendLine("        public string Path { get; }");
            source.AppendLine("    }");
            source.AppendLine("}");

            ctx.AddSource("LocalizedTextsAttribute.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        });
    }
}

public static class Extensions
{
    public static string CalculateFullNamespaceName(
        this ISymbol symbol)
    {
        INamespaceSymbol? ns = symbol.ContainingNamespace;

        string fullName = "";
        while (ns is not null && !string.IsNullOrEmpty(ns.Name))
        {
            if (fullName != "")
            {
                fullName = "." + fullName;
            }

            fullName = ns.Name + fullName;

            ns = ns.ContainingNamespace;
        }

        return fullName;
    }
}

public class LocalizationFile
{
    public string Path { get; set; } = "";

    public bool MissingPath { get; set; } = false;

    public string[] Keys { get; set; } = [];
}

public class LocalizationKeysClass
{
    public string Namespace { get; set; } = "";

    public string ClassName { get; set; } = "";

    public string[] Paths { get; set; } = [];
}

public class GeneratedLocalizationKeysClass
{
    public string Namespace { get; set; } = "";

    public string ClassName { get; set; } = "";

    public IEnumerable<LocalizationFile> LocalizationFiles { get; set; } = [];
}