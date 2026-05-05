namespace ThesisDocx.Core.Testing.NegativeFixtures;

public sealed class NegativeFixtureCase
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public int ExpectedExitCode { get; set; } = 2;

    public List<string> ExpectedCodes { get; set; } = [];

    public string ExpectedSeverity { get; set; } = "breaking";

    public List<string> ExpectedFixHintIds { get; set; } = [];

    public string Notes { get; set; } = string.Empty;
}
