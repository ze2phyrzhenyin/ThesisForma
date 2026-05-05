using System.Security;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Utilities;
using ThesisDocx.Core.Validation;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class EquationRenderer
{
    private const string MathNs = OmmlSafetyValidator.MathNamespace;
    private readonly ThesisFormatSpec _format;
    private readonly RelationshipManager _relationshipManager;
    private int _currentChapter;
    private int _equationIndex;

    public EquationRenderer(ThesisFormatSpec format, RelationshipManager relationshipManager)
    {
        _format = format;
        _relationshipManager = relationshipManager;
    }

    public void NotifyHeading(HeadingBlock heading)
    {
        var restartLevel = _format.Equations.Numbering.RestartByHeadingLevel;
        if (heading.Numbered && heading.Level == restartLevel)
        {
            _currentChapter++;
            _equationIndex = 0;
        }
    }

    public IEnumerable<OpenXmlElement> Render(EquationBlock equation)
    {
        var numberingEnabled = equation.Numbering?.Enabled ?? _format.Equations.Numbering.Enabled;
        string? number = null;
        if (numberingEnabled)
        {
            _equationIndex++;
            number = FormatNumber(equation.Numbering);
        }

        var paragraph = new W.Paragraph(CreateParagraphProperties(equation.Alignment));
        var bookmarkName = equation.BookmarkId ?? equation.BookmarkName ?? (numberingEnabled ? equation.Id : null);
        string? bookmarkId = null;
        if (!string.IsNullOrWhiteSpace(bookmarkName))
        {
            bookmarkId = _relationshipManager.AllocateBookmarkId().ToString();
            paragraph.AppendChild(new W.BookmarkStart { Id = bookmarkId, Name = bookmarkName });
        }

        paragraph.AppendChild(CreateMathElement(equation));

        if (number is not null && _format.Equations.Numbering.Position == EquationNumberPosition.Right)
        {
            paragraph.AppendChild(new W.Run(new W.TabChar()));
            paragraph.AppendChild(new W.Run(new W.Text(number)));
        }

        if (bookmarkId is not null)
        {
            paragraph.AppendChild(new W.BookmarkEnd { Id = bookmarkId });
        }

        yield return paragraph;

        if (!string.IsNullOrWhiteSpace(equation.Caption) || (number is not null && _format.Equations.Numbering.Position == EquationNumberPosition.Caption))
        {
            var captionText = string.IsNullOrWhiteSpace(equation.Caption)
                ? number!
                : number is null ? equation.Caption! : $"{number} {equation.Caption}";
            yield return new W.Paragraph(
                new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = _format.Equations.CaptionStyle },
                    new W.Justification { Val = W.JustificationValues.Center }),
                new W.Run(new W.Text(captionText)));
        }
    }

    private W.ParagraphProperties CreateParagraphProperties(TextAlignment alignment)
    {
        return new W.ParagraphProperties(
            new W.ParagraphStyleId { Val = StyleIds.ThesisBody },
            new W.Tabs(new W.TabStop { Val = W.TabStopValues.Right, Position = 9000 }),
            new W.SpacingBetweenLines
            {
                Before = UnitConverter.PointsToTwips(_format.Equations.SpacingBeforePt).ToString(),
                After = UnitConverter.PointsToTwips(_format.Equations.SpacingAfterPt).ToString()
            },
            new W.Justification { Val = StyleBuilder.ToJustification(alignment) });
    }

    private OpenXmlElement CreateMathElement(EquationBlock equation)
    {
        var sourceType = equation.SourceType;
        if (sourceType == EquationSourceType.Omml && !string.IsNullOrWhiteSpace(equation.Omml))
        {
            var safety = OmmlSafetyValidator.Validate(equation.Omml, "$.omml");
            if (!safety.IsValid)
            {
                return new M.OfficeMath(new M.Run(new M.Text(equation.Placeholder ?? string.Empty)));
            }

            var rootName = XDocument.Parse(equation.Omml, LoadOptions.PreserveWhitespace).Root?.Name.LocalName;
            return rootName == "oMathPara"
                ? new M.Paragraph(equation.Omml)
                : new M.OfficeMath(equation.Omml);
        }

        if (sourceType == EquationSourceType.Latex && !string.IsNullOrWhiteSpace(equation.Latex))
        {
            return new M.OfficeMath(CreateLatexSubsetOmml(equation.Latex));
        }

        var text = !string.IsNullOrWhiteSpace(equation.PlainText)
            ? equation.PlainText!
            : !string.IsNullOrWhiteSpace(equation.Placeholder) ? equation.Placeholder : string.Empty;
        return new M.OfficeMath(CreatePlainOmml(text));
    }

    private string FormatNumber(EquationNumberingSpec? overrideSpec)
    {
        var format = overrideSpec?.Format ?? _format.Equations.Numbering.Format;
        return format
            .Replace("{chapter}", Math.Max(_currentChapter, 1).ToString(), StringComparison.Ordinal)
            .Replace("{index}", _equationIndex.ToString(), StringComparison.Ordinal);
    }

    public static string CreatePlainOmml(string text)
    {
        return $"""<m:oMath xmlns:m="{MathNs}"><m:r><m:t>{SecurityElement.Escape(text)}</m:t></m:r></m:oMath>""";
    }

    public static string CreateLatexSubsetOmml(string latex)
    {
        var normalized = latex.Trim();
        var match = Regex.Match(normalized, @"^(?<base>.+?)(?<op>[\^_])(?<arg>[A-Za-z0-9]+)$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return CreatePlainOmml(normalized);
        }

        var baseText = SecurityElement.Escape(match.Groups["base"].Value);
        var argText = SecurityElement.Escape(match.Groups["arg"].Value);
        return match.Groups["op"].Value == "^"
            ? $"""<m:oMath xmlns:m="{MathNs}"><m:sSup><m:e><m:r><m:t>{baseText}</m:t></m:r></m:e><m:sup><m:r><m:t>{argText}</m:t></m:r></m:sup></m:sSup></m:oMath>"""
            : $"""<m:oMath xmlns:m="{MathNs}"><m:sSub><m:e><m:r><m:t>{baseText}</m:t></m:r></m:e><m:sub><m:r><m:t>{argText}</m:t></m:r></m:sub></m:sSub></m:oMath>""";
    }

    public static bool IsLatexSubsetSupported(string latex)
    {
        return Regex.IsMatch(latex.Trim(), @"^.+?[\^_][A-Za-z0-9]+$", RegexOptions.CultureInvariant);
    }
}
