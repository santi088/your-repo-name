using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace PAGELY;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The status bar continues the paper background instead of sitting on a dark
        // strip, and its icons are drawn dark so they stay legible on it.
        var window = Window;
        if (window is null || window.DecorView is not { } decorView)
        {
            return;
        }

        window.SetStatusBarColor(global::Android.Graphics.Color.ParseColor("#FBF6EA"));

        var insets = WindowCompat.GetInsetsController(window, decorView);
        if (insets is not null)
        {
            insets.AppearanceLightStatusBars = true;
        }
    }
}
