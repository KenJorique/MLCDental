using ClinicApp.Views.UsersRelated;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicApp.Services;

/// <summary>
/// Switches the app's root page between the (Shell-free) LoginPage and the
/// full AppShell. This exists because Shell.GoToAsync("//LoginPage") does
/// not work for a page registered only via Routing.RegisterRoute — MAUI
/// treats that as a "global/detail" route, which cannot be the sole page
/// on the nav stack ("Global routes currently cannot be the only page on
/// the stack..."). Swapping MainPage sidesteps that restriction entirely:
/// LoginPage is never part of Shell's routing tree, so this rule never
/// applies to it.
///
/// Requires both LoginPage and AppShell to be registered in MauiProgram.cs
/// (Transient is fine for both — see IMPLEMENTATION_GUIDE).
/// </summary>
public static class NavigationHelper
{
    private static IServiceProvider? Services =>
        Application.Current?.Handler?.MauiContext?.Services;

    public static void ShowLogin()
    {
        var loginPage = Services?.GetService<LoginPage>();
        if (loginPage != null && Application.Current != null)
            Application.Current.MainPage = loginPage;
    }

    public static void ShowApp()
    {
        var shell = Services?.GetService<AppShell>();
        if (shell != null && Application.Current != null)
            Application.Current.MainPage = shell;
    }
}