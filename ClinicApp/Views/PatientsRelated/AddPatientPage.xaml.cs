using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class AddPatientPage : ContentPage
{
    public AddPatientPage(AddPatientViewModel vm)
    {
        InitializeComponent();

        BindingContext = vm;
    }
}