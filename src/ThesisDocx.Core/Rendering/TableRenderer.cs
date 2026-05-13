using DocumentFormat.OpenXml;
using ThesisDocx.Core.Models;
using ThesisDocx.Core.OpenXml;
using ThesisDocx.Core.Utilities;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ThesisDocx.Core.Rendering;

public sealed class TableRenderer
{
    private readonly ThesisFormatSpec _format;
    private readonly CaptionRenderer _captionRenderer;
    private readonly RelationshipManager _relationshipManager;
    private readonly ParagraphRenderer _paragraphRenderer;

    public TableRenderer(ThesisFormatSpec format, CaptionRenderer captionRenderer, RelationshipManager relationshipManager, ParagraphRenderer paragraphRenderer)
    {
        _format = format;
        _captionRenderer = captionRenderer;
        _relationshipManager = relationshipManager;
        _paragraphRenderer = paragraphRenderer;
    }

    public IEnumerable<OpenXmlElement> Render(TableBlock block)
    {
        var captionPosition = block.CaptionPosition ?? _format.Tables.Caption.Position;
        if (captionPosition == CaptionPosition.Before)
        {
            yield return _captionRenderer.CreateTableCaption(block.Caption);
        }

        yield return CreateTable(block);

        if (captionPosition == CaptionPosition.After)
        {
            yield return _captionRenderer.CreateTableCaption(block.Caption);
        }
    }

