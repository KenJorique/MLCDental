namespace ClinicApp.Views.Shared;

public partial class CustomTabBar : ContentView
{
    static readonly Color Pink = Color.FromArgb("#C4607A");
    static readonly Color Gray = Color.FromArgb("#B0B7C3");

    public CustomTabBar()
    {
        InitializeComponent();
    }

    // Set this from each page: "Home", "Appointment", "Patient", "Menu"
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(
            nameof(ActiveTab),
            typeof(string),
            typeof(CustomTabBar),
            defaultValue: "Home",
            propertyChanged: (b, o, n) => ((CustomTabBar)b).RefreshColors());

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    // Called whenever ActiveTab changes or the control loads
    void RefreshColors()
    {
        SetTab("Home");
        SetTab("Appointment");
        SetTab("Patient");
        SetTab("Menu");
    }

    void SetTab(string tab)
    {
        bool isActive = ActiveTab == tab;
        Color color = isActive ? Pink : Gray;

        switch (tab)
        {
            case "Home":
                IconHome.TextColor = color;
                LabelHome.TextColor = color;
                LabelHome.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
                break;
            case "Appointment":
                IconAppointment.TextColor = color;
                LabelAppointment.TextColor = color;
                LabelAppointment.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
                break;
            case "Patient":
                IconPatient.TextColor = color;
                LabelPatient.TextColor = color;
                LabelPatient.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
                break;
            case "Menu":
                IconMenu.TextColor = color;
                LabelMenu.TextColor = color;
                LabelMenu.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
                break;
        }
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        // Set initial colors once control is attached to the visual tree
        RefreshColors();
    }

    // ?? Tap handlers ??????????????????????????????????????????
    async void OnHomeTapped(object sender, EventArgs e)
    {
        if (ActiveTab == "Home") return;
        await Shell.Current.GoToAsync("//HomePage");
    }

    async void OnAppointmentTapped(object sender, EventArgs e)
    {
        if (ActiveTab == "Appointment") return;
        await Shell.Current.GoToAsync("//AppointmentPage");
    }

    async void OnPatientTapped(object sender, EventArgs e)
    {
        if (ActiveTab == "Patient") return;
        await Shell.Current.GoToAsync("//PatientListPage");
    }

    async void OnMenuTapped(object sender, EventArgs e)
    {
        if (ActiveTab == "Menu") return;
        await Shell.Current.GoToAsync("//MenuPage");
    }
}
