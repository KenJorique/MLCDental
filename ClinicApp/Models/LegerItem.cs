namespace ClinicApp.Models;
public class LedgerItem
{
    public string Title { get; set; } = "";
    public string BillId { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string PaymentId { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal RemainingBalance { get; set; }

    // Amount already paid on this bill — needed for the "Bill Amount /
    // Paid" two-column layout on the redesigned ledger card. Set
    // alongside Amount/RemainingBalance in TransactionViewModel.
    public decimal PaidAmount { get; set; }
    public string PaidAmountDisplay => $"₱{PaidAmount:N2}";

    // Raw status string mirrors SupabaseBill.Status ("paid" / "partial" /
    // "unpaid") — display/color helpers below match SupabaseBill's own
    // StatusDisplay/StatusColor/StatusBgColor exactly, so the same status
    // pill looks identical wherever it shows up in the app.
    public string Status { get; set; } = "";

    public string StatusDisplay => Status switch
    {
        "paid" => "Paid",
        "partial" => "Partial",
        "unpaid" => "Unpaid",
        _ => Status
    };

    public Color StatusColor => Status switch
    {
        "paid" => Color.FromArgb("#2E7D32"),
        "partial" => Color.FromArgb("#E65100"),
        "unpaid" => Color.FromArgb("#C62828"),
        _ => Color.FromArgb("#888888")
    };

    public Color StatusBgColor => Status switch
    {
        "paid" => Color.FromArgb("#E8F5E9"),
        "partial" => Color.FromArgb("#FFF3E0"),
        "unpaid" => Color.FromArgb("#FCEAEA"),
        _ => Color.FromArgb("#F5F5F5")
    };

    public DateTime Date { get; set; }
    public bool IsPayment { get; set; }
    public bool IsBill { get; set; }
    public bool IsOverdue { get; set; }
    public Color DueStatusColor =>
    IsOverdue ? Color.FromArgb("#DC2626") : Color.FromArgb("#6B7280");
    public string Icon =>
        IsPayment ? "\ue263" : "\ue873";
    public string AmountDisplay =>
        IsPayment
            ? $"+₱{Amount:N2}"
            : $"₱{Amount:N2}";
    public string RemainingDisplay =>
        $"Balance ₱{RemainingBalance:N2}";
    public Color IconBackground =>
    IsPayment
        ? Color.FromArgb("#DCFCE7")
        : Color.FromArgb("#DBEAFE");
    public Color IconColor =>
        IsPayment
            ? Color.FromArgb("#16A34A")
            : Color.FromArgb("#2563EB");
    public Color AmountColor =>
        IsPayment
            ? Color.FromArgb("#16A34A")
            : Color.FromArgb("#2563EB");
    public string DueStatusText =>
    IsOverdue ? "Overdue" : string.Empty;
    public string EntryKind =>
        IsPayment ? "Payment" : "Bill Created";
    public string EntryColor =>
    IsPayment ? "#16A34A" : "#2563EB";
    public bool ShowTimelineTop => true;
    public bool ShowTimelineBottom => true;

    // A bill entry with money still owed — surfaces the
    // "Add payment" affordance directly on the ledger card
    // instead of requiring a tap into Bill Details first.
    public bool ShowPayAction =>
        IsBill && RemainingBalance > 0;

    public string BalanceDueDisplay =>
        $"Balance due \u00b7 \u20b1{RemainingBalance:N2}";
}