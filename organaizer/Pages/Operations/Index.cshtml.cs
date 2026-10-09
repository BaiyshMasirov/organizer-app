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
    public static string TypeName(string code) => code == TransferService.TypeCode ? TransferService.Title : OperationTypes.All.GetValueOrDefault(code,code);
    public static DateTimeOffset DisplayDate(TradeOperation item) => item.TypeCode==TransferService.TypeCode ? item.OccurredAt.ToOffset(TimeSpan.FromHours(5)) : item.OccurredAt;

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
        var items = await ApplySorting(
            BuildQuery(Search, TypeCode, Status, From, To).Where(x => x.Status != OperationStatus.Cancelled), Sort)
            .ToListAsync();

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Операции");
        var headers = new[]
        {
            "№ п/п", "Дата", "Тип / клиент", "Отдаём", "Валюта отдаём", "Откуда отправляем",
            "Получаем", "Валюта получаем", "Куда получаем", "Курс (деньги / товар)", "Прибыль", "Валюта прибыли"
        };
        for (var column = 1; column <= headers.Length; column++) sheet.Cells[1, column].Value = headers[column - 1];

        for (var index = 0; index < items.Count; index++)
        {
            var row = index + 2;
            var operation = items[index];
            sheet.Cells[row, 1].Value = index + 1;
            sheet.Cells[row, 2].Value = DisplayDate(operation).DateTime;
            sheet.Cells[row, 2].Style.Numberformat.Format = "dd.mm.yyyy";
            var transfer=operation.TypeCode==TransferService.TypeCode;
            sheet.Cells[row, 3].Value = $"{TypeName(operation.TypeCode)}\n{(transfer ? operation.Note : operation.Counterparty?.Name ?? "Без клиента")}";
            sheet.Cells[row, 3].Style.WrapText = true;
            SetNumberCell(sheet.Cells[row, 4], operation.SellAmount);
            sheet.Cells[row, 5].Value = operation.SellCurrency;
            sheet.Cells[row, 6].Value = operation.SourceAccount ?? "—";
            SetNumberCell(sheet.Cells[row, 7], operation.BuyAmount);
            sheet.Cells[row, 8].Value = operation.BuyCurrency;
            sheet.Cells[row, 9].Value = operation.DestinationAccount ?? "—";
            var rate = transfer ? null : OperationTypes.CanonicalRate(operation.TypeCode, operation.SellCurrency, operation.SellAmount, operation.BuyCurrency, operation.BuyAmount);
            sheet.Cells[row, 10].Value = rate.HasValue ? $"{rate.Value:N8} {OperationTypes.RateLabel(operation.TypeCode)}" : "—";
            if(!transfer) { SetNumberCell(sheet.Cells[row, 11], operation.BaseCurrencyProfit);sheet.Cells[row, 12].Value = "USD"; }
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
        sheet.Column(1).Width = 9;
        sheet.Column(2).Width = 14;
        sheet.Column(3).Width = 42;
        sheet.Column(4).Width = 20;
        sheet.Column(5).Width = 16;
        sheet.Column(6).Width = 24;
        sheet.Column(7).Width = 20;
        sheet.Column(8).Width = 18;
        sheet.Column(9).Width = 24;
        sheet.Column(10).Width = 28;
        sheet.Column(11).Width = 20;
        sheet.Column(12).Width = 18;
        sheet.Cells[2, 1, Math.Max(items.Count + 1, 2), 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        sheet.Cells[2, 4, Math.Max(items.Count + 1, 2), 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        sheet.Cells[2, 7, Math.Max(items.Count + 1, 2), 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        sheet.Cells[2, 11, Math.Max(items.Count + 1, 2), 11].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        sheet.Cells[1, 1, Math.Max(items.Count + 1, 1), headers.Length].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        var fileName = $"operations_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> OnPostCompleteAsync(Guid id, string? search, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort, int pageNumber = 1)
    {
        if(await db.Operations.AnyAsync(x=>x.Id==id && x.TypeCode==TransferService.TypeCode)) return BadRequest();
        await dispatcher.Send(new CompleteOperationCommand(id));
        TempData["Message"] = "Операция завершена";
        return RedirectToPage(new { search, typeCode, status, from = from?.ToString("yyyy-MM-dd"), to = to?.ToString("yyyy-MM-dd"), sort, pageNumber });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id, string? search, Guid? companyId, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort, int pageNumber = 1)
    {
        if(await db.Operations.AnyAsync(x=>x.Id==id && x.TypeCode==TransferService.TypeCode)) return RedirectToPage("/Operations/Transfers/Details",new { id });
        await dispatcher.Send(new CancelOperationCommand(id));
        TempData["Message"] = "Операция отменена";
        return RedirectToPage(new { search, companyId, typeCode, status, from = from?.ToString("yyyy-MM-dd"), to = to?.ToString("yyyy-MM-dd"), sort, pageNumber });
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id, string? search, Guid? companyId, string? typeCode, string? status, DateTime? from, DateTime? to, string? sort, int pageNumber = 1)
    {
        if(await db.Operations.AnyAsync(x=>x.Id==id && x.TypeCode==TransferService.TypeCode)) return BadRequest();
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
            var term = search.ToLowerInvariant();
            query = query.Where(x =>
                (x.Counterparty != null && x.Counterparty.Name.ToLower().Contains(term)) ||
                x.SellCurrency.ToLower().Contains(term) ||
                x.BuyCurrency.ToLower().Contains(term) ||
                (x.TypeCode==TransferService.TypeCode &&
                 ((x.SourceAccount!=null && x.SourceAccount.ToLower().Contains(term)) ||
                  (x.DestinationAccount!=null && x.DestinationAccount.ToLower().Contains(term)) ||
                  (x.Note!=null && x.Note.ToLower().Contains(term)))));
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
            var transferStart=TransferService.Instant(from.Value);
            query = query.Where(x => x.TypeCode==TransferService.TypeCode ? x.OccurredAt>=transferStart : x.OccurredAt >= start);
        }
        if (to.HasValue)
        {
            var end = new DateTimeOffset(DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc));
            var transferEnd=TransferService.Instant(to.Value.AddDays(1));
            query = query.Where(x => x.TypeCode==TransferService.TypeCode ? x.OccurredAt<transferEnd : x.OccurredAt < end);
        }
        return query;
    }

    private static IOrderedQueryable<TradeOperation> ApplySorting(IQueryable<TradeOperation> query, string sort) =>
        sort == DateAscending
            ? query.OrderBy(x => x.OccurredAt).ThenBy(x => x.CreatedAt ?? x.OccurredAt).ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.CreatedAt ?? x.OccurredAt).ThenByDescending(x => x.Id);

    private static void SetNumberCell(ExcelRange cell, decimal amount)
    {
        cell.Value = amount;
        cell.Style.Numberformat.Format = "#,##0.00########;[Red]-#,##0.00########;-";
    }
}