    private W.Table CreateTable(TableBlock block)
    {
        var table = new W.Table(CreateTableProperties(block));
        table.AppendChild(CreateTableGrid(block));
        string? bookmarkRangeId = null;

        for (var rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
        {
            var row = block.Rows[rowIndex];
            var tableRow = new W.TableRow();
            var rowProperties = new W.TableRowProperties();
            if ((row.IsHeader && ((_format.Tables.RepeatHeaderRowsDefault > 0 && rowIndex < _format.Tables.RepeatHeaderRowsDefault) || block.RepeatHeaderRows is null))
                || (block.RepeatHeaderRows.HasValue && rowIndex < block.RepeatHeaderRows.Value))
            {
                rowProperties.AppendChild(new W.TableHeader());
            }

            var allowBreak = block.AllowRowBreakAcrossPages ?? _format.Tables.AllowRowBreakAcrossPagesDefault;
            if (row.CantSplit == true || !allowBreak)
            {
                rowProperties.AppendChild(new W.CantSplit());
            }

            if (row.HeightPt.HasValue)
            {
                rowProperties.AppendChild(new W.TableRowHeight { Val = (UInt32Value)(uint)UnitConverter.PointsToTwips(row.HeightPt.Value) });
            }

            if (rowProperties.HasChildren)
            {
                tableRow.AppendChild(rowProperties);
            }

            foreach (var cell in row.Cells)
            {
                var tableCell = CreateCell(cell, row.IsHeader, block.Style);
                if (bookmarkRangeId is null && !string.IsNullOrWhiteSpace(block.BookmarkId))
                {
                    bookmarkRangeId = _relationshipManager.AllocateBookmarkId().ToString();
                    var firstParagraph = tableCell.Elements<W.Paragraph>().FirstOrDefault();
                    if (firstParagraph?.ParagraphProperties is not null)
                    {
                        firstParagraph.InsertAfter(new W.BookmarkStart { Id = bookmarkRangeId, Name = block.BookmarkId }, firstParagraph.ParagraphProperties);
                    }
                    else
                    {
                        firstParagraph?.PrependChild(new W.BookmarkStart { Id = bookmarkRangeId, Name = block.BookmarkId });
                    }

                    firstParagraph?.AppendChild(new W.BookmarkEnd { Id = bookmarkRangeId });
                }

                tableRow.AppendChild(tableCell);
            }

            table.AppendChild(tableRow);
        }

        return table;
    }

    private W.TableProperties CreateTableProperties(TableBlock block)
    {
        var properties = new W.TableProperties();
        properties.AppendChild(ToTableWidth(block.Width ?? _format.Tables.DefaultWidth ?? new TableWidthSpec { Type = TableWidthKind.Percent, Value = _format.Tables.WidthPercent }));

        var alignment = block.Alignment ?? _format.Tables.DefaultAlignment;
        properties.AppendChild(new W.TableJustification { Val = ToTableJustification(alignment) });

        properties.AppendChild(CreateBorders(block.Borders ?? (block.Style == TableStyleKind.ThreeLine ? _format.Tables.ThreeLineTableBorders : _format.Tables.DefaultBorders)));

        var layout = block.Layout ?? _format.Tables.DefaultLayout;
        properties.AppendChild(new W.TableLayout
        {
            Type = layout == TableLayoutKind.Fixed ? W.TableLayoutValues.Fixed : W.TableLayoutValues.Autofit
        });
        properties.AppendChild(CreateCellMargins(block.CellMargins ?? _format.Tables.DefaultCellMargins));

        return properties;
    }

    private static W.TableGrid CreateTableGrid(TableBlock block)
    {
        var grid = new W.TableGrid();
        var columns = block.Rows.Select(row => row.Cells.Sum(cell => Math.Max(1, cell.GridSpan))).DefaultIfEmpty(0).Max();
        for (var i = 0; i < columns; i++)
        {
            grid.AppendChild(new W.GridColumn { Width = UnitConverter.CentimetersToTwips(4).ToString() });
        }

        return grid;
    }

    private W.TableCell CreateCell(TableCellNode cell, bool isHeader, TableStyleKind tableStyle)
    {
        var tableCell = new W.TableCell();
        var cellProperties = new W.TableCellProperties();

        if (cell.Width is not null)
        {
            cellProperties.AppendChild(ToTableCellWidth(cell.Width));
        }
        else if (cell.WidthCm.HasValue)
        {
            cellProperties.AppendChild(new W.TableCellWidth
            {
                Type = W.TableWidthUnitValues.Dxa,
                Width = UnitConverter.CentimetersToTwips(cell.WidthCm.Value).ToString()
            });
        }

        if (cell.GridSpan > 1)
        {
            cellProperties.AppendChild(new W.GridSpan { Val = cell.GridSpan });
        }

        if (cell.VerticalMerge != VerticalMergeKind.None)
        {
            cellProperties.AppendChild(new W.VerticalMerge
            {
                Val = cell.VerticalMerge == VerticalMergeKind.Restart ? W.MergedCellValues.Restart : W.MergedCellValues.Continue
            });
        }

        if (cell.Borders is not null || (tableStyle == TableStyleKind.ThreeLine && isHeader))
        {
            cellProperties.AppendChild(CreateCellBorders(cell.Borders, tableStyle, isHeader));
        }

        if (!string.IsNullOrWhiteSpace(cell.Shading))
        {
            cellProperties.AppendChild(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = cell.Shading });
        }

        var margins = cell.CellMargins ?? null;
        if (margins is not null)
        {
            cellProperties.AppendChild(CreateTableCellMargins(margins));
        }

        if (cell.VerticalAlignment.HasValue)
        {
            cellProperties.AppendChild(new W.TableCellVerticalAlignment
            {
                Val = cell.VerticalAlignment.Value switch
                {
                    TableCellVerticalAlignment.Center => W.TableVerticalAlignmentValues.Center,
                    TableCellVerticalAlignment.Bottom => W.TableVerticalAlignmentValues.Bottom,
                    _ => W.TableVerticalAlignmentValues.Top
                }
            });
        }

        tableCell.AppendChild(cellProperties);

        if (cell.Blocks.Count > 0)
        {
            foreach (var block in cell.Blocks)
            {
                foreach (var paragraph in CreateCellParagraphs(block, cell, isHeader))
                {
                    tableCell.AppendChild(paragraph);
                }
            }
        }
        else
        {
            tableCell.AppendChild(CreateTextCellParagraph(cell, isHeader));
        }

        return tableCell;
    }

