using SanbukInternal;
using UnityEngine;

/// <summary>
/// Where the banner sits over your game.
///
/// Prefixed, and in no namespace, like everything else a game sees from this
/// package: Unity SDKs are used without a `using` line by convention, and an
/// unprefixed SanbukBannerPosition in the global namespace would sooner or later
/// collide with a type the game already has.
/// </summary>
public enum SanbukBannerPosition
{
    Top,
    Bottom,
}

/// <summary>
/// A banner drawn by the native SDK, laid over your game.
///
/// This is the one place where the Unity binding cannot mirror the others.
/// Everywhere else a banner is a view the app puts in its own layout; a
/// game has no layout — it renders into a single surface — so there is
/// nowhere to put one. The banner is therefore positional rather than a
/// view: it is added to the activity's content, above the surface your game
/// draws on, which is the same thing every other ad SDK on Unity does.
///
/// Because the native SDK draws it, its viewability gate is the native one:
/// the impression is counted when the ad was actually visible for long
/// enough, and you do not report anything. That is the difference between
/// this and <see cref="Sanbuk.LoadAd"/>, and the reason to prefer it
/// wherever the ad does not need to live inside your scene.
/// </summary>
public static class SanbukBanner
{
    // android.view.Gravity
    private const int GravityTop = 48;
    private const int GravityBottom = 80;
    private const int GravityCenterHorizontal = 1;

    // android.view.ViewGroup.LayoutParams
    private const int MatchParent = -1;
    private const int WrapContent = -2;

    // android.view.View
    private const int Gone = 8;
    private const int Visible = 0;

    private static AndroidJavaObject _view;

    /// <summary>
    /// Show a banner for this placement. Calling it again with a different
    /// placement replaces the current one rather than stacking a second
    /// banner over the first.
    /// </summary>
    public static void Show(string placementCode, SanbukBannerPosition position = SanbukBannerPosition.Bottom)
    {
        if (!AndroidBridge.Available)
        {
            return;
        }

        MainThread.Ensure();

        AndroidBridge.OnUiThread(() =>
        {
            Detach();

            var activity = AndroidBridge.Activity;
            _view = new AndroidJavaObject("ir.sanbuk.sdk.SanbukAdView", activity);

            var gravity = position == SanbukBannerPosition.Top ? GravityTop : GravityBottom;
            using var layout = new AndroidJavaObject(
                "android.widget.FrameLayout$LayoutParams",
                MatchParent,
                WrapContent,
                gravity | GravityCenterHorizontal);

            // addContentView rather than reaching into the view tree: it is
            // the documented way to put something over an activity's
            // content, and it does not care how Unity built that content.
            activity.Call("addContentView", _view, layout);
            _view.Call("load", placementCode);
        });
    }

    /// <summary>
    /// Take the banner off screen. The next <see cref="Show"/> asks for a
    /// fresh ad rather than restoring this one, because an ad held off
    /// screen and shown later is an ad whose auction has gone stale.
    /// </summary>
    public static void Hide()
    {
        if (!AndroidBridge.Available || _view == null)
        {
            return;
        }

        AndroidBridge.OnUiThread(Detach);
    }

    private static void Detach()
    {
        if (_view == null)
        {
            return;
        }

        // Hidden first: removeView on a view mid-layout can leave a frame
        // of it painted, and a banner that flickers as it leaves looks like
        // a bug in the game rather than in us.
        _view.Call("setVisibility", Gone);

        using var parent = _view.Call<AndroidJavaObject>("getParent");
        if (parent != null)
        {
            parent.Call("removeView", _view);
        }

        _view.Dispose();
        _view = null;
    }

    /// <summary>
    /// Keep the banner but stop showing it — for a cutscene, or a menu that
    /// covers the screen. Cheaper than <see cref="Hide"/> plus
    /// <see cref="Show"/>, and it does not spend a fresh ad.
    /// </summary>
    public static void SetVisible(bool visible)
    {
        if (!AndroidBridge.Available || _view == null)
        {
            return;
        }

        AndroidBridge.OnUiThread(() => _view.Call("setVisibility", visible ? Visible : Gone));
    }
}
