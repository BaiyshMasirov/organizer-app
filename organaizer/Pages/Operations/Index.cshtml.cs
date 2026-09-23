using System.Drawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations;

public sealed class IndexModel(FinanceDbContext db, Dispatcher dispatcher, ActiveCompany active) : PageModel
{
    public const int PageSize = 20;
    public const string DateDescending = "dateDesc";
    public const string DateAscending = "dateAsc";

    public List<TradeOperation> Items { get; private set; } = [];
    public List<Domain.Company> Companies { get; private set; } = [];
    public string? Search { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string? TypeCode { get; private set; }
    public string? Status { get; private set; }
    public DateTime? From { get; private set; }
    public DateTime? To { get; private set; }
    public string Sort { get; private set; } = DateDescending;
    public int PageNumber { get; private set; } = 1;
    public int TotalPages { get; private set; }
    public int TotalCount { get; private set; }

    public async Task OnGetAsync(string? search, Guid? companyId, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort, int pageNumber = 1)
    {
        SetFilters(search, typeCode, status, from, to, sort);
        CompanyId = active.RequiredId;
        PageNumber = Math.Max(1, pageNumber);
        Companies = await db.Companies.AsNoTracking().Where(x => x.Id == active.RequiredId).ToListAsync();

        var query = BuildQuery(Search, TypeCode, Status, From, To);
        TotalCount = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Min(PageNumber, TotalPages);
        Items = await ApplySorting(query, Sort)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetExportAsync(string? search, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort)
    {
        SetFilters(search, typeCode, status, from, to, sort);
        var items = await ApplySorting(BuildQuery(Search, TypeCode, Status, From, To), Sort).ToListAsync();

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Операции");
        var headers = new[] { "Дата", "Тип / клиент", "Отдаем", "Откуда отправляем", "Получаем", "Куда получаем", "Прибыль" };
        for (var column = 1; column <= headers.Length; column++) sheet.Cells[1, column].Value = headers[column - 1];

        for (var index = 0; index < items.Count; index++)
        {
            var row = index + 2;
            var operation = items[index];
            sheet.Cells[row, 1].Value = operation.OccurredAt.DateTime;
            sheet.Cells[row, 1].Style.Numberformat.Format = "dd.mm.yyyy";
            sheet.Cells[row, 2].Value = $"{OperationTypes.All.GetValueOrDefault(operation.TypeCode, operation.TypeCode)}\n{operation.Counterparty?.Name ?? "Без клиента"}";
            sheet.Cells[row, 2].Style.WrapText = true;
            SetMoneyCell(sheet.Cells[row, 3], operation.SellAmount, operation.SellCurrency);
            sheet.Cells[row, 4].Value = operation.SourceAccount ?? "—";
            SetMoneyCell(sheet.Cells[row, 5], operation.BuyAmount, operation.BuyCurrency);
            sheet.Cells[row, 6].Value = operation.DestinationAccount ?? "—";
            SetMoneyCell(sheet.Cells[row, 7], operation.BaseCurrencyProfit, "USD");
        }

        using (var header = sheet.Cells[1, 1, 1, headers.Length])
        {
            header.Style.Font.Bold = true;
            header.Style.Font.Color.SetColor(Color.White);
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 78, 121));
            header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        sheet.Row(1).Height = 24;
        sheet.View.FreezePanes(2, 1);
        sheet.Cells[1, 1, Math.Max(items.Count + 1, 1), headers.Length].AutoFilter = true;
        sheet.Column(1).Width = 14;
        sheet.Column(2).Width = 42;
        sheet.Column(3).Width = 22;
        sheet.Column(4).Width = 24;
        sheet.Column(5).Width = 22;
        sheet.Column(6).Width = 24;
        sheet.Column(7).Width = 20;
        sheet.Cells[2, 3, Math.Max(items.Count + 1, 2), 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        sheet.Cells[1, 1, Math.Max(items.Count + 1, 1), headers.Length].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        var fileName = $"operations_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> OnPostCompleteAsync(Guid id, string? search, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort, int pageNumber = 1)
    {
        await dispatcher.Send(new CompleteOperationCommand(id));
        TempData["Message"] = "Операция завершена";
        return RedirectToPage(new { search, typeCode, status, from = from?.ToString("yyyy-MM-dd"), to = to?.ToString("yyyy-MM-dd"), sort, pageNumber });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id, string? search, Guid? companyId, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort, int pageNumber = 1)
    {
        await dispatcher.Send(new CancelOperationCommand(id));
        TempData["Message"] = "Операция отменена";
        return RedirectToPage(new { search, companyId, typeCode, status, from = from?.ToString("yyyy-MM-dd"), to = to?.ToString("yyyy-MM-dd"), sort, pageNumber });
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id, string? search, Guid? companyId, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort, int pageNumber = 1)
    {
        var restored = await dispatcher.Send(new ReactivateOperationCommand(id));
        TempData["Message"] = restored ? "Операция восстановлена и переведена в статус «Создана»" : "Операцию не удалось восстановить";
        return RedirectToPage(new { search, companyId, typeCode, status, from = from?.ToString("yyyy-MM-dd"), to = to?.ToString("yyyy-MM-dd"), sort, pageNumber });
    }

    private void SetFilters(string? search, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort)
    {
        Search = search?.Trim();
        TypeCode = typeCode;
        Status = status;
        From = from;
        To = to;
        Sort = sort == DateAscending ? DateAscending : DateDescending;
    }

    private IQueryable<TradeOperation> BuildQuery(string? search, string? typeCode, string? status, DateTime? from, DateTime? to)
    {
        var query = db.Operations.AsNoTracking().Include(x => x.Counterparty).Where(x => x.CompanyId == active.RequiredId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(x =>
                (x.Counterparty != null && x.Counterparty.Name.ToLower().Contains(term)) ||
                (x.Note != null && x.Note.ToLower().Contains(term)) ||
                x.SellCurrency.ToLower().Contains(term) ||
                x.BuyCurrency.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(typeCode)) query = query.Where(x => x.TypeCode == typeCode);
        query = status switch
        {
            OperationStatuses.Completed => query.Where(x => x.Status == OperationStatus.Settled),
            OperationStatuses.Cancelled => query.Where(x => x.Status == OperationStatus.Cancelled),
            OperationStatuses.Created => query.Where(x => x.Status != OperationStatus.Settled && x.Status != OperationStatus.Cancelled),
            _ => query
        };
        if (from.HasValue)
        {
            var start = new DateTimeOffset(DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc));
            query = query.Where(x => x.OccurredAt >= start);
        }
        if (to.HasValue)
        {
            var end = new DateTimeOffset(DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc));
            query = query.Where(x => x.OccurredAt < end);
        }
        return query;
    }

    private static IOrderedQueryable<TradeOperation> ApplySorting(IQueryable<TradeOperation> query, string sort) =>
        sort == DateAscending
            ? query.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id);

    private static void SetMoneyCell(ExcelRange cell, decimal amount, string currency)
    {
        cell.Value = amount;
        var safeCurrency = currency.Replace("\"", "\"\"");
        cell.Style.Numberformat.Format = $"#,##0.00 \"{safeCurrency}\"";
    }
}
