using System.Drawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Balance;

public sealed class HistoryModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    private static readonly TimeSpan AlmatyOffset = TimeSpan.FromHours(5);
    private const string AmountFormat = "#,##0.00########;[Red]-#,##0.00########;-";

    public sealed record HistoryRow(Guid AccountId, string Account, string Currency, DateTimeOffset OccurredAt,
        string Type, string Description, decimal Amount, decimal Balance);

    public sealed record PeriodSummary(Guid AccountId, string Account, string Currency, decimal Opening,
        decimal Income, decimal Expense, decimal Closing);

    private sealed record RawEntry(Guid AccountId, string Account, string Currency, decimal OpeningBalance,
        DateTimeOffset OccurredAt, string Type, string Description, decimal Amount);

    [BindProperty(SupportsGet = true)] public Guid? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Currency { get; set; }

    public string AccountTitle { get; private set; } = "Все счета";
    public List<HistoryRow> Items { get; private set; } = [];
    public List<PeriodSummary> PeriodSummaries { get; private set; } = [];
    public List<string> Types { get; private set; } = [];
    public List<string> Currencies { get; private set; } = [];
    public bool ShowPeriodSummary => AccountId.HasValue || From.HasValue || To.HasValue;

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!await LoadAsync()) return NotFound();

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("История");
        sheet.Cells.Style.Font.Name = "Arial";

        sheet.Cells[1, 1].Value = "История по счетам";
        sheet.Cells[1, 1, 1, 9].Merge = true;
        sheet.Cells[1, 1].Style.Font.Bold = true;
        sheet.Cells[1, 1].Style.Font.Size = 16;

        sheet.Cells[2, 1].Value = "Период";
        sheet.Cells[2, 2].Value = From?.Date;
        sheet.Cells[2, 3].Value = To?.Date;
        if (From.HasValue) sheet.Cells[2, 2].Style.Numberformat.Format = "dd.mm.yyyy";
        else sheet.Cells[2, 2].Value = "с начала ведения учёта";
        if (To.HasValue) sheet.Cells[2, 3].Style.Numberformat.Format = "dd.mm.yyyy";
        else sheet.Cells[2, 3].Value = "по текущую дату";

        var summaryHeaders = new[] { "Счёт", "Валюта", "Сальдо на начало", "Приход", "Расход", "Сальдо на конец" };
        for (var column = 1; column <= summaryHeaders.Length; column++)
            sheet.Cells[4, column].Value = summaryHeaders[column - 1];
        StyleHeader(sheet.Cells[4, 1, 4, summaryHeaders.Length]);

        var summaryRow = 5;
        foreach (var item in PeriodSummaries)
        {
            sheet.Cells[summaryRow, 1].Value = item.Account;
            sheet.Cells[summaryRow, 2].Value = item.Currency;
            SetNumberCell(sheet.Cells[summaryRow, 3], item.Opening);
            SetNumberCell(sheet.Cells[summaryRow, 4], item.Income);
            SetNumberCell(sheet.Cells[summaryRow, 5], item.Expense);
            SetNumberCell(sheet.Cells[summaryRow, 6], item.Closing);
            summaryRow++;
        }

        var detailHeaderRow = summaryRow + 1;
        var headers = new[] { "№ п/п", "Дата", "Счёт", "Тип операции", "Описание", "Приход", "Расход", "Валюта", "Остаток" };
        for (var column = 1; column <= headers.Length; column++)
            sheet.Cells[detailHeaderRow, column].Value = headers[column - 1];
        StyleHeader(sheet.Cells[detailHeaderRow, 1, detailHeaderRow, headers.Length]);

        for (var index = 0; index < Items.Count; index++)
        {
            var row = detailHeaderRow + index + 1;
            var item = Items[index];
            sheet.Cells[row, 1].Value = index + 1;
            sheet.Cells[row, 2].Value = item.OccurredAt.ToOffset(AlmatyOffset).DateTime;
            sheet.Cells[row, 2].Style.Numberformat.Format = "dd.mm.yyyy hh:mm";
            sheet.Cells[row, 3].Value = item.Account;
            sheet.Cells[row, 4].Value = item.Type;
            sheet.Cells[row, 5].Value = string.IsNullOrWhiteSpace(item.Description) ? "—" : item.Description;
            if (item.Amount > 0) SetNumberCell(sheet.Cells[row, 6], item.Amount);
            if (item.Amount < 0) SetNumberCell(sheet.Cells[row, 7], Math.Abs(item.Amount));
            sheet.Cells[row, 8].Value = item.Currency;
            SetNumberCell(sheet.Cells[row, 9], item.Balance);
        }

        var lastDetailRow = Math.Max(detailHeaderRow, detailHeaderRow + Items.Count);
        sheet.View.FreezePanes(detailHeaderRow + 1, 1);
        sheet.Cells[detailHeaderRow, 1, lastDetailRow, headers.Length].AutoFilter = true;
        sheet.Column(1).Width = 9;
        sheet.Column(2).Width = 20;
        sheet.Column(3).Width = 34;
        sheet.Column(4).Width = 28;
        sheet.Column(5).Width = 48;
        sheet.Column(6).Width = 20;
        sheet.Column(7).Width = 20;
        sheet.Column(8).Width = 12;
        sheet.Column(9).Width = 22;
        sheet.Cells[detailHeaderRow + 1, 1, Math.Max(lastDetailRow, detailHeaderRow + 1), 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        sheet.Cells[4, 1, lastDetailRow, headers.Length].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"balance_history_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    private async Task<bool> LoadAsync()
    {
        var accounts = await db.Accounts.AsNoTracking().Include(x => x.FinancialInstitution)
            .Where(x => x.IsActive && x.CompanyId == active.RequiredId).ToListAsync();
        if (AccountId.HasValue && accounts.All(x => x.Id != AccountId.Value)) return false;
        if (AccountId.HasValue)
        {
            var selected = accounts.Single(x => x.Id == AccountId.Value);
            AccountTitle = $"{AccountLabel(selected)} · {selected.Currency}";
        }

        var accountIds = accounts.Select(x => x.Id).ToList();
        var accountMap = accounts.ToDictionary(x => x.Id);
        var settlements = await db.Settlements.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.Operation!.Status != OperationStatus.Cancelled)
            .Select(x => new
            {
                x.AccountId, x.OccurredAt, x.Amount, x.Note, x.Operation!.TypeCode,
                Counterparty = x.Operation.Counterparty != null ? x.Operation.Counterparty.Name : null
            }).ToListAsync();
        var expenses = await db.Expenses.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .Select(x => new { x.AccountId, x.OccurredAt, x.Amount, x.Category, x.Note }).ToListAsync();
        var movements = await db.AccountMovements.AsNoTracking().Where(x => accountIds.Contains(x.AccountId) && !x.IsCancelled)
            .Select(x => new { x.AccountId, x.GroupId, x.OccurredAt, x.Amount, x.Currency, x.Kind, x.Note }).ToListAsync();

        RawEntry Create(Guid id, DateTimeOffset at, string type, string description, decimal amount)
        {
            var account = accountMap[id];
            return new RawEntry(id, AccountLabel(account), account.Currency, account.OpeningBalance,
                at, type, description, amount);
        }

        var entries = new List<RawEntry>();
        entries.AddRange(settlements.Select(x => Create(x.AccountId, x.OccurredAt,
            OperationTypes.All.GetValueOrDefault(x.TypeCode, x.TypeCode),
            string.Join(" · ", new[] { x.Counterparty, x.Note }.Where(v => !string.IsNullOrWhiteSpace(v))), x.Amount)));
        entries.AddRange(expenses.Select(x => Create(x.AccountId, x.OccurredAt, "Расход",
            string.Join(" · ", new[] { x.Category, x.Note }.Where(v => !string.IsNullOrWhiteSpace(v))), -x.Amount)));

        var movementPairs = movements.GroupBy(x => x.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var movement in movements)
        {
            var pair = movementPairs[movement.GroupId].FirstOrDefault(x => x.AccountId != movement.AccountId);
            var otherAccount = pair is null ? null : accountMap.GetValueOrDefault(pair.AccountId);
            var otherLabel = otherAccount is null ? "другого счёта" : AccountLabel(otherAccount);
            string description;
            if (movement.Kind == AccountMovementKind.Transfer)
            {
                description = movement.Amount > 0 ? $"Со счёта {otherLabel}" : $"На счёт {otherLabel}";
            }
            else if (pair is not null)
            {
                description = movement.Amount > 0
                    ? $"Из {otherLabel}: {Math.Abs(pair.Amount):N2} {pair.Currency} → {Math.Abs(movement.Amount):N2} {movement.Currency}"
                    : $"В {otherLabel}: {Math.Abs(movement.Amount):N2} {movement.Currency} → {Math.Abs(pair.Amount):N2} {pair.Currency}";
            }
            else
            {
                description = "Конвертация между счетами";
            }
            if (!string.IsNullOrWhiteSpace(movement.Note)) description += $" · {movement.Note.Trim()}";
            entries.Add(Create(movement.AccountId, movement.OccurredAt,
                movement.Kind == AccountMovementKind.Transfer ? "Перевод" : "Конвертация", description, movement.Amount));
        }

        Types = entries.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        Currencies = accounts.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        Type = Type?.Trim();
        Currency = Currency?.Trim().ToUpperInvariant();

        var rows = new List<HistoryRow>();
        foreach (var account in accounts)
        {
            var balance = account.OpeningBalance;
            foreach (var entry in entries.Where(x => x.AccountId == account.Id)
                         .OrderBy(x => x.OccurredAt).ThenBy(x => x.Type).ThenBy(x => x.Description))
            {
                balance += entry.Amount;
                rows.Add(new HistoryRow(entry.AccountId, entry.Account, entry.Currency, entry.OccurredAt,
                    entry.Type, entry.Description, entry.Amount, balance));
            }
        }

        var fromInstant = From.HasValue ? new DateTimeOffset(From.Value.Date, AlmatyOffset) : (DateTimeOffset?)null;
        var toInstant = To.HasValue ? new DateTimeOffset(To.Value.Date.AddDays(1), AlmatyOffset) : (DateTimeOffset?)null;
        var summaryAccounts = accounts.Where(x =>
            (!AccountId.HasValue || x.Id == AccountId.Value) &&
            (string.IsNullOrWhiteSpace(Currency) || x.Currency.Equals(Currency, StringComparison.OrdinalIgnoreCase)));
        foreach (var account in summaryAccounts.OrderBy(AccountLabel).ThenBy(x => x.Currency))
        {
            var accountEntries = entries.Where(x => x.AccountId == account.Id).ToList();
            var opening = account.OpeningBalance + (fromInstant.HasValue
                ? accountEntries.Where(x => x.OccurredAt < fromInstant.Value).Sum(x => x.Amount)
                : 0m);
            var periodEntries = accountEntries.Where(x =>
                (!fromInstant.HasValue || x.OccurredAt >= fromInstant.Value) &&
                (!toInstant.HasValue || x.OccurredAt < toInstant.Value)).ToList();
            var income = periodEntries.Where(x => x.Amount > 0).Sum(x => x.Amount);
            var expense = Math.Abs(periodEntries.Where(x => x.Amount < 0).Sum(x => x.Amount));
            if (AccountId.HasValue || periodEntries.Count > 0 || opening != 0)
                PeriodSummaries.Add(new PeriodSummary(account.Id, AccountLabel(account), account.Currency,
                    opening, income, expense, opening + income - expense));
        }

        Items = rows.Where(x =>
                (!AccountId.HasValue || x.AccountId == AccountId.Value) &&
                (string.IsNullOrWhiteSpace(Type) || x.Type.Equals(Type, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(Currency) || x.Currency.Equals(Currency, StringComparison.OrdinalIgnoreCase)) &&
                (!fromInstant.HasValue || x.OccurredAt >= fromInstant.Value) &&
                (!toInstant.HasValue || x.OccurredAt < toInstant.Value))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Account).ThenBy(x => x.Type).ThenBy(x => x.Description).ToList();
        return true;
    }

    public static string LocalDate(DateTimeOffset value) => value.ToOffset(AlmatyOffset).ToString("dd.MM.yyyy HH:mm");

    private static string AccountLabel(MoneyAccount account)
    {
        var institution = account.FinancialInstitution?.Name?.Trim();
        var accountName = account.Name.Trim();
        return !string.IsNullOrWhiteSpace(institution) && !institution.Equals(accountName, StringComparison.OrdinalIgnoreCase)
            ? $"{institution} · {accountName}"
            : accountName;
    }

    private static void StyleHeader(ExcelRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.Color.SetColor(Color.White);
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 78, 121));
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    private static void SetNumberCell(ExcelRange cell, decimal amount)
    {
        cell.Value = amount;
        cell.Style.Numberformat.Format = AmountFormat;
    }
}
