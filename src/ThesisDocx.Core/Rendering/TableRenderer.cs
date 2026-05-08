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

    public TableRenderer(ThesisFormatSpec format, CaptionRenderer captionRenderer, RelationshipManager relationshipManager)
    {
        _format = format;
        _captionRenderer = captionRenderer;
        _relationshipManager = relationshipManager;
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
                tableCell.AppendChild(CreateCellParagraph(block, cell, isHeader));
            }
        }
        else
        {
            tableCell.AppendChild(CreateTextCellParagraph(cell, isHeader));
        }

        return tableCell;
    }

    private W.Paragraph CreateCellParagraph(BlockNode block, TableCellNode cell, bool isHeader)
    {
        return block switch
        {
            ParagraphBlock paragraph => CreateInlineCellParagraph(paragraph.Inlines, cell, isHeader),
            HeadingBlock heading => CreateInlineCellParagraph(heading.Inlines, cell, isHeader),
            _ => CreateTextCellParagraph(new TableCellNode { Text = string.Empty, Alignment = cell.Alignment }, isHeader)
        };
    }

    private W.Paragraph CreateInlineCellParagraph(IEnumerable<InlineNode> inlines, TableCellNode cell, bool isHeader)
    {
        var paragraph = new W.Paragraph(CreateCellParagraphProperties(cell));
        foreach (var text in inlines.OfType<TextInline>())
        {
            var run = ParagraphRenderer.CreateTextRun(text);
            if (isHeader)
            {
                run.RunProperties ??= new W.RunProperties();
                run.RunProperties.AppendChild(new W.Bold());
            }

            paragraph.AppendChild(run);
        }

        if (!paragraph.Elements<W.Run>().Any())
        {
            paragraph.AppendChild(new W.Run(new W.Text(string.Empty)));
        }

        return paragraph;
    }

    private W.Paragraph CreateTextCellParagraph(TableCellNode cell, bool isHeader)
    {
        var run = new W.Run(new W.Text(cell.Text) { Space = SpaceProcessingModeValues.Preserve });
        if (isHeader)
        {
            run.PrependChild(new W.RunProperties(new W.Bold()));
        }

        return new W.Paragraph(CreateCellParagraphProperties(cell), run);
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
