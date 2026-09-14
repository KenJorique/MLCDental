using ClinicApp.Models.SupabaseModels;

namespace ClinicApp.Models.TransactionModels;

public class BillingResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public SupabaseBill? Bill { get; set; }
}