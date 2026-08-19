namespace ClinicApp.Models;

public class LedgerEntry
{
    public string Id { get; set; } = "";

    // BillCreated, Payment, Discount, Refund, Adjustment
    public string Type { get; set; } = "";

    public DateTime Date { get; set; }

    public string ReferenceNumber { get; set; } = "";

    public decimal Amount { get; set; }

    public decimal RemainingBalance { get; set; }

    public string Description { get; set; } = "";

    public string BillId { get; set; } = "";

    public string ReceiptId { get; set; } = "";

    public string DateDisplay =>
    Date.ToString("MMM dd, yyyy");

    public string TimeDisplay =>
        Date.ToString("hh:mm tt");

    public string AmountDisplay =>
        $"{(Amount >= 0 ? "+" : "-")}₱{Math.Abs(Amount):N2}";

    public string BalanceDisplay =>
        $"₱{RemainingBalance:N2}";

    public string Icon =>
    Type switch
    {
        "Payment" => "payment.png",
        "BillCreated" => "bill.png",
        "Discount" => "discount.png",
        _ => "ledger.png"
    };

    public Color AmountColor =>
    Type switch
    {
        "Payment" => Colors.Green,
        "Discount" => Colors.Orange,
        "Refund" => Colors.Red,
        _ => Colors.Blue
    };

    public string Title =>
    Type switch
    {
        "Payment" => "Payment",
        "BillCreated" => "Bill Created",
        "Discount" => "Discount Applied",
        _ => Type
    };
}