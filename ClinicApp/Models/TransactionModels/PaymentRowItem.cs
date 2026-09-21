namespace ClinicApp.Models.TransactionModels;

// One row in a bill's payment table: a single payment plus the running balance after it.
public class PaymentRowItem
{
    public string PaymentId { get; set; } = "";

    public string BillId { get; set; } = "";

    public DateTime Date { get; set; }

    public decimal Amount { get; set; }

    public decimal RemainingBalance { get; set; }

    public string DateDisplay => Date.ToString("MMM dd, yyyy");

    public string AmountDisplay => $"₱{Amount:N2}";

    public string BalanceDisplay => $"₱{RemainingBalance:N2}";
}
