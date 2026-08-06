namespace ClinicApp.Config;

public static class ApiConfig
{
    // ⚠️ REPLACE 192.168.1.100 WITH YOUR MACHINE'S IP ADDRESS
    // See it when you run: python server.py
    public static readonly string CephalometricApiUrl = "http://192.168.68.111:8000/analyze";
}