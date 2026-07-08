using ClinicApp.Models;
using ClinicApp.Services;
using ClinicApp.Views.CephalometricRelated;
using ClinicApp.Views.DentalChart;
using ClinicApp.Views.PatientsRelated;
using ClinicApp.Views.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ClinicApp.ViewModels.PatientsRelatedVM
{
    public partial class PatientListViewModel : ObservableObject
    {
        readonly DatabaseService _db;

        private List<PatientCardViewModel> _allPatients = new();
        public ObservableCollection<PatientCardViewModel> Patients { get; set; } = new();

        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isRefreshing;
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private string currentSort = "All";

        public PatientListViewModel(DatabaseService db) => _db = db;

        [RelayCommand]
        async Task LoadPatients()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var list = await _db.GetPatients();
                _allPatients = list.Select(p => new PatientCardViewModel(p)).ToList();
                ApplyFilterAndSort();
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

        private void ApplyFilterAndSort()
        {
            var filtered = _allPatients.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLower();
                filtered = filtered.Where(c =>
                    c.Patient.FullName.ToLower().Contains(term));
            }

            filtered = CurrentSort switch
            {
                "A-Z" => filtered.OrderBy(c => c.Patient.LastName).ThenBy(c => c.Patient.FirstName),
                "Z-A" => filtered.OrderByDescending(c => c.Patient.LastName).ThenByDescending(c => c.Patient.FirstName),
                "Recently Added" => filtered.OrderByDescending(c => c.Patient.PatientID),
                _ => filtered
            };

            Patients.Clear();
            foreach (var card in filtered)
                Patients.Add(card);
        }

        [RelayCommand]
        async Task ShowSortOptions()
        {
            string result = await Shell.Current.DisplayActionSheet(
                "Sort Patients", "Cancel", null,
                "All", "A-Z", "Z-A", "Recently Added");

            if (!string.IsNullOrEmpty(result) && result != "Cancel")
            {
                CurrentSort = result;
                ApplyFilterAndSort();
            }
        }

        // Opens the bottom action sheet when a card is tapped
        [RelayCommand]
        async Task OpenActionSheet(PatientCardViewModel card)
        {
            if (card is null) return;

            var sheet = new ItemActionSheet();
            sheet.Configure(
                title: card.Patient.FullName,
                subtitle: $"Patient ID: {card.Patient.PatientID:D3}",
                options: new[]
                {
                    new ActionSheetOption
                    {
                        Icon = "\ue09e",
                        Label = "Patient Records",
                        Subtitle = "View full patient details",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(PatientDetailsPage)}?id={card.Patient.PatientID}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue0a6",
                        Label = "Dental Chart",
                        Subtitle = "View dental chart",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(DentalChartPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue0aa",
                        Label = "Cephalometric",
                        Subtitle = "View cephalometric analysis",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(CephalometricPage)}?PatientId={card.Patient.PatientID}&PatientName={Uri.EscapeDataString(card.Patient.FullName)}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue889",
                        Label = "Treatment History",
                        Subtitle = "View past treatments",
                        IconBackgroundColor = Color.FromArgb("#E8F5E9"),
                        IconColor = Color.FromArgb("#1A6B2F"),
                        OnTapped = async () =>
                            await Shell.Current.GoToAsync(
                                $"{nameof(TreatmentHistoryPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue8f1",
                        Label = "Transaction History",
                        Subtitle = "Coming soon",
                        IconBackgroundColor = Color.FromArgb("#F5F5F5"),
                        IconColor = Color.FromArgb("#9E9E9E"),
                        OnTapped = async () =>
                            await Shell.Current.DisplayAlert("Coming Soon",
                                "Transaction History is not available yet.", "OK"),
                    },
                    new ActionSheetOption
                    {
                        Icon = "\ue872",
                        Label = "Delete Patient",
                        Subtitle = "Remove from records",
                        LabelColor = Colors.Crimson,
                        IconBackgroundColor = Color.FromArgb("#FFEBEE"),
                        IconColor = Colors.Crimson,
                        OnTapped = async () => await DeletePatient(card),
                    },
                });

            await sheet.ShowAsync();
        }

        // Call button — opens phone dialer
        [RelayCommand]
        async Task CallPatient(PatientCardViewModel card)
        {
            if (card is null || string.IsNullOrWhiteSpace(card.Patient.MobileNo)) return;
            try
            {
                if (PhoneDialer.Default.IsSupported)
                    PhoneDialer.Default.Open(card.Patient.MobileNo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Call] {ex.Message}");
            }
        }

        [RelayCommand]
        async Task GoToAddPatient() =>
            await Shell.Current.GoToAsync(nameof(AddPatientPage));

        [RelayCommand]
        async Task ViewPatient(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync($"{nameof(PatientDetailsPage)}?id={card.Patient.PatientID}");
        }

        [RelayCommand]
        async Task EditPatient(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync($"{nameof(AddPatientPage)}?PatientId={card.Patient.PatientID}");
        }

        [RelayCommand]
        async Task DeletePatient(PatientCardViewModel card)
        {
            if (card is null) return;

            bool answer = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete {card.Patient.FullName}?",
                "Yes", "No");

            if (answer)
            {
                try { await _db.DeletePatient(card.Patient); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Delete] {ex.Message}"); }
                await MainThread.InvokeOnMainThreadAsync(async () => await LoadPatients());
            }
        }

        [RelayCommand]
        async Task ViewDentalChart(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(DentalChartPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }

        [RelayCommand]
        async Task GoToCephalometric(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(CephalometricPage)}?PatientId={card.Patient.PatientID}&PatientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }

        [RelayCommand]
        async Task ViewTreatmentHistory(PatientCardViewModel card)
        {
            if (card is null) return;
            await Shell.Current.GoToAsync(
                $"{nameof(TreatmentHistoryPage)}?patientId={card.Patient.PatientID}&patientName={Uri.EscapeDataString(card.Patient.FullName)}");
        }
    }
}
