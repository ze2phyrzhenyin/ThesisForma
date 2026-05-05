namespace ThesisDocx.Core.Testing.NegativeFixtures;

public sealed class NegativeFixtureManifest
{
    public string SchemaVersion { get; set; } = "1.0.0";

    public string SuiteId { get; set; } = string.Empty;

    public List<NegativeFixtureCase> Cases { get; set; } = [];
}