    private IEnumerable<W.Paragraph> CreateCellParagraphs(BlockNode block, TableCellNode cell, bool isHeader)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                yield return ApplyCellParagraphFormatting(_paragraphRenderer.CreateParagraph(paragraph), cell, isHeader);
                break;
            case HeadingBlock heading:
                yield return ApplyCellParagraphFormatting(CreateHeadingCellParagraph(heading), cell, isHeader);
                break;
            case QuoteBlock quote:
                yield return ApplyCellParagraphFormatting(_paragraphRenderer.CreateParagraph(new ParagraphBlock
                {
                    StyleId = StyleIds.Quote,
                    Inlines = quote.Inlines,
                    Alignment = cell.Alignment
                }), cell, isHeader);
                break;
            case ListBlock list:
                foreach (var paragraph in CreateListCellParagraphs(list, cell, isHeader))
                {
                    yield return paragraph;
                }

                break;
            case FootnoteBlock footnote:
                yield return ApplyCellParagraphFormatting(CreateNoteCellParagraph(cell, new FootnoteInline { NoteId = footnote.NoteId, Inlines = footnote.Inlines }), cell, isHeader);
                break;
            case EndnoteBlock endnote:
                yield return ApplyCellParagraphFormatting(CreateNoteCellParagraph(cell, new EndnoteInline { NoteId = endnote.NoteId, Inlines = endnote.Inlines }), cell, isHeader);
                break;
            default:
                yield return CreateTextCellParagraph(new TableCellNode { Text = string.Empty, Alignment = cell.Alignment }, isHeader);
                break;
        }
    }

    private W.Paragraph CreateNoteCellParagraph(TableCellNode cell, InlineNode note)
    {
        var paragraph = new W.Paragraph(CreateCellParagraphProperties(cell));
        foreach (var element in _paragraphRenderer.CreateInlineElements([note]))
        {
            paragraph.AppendChild(element);
        }

        return paragraph;
    }

    private IEnumerable<W.Paragraph> CreateListCellParagraphs(ListBlock list, TableCellNode cell, bool isHeader)
    {
        foreach (var item in list.Items)
        {
            var firstParagraph = item.Blocks.OfType<ParagraphBlock>().FirstOrDefault();
            if (firstParagraph is null)
            {
                continue;
            }

            var paragraph = _paragraphRenderer.CreateParagraph(firstParagraph);
            paragraph.ParagraphProperties ??= new W.ParagraphProperties();
            if (list.Ordered)
            {
                paragraph.ParagraphProperties.AppendChild(new W.NumberingProperties(
                    new W.NumberingLevelReference { Val = 0 },
                    new W.NumberingId { Val = NumberingBuilder.OrderedListNumberingId }));
            }
            else
            {
                InsertRunAfterProperties(paragraph, new W.Run(new W.Text("• ")));
            }

            yield return ApplyCellParagraphFormatting(paragraph, cell, isHeader);
        }
    }

    private W.Paragraph CreateHeadingCellParagraph(HeadingBlock heading)
    {
        var level = Math.Clamp(heading.Level, 1, 3);
        var paragraph = new W.Paragraph(new W.ParagraphProperties(
            new W.ParagraphStyleId
            {
                Val = level switch
                {
                    1 => StyleIds.Heading1,
                    2 => StyleIds.Heading2,
                    _ => StyleIds.Heading3
                }
            },
            new W.OutlineLevel { Val = level - 1 }));
        foreach (var inline in _paragraphRenderer.CreateInlineElements(heading.Inlines))
        {
            paragraph.AppendChild(inline);
        }

        return paragraph;
    }

    private static void InsertRunAfterProperties(W.Paragraph paragraph, W.Run run)
    {
        if (paragraph.ParagraphProperties is not null)
        {
            paragraph.InsertAfter(run, paragraph.ParagraphProperties);
        }
        else
        {
            paragraph.PrependChild(run);
        }
    }

    private W.Paragraph CreateTextCellParagraph(TableCellNode cell, bool isHeader)
    {
        var run = new W.Run(new W.Text(cell.Text) { Space = SpaceProcessingModeValues.Preserve });
        if (isHeader)
        {
            run.PrependChild(new W.RunProperties(new W.Bold()));
        }

        return ApplyCellParagraphFormatting(new W.Paragraph(CreateCellParagraphProperties(cell), run), cell, isHeader);
    }

    private static W.ParagraphProperties CreateCellParagraphProperties(TableCellNode cell)
    {
        var paragraphProperties = new W.ParagraphProperties(new W.ParagraphStyleId { Val = StyleIds.ThesisBody });
        if (cell.Alignment.HasValue)
        {
            paragraphProperties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(cell.Alignment.Value) });
        }

        return paragraphProperties;
    }

    private static W.Paragraph ApplyCellParagraphFormatting(W.Paragraph paragraph, TableCellNode cell, bool isHeader)
    {
        paragraph.ParagraphProperties ??= new W.ParagraphProperties();
        if (paragraph.ParagraphProperties.ParagraphStyleId is null)
        {
            paragraph.ParagraphProperties.PrependChild(new W.ParagraphStyleId { Val = StyleIds.ThesisBody });
        }

        if (cell.Alignment.HasValue)
        {
            paragraph.ParagraphProperties.RemoveAllChildren<W.Justification>();
            paragraph.ParagraphProperties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(cell.Alignment.Value) });
        }

        if (cell.Paragraph is not null)
        {
            ApplyCellParagraphSpec(paragraph.ParagraphProperties, cell.Paragraph, cell.Font);
        }

        if (cell.Font is not null || isHeader)
        {
            foreach (var run in paragraph.Descendants<W.Run>())
            {
                ApplyCellRunFormatting(run, cell.Font, isHeader);
            }
        }

        if (!paragraph.Descendants<W.Run>().Any())
        {
            paragraph.AppendChild(new W.Run(new W.Text(string.Empty)));
        }

        return paragraph;
    }

    private static void ApplyCellParagraphSpec(W.ParagraphProperties properties, ParagraphFormatSpec paragraph, FontFormatSpec? font)
    {
        var style = properties.ParagraphStyleId?.CloneNode(true);
        var numbering = properties.NumberingProperties?.CloneNode(true);
        var outline = properties.OutlineLevel?.CloneNode(true);
        foreach (var child in properties.ChildElements.ToList())
        {
            child.Remove();
        }

        properties.AppendChild(style as W.ParagraphStyleId ?? new W.ParagraphStyleId { Val = StyleIds.ThesisBody });
        properties.AppendChild(new W.WidowControl { Val = paragraph.WidowControl });
        if (numbering is W.NumberingProperties numberingProperties)
        {
            properties.AppendChild(numberingProperties);
        }

        properties.AppendChild(StyleBuilder.CreateSpacing(paragraph));

        var indentation = new W.Indentation();
        var fontSize = font?.SizePt > 0 ? font.SizePt : 12;
        if (paragraph.FirstLineIndentChars > 0)
        {
            indentation.FirstLine = UnitConverter.PointsToTwips(fontSize * paragraph.FirstLineIndentChars).ToString();
        }

        if (paragraph.HangingIndentCm > 0)
        {
            indentation.Hanging = UnitConverter.CentimetersToTwips(paragraph.HangingIndentCm).ToString();
        }

        if (indentation.HasAttributes)
        {
            properties.AppendChild(indentation);
        }

        properties.AppendChild(new W.Justification { Val = StyleBuilder.ToJustification(paragraph.Alignment) });
        if (outline is W.OutlineLevel outlineLevel)
        {
            properties.AppendChild(outlineLevel);
        }
    }

    private static void ApplyCellRunFormatting(W.Run run, FontFormatSpec? font, bool isHeader)
    {
        var runProperties = run.RunProperties;
        if (runProperties is null)
        {
            runProperties = new W.RunProperties();
            run.PrependChild(runProperties);
        }

        var runStyle = runProperties.RunStyle?.CloneNode(true);
        var color = runProperties.Color?.CloneNode(true);
        var underline = runProperties.Underline?.CloneNode(true);
        var vertical = runProperties.VerticalTextAlignment?.CloneNode(true);
        foreach (var child in runProperties.ChildElements.ToList())
        {
            child.Remove();
        }

        if (runStyle is W.RunStyle clonedRunStyle)
        {
            runProperties.AppendChild(clonedRunStyle);
        }

        if (font is not null)
        {
            runProperties.AppendChild(StyleBuilder.CreateRunFonts(font));
        }

        if (isHeader || font?.Bold == true)
        {
            runProperties.AppendChild(new W.Bold());
            runProperties.AppendChild(new W.BoldComplexScript());
        }

        if (font?.Italic == true)
        {
            runProperties.AppendChild(new W.Italic());
            runProperties.AppendChild(new W.ItalicComplexScript());
        }

        if (color is W.Color clonedColor)
        {
            runProperties.AppendChild(clonedColor);
        }

        if (font is not null)
        {
            runProperties.AppendChild(new W.FontSize { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() });
            runProperties.AppendChild(new W.FontSizeComplexScript { Val = UnitConverter.PointsToHalfPoints(font.SizePt).ToString() });
        }

        if (underline is W.Underline clonedUnderline)
        {
            runProperties.AppendChild(clonedUnderline);
        }

        if (vertical is W.VerticalTextAlignment clonedVertical)
        {
            runProperties.AppendChild(clonedVertical);
        }
    }

    private static W.TableWidth ToTableWidth(TableWidthSpec width)
    {
        return width.Type switch
        {
            TableWidthKind.Auto => new W.TableWidth { Type = W.TableWidthUnitValues.Auto, Width = "0" },
            TableWidthKind.Dxa => new W.TableWidth { Type = W.TableWidthUnitValues.Dxa, Width = ((int)Math.Round(width.Value ?? 0)).ToString() },
            _ => new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = ((int)Math.Round((width.Value ?? 100) * 50)).ToString() }
        };
    }

    private static W.TableCellWidth ToTableCellWidth(TableWidthSpec width)
    {
        return width.Type switch
        {
            TableWidthKind.Auto => new W.TableCellWidth { Type = W.TableWidthUnitValues.Auto, Width = "0" },
            TableWidthKind.Dxa => new W.TableCellWidth { Type = W.TableWidthUnitValues.Dxa, Width = ((int)Math.Round(width.Value ?? 0)).ToString() },
            _ => new W.TableCellWidth { Type = W.TableWidthUnitValues.Pct, Width = ((int)Math.Round((width.Value ?? 100) * 50)).ToString() }
        };
    }

    private static W.TableCellMarginDefault CreateCellMargins(TableCellMarginsSpec margins)
    {
        var top = UnitConverter.CentimetersToTwips(margins.TopCm ?? 0).ToString();
        var bottom = UnitConverter.CentimetersToTwips(margins.BottomCm ?? 0).ToString();
        var left = UnitConverter.CentimetersToTwips(margins.LeftCm ?? 0);
        var right = UnitConverter.CentimetersToTwips(margins.RightCm ?? 0);
        return new W.TableCellMarginDefault(
            new W.TopMargin { Width = top, Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellLeftMargin { Width = (Int16Value)left, Type = W.TableWidthValues.Dxa },
            new W.BottomMargin { Width = bottom, Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellRightMargin { Width = (Int16Value)right, Type = W.TableWidthValues.Dxa });
    }

    private static W.TableCellMargin CreateTableCellMargins(TableCellMarginsSpec margins)
    {
        return new W.TableCellMargin(
            new W.TopMargin { Width = UnitConverter.CentimetersToTwips(margins.TopCm ?? 0).ToString(), Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellLeftMargin { Width = (Int16Value)UnitConverter.CentimetersToTwips(margins.LeftCm ?? 0), Type = W.TableWidthValues.Dxa },
            new W.BottomMargin { Width = UnitConverter.CentimetersToTwips(margins.BottomCm ?? 0).ToString(), Type = W.TableWidthUnitValues.Dxa },
            new W.TableCellRightMargin { Width = (Int16Value)UnitConverter.CentimetersToTwips(margins.RightCm ?? 0), Type = W.TableWidthValues.Dxa });
    }

    private static W.TableBorders CreateBorders(TableBordersSpec borders)
    {
        return new W.TableBorders(
            ToTopBorder(borders.Top),
            ToLeftBorder(borders.Left),
            ToBottomBorder(borders.Bottom),
            ToRightBorder(borders.Right),
            ToInsideHorizontalBorder(borders.InsideH),
            ToInsideVerticalBorder(borders.InsideV));
    }

    private static W.TableCellBorders CreateCellBorders(TableBordersSpec? borders, TableStyleKind tableStyle, bool isHeader)
    {
        if (borders is null && tableStyle == TableStyleKind.ThreeLine && isHeader)
        {
            borders = new TableBordersSpec { Bottom = new BorderSpec { Style = BorderStyleKind.Single, Size = 6, Color = "000000" } };
        }

        borders ??= new TableBordersSpec();
        return new W.TableCellBorders(
            ToTopBorder(borders.Top),
            ToLeftBorder(borders.Left),
            ToBottomBorder(borders.Bottom),
            ToRightBorder(borders.Right));
    }

    private static W.TopBorder ToTopBorder(BorderSpec? border)
    {
        var value = ToBorderValues(border);
        return new W.TopBorder { Val = value.Val, Size = value.Size, Color = value.Color, Space = value.Space };
    }

    private static W.BottomBorder ToBottomBorder(BorderSpec? border)
    {
        var value = ToBorderValues(border);
        return new W.BottomBorder { Val = value.Val, Size = value.Size, Color = value.Color, Space = value.Space };
    }

    private static W.LeftBorder ToLeftBorder(BorderSpec? border)
    {
        var value = ToBorderValues(border);
        return new W.LeftBorder { Val = value.Val, Size = value.Size, Color = value.Color, Space = value.Space };
    }

    private static W.RightBorder ToRightBorder(BorderSpec? border)
    {
        var value = ToBorderValues(border);
        return new W.RightBorder { Val = value.Val, Size = value.Size, Color = value.Color, Space = value.Space };
    }

    private static W.InsideHorizontalBorder ToInsideHorizontalBorder(BorderSpec? border)
    {
        var value = ToBorderValues(border);
        return new W.InsideHorizontalBorder { Val = value.Val, Size = value.Size, Color = value.Color, Space = value.Space };
    }

    private static W.InsideVerticalBorder ToInsideVerticalBorder(BorderSpec? border)
    {
        var value = ToBorderValues(border);
        return new W.InsideVerticalBorder { Val = value.Val, Size = value.Size, Color = value.Color, Space = value.Space };
    }

    private static (W.BorderValues Val, UInt32Value Size, string Color, UInt32Value Space) ToBorderValues(BorderSpec? border)
    {
        border ??= new BorderSpec();
        return (border.Style switch
        {
            BorderStyleKind.Nil => W.BorderValues.Nil,
            BorderStyleKind.None => W.BorderValues.None,
            BorderStyleKind.Double => W.BorderValues.Double,
            BorderStyleKind.Dotted => W.BorderValues.Dotted,
            BorderStyleKind.Dashed => W.BorderValues.Dashed,
            _ => W.BorderValues.Single
        }, (UInt32Value)(uint)border.Size, border.Color, (UInt32Value)(uint)border.Space);
    }

    private static W.TableRowAlignmentValues ToTableJustification(TextAlignment alignment)
    {
        return alignment switch
        {
            TextAlignment.Center => W.TableRowAlignmentValues.Center,
            TextAlignment.Right => W.TableRowAlignmentValues.Right,
            _ => W.TableRowAlignmentValues.Left
        };
    }
}
