using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ClinicApp.Models
{
    [Table("bill_items")]
    public class SupabaseBillItemInsert : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();


        [Column("bill_id")]
        public string BillId { get; set; } = string.Empty;


        [Column("service_id")]
        public string? ServiceId { get; set; }


        [Column("service_name")]
        public string ServiceName { get; set; } = string.Empty;


        [Column("unit_price")]
        public decimal UnitPrice { get; set; }


        [Column("quantity")]
        public int Quantity { get; set; } = 1;


        [Column("notes")]
        public string? Notes { get; set; }


        [Column("tooth_numbers")]
        public string? ToothNumbers { get; set; }


        [Column("affects_teeth")]
        public bool AffectsTeeth { get; set; }

        // ── Per-item installment plan (requires migration_bill_items_installment.sql to be run first) ──
        [Column("is_installment")]
        public bool IsInstallment { get; set; }

        [Column("installment_months")]
        public int InstallmentMonths { get; set; }

        [Column("downpayment_amount")]
        public decimal DownpaymentAmount { get; set; }

        [Column("monthly_payment")]
        public decimal MonthlyPayment { get; set; }

        [Column("balance")]
        public decimal Balance { get; set; }
    }
}