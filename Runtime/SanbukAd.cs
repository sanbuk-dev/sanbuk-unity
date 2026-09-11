using System;
using SanbukInternal;
using UnityEngine;

/// <summary>
/// One ad, described rather than drawn.
///
/// In a game this is not one of two options — it is the only one. A game
/// renders into a single surface and has no Android layout to put a view
/// in, so an ad that has to live inside the scene has to be drawn by the
/// game, with the game's own uGUI, fonts and framing. That is exactly what
/// the server's reply is shaped for: it describes an ad, it never sends
/// markup.
///
/// Two calls carry obligations rather than just behaviour:
///
/// - <see cref="RecordImpression"/> must fire when the ad is really on
///   screen, not when it was loaded. Nothing can check this from the
///   server, which is why it is stated here rather than assumed.
/// - <see cref="DisclosureLabel"/> has to appear on the ad. Store policy
///   asks for it, advertisers expect it, and a player deserves to know
///   which part of the screen was paid for.
/// </summary>
public sealed class SanbukAd
{
    private readonly AndroidJavaObject _native;
    private bool _impressionRecorded;

    internal SanbukAd(AndroidJavaObject native)
    {
        _native = native;
        Format = native.Call<string>("getFormat");
        CampaignId = native.Call<string>("getCampaignId");
        Headline = native.Call<string>("getHeadline");
        Body = native.Call<string>("getBody");
        CallToAction = native.Call<string>("getCallToAction");
        ImageUrl = native.Call<string>("getImageUrl");
        LogoUrl = native.Call<string>("getLogoUrl");
        DisclosureLabel = native.Call<string>("getDisclosureLabel");
        BrandColor = ReadBrandColor(native);
    }

    /// <summary>banner | native | interstitial | rewarded — chosen by the
    /// publisher when the slot was registered, never by your code.</summary>
    public string Format { get; }

    public string CampaignId { get; }
    public string Headline { get; }
    public string Body { get; }
    public string CallToAction { get; }
    public string ImageUrl { get; }
    public string LogoUrl { get; }

    /// <summary>The advertiser's own colour, when they chose one.</summary>
    public Color32? BrandColor { get; }

    /// <summary>The word that has to appear on any ad your game draws.</summary>
    public string DisclosureLabel { get; }

    /// <summary>
    /// Report that this ad was seen. Safe to call more than once — the
    /// second call does nothing, and a retry from the offline queue is
    /// recognised by the server rather than counted twice.
    /// </summary>
    public void RecordImpression()
    {
        if (_impressionRecorded || _native == null)
        {
            return;
        }

        _impressionRecorded = true;
        _native.Call("recordImpression");
    }

    /// <summary>
    /// Open the destination. Always through the tracker and always in a
    /// real browser: an in-app webview has its own cookie jar, so the
    /// first-party click id does not survive it and the conversion is never
    /// attributed — the publisher does the work and earns nothing.
    /// </summary>
    public void Click()
    {
        if (_native == null)
        {
            return;
        }

        AndroidBridge.OnUiThread(() => _native.Call("click", AndroidBridge.Activity));
    }

    // Kotlin's Int? is a boxed Integer over JNI, so it arrives as an object
    // that may be null rather than as a value.
    private static Color32? ReadBrandColor(AndroidJavaObject native)
    {
        using var boxed = native.Call<AndroidJavaObject>("getBrandColor");
        if (boxed == null)
        {
            return null;
        }

        var argb = boxed.Call<int>("intValue");

        return new Color32(
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF),
            (byte)((argb >> 24) & 0xFF));
    }
}
