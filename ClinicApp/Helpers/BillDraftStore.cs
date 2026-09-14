using ClinicApp.Models.SupabaseModels;
using ClinicApp.Models.TransactionModels;

namespace ClinicApp.Helpers;

public static class BillDraftStore
{
    public static BillDraft? Current { get; set; }
    public static class CreatedBillStore
    {
        public static SupabaseBill? Current { get; set; }
    }
}