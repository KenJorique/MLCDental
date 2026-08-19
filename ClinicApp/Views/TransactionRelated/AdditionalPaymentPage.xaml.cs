using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class AdditionalPaymentPage : ContentPage
{
    readonly AdditionalPaymentViewModel _vm;
    bool _formattingInProgress;

    public AdditionalPaymentPage(AdditionalPaymentViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Same reasoning as PaymentPage: PaymentAmount isn't bound
        // directly to the Entry (comma-formatted text doesn't parse back
        // into decimal on its own — see OnAmountTextChanged), so clear the
        // Entry's own text on each visit to match a fresh PaymentAmount.
        AmountEntry.Text = string.Empty;
        _vm.PaymentAmount = 0;
    }

    // Identical live thousands-separator formatting to PaymentPage's
    // OnAmountTextChanged — duplicated rather than shared because the two
    // pages' code-behind have no common base to hang a shared helper off
    // without a bigger refactor, and this logic is small and stable.
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
