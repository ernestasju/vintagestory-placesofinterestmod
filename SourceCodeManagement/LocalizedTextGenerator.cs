using Microsoft.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SourceCodeManagement;

[Generator(LanguageNames.CSharp)]
public class LocalizedTextGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<AdditionalText> textFiles = context.AdditionalTextsProvider.Where(
            file => file.Path.EndsWith("en.json", StringComparison.OrdinalIgnoreCase));

        IncrementalValuesProvider<(string name, string content)> namesAndContents = textFiles.Select(
            (text, cancellationToken) => (name: Path.GetFileNameWithoutExtension(text.Path), content: text.GetText(cancellationToken)!.ToString()));

        context.RegisterSourceOutput(
            namesAndContents,
            (spc, nameAndContent) =>
            {
                JsonObject jsonObject = JsonSerializer.Deserialize<JsonObject>(nameAndContent.content) ?? new();
            
                StringBuilder codeBuilder = new();
                codeBuilder.AppendLine("namespace PlacesOfInterestMod;");
                codeBuilder.AppendLine();
                codeBuilder.AppendLine("public static class LocalizedTexts");
                codeBuilder.AppendLine("{");
                foreach (KeyValuePair<string, JsonNode> kv in jsonObject)
                {
                    codeBuilder.AppendLine($"    public const string \"{kv.Key}\" = \"places-of-interest-mod:{kv.Value}\";");
                }
                codeBuilder.AppendLine("}");

                spc.AddSource($"PlacesOfInterestMod.LocalizedTexts.{nameAndContent.name}", codeBuilder.ToString());
            });
    }
}
