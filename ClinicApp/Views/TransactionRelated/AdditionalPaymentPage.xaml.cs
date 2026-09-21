using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class AdditionalPaymentPage : ContentPage
{
    readonly AdditionalPaymentViewModel _vm;
    bool _formattingInProgress;

    // Wires up the view model as the binding context.
    public AdditionalPaymentPage(AdditionalPaymentViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = vm;
    }

    // Clears the amount entry on each visit, since PaymentAmount doesn't parse back from the formatted text on its own.
    protected override void OnAppearing()
    {
        base.OnAppearing();

        AmountEntry.Text = string.Empty;
        _vm.PaymentAmount = 0;
    }

    // Live thousands-separator formatting for the amount entry, duplicated from PaymentPage since the two pages share no common base.
    void OnAmountTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_formattingInProgress) return;
        if (sender is not Entry entry) return;

        var raw = new string((e.NewTextValue ?? "")
            .Where(c => char.IsDigit(c) || c == '.').ToArray());

        var firstDot = raw.IndexOf('.');
        if (firstDot >= 0)
            raw = raw.Substring(0, firstDot + 1) +
                  raw.Substring(firstDot + 1).Replace(".", "");

        if (string.IsNullOrEmpty(raw) || raw == ".")
        {
            _formattingInProgress = true;
            entry.Text = string.Empty;
            _formattingInProgress = false;
            _vm.PaymentAmount = 0;
            return;
        }

        var parts = raw.Split('.');
        var wholeDigits = parts[0].TrimStart('0');
        if (wholeDigits.Length == 0) wholeDigits = "0";

        var hasTrailingDot = raw.EndsWith(".");
        var decimalPart = parts.Length > 1 ? parts[1] : "";
        if (decimalPart.Length > 2) decimalPart = decimalPart.Substring(0, 2);

        var wholeFormatted = decimal.TryParse(wholeDigits, out var wholeNum)
            ? wholeNum.ToString("#,##0")
            : wholeDigits;

        var displayText = wholeFormatted;
        if (hasTrailingDot || decimalPart.Length > 0)
            displayText += "." + decimalPart;

        _formattingInProgress = true;
        entry.Text = displayText;
        entry.CursorPosition = displayText.Length;
        _formattingInProgress = false;

        var parseable = wholeDigits + (decimalPart.Length > 0 ? "." + decimalPart : "");
        if (decimal.TryParse(parseable, out var amount))
            _vm.PaymentAmount = amount;
    }
}
