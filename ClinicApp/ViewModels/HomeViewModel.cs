using ClinicApp.Services;
using ClinicApp.Models.HomeModels;
using ClinicApp.Views;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.SupplyRelated;
using ClinicApp.Views.TransactionRelated;
using ClinicApp.ViewModels.TransactionVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    readonly SupabaseDataService dataService;

    // Owns Today's Appointments data and the SelectTodayAppointmentCommand that opens the modal sheet — reused as-is rather than duplicated here.
    public AppointmentScheduleViewModel ScheduleVM { get; }

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private int totalAppointments;
    [ObservableProperty] private int doneAppointments;
    [ObservableProperty] private int pendingAppointments;
    [ObservableProperty] private int cancelledAppointments;

    public ObservableCollection<NeedsAttentionSummaryRow> NeedsAttentionRows { get; } = new();
    public ObservableCollection<Models.SupabaseModels.SupabaseActivityLog> RecentActivities { get; } = new();

    // Injects the shared data service and the singleton schedule ViewModel (so its Today's Appointments/commands are shared with the Appointment tab).
    public HomeViewModel(SupabaseDataService dataService, AppointmentScheduleViewModel scheduleVM)
    {
        this.dataService = dataService;
        ScheduleVM = scheduleVM;
    }

    // Refreshes every section on the page — called from OnAppearing. Runs everything in parallel so the page settles fast instead of queueing one wait behind another.
    [RelayCommand]
    async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var scheduleTask = ScheduleVM.LoadAppointments();
            var overviewTask = LoadTodayOverviewAsync();
            var suppliesTask = dataService.GetSuppliesAsync();
            var unpaidBillsTask = dataService.GetUnpaidBillsAsync();
            var activitiesTask = dataService.GetRecentActivitiesAsync(8);

            await Task.WhenAll(scheduleTask, overviewTask, suppliesTask, unpaidBillsTask, activitiesTask);

            RecentActivities.Clear();
            foreach (var activity in activitiesTask.Result)
                RecentActivities.Add(activity);

            // Needs ScheduleVM.PendingBookingsCount, which scheduleTask just refreshed — built synchronously now that everything's in.
            BuildNeedsAttentionRows(unpaidBillsTask.Result, suppliesTask.Result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Load error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Failed to load home data.", "OK");
        }
        finally { IsBusy = false; }
    }

    // Computes the 4 stat cards — same Done/Pending/Cancelled definitions ReportsViewModel uses for its Daily tab, narrowed to just today.
    async Task LoadTodayOverviewAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var allBills = await dataService.GetAllBillsAsync();
        var billsToday = allBills.Where(b => b.VisitDate >= today && b.VisitDate < tomorrow).ToList();

        var entriesToday = await dataService.GetAllAppointmentEntriesForReportAsync(today, tomorrow);
        var cancelledToday = await dataService.GetAllCancelledAppointmentsForReportAsync(today, tomorrow);

        DoneAppointments = billsToday.Count; // a bill existing for today IS the "visit happened" signal
        CancelledAppointments = cancelledToday.Count;
        PendingAppointments = entriesToday.Count;
        TotalAppointments = DoneAppointments + PendingAppointments + CancelledAppointments;
    }

    // Builds the Needs Attention summary lines, grouped Bills → Appointments → Stock. Each line is skipped when its count is 0.
    // NOTE: the "?filter=" routes below assume BalanceManagementViewModel/SupplyListViewModel can accept a starting filter via query property —
    // see the note back in chat; without that addition these will open the page but land on its default "All" tab, not the specific one.
    void BuildNeedsAttentionRows(List<Models.SupabaseModels.SupabaseBill> unpaidBills, List<Models.SupabaseModels.SupabaseSupplyItem> supplies)
    {
        NeedsAttentionRows.Clear();

        var overdueCount = 0;
        var dueSoonCount = 0;
        if (unpaidBills.Count > 0)
        {
            // Same grouping BalanceManagementViewModel uses — counts patients, not raw bills, so the numbers agree.
            var patients = unpaidBills
                .Where(b => b.Balance > 0)
                .GroupBy(b => string.IsNullOrWhiteSpace(b.PatientId)
                    ? $"name:{b.PatientName.Trim().ToLowerInvariant()}"
                    : $"id:{b.PatientId}")
                .Select(g => new PatientBalanceCardViewModel(g.First().PatientId, g.First().PatientName, g.ToList()))
                .ToList();

            overdueCount = patients.Count(p => p.IsOverdue);
            dueSoonCount = patients.Count(p => p.IsDueSoon);
        }

        if (overdueCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{overdueCount} Patient{(overdueCount == 1 ? "" : "s")} with Overdue Bills",
                Route = $"{nameof(BalanceManagementPage)}?filter={Uri.EscapeDataString("Overdue")}"
            });
        }

        if (dueSoonCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{dueSoonCount} Patient{(dueSoonCount == 1 ? "" : "s")} with Bills Due Soon",
                Route = $"{nameof(BalanceManagementPage)}?filter={Uri.EscapeDataString("DueSoon")}"
            });
        }

        if (ScheduleVM.PendingBookingsCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{ScheduleVM.PendingBookingsCount} Appointment{(ScheduleVM.PendingBookingsCount == 1 ? "" : "s")} Awaiting Confirmation",
                Route = nameof(AppointmentPage)
            });
        }

        var outOfStockCount = supplies.Count(s => s.IsOutOfStock);
        var lowStockCount = supplies.Count(s => s.IsLowStock && !s.IsOutOfStock);

        if (outOfStockCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{outOfStockCount} Out of Stock Item{(outOfStockCount == 1 ? "" : "s")}",
                Route = $"{nameof(SupplyListPage)}?filter={Uri.EscapeDataString("Out of Stock")}"
            });
        }

        if (lowStockCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{lowStockCount} Low Stock Item{(lowStockCount == 1 ? "" : "s")}",
                Route = $"{nameof(SupplyListPage)}?filter={Uri.EscapeDataString("Low Stock")}"
            });
        }
    }

    // Tapping a Needs Attention summary line — opens that category's list page (pre-filtered, once the target ViewModel supports it).
    [RelayCommand]
    async Task OpenNeedsAttentionRow(NeedsAttentionSummaryRow row)
    {
        if (row == null || string.IsNullOrEmpty(row.Route)) return;
        await Shell.Current.GoToAsync(row.Route);
    }

    // Quick Actions — "+ Add Appointment" (no dedicated page exists yet, so this opens the walk-in booking flow).
    [RelayCommand]
    async Task AddAppointment() => await Shell.Current.GoToAsync(nameof(WalkInBookingPage));

    // Quick Actions — "+ Add Patient".
    [RelayCommand]
    async Task AddPatient() => await Shell.Current.GoToAsync(nameof(AddPatientPage));

    // "View All" next to Recent Activity — opens the full activity history.
    [RelayCommand]
    async Task ViewAllActivity() => await Shell.Current.GoToAsync(nameof(ActivityLogPage));
}
