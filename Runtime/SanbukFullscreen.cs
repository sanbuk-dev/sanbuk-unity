using System;
using System.Collections.Generic;
using SanbukInternal;
using UnityEngine;

/// <summary>
/// A full-screen ad — an interstitial, or a rewarded one the player asked
/// for.
///
/// Loaded first and shown later, because the moment worth interrupting is
/// rarely the moment you have a network round trip to spare: load between
/// levels, show at the end of one.
///
/// The interruption rules — nothing during a cold start, nothing back to
/// back, a ceiling per session, a close control only after a few seconds —
/// are enforced in the native SDK, not here. They are the same on every
/// platform, and a shell cannot tune them away. They are also what
/// separates a format a publisher keeps from one they remove.
/// </summary>
public sealed class SanbukFullscreen
{
    private const string ClassName = "ir.sanbuk.sdk.SanbukFullscreen";

    private readonly AndroidJavaObject _native;

    private SanbukFullscreen(AndroidJavaObject native)
    {
        _native = native;
    }

    /// <summary>
    /// An interstitial: shown when your game decides, gated by the rules
    /// above. Null means nothing to show — carry on without it.
    /// </summary>
    public static void Load(string placementCode, Action<SanbukFullscreen> onResult)
    {
        Request("load", placementCode, onResult);
    }

    /// <summary>
    /// A rewarded ad: the player asked for it, so the interruption rules do
    /// not apply — refusing an ad somebody requested is refusing them their
    /// reward. A watch-duration requirement applies instead, and leaving
    /// early earns nothing.
    /// </summary>
    public static void LoadRewarded(string placementCode, Action<SanbukFullscreen> onResult)
    {
        Request("loadRewarded", placementCode, onResult);
    }

    /// <summary>
    /// Put it on screen.
    ///
    /// Pause your game in <paramref name="onShown"/> and resume in
    /// <paramref name="onClosed"/>: the ad is a separate activity, so your
    /// game keeps running behind it unless you stop it yourself.
    ///
    /// Grant the reward in <paramref name="onReward"/> and nowhere else.
    /// <paramref name="onClosed"/> fires whether or not the player earned
    /// anything, so granting there pays out for a skipped ad.
    ///
    /// <paramref name="onRefused"/> is not an error path to log and forget:
    /// a show can be refused by the rules above, and the game has to carry
    /// on as if it had never asked.
    /// </summary>
    public void Show(
        Action onShown = null,
        Action onClosed = null,
        Action onReward = null,
        Action<string> onRefused = null)
    {
        if (_native == null)
        {
            // The editor path. Nothing was ever loaded, so a refusal is the
            // honest answer and keeps the game's own handling exercised.
            MainThread.Run(() => onRefused?.Invoke("not_available"));

            return;
        }

        var callbacks = new Callbacks(onShown, onClosed, onReward, onRefused);
        AndroidBridge.OnUiThread(() => _native.Call("show", AndroidBridge.Activity, callbacks));
    }

    private static void Request(string method, string placementCode, Action<SanbukFullscreen> onResult)
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

        using var cls = new AndroidJavaClass(ClassName);
        cls.CallStatic(
            method,
            AndroidBridge.Activity,
            placementCode,
            new AndroidBridge.Lambda1(native =>
                onResult(native == null ? null : new SanbukFullscreen(native))));
    }

    /// <summary>
    /// Stands in for the native Callbacks interface. Every method arrives
    /// on a Java thread, so every one of them hands straight to the main
    /// thread before the game sees it.
    /// </summary>
    private sealed class Callbacks : AndroidJavaProxy
    {
        // Held for the same reason as AndroidBridge.Lambda1, but for
        // longer: the native side keeps this from the moment the ad is
        // shown until it is dismissed, and for a rewarded ad that is as
        // long as somebody watches. Released on onClosed, which the native
        // SDK promises to call whether or not a reward was earned — and
        // also on onRefused, where nothing is ever shown and so nothing
        // else will ever be called.
        private static readonly HashSet<Callbacks> Live = new HashSet<Callbacks>();

        private readonly Action _onShown;
        private readonly Action _onClosed;
        private readonly Action _onReward;
        private readonly Action<string> _onRefused;

        internal Callbacks(Action onShown, Action onClosed, Action onReward, Action<string> onRefused)
            : base("ir.sanbuk.sdk.SanbukFullscreen$Callbacks")
        {
            _onShown = onShown;
            _onClosed = onClosed;
            _onReward = onReward;
            _onRefused = onRefused;

            lock (Live)
            {
                Live.Add(this);
            }
        }

        public void onShown() => MainThread.Run(() => _onShown?.Invoke());

        public void onClosed()
        {
            Release();
            MainThread.Run(() => _onClosed?.Invoke());
        }

        public void onReward() => MainThread.Run(() => _onReward?.Invoke());

        public void onRefused(string reason)
        {
            Release();
            MainThread.Run(() => _onRefused?.Invoke(reason));
        }

        private void Release()
        {
            lock (Live)
            {
                Live.Remove(this);
            }
        }
    }
}
