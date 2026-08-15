using ClinicApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClinicApp.ViewModels.TransactionVM
{
    /// One card = one patient's aggregated outstanding balance across
    /// all their unpaid/partial bills. Built from a group of SupabaseBill.
    public partial class PatientBalanceCardViewModel : ObservableObject
    {
        public const int DueSoonWindowDays = 3;

        public string PatientId { get; }
        public string PatientName { get; }

        /// The bill used to drive "Next Payment" / "Due" — the soonest
        /// due, unpaid bill for this patient.
        public SupabaseBill PrimaryBill { get; }

        public List<SupabaseBill> Bills { get; }

        public decimal TotalBalance { get; }
        public DateTime? NextDueDate { get; }
        public decimal NextPaymentAmount { get; }
        public bool IsOverdue { get; }
        public bool IsDueSoon { get; }

        [ObservableProperty]
        bool isExpanded;

        public PatientBalanceCardViewModel(string patientId, string patientName, List<SupabaseBill> bills)
        {
            PatientId = patientId;
            PatientName = patientName;
            Bills = bills;

            TotalBalance = bills.Sum(b => b.Balance);

            PrimaryBill = bills
                .OrderBy(b => b.DueDate ?? DateTime.MaxValue)
                .ThenBy(b => b.VisitDate)
                .First();

            NextDueDate = PrimaryBill.DueDate ?? PrimaryBill.VisitDate.AddDays(30);
            NextPaymentAmount = PrimaryBill.IsInstallment && PrimaryBill.MonthlyPayment > 0
                ? PrimaryBill.MonthlyPayment
                : PrimaryBill.Balance;

            IsOverdue = bills.Any(b => b.IsOverdue);

            IsDueSoon = !IsOverdue && NextDueDate.HasValue &&
                        NextDueDate.Value.Date <= DateTime.Today.AddDays(DueSoonWindowDays) &&
                        NextDueDate.Value.Date >= DateTime.Today;
        }

        public string DisplayName => PatientName;

        public string BalanceDisplay => $"₱{TotalBalance:N2}";

        public string NextPaymentDisplay => $"₱{NextPaymentAmount:N2}";

        public string DueDateDisplay =>
            NextDueDate.HasValue ? NextDueDate.Value.ToString("MMM dd, yyyy") : "—";

        public int DaysOverdue =>
            NextDueDate.HasValue && IsOverdue
                ? Math.Max(0, (DateTime.Today - NextDueDate.Value.Date).Days)
                : 0;

        public string StatusLabel =>
            IsOverdue ? (DaysOverdue > 0 ? $"Overdue · {DaysOverdue}d" : "Overdue")
            : IsDueSoon ? "Due Soon"
            : string.Empty;

        public bool HasStatus => IsOverdue || IsDueSoon;

        public string StatusBg => IsOverdue ? "#FFF1F2" : "#FEF3C7";
        public string StatusBorder => IsOverdue ? "#FECACA" : "#FDE68A";
        public string StatusText => IsOverdue ? "#B91C1C" : "#92400E";
    }
}