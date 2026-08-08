namespace ERP.AI.Core.Entities;

public class Sale
{
    public int Id { get; set; }
    public string DocumentNo { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
}
