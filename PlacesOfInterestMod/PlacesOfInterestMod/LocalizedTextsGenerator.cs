#if FALSE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ExperrrimentalStuff.SourceCodeGenerators.CodeFromAdditionalTexts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PlacesOfInterestMod;

[CodeFromAdditionalTexts("LocalizedTextsGenerator")]
public sealed class LocalizedTextsGenerator : ICodeFromAdditionalTexts
{
    // NOTE: The expression is more readable this way. Regex performance is not critical here and also it would not work with current lambda syntax parser.
#pragma warning disable T0010 // Internal Styling Rule T0010
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    public Expression<Func<string, bool>> AdditionalTextsSelector => path => Regex.IsMatch(Path.GetFileName(path), "[a-z]{2}.json$", RegexOptions.IgnoreCase);
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
#pragma warning restore T0010 // Internal Styling Rule T0010

    public void AddSources(
        SourceProductionContext sourceProductionContext,
        string codeGeneratorName,
        ClassDeclarationSyntax classDeclarationSyntax,
        IEnumerable<AdditionalText> additionalTexts)
    {
        AdditionalText additionalText = additionalTexts.Single();
        string prefix = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(additionalText.Path)!)!);

        JsonObject input = JsonSerializer.Deserialize<JsonObject>(additionalText.GetText()!.ToString())!;

        var @class =
            SyntaxFactory.ClassDeclaration("LocalizedTexts")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers([.. input.Select(x => MakeField(x.Key, prefix))]);

        var @namespace =
            SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("PlacesOfInterestMod.Generated"))
                .AddMembers(@class);

        var compilationUnit =
            SyntaxFactory.CompilationUnit()
                .AddMembers(@namespace);

        sourceProductionContext.AddSource(
            $"CodeTemplate.{codeGeneratorName}.g.cs",
            compilationUnit.NormalizeWhitespace().ToFullString());

        static FieldDeclarationSyntax MakeField(string key, string prefix) =>
            SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName("string"),
                    SyntaxFactory.SeparatedList([
                        SyntaxFactory.VariableDeclarator(key)
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal($"{prefix}:{key}"))))])))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.ConstKeyword));
    }
}
#endif
