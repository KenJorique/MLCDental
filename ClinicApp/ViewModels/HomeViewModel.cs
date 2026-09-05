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

    // Shared with the Appointment tab, so Today's Appointments and its modal-open command aren't duplicated here.
    public AppointmentScheduleViewModel ScheduleVM { get; }

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private int totalAppointments;
    [ObservableProperty] private int doneAppointments;
    [ObservableProperty] private int pendingAppointments;
    [ObservableProperty] private int cancelledAppointments;

    public ObservableCollection<NeedsAttentionSummaryRow> NeedsAttentionRows { get; } = new();
    public ObservableCollection<Models.SupabaseModels.SupabaseActivityLog> RecentActivities { get; } = new();

    // Injects the shared data service and schedule ViewModel.
    public HomeViewModel(SupabaseDataService dataService, AppointmentScheduleViewModel scheduleVM)
    {
        this.dataService = dataService;
        ScheduleVM = scheduleVM;
    }

    // Refreshes every section in parallel — called from OnAppearing.
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

            // Needs ScheduleVM.PendingBookingsCount, refreshed by scheduleTask above.
            BuildNeedsAttentionRows(unpaidBillsTask.Result, suppliesTask.Result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Load error: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", "Failed to load home data.", "OK");
        }
        finally { IsBusy = false; }
    }

    // Computes the 4 stat cards for today, matching ReportsViewModel's Daily-tab definitions.
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

    // Builds Needs Attention in urgency order: confirmations, overdue, out of stock, due soon, low stock.
    void BuildNeedsAttentionRows(List<Models.SupabaseModels.SupabaseBill> unpaidBills, List<Models.SupabaseModels.SupabaseSupplyItem> supplies)
    {
        NeedsAttentionRows.Clear();

        var overdueCount = 0;
        var dueSoonCount = 0;
        if (unpaidBills.Count > 0)
        {
            // Same per-patient grouping BalanceManagementViewModel uses, so the counts always agree.
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

        var outOfStockCount = supplies.Count(s => s.IsOutOfStock);
        var lowStockCount = supplies.Count(s => s.IsLowStock && !s.IsOutOfStock);

        if (ScheduleVM.PendingBookingsCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{ScheduleVM.PendingBookingsCount} Appointment{(ScheduleVM.PendingBookingsCount == 1 ? "" : "s")} Awaiting Confirmation",
                IconGlyph = "\ue8b5", // schedule
                IconColor = Color.FromArgb("#E65100"),
                Route = nameof(AppointmentPage)
            });
        }

        if (overdueCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{overdueCount} Patient{(overdueCount == 1 ? "" : "s")} with Overdue Bills",
                IconGlyph = "\ue8a1", // payment
                IconColor = Color.FromArgb("#C62828"),
                Route = $"{nameof(BalanceManagementPage)}?filter={Uri.EscapeDataString("Overdue")}"
            });
        }

        if (outOfStockCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{outOfStockCount} Out of Stock Item{(outOfStockCount == 1 ? "" : "s")}",
                IconGlyph = "\ue928", // remove_shopping_cart
                IconColor = Color.FromArgb("#C62828"),
                Route = $"{nameof(SupplyListPage)}?filter={Uri.EscapeDataString("Out of Stock")}"
            });
        }

        if (dueSoonCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{dueSoonCount} Patient{(dueSoonCount == 1 ? "" : "s")} with Bills Due Soon",
                IconGlyph = "\ue8a1", // payment
                IconColor = Color.FromArgb("#F9A825"),
                Route = $"{nameof(BalanceManagementPage)}?filter={Uri.EscapeDataString("DueSoon")}"
            });
        }

        if (lowStockCount > 0)
        {
            NeedsAttentionRows.Add(new NeedsAttentionSummaryRow
            {
                Text = $"{lowStockCount} Low Stock Item{(lowStockCount == 1 ? "" : "s")}",
                IconGlyph = "\ue1a1", // inventory
                IconColor = Color.FromArgb("#F9A825"),
                Route = $"{nameof(SupplyListPage)}?filter={Uri.EscapeDataString("Low Stock")}"
            });
        }
    }

    // Opens the tapped Needs Attention row's list page (pre-filtered where supported).
    [RelayCommand]
    async Task OpenNeedsAttentionRow(NeedsAttentionSummaryRow row)
    {
        if (row == null || string.IsNullOrEmpty(row.Route)) return;
        await Shell.Current.GoToAsync(row.Route);
    }

    // Quick Actions — opens the walk-in booking flow (no dedicated "add appointment" page exists).
    [RelayCommand]
    async Task AddAppointment() => await Shell.Current.GoToAsync(nameof(WalkInBookingPage));

    // Quick Actions — opens Add Patient.
    [RelayCommand]
    async Task AddPatient() => await Shell.Current.GoToAsync(nameof(AddPatientPage));

    // Quick Actions — opens Balance Management to pick who's paying.
    [RelayCommand]
    async Task RecordPayment() => await Shell.Current.GoToAsync(nameof(BalanceManagementPage));

    // Quick Actions — opens the Supply list.
    [RelayCommand]
    async Task ManageSupply() => await Shell.Current.GoToAsync(nameof(SupplyListPage));

    // "View All" next to Recent Activity — opens the full activity history.
    [RelayCommand]
    async Task ViewAllActivity() => await Shell.Current.GoToAsync(nameof(ActivityLogPage));
}
