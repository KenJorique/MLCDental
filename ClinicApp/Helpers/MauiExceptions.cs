namespace ClinicApp
{
    public static class MauiExceptions
    {
        public static event UnhandledExceptionEventHandler? UnhandledException;

        static MauiExceptions()
        {
#if ANDROID
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser +=
                (sender, args) =>
                {
                    var ex = args.Exception;
                    System.Diagnostics.Debug.WriteLine(
                        $"[Android FATAL] {ex.Message}");
                    System.Diagnostics.Debug.WriteLine(
                        $"[Android FATAL] {ex.StackTrace}");
                    UnhandledException?.Invoke(sender,
                        new UnhandledExceptionEventArgs(ex, true));
                    args.Handled = true;
                };
#endif
        }

        public static void Initialize() { }
    }
}