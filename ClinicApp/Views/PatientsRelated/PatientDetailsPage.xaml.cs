using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class PatientDetailsPage : ContentPage
{
    public PatientDetailsPage(PatientDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
