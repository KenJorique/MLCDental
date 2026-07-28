using ClinicApp.Models;

namespace ClinicApp.Helpers;

public static class BillDraftStore
{
    public static BillDraft? Current { get; set; }
    public static class CreatedBillStore
    {
        public static SupabaseBill? Current { get; set; }
    }
}