
using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.TransactionVM;

public partial class BalanceManagementViewModel : ObservableObject
{
    readonly SupabaseDataService _supabase;
    List<PatientBalanceCardViewModel> _allPatients = new();

    public ObservableCollection<PatientBalanceCardViewModel> Patients { get; } = new();

    [ObservableProperty] string searchText = string.Empty;
    [ObservableProperty] string currentFilter = "All";      // All | DueSoon | Overdue
    [ObservableProperty] string currentSort = "Nearest due date";
    [ObservableProperty] bool isBusy;
    [ObservableProperty] bool isRefreshing;

    [ObservableProperty] int allCount;
    [ObservableProperty] int dueSoonCount;
    [ObservableProperty] int overdueCount;

    /// Empty-state copy shown by the CollectionView when Patients is empty.
    /// Depends on the active filter and search term, so "Due Soon (0)" no
    /// longer shows the same "everyone's paid up" message as "All (0)".
    public string EmptyStateTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
                return "No matches";

            return CurrentFilter switch
            {
                "DueSoon" => "Nothing due soon",
                "Overdue" => "No overdue balances",
                _ => "No outstanding balances"
            };
        }
    }

    public string EmptyStateMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
                return $"No patients match \"{SearchText.Trim()}\".";

            return CurrentFilter switch
            {
                "DueSoon" => "No payments are due in the next few days.",
                "Overdue" => "No payments are overdue right now.",
                _ => "Every patient is paid up."
            };
        }
    }

    public BalanceManagementViewModel(SupabaseDataService supabase)
    {
        _supabase = supabase;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

    [RelayCommand]
    public async Task LoadBalancesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var bills = await _supabase.GetUnpaidBillsAsync();

            // Only bills that actually still owe something — a "paid"
            // status filter alone can miss $0-balance edge cases.
            bills = bills.Where(b => b.Balance > 0).ToList();

            var grouped = bills
                .GroupBy(b => string.IsNullOrWhiteSpace(b.PatientId)
                    ? $"name:{b.PatientName.Trim().ToLowerInvariant()}"
                    : $"id:{b.PatientId}")
                .Select(g => new PatientBalanceCardViewModel(
                    g.First().PatientId,
                    g.First().PatientName,
                    g.ToList()))
                .ToList();

            _allPatients = grouped;

            AllCount = _allPatients.Count;
            OverdueCount = _allPatients.Count(p => p.IsOverdue);
            DueSoonCount = _allPatients.Count(p => p.IsDueSoon);

            ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BalanceManagementVM] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    async Task Refresh()
    {
        IsRefreshing = true;
        await LoadBalancesAsync();
    }

    [RelayCommand]
    void SelectFilter(string filter)
    {
        CurrentFilter = filter;
        ApplyFilterAndSort();
    }

    [RelayCommand]
    async Task ShowSortOptions()
    {
        var result = await Shell.Current.DisplayActionSheet(
            "Sort By", "Cancel", null,
            "Nearest due date", "Highest balance", "Newest balance", "Patient name A-Z");

        if (!string.IsNullOrEmpty(result) && result != "Cancel")
        {
            CurrentSort = result;
            ApplyFilterAndSort();
        }
    }

    void ApplyFilterAndSort()
    {
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));

        var filtered = _allPatients.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLower();
            filtered = filtered.Where(p => p.DisplayName.ToLower().Contains(term));
        }

        filtered = CurrentFilter switch
        {
            "DueSoon" => filtered.Where(p => p.IsDueSoon),
            "Overdue" => filtered.Where(p => p.IsOverdue),
            _ => filtered
        };

        filtered = CurrentSort switch
        {
            "Highest balance" => filtered.OrderByDescending(p => p.TotalBalance),
            "Newest balance" => filtered.OrderByDescending(p => p.MostRecentBillDate),
            "Patient name A-Z" => filtered.OrderBy(p => p.DisplayName),
            _ => filtered.OrderBy(p => p.NextDueDate ?? DateTime.MaxValue) // Nearest due date
        };

        Patients.Clear();
        foreach (var p in filtered)
            Patients.Add(p);
    }

    [RelayCommand]
    async Task OpenPatient(PatientBalanceCardViewModel card)
    {
        if (card == null) return;

        await Shell.Current.GoToAsync(
            $"{nameof(TransactionPage)}" +
            $"?patientId={Uri.EscapeDataString(card.PatientId)}" +
            $"&patientName={Uri.EscapeDataString(card.PatientName)}");
    }
}