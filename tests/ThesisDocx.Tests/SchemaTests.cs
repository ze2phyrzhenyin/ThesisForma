using System.Text.Json.Nodes;
using ThesisDocx.Core.Validation;
using ThesisDocx.Tests.Fixtures;

namespace ThesisDocx.Tests;

public sealed class SchemaTests
{
    [Fact]
    public void Schema_ShouldValidateSimpleThesisDocument()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var result = new ThesisSchemaValidator().ValidateDocumentFile(
            Path.Combine(root, "examples", "simple-thesis", "document.json"),
            Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldValidateVersion100Document()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "simple-thesis", "document.json"));

        Assert.Equal("1.0.0", node["schemaVersion"]!.GetValue<string>());
        var result = new ThesisSchemaValidator().ValidateDocumentFile(
            Path.Combine(root, "examples", "simple-thesis", "document.json"),
            Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldValidateVersion110Document()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "full-thesis", "document.json"));

        Assert.Equal("1.1.0", node["schemaVersion"]!.GetValue<string>());
        var result = new ThesisSchemaValidator().ValidateDocumentFile(
            Path.Combine(root, "examples", "full-thesis", "document.json"),
            Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldValidateBasicFormatSpec()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var result = new ThesisSchemaValidator().ValidateFormatFile(
            Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"),
            Path.Combine(root, "schemas", "thesis-format-spec.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [MemberData(nameof(ExampleThesisDocuments))]
    public void Schema_ShouldValidateExampleThesisDocuments(string path)
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var result = new ThesisSchemaValidator().ValidateDocumentFile(path, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.True(result.IsValid, $"{path}:{Environment.NewLine}{string.Join(Environment.NewLine, result.Errors)}");
    }

    [Theory]
    [MemberData(nameof(ExampleFormatSpecs))]
    public void Schema_ShouldValidateExampleFormatSpecs(string path)
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var result = new ThesisSchemaValidator().ValidateFormatFile(path, Path.Combine(root, "schemas", "thesis-format-spec.schema.json"));

        Assert.True(result.IsValid, $"{path}:{Environment.NewLine}{string.Join(Environment.NewLine, result.Errors)}");
    }

    [Theory]
    [MemberData(nameof(ExampleTemplatePackages))]
    public void Schema_ShouldValidateExampleTemplatePackages(string path)
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var result = new ThesisSchemaValidator().ValidateTemplateFile(path, Path.Combine(root, "schemas", "template-package.schema.json"));

        Assert.True(result.IsValid, $"{path}:{Environment.NewLine}{string.Join(Environment.NewLine, result.Errors)}");
    }

    [Fact]
    public void Schema_ShouldRejectDocumentMissingSchemaVersion()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "simple-thesis", "document.json"));
        node.AsObject().Remove("schemaVersion");
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectUnknownBlockType()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "simple-thesis", "document.json"));
        node["sections"]![3]!["blocks"]![0]!["type"] = "unknownBlock";
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidHeadingLevel()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "simple-thesis", "document.json"));
        node["sections"]![3]!["blocks"]![0]!["level"] = 7;
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidPageMargin()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "format-specs", "basic-cn-thesis.json"));
        node["pageSetup"]!["topMarginCm"] = 0;
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateFormatFile(temp, Path.Combine(root, "schemas", "thesis-format-spec.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldValidateEquationBlock()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var result = new ThesisSchemaValidator().ValidateDocumentFile(
            Path.Combine(root, "examples", "full-thesis", "document.json"),
            Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldRejectEquationWithInvalidSourceType()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "full-thesis", "document.json"));
        var equation = FindFirstBlock(node, "equation");
        equation["sourceType"] = "mathml";
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectEquationWithInvalidNumberingFormat()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "full-thesis", "document.json"));
        var equation = FindFirstBlock(node, "equation");
        equation["numbering"]!["format"] = "chapter.index";
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldValidateAdvancedTable()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var result = new ThesisSchemaValidator().ValidateDocumentFile(
            Path.Combine(root, "examples", "full-thesis", "document.json"),
            Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Schema_ShouldRejectInvalidVerticalMergeValue()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "full-thesis", "document.json"));
        var table = FindFirstBlock(node, "table");
        table["rows"]![1]!["cells"]![0]!["verticalMerge"] = "start";
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidBorderStyle()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "full-thesis", "document.json"));
        var table = FindFirstBlock(node, "table");
        table["rows"]![1]!["cells"]![2]!["borders"]!["bottom"]!["style"] = "heavy";
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ShouldRejectInvalidTableWidth()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        var node = LoadJson(Path.Combine(root, "examples", "full-thesis", "document.json"));
        var table = FindFirstBlock(node, "table");
        table["width"]!["value"] = -1;
        var temp = WriteTempJson(node);

        var result = new ThesisSchemaValidator().ValidateDocumentFile(temp, Path.Combine(root, "schemas", "thesis-document.schema.json"));

        Assert.False(result.IsValid);
    }

    private static JsonNode LoadJson(string path)
    {
        return JsonNode.Parse(File.ReadAllText(path)) ?? throw new InvalidOperationException("Could not parse JSON.");
    }

    private static string WriteTempJson(JsonNode node)
    {
        var path = Path.Combine(Path.GetTempPath(), "ThesisDocx.Tests", $"{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, node.ToJsonString());
        return path;
    }

    private static JsonObject FindFirstBlock(JsonNode document, string type)
    {
        foreach (var section in document["sections"]!.AsArray())
        {
            foreach (var block in section!["blocks"]!.AsArray())
            {
                if (block!["type"]?.GetValue<string>() == type)
                {
                    return block.AsObject();
                }
            }
        }

        throw new InvalidOperationException($"Could not find block '{type}'.");
    }

    public static IEnumerable<object[]> ExampleThesisDocuments()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        return Directory.EnumerateFiles(Path.Combine(root, "examples", "simple-thesis"), "*.json", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "examples", "full-thesis"), "*.json", SearchOption.TopDirectoryOnly))
            .Where(path => Path.GetFileName(path).Equals("document.json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => new object[] { path });
    }

    public static IEnumerable<object[]> ExampleFormatSpecs()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        return Directory.EnumerateFiles(Path.Combine(root, "examples", "format-specs"), "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => new object[] { path });
    }

    public static IEnumerable<object[]> ExampleTemplatePackages()
    {
        var root = TestRenderHelper.LocateRepoRootForTests();
        return Directory.EnumerateFiles(Path.Combine(root, "examples", "templates"), "template.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new object[] { path });
    }
}
