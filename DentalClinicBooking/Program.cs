using DentalClinicBooking.Models;
using DentalClinicBooking.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<SupabaseService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ?? Keep Supabase awake (free tier pauses after 7 days inactivity) ??
var supabase = app.Services.GetRequiredService<SupabaseService>();
var timer = new System.Timers.Timer(TimeSpan.FromHours(24).TotalMilliseconds);
timer.Elapsed += async (s, e) =>
{
    try
    {
        await supabase.Client.From<DentalClinicBooking.Models.Booking>().Limit(1).Get();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[KeepAlive] {ex.Message}");
    }
};
timer.AutoReset = true;
timer.Start();

app.Run();