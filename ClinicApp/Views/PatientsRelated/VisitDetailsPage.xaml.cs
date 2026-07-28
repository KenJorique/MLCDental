using ClinicApp.ViewModels.PatientsRelatedVM;

namespace ClinicApp.Views.PatientsRelated;

public partial class VisitDetailsPage : ContentPage
{
    public VisitDetailsPage(VisitDetailsViewModel vm)
    {
        InitializeComponent();

        BindingContext = vm;
    }
}