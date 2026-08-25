using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Expenses;

public sealed class IndexModel(FinanceDbContext db):PageModel
{
    public List<Expense> Items{get;private set;}=[];
    public Dictionary<Guid,string> Companies{get;private set;}=[];
    public Dictionary<Guid,string> Accounts{get;private set;}=[];
    public Guid? CompanyId{get;private set;} public int? Year{get;private set;} public int? Month{get;private set;} public string? Category{get;private set;}
    public async Task OnGetAsync(Guid? companyId,int? year,int? month,string? category)
    {
        CompanyId=companyId;Year=year;Month=month;Category=category;
        Companies=await db.Companies.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Name);
        Accounts=await db.Accounts.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Name);
        var query=db.Expenses.AsNoTracking().AsQueryable();
        if(companyId.HasValue)query=query.Where(x=>x.CompanyId==companyId.Value);
        if(year.HasValue)query=query.Where(x=>x.OccurredAt.Year==year.Value);
        if(month is >=1 and <=12)query=query.Where(x=>x.OccurredAt.Month==month.Value);
        if(!string.IsNullOrWhiteSpace(category))query=query.Where(x=>x.Category.ToLower().Contains(category.Trim().ToLower()));
        Items=await query.OrderByDescending(x=>x.OccurredAt).ThenBy(x=>x.Category).ToListAsync();
    }
}
