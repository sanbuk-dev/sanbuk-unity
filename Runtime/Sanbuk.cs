using System;
using SanbukInternal;
using UnityEngine;

/// <summary>
/// The publisher's entry point.
///
/// <code>
/// Sanbuk.Init("YOUR-MEDIA-CODE");
///
/// Sanbuk.LoadAd("LEVEL-END", ad =>
/// {
///     if (ad == null) return;          // a normal answer, not a failure
///     DrawItWithYourOwnUI(ad);
///     ad.RecordImpression();           // when it is really on screen
/// });
/// </code>
///
/// A thin binding, deliberately. Matching, pacing, frequency caps, trial
/// periods and budgets live on the Sanbuk server; the viewability rule, the
/// offline impression queue and the interruption rules live in the native
/// SDK. This package asks, draws and reports — nothing else.
///
/// A build shipped to a store is frozen for months, so a rule hardened into
/// a shell would outlive several changes of mind, and would have to be
/// written again for every other shell, where the copies would drift.
/// </summary>
public static class Sanbuk
{
    /// <summary>
    /// Once, at startup. Returns immediately — nothing here touches the
    /// disk on the calling thread, because this belongs in the same frame
    /// as the rest of your boot and every millisecond there is charged to
    /// your cold start.
    /// </summary>
    public static void Init(string mediaCode, bool debug = false)
    {
        if (!AndroidBridge.Available)
        {
            return;
        }

        MainThread.Ensure();

        // init(Context, String, boolean) rather than building a SanbukConfig:
        // the config carries the tracker URL between the code and the flag, and
        // constructing one from here would mean naming our own endpoint in this
        // file, where it would drift from the SDK's.
        AndroidBridge.Sdk.Call("init", AndroidBridge.Activity, mediaCode, debug);
    }

    /// <summary>
    /// Ask for an ad as plain data, to draw with your own UI.
    ///
    /// <paramref name="onResult"/> is called exactly once, on Unity's main
    /// thread, and null is a normal answer: no campaign matched, the budget
    /// is spent, the slot is capped for today. Leave the space empty or
    /// draw your own content — never show an error.
    ///
    /// In the editor this answers null without touching the network, so
    /// pressing Play never depends on an ad being available.
    /// </summary>
    public static void LoadAd(string placementCode, Action<SanbukAd> onResult)
    {
        if (onResult == null)
        {
            throw new ArgumentNullException(nameof(onResult));
        }

        if (!AndroidBridge.Available)
        {
            onResult(null);

            return;
        }

        MainThread.Ensure();

        AndroidBridge.Sdk.Call(
            "loadAd",
            AndroidBridge.Activity,
            placementCode,
            new AndroidBridge.Lambda1(native =>
                onResult(native == null ? null : new SanbukAd(native))));
    }
}
