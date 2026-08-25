using System.ComponentModel.DataAnnotations;
namespace organaizer.Pages.Expenses;
public sealed class ExpenseForm
{
    public Guid Id{get;set;} [Required]public Guid CompanyId{get;set;} [Required]public Guid AccountId{get;set;}
    [Required]public DateTime OccurredAt{get;set;}=DateTime.Today;[Required,StringLength(120)]public string Category{get;set;}="";
    [Range(typeof(decimal),"0.00000001","9999999999999999")]public decimal Amount{get;set;}[Required,StringLength(5)]public string Currency{get;set;}="USD";
    [StringLength(300)]public string?Note{get;set;}
}
