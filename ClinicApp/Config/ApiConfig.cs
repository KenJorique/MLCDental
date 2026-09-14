namespace ClinicApp.Config;

public static class ApiConfig
{
    // Use the Wi-Fi IP, not Ethernet!
    public static readonly string CephalometricApiUrl = "http://192.168.68.217:8000/analyze";

    
    public static readonly List<string> LandmarkClassOrder = new()
{
    "Anterior nasal spine", "Articulare", "Gnathion", "Gonion",
    "Incision inferius", "Incision superius", "Lower lip", "Menton",
    "Nasion", "Orbitale", "Pogonion", "Porion", "Posterior nasal spine",
    "Sella", "Soft tissue pogonion", "Subnasale", "Subspinale",
    "Supramentale", "Upper lip"
};

}

