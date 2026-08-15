
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
            "Overdue first", "Nearest due date", "Highest balance", "Patient name A-Z");

        if (!string.IsNullOrEmpty(result) && result != "Cancel")
        {
            CurrentSort = result;
            ApplyFilterAndSort();
        }
    }

    void ApplyFilterAndSort()
    {
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
            "Overdue first" => filtered.OrderByDescending(p => p.IsOverdue)
                                        .ThenBy(p => p.NextDueDate ?? DateTime.MaxValue),
            "Highest balance" => filtered.OrderByDescending(p => p.TotalBalance),
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