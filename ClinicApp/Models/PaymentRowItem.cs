namespace ClinicApp.Models;

// One row in a bill card's payment table (Date | Payment | Balance).
// RemainingBalance is the running balance AFTER this payment was
// applied — computed chronologically (oldest payment first) in
// TransactionViewModel.LoadBillsAsync, then the list is reversed so
// the newest payment displays at the top, matching the rest of the app.
public class PaymentRowItem
{
    public string PaymentId { get; set; } = "";
    public string BillId { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal RemainingBalance { get; set; }

    public string DateDisplay => Date.ToString("MMM dd, yyyy");
    public string AmountDisplay => $"+₱{Amount:N2}";
    public string BalanceDisplay => $"₱{RemainingBalance:N2}";
}
