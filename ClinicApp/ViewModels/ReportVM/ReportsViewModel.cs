using ClinicApp.Models.ReportModels;
using ClinicApp.Services;
using ClinicApp.Views;
using ClinicApp.Views.AppointmentRelated;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.SupplyRelated;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels
{
    // ── HOW THIS PAGE WORKS ──────────────────────────────────────────
    // 1. Three tabs (Daily/Weekly/Monthly) pick a GRANULARITY, not a
    //    single fixed period. Tapping a tab calls SetPeriod, which
    //    rebuilds the dropdown (DateOptions) for that granularity —
    //    e.g. Daily rebuilds to "the last 30 days" (Sundays skipped,
    //    clinic is closed), Weekly to "the last 4 weeks," Monthly to
    //    "the last 3 months."
    // 2. The dropdown is capped to how far back real data actually
    //    goes (EnsureEarliestDateAsync), so a brand-new clinic sees a
    //    short dropdown instead of a long one full of empty periods.
    // 3. Picking a dropdown entry (SelectedDateOption) triggers
    //    LoadReport, which pulls all 5 report sections for that exact
    //    date range and fills 4 ObservableCollection<ChartDataPoint>
    //    properties — the page's Syncfusion chart series bind directly
    //    to these via ItemsSource/XBindingPath/YBindingPath.
    // 4. Billing chart granularity depends on the tab, and always
    //    zooms INTO the selected period (never a trend across separate
    //    periods anymore):
    //    - Daily: hourly bars for the SELECTED day, using CreatedAt
    //      (real time-of-day), bucketed to clinic hours.
    //    - Weekly/Monthly: one bar per day inside the selected
    //      week/month (7 days, or ~28-31 days), using VisitDate.
    // 5. Each report card's stat area is tappable — confirms with the
    //    user, then deep-links to the relevant list page.
    // 6. Appointments: appointment_entries only holds NOT-YET-FINISHED
    //    visits (the row gets deleted, not marked "completed", once a
    //    visit is fully processed — see ReceiptViewModel.Done()). So
    //    Completed comes from Bills instead; Pending/Cancelled come
    //    from whatever's still sitting in appointment_entries, with a
    //    report-time no-show rule for entries left over from a day
    //    that's already fully ended.
    // ───────────────────────────────────────────────────────────────
    public partial class ReportsViewModel : ObservableObject
    {
        readonly SupabaseDataService dataService;

        // Clinic operating hours, used to bucket the Daily billing
        // chart into hourly bars. Adjust these two numbers if the
        // clinic's actual hours differ (24-hour format, CloseHour is
        // exclusive — 17 means "up to but not including 5 PM").
        const int ClinicOpenHour = 8;
        const int ClinicCloseHour = 17;

        // Earliest date any real data exists (earliest booking or bill).
        // Computed once and cached — see EnsureEarliestDateAsync.
        DateTime? _earliestDataDate;

        [ObservableProperty] private ReportPeriod selectedPeriod = ReportPeriod.Daily;
        [ObservableProperty] private ReportsSummary? currentReport;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string dateRangeLabel = string.Empty;

        // Billing chart X-axis title — "Hour" on the Daily tab, "Date" on Weekly/Monthly. Set in SetPeriod.
        [ObservableProperty] private string billingAxisTitle = "Hour";

        // Whether the Billing line chart shows a number label on every point. Off for Monthly (too many points, labels overlap the line) — on for Daily/Weekly.
        [ObservableProperty] private bool showBillingDataLabels = true;

        // Whether the chevron/navigation icons show on the report cards — only on the Daily tab, per request.
        [ObservableProperty] private bool isDailyPeriod = true;

        // Treatments chart height, sized to the number of treatment categories so Daily/Weekly stay compact while Monthly (usually more categories) gets room to breathe. Set in LoadReport.
        [ObservableProperty] private double treatmentsChartHeight = 240;

        public ObservableCollection<DateRangeOption> DateOptions { get; } = new();

        [ObservableProperty] private DateRangeOption? selectedDateOption;

        // Chart data — the page's Syncfusion series bind straight to
        // these (ItemsSource="{Binding AppointmentChartData}" etc.).
        // Cleared and rebuilt every LoadReport call.
        public ObservableCollection<ChartDataPoint> AppointmentChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> SupplyChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> BillingChartData { get; } = new();
        public ObservableCollection<ChartDataPoint> TreatmentChartData { get; } = new();

        // Injects the shared data service used for every Supabase call on this page.
        public ReportsViewModel(SupabaseDataService dataService)
        {
            this.dataService = dataService;
        }

        // Runs when the page appears; loads the Daily tab by default.
        public void OnAppearing() => _ = SetPeriod("Daily");

        // Tapping a Daily/Weekly/Monthly tab lands here. Rebuilds the
        // dropdown for that granularity and auto-selects the most
        // recent entry, which triggers LoadReport via
        // OnSelectedDateOptionChanged below.
        [RelayCommand]
        async Task SetPeriod(string period)
        {
            SelectedPeriod = period switch
            {
                "Weekly" => ReportPeriod.Weekly,
                "Monthly" => ReportPeriod.Monthly,
                _ => ReportPeriod.Daily
            };

            // Daily shows hours, so the axis label changes; Monthly has too many points for on-chart labels to stay readable, so those are hidden there (values are still visible via tooltip in the chart).
            BillingAxisTitle = SelectedPeriod == ReportPeriod.Daily ? "Hour" : "Date";
            ShowBillingDataLabels = SelectedPeriod != ReportPeriod.Monthly;
            IsDailyPeriod = SelectedPeriod == ReportPeriod.Daily; // chevrons only show on Daily, per request

            await EnsureEarliestDateAsync();

            DateOptions.Clear();
            foreach (var opt in BuildDateOptions(SelectedPeriod))
                DateOptions.Add(opt);

            // Setting this always re-triggers a load since it's a fresh
            // object each rebuild.
            SelectedDateOption = DateOptions.FirstOrDefault();
        }

        // Fires when the dropdown selection changes; triggers a fresh report load for that range.
        partial void OnSelectedDateOptionChanged(DateRangeOption? value)
        {
            if (value != null) _ = LoadReport();
        }

        // Finds the earliest booking/bill date once and caches it, so
        // every dropdown rebuild after the first doesn't re-fetch. This
        // is what makes the dropdown "shrink" for a new clinic instead
        // of always showing a fixed 30/4/3 count.
        async Task EnsureEarliestDateAsync()
        {
            if (_earliestDataDate.HasValue) return;

            try
            {
                var allBookings = await dataService.GetAllBookingsForReportAsync(DateTime.MinValue, DateTime.MaxValue);
                var allEntries = await dataService.GetAllAppointmentEntriesForReportAsync(DateTime.MinValue, DateTime.MaxValue);
                var allBills = await dataService.GetAllBillsAsync();

                var earliestBooking = allBookings.Count > 0 ? allBookings.Min(b => b.AppointmentDate) : (DateTime?)null;
                var earliestEntry = allEntries.Count > 0 ? allEntries.Min(e => e.AppointmentDateTime) : (DateTime?)null;
                var earliestBill = allBills.Count > 0 ? allBills.Min(b => b.CreatedAt) : (DateTime?)null;

                _earliestDataDate = new[] { earliestBooking, earliestEntry, earliestBill }
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .DefaultIfEmpty(DateTime.Today)
                    .Min();
            }
            catch
            {
                // If this fails for any reason, fall back to "today" —
                // worst case the dropdown just shows one entry instead
                // of crashing the page.
                _earliestDataDate = DateTime.Today;
            }
        }

        // Builds the dropdown entries for the given granularity, newest
        // first, stopping once it reaches earlier than any real data.
        List<DateRangeOption> BuildDateOptions(ReportPeriod period)
        {
            var today = DateTime.Today;
            var earliest = _earliestDataDate ?? today;
            var options = new List<DateRangeOption>();

            switch (period)
            {
                case ReportPeriod.Daily:
                    // Clinic is closed Sundays — skip them so the
                    // dropdown never offers a day that's guaranteed
                    // to show an empty report. Still counts back 30
                    // calendar days; Sundays just aren't added.
                    for (int i = 0; i < 30; i++)
                    {
                        var day = today.AddDays(-i);
                        if (day < earliest.Date) break;
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue;

                        options.Add(new DateRangeOption
                        {
                            Start = day,
                            End = day.AddDays(1),
                            Label = i == 0 ? "Today" : i == 1 ? "Yesterday" : day.ToString("MMM dd")
                        });
                    }
                    break;

                case ReportPeriod.Weekly:
                    for (int i = 0; i < 4; i++)
                    {
                        var weekStart = today.AddDays(-(int)today.DayOfWeek).AddDays(-7 * i);
                        var weekEnd = weekStart.AddDays(7);
                        if (weekEnd <= earliest.Date) break;

                        options.Add(new DateRangeOption
                        {
                            Start = weekStart,
                            End = weekEnd,
                            Label = i == 0 ? "This Week" : $"{weekStart:MMM d} - {weekEnd.AddDays(-1):MMM d}"
                        });
                    }
                    break;

                case ReportPeriod.Monthly:
                    for (int i = 0; i < 3; i++)
                    {
                        var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                        var monthEnd = monthStart.AddMonths(1);
                        if (monthEnd <= earliest.Date) break;

                        options.Add(new DateRangeOption
                        {
                            Start = monthStart,
                            End = monthEnd,
                            Label = i == 0 ? "This Month" : monthStart.ToString("MMM yyyy")
                        });
                    }
                    break;
            }

            // A brand-new clinic with zero data yet would otherwise get
            // an empty dropdown — always keep at least the current
            // period so there's something to select.
            if (options.Count == 0)
            {
                options.Add(period switch
                {
                    ReportPeriod.Weekly => new DateRangeOption
                    {
                        Start = today.AddDays(-(int)today.DayOfWeek),
                        End = today.AddDays(7 - (int)today.DayOfWeek),
                        Label = "This Week"
                    },
                    ReportPeriod.Monthly => new DateRangeOption
                    {
                        Start = new DateTime(today.Year, today.Month, 1),
                        End = new DateTime(today.Year, today.Month, 1).AddMonths(1),
                        Label = today.ToString("MMM yyyy")
                    },
                    _ => new DateRangeOption { Start = today, End = today.AddDays(1), Label = "Today" }
                });
            }

            return options;
        }

        [RelayCommand]
        async Task LoadReport()
        {
            if (IsBusy || SelectedDateOption == null) return;
            IsBusy = true;

            try
            {
                var start = SelectedDateOption.Start;
                var end = SelectedDateOption.End;
                var label = SelectedDateOption.Label;

                var report = new ReportsSummary { PeriodLabel = label, StartDate = start, EndDate = end };
                DateRangeLabel = label;

                // ── BILLING (fetched early — Completed appointments below need it) ──
                var allBills = await dataService.GetAllBillsAsync();
                var billsInRange = allBills.Where(b => b.VisitDate >= start && b.VisitDate < end).ToList();

                // ── APPOINTMENTS ──
                // appointment_entries only holds NOT-YET-FINISHED visits —
                // ReceiptViewModel.Done() deletes the row the moment a visit
                // is fully processed, it never marks it "completed". So a
                // finished visit's only remaining trace is its Bill.
                var entries = await dataService.GetAllAppointmentEntriesForReportAsync(start, end);
                var today = DateTime.Today;

                report.CompletedAppointments = billsInRange.Count; // a bill existing for this date IS the "this visit happened" signal, since the entry that produced it is already gone

                // Explicit cancellations — CancelAppointment() logs one row here BEFORE
                // deleting the entry, since the entry's Status never actually reaches
                // "cancelled" before it's gone (the row is deleted, not status-flipped).
                var cancelledLogs = await dataService.GetAllCancelledAppointmentsForReportAsync(start, end);
                var explicitlyCancelled = cancelledLogs.Count;

                // No-show rule: an entry left over from a day that's already
                // FULLY ended (not just "later today") without being
                // explicitly cancelled or converted into a bill. Never
                // applies mid-day — only once the calendar day is over.
                var noShowPastDay = entries.Count(e => e.AppointmentDateTime.Date < today);

                report.CancelledAppointments = explicitlyCancelled + noShowPastDay;

                // Genuinely still pending — scheduled today or later, not yet resolved either way.
                report.PendingAppointments = entries.Count(e => e.AppointmentDateTime.Date >= today);

                report.TotalAppointments = report.CompletedAppointments + report.PendingAppointments + report.CancelledAppointments;

                AppointmentChartData.Clear();
                AppointmentChartData.Add(new ChartDataPoint { Label = "Completed", Value = report.CompletedAppointments });
                AppointmentChartData.Add(new ChartDataPoint { Label = "Pending", Value = report.PendingAppointments });
                AppointmentChartData.Add(new ChartDataPoint { Label = "Cancelled", Value = report.CancelledAppointments });

                // ── TREATMENTS (condition-based — see note below) ──
                var treatments = await dataService.GetAllTreatmentHistoryForReportAsync(start, end);
                report.TotalTreatments = treatments.Count;
                report.TreatmentBreakdown = treatments
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Condition) ? "Unspecified" : t.Condition)
                    .ToDictionary(g => g.Key, g => g.Count());
                report.MostCommonTreatment = report.TreatmentBreakdown.Count > 0
                    ? report.TreatmentBreakdown.OrderByDescending(kv => kv.Value).First().Key
                    : "—";

                TreatmentChartData.Clear();
                foreach (var kv in report.TreatmentBreakdown.OrderByDescending(kv => kv.Value))
                    TreatmentChartData.Add(new ChartDataPoint { Label = kv.Key, Value = kv.Value });

                // ~50dp per row is enough for a 2-line wrapped label; 240 is the floor so the card never looks squashed with few categories
                TreatmentsChartHeight = Math.Max(240, report.TreatmentBreakdown.Count * 50);

                report.TotalRevenue = billsInRange.Sum(b => b.AmountPaid);
                report.OutstandingBalance = billsInRange.Sum(b => b.Balance);

                BillingChartData.Clear();

                if (SelectedPeriod == ReportPeriod.Daily)
                {
                    // Hourly breakdown of the SELECTED day (not "last 7
                    // days" like before, which ignored which date you
                    // picked). Uses CreatedAt since it has real
                    // time-of-day; VisitDate is date-only. Bucketed to
                    // clinic hours so it isn't 24 mostly-empty bars.
                    for (int hour = ClinicOpenHour; hour < ClinicCloseHour; hour++)
                    {
                        var hourStart = start.AddHours(hour);
                        var hourEnd = hourStart.AddHours(1);
                        var revenue = (double)allBills
                            .Where(b => b.CreatedAt >= hourStart && b.CreatedAt < hourEnd)
                            .Sum(b => b.AmountPaid);
                        BillingChartData.Add(new ChartDataPoint { Label = hourStart.ToString("h tt"), Value = revenue });
                    }
                }
                else
                {
                    // Weekly/Monthly: zoom INTO the selected period —
                    // one bar per day inside it (7 for a week, ~28-31
                    // for a month) — instead of a trend across separate
                    // weeks/months. Sunday is skipped since the clinic
                    // is always closed then (Weekly ends up with 6 bars).
                    for (var day = start.Date; day < end.Date; day = day.AddDays(1))
                    {
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue; // clinic closed, no data possible

                        var dayEnd = day.AddDays(1);
                        var revenue = (double)allBills
                            .Where(b => b.VisitDate >= day && b.VisitDate < dayEnd)
                            .Sum(b => b.AmountPaid);
                        BillingChartData.Add(new ChartDataPoint { Label = day.ToString("MMM d"), Value = revenue });
                    }
                }

                // ── SERVICES RENDERED — counts each billed line item within this period ──
                var billIdsInRange = billsInRange.Select(b => b.Id).ToHashSet(); // bill IDs in range, used to filter items below
                var allBillItems = await dataService.GetAllBillItemsAsync();
                var itemsInRange = allBillItems.Where(i => billIdsInRange.Contains(i.BillId)).ToList();

                report.TotalServicesRendered = itemsInRange.Count;
                report.ServiceBreakdown = itemsInRange
                    .GroupBy(i => string.IsNullOrWhiteSpace(i.ServiceName) ? "Unspecified" : i.ServiceName)
                    .ToDictionary(g => g.Key, g => g.Count());

                // ── SUPPLIES — reconstructed AS OF the end of the selected period, not just "right now" ──
                var supplies = await dataService.GetSuppliesAsync(); // today's live quantities — the starting point we work backward from
                report.TotalSupplies = supplies.Count;

                // Every log entry that happened AFTER this period ended — subtracting these
                // from today's live quantity "undoes" everything that's happened since,
                // leaving what stock actually looked like at the end of the period. For a
                // period that includes today (e.g. the Daily tab on "Today"), there are no
                // future logs yet, so this naturally just equals the live snapshot.
                var logsAfterPeriod = await dataService.GetAllStockLogsForReportAsync(end, DateTime.MaxValue);

                var lowStockNames = new List<string>(); // names of items that were low as of that period, for the tap-to-view alert
                int inStockAsOf = 0, lowAsOf = 0, outAsOf = 0; // per-item classification counters for that point in time

                foreach (var supply in supplies)
                {
                    // Sum of every change to THIS item that happened after the period ended
                    var changesSincePeriod = logsAfterPeriod
                        .Where(l => l.SupplyId == supply.Id)
                        .Sum(l => l.ChangeInPieces);

                    // Reverse those changes off today's quantity to get the historical quantity
                    var quantityAsOfPeriod = supply.QuantityInPieces - changesSincePeriod;

                    if (quantityAsOfPeriod <= 0)
                        outAsOf++;
                    else if (quantityAsOfPeriod <= supply.MinimumStockPieces)
                    {
                        lowAsOf++;
                        lowStockNames.Add(supply.Name);
                    }
                    else
                        inStockAsOf++;
                }

                report.OutOfStockCount = outAsOf;
                report.LowStockItemCount = lowAsOf;
                report.InStockCount = inStockAsOf;
                report.LowStockItemNames = lowStockNames;

                SupplyChartData.Clear();
                SupplyChartData.Add(new ChartDataPoint { Label = "In Stock", Value = report.InStockCount });
                SupplyChartData.Add(new ChartDataPoint { Label = "Low Stock", Value = report.LowStockItemCount });
                SupplyChartData.Add(new ChartDataPoint { Label = "Out of Stock", Value = report.OutOfStockCount });

                // ── SUPPLIES — movement WITHIN this period (supply_stock_logs) ──
                var logs = await dataService.GetAllStockLogsForReportAsync(start, end); // logs that happened DURING the period, for the Restocked/Used totals
                report.PiecesRestocked = logs.Where(l => l.ChangeInPieces > 0).Sum(l => l.ChangeInPieces); // positive changes = stock added
                report.PiecesUsed = Math.Abs(logs.Where(l => l.ChangeInPieces < 0).Sum(l => l.ChangeInPieces)); // negative changes = stock consumed

                CurrentReport = report;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportsViewModel] LoadReport error: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Failed to load report.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Tap-to-confirm-then-navigate, one per card ──────────────
        // Each stat area shows what's relevant, asks the user to
        // confirm, then deep-links via Shell route (all confirmed
        // registered in AppShell.cs).

        // Tap on the Appointments stat area; shows a summary, then goes to the Appointments page if confirmed.
        [RelayCommand]
        async Task ViewAppointments()
        {
            if (CurrentReport == null) return;

            bool go = await Shell.Current.DisplayAlert(
                "Appointments",
                $"{CurrentReport.TotalAppointments} appointment(s) this period " +
                $"({CurrentReport.CompletedAppointments} done, {CurrentReport.PendingAppointments} pending, " +
                $"{CurrentReport.CancelledAppointments} cancelled).\n\nView the full appointments list?",
                "View", "Cancel");

            if (go) await Shell.Current.GoToAsync(nameof(AppointmentPage));
        }

        // Tap on the Billing stat area; shows a summary, then goes to Transaction History if confirmed.
        [RelayCommand]
        async Task ViewBilling()
        {
            if (CurrentReport == null) return;

            bool go = await Shell.Current.DisplayAlert(
                "Billing",
                $"₱{CurrentReport.TotalRevenue:N2} collected, ₱{CurrentReport.OutstandingBalance:N2} outstanding this period.\n\n" +
                "View the full transaction history?",
                "View", "Cancel");

            if (go) await Shell.Current.GoToAsync(nameof(TransactionPage));
        }

        // Tap on the Supplies stat area; names which items are low, then goes to Supply List if confirmed.
        [RelayCommand]
        async Task ViewSupplies()
        {
            if (CurrentReport == null) return;

            var message = $"{CurrentReport.TotalSupplies} total supply item(s): " +
                           $"{CurrentReport.InStockCount} in stock, {CurrentReport.LowStockItemCount} low, " +
                           $"{CurrentReport.OutOfStockCount} out of stock.";

            if (CurrentReport.LowStockItemNames.Count > 0)
                message += $"\n\nLow on: {string.Join(", ", CurrentReport.LowStockItemNames)}"; // name the actual items, not just a count

            message += "\n\nView the full supply list?";

            bool go = await Shell.Current.DisplayAlert("Medical Supplies", message, "View", "Cancel");

            if (go) await Shell.Current.GoToAsync(nameof(SupplyListPage));
        }

        // Tap on the Treatments stat area; no general all-treatments page exists, so this goes to Patient List instead.
        [RelayCommand]
        async Task ViewTreatments()
        {
            if (CurrentReport == null) return;

            bool go = await Shell.Current.DisplayAlert(
                "Treatments",
                $"{CurrentReport.TotalTreatments} treatment(s) logged this period. " +
                $"Most common: {CurrentReport.MostCommonTreatment}.\n\nView the patient list?",
                "View", "Cancel");

            if (go) await Shell.Current.GoToAsync(nameof(PatientListPage));
        }
    }
}
