using ClinicApp.ViewModels.TransactionVM;

namespace ClinicApp.Views.TransactionRelated;

public partial class PaymentPage : ContentPage
{
    readonly PaymentViewModel _vm;
    bool _formattingInProgress;

    public PaymentPage(PaymentViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // No QueryProperty triggers this anymore — PaymentViewModel now
        // reads straight from BillDraftStore.Current (see LoadDraft),
        // since there's no real bill/billId to navigate in with yet.
        _vm.LoadDraft();

        // PaymentAmount resets to 0 each visit (see PaymentViewModel.
        // LoadDraft) — clear the entry text to match, since it's no
        // longer bound directly (see OnAmountTextChanged below).
        AmountEntry.Text = string.Empty;
    }

    // Live thousands-separator formatting as the staff types (e.g. typing
    // "200000" displays as "200,000"). The Entry's Text is NOT bound
    // directly to PaymentAmount anymore — comma-formatted text like
    // "200,000.00" won't parse back into a decimal automatically, so this
    // strips the formatting to get the real number, updates the ViewModel
    // directly, then re-displays the formatted version.
    void OnAmountTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_formattingInProgress) return;
        if (sender is not Entry entry) return;

        // Keep only digits and a single decimal point.
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
