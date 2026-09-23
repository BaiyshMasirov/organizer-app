using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace organaizer.Infrastructure;

public sealed record ReconciliationLine(Guid OperationId, DateTimeOffset Date, string Description, decimal Debit, decimal Credit, decimal UsdEquivalent);

public static class ReconciliationActExcel
{
    public static byte[] Create(string company, string counterparty, string currency, DateTime from, DateTime to,
        decimal openingDebit, decimal openingCredit, decimal openingUsd, IReadOnlyList<ReconciliationLine> lines)
    {
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Акт сверки");
        ws.View.ShowGridLines = false;
        ws.PrinterSettings.Orientation = eOrientation.Landscape;
        ws.PrinterSettings.FitToPage = true;
        ws.PrinterSettings.FitToWidth = 1;
        ws.PrinterSettings.FitToHeight = 0;

        ws.Cells[1, 1, 1, 7].Merge = true;
        ws.Cells[1, 1].Value = "АКТ";
        ws.Cells[1, 1].Style.Font.Size = 16;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Cells[2, 1, 2, 7].Merge = true;
        ws.Cells[2, 1].Value = $"сверки взаиморасчетов между {company} и «{counterparty}»";
        ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Cells[2, 1].Style.Font.Bold = true;
        ws.Cells[3, 1, 3, 7].Merge = true;
        ws.Cells[3, 1].Value = $"с {from:dd.MM.yyyy} по {to:dd.MM.yyyy} · валюта сверки {currency}";
        ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        ws.Cells[5, 1, 6, 1].Merge = true;
        ws.Cells[5, 2, 6, 2].Merge = true;
        ws.Cells[5, 3, 5, 4].Merge = true;
        ws.Cells[5, 5, 5, 6].Merge = true;
        ws.Cells[5, 7, 6, 7].Merge = true;
        ws.Cells[5, 1].Value = "№";
        ws.Cells[5, 2].Value = "Содержание записи";
        ws.Cells[5, 3].Value = company;
        ws.Cells[5, 5].Value = counterparty;
        ws.Cells[6, 3].Value = "Дт"; ws.Cells[6, 4].Value = "Кт";
        ws.Cells[6, 5].Value = "Дт"; ws.Cells[6, 6].Value = "Кт";
        ws.Cells[5, 7].Value = "Эквивалент, USD";
        using (var header = ws.Cells[5, 1, 6, 7])
        {
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(221, 247, 244));
            header.Style.Font.Bold = true;
            header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            header.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        var row = 7;
        ws.Cells[row, 2].Value = "Сальдо начальное:";
        ws.Cells[row, 2].Style.Font.Bold = true;
        SetSides(ws, row, openingDebit, openingCredit);
        ws.Cells[row, 7].Value = openingUsd;
        row++;
        var number = 1;
        foreach (var line in lines)
        {
            ws.Cells[row, 1].Value = number++;
            ws.Cells[row, 2].Value = $"{line.Date:dd.MM.yyyy} · {line.Description}";
            SetSides(ws, row, line.Debit, line.Credit);
            ws.Cells[row, 7].Value = line.UsdEquivalent;
            row++;
        }

        var debitTurnover = lines.Sum(x => x.Debit);
        var creditTurnover = lines.Sum(x => x.Credit);
        ws.Cells[row, 2].Value = "Итого обороты:";
        ws.Cells[row, 2].Style.Font.Bold = true;
        SetSides(ws, row, debitTurnover, creditTurnover);
        ws.Cells[row, 7].Value = lines.Sum(x => x.UsdEquivalent);
        ws.Cells[row, 3, row, 7].Style.Font.Bold = true;
        row++;

        var closing = openingDebit - openingCredit + debitTurnover - creditTurnover;
        var closingDebit = closing >= 0 ? closing : 0;
        var closingCredit = closing < 0 ? -closing : 0;
        ws.Cells[row, 2].Value = "Сальдо конечное:";
        ws.Cells[row, 2].Style.Font.Bold = true;
        SetSides(ws, row, closingDebit, closingCredit);
        var closingUsd = openingUsd + lines.Sum(x => x.UsdEquivalent);
        ws.Cells[row, 7].Value = closingUsd;
        ws.Cells[row, 3, row, 7].Style.Font.Bold = true;

        using (var table = ws.Cells[5, 1, row, 7])
        {
            table.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            table.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            table.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }
        ws.Cells[7, 3, row, 7].Style.Numberformat.Format = "#,##0.00########";
        ws.Cells[7, 3, row, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

        row += 2;
        ws.Cells[row, 2, row, 7].Merge = true;
        ws.Cells[row, 2].Value = closing > 0
            ? $"Задолженность «{counterparty}» перед {company} на {to.AddDays(1):dd.MM.yyyy} составляет {closing:N2} {currency}. Эквивалент: {closingUsd:N2} USD."
            : closing < 0
                ? $"Задолженность {company} перед «{counterparty}» на {to.AddDays(1):dd.MM.yyyy} составляет {Math.Abs(closing):N2} {currency}. Эквивалент: {Math.Abs(closingUsd):N2} USD."
                : $"Задолженность между {company} и «{counterparty}» на {to.AddDays(1):dd.MM.yyyy} отсутствует.";
        ws.Cells[row, 2].Style.Font.Bold = true;

        ws.Column(1).Width = 7;
        ws.Column(2).Width = 54;
        for (var col = 3; col <= 6; col++) ws.Column(col).Width = 18;
        ws.Column(7).Width = 22;
        ws.Row(5).Height = 26;
        ws.Row(6).Height = 22;
        ws.Cells[1, 1, row, 7].Style.Font.Name = "Arial";
        ws.Cells[1, 1, row, 7].Style.WrapText = true;
        ws.PrinterSettings.PrintArea = ws.Cells[1, 1, row, 7];
        return package.GetAsByteArray();
    }

    private static void SetSides(ExcelWorksheet ws, int row, decimal debit, decimal credit)
    {
        if (debit != 0) { ws.Cells[row, 3].Value = debit; ws.Cells[row, 6].Value = debit; }
        if (credit != 0) { ws.Cells[row, 4].Value = credit; ws.Cells[row, 5].Value = credit; }
    }
}
