using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanbukInternal
{
    /// <summary>
    /// The only file in this package that knows Java exists.
    ///
    /// Everything above it is plain C#, so the rest of the package reads the
    /// same whether it is talking to Android, to a future iOS build, or to
    /// nothing at all in the editor.
    /// </summary>
    internal static class AndroidBridge
    {
        /// <summary>
        /// False in the editor and in a desktop build. Every entry point checks
        /// this and answers "no ad" rather than throwing: a developer pressing
        /// Play should see an empty slot, which is a state their game has to
        /// handle anyway, not an exception from an ad SDK.
        /// </summary>
        internal static bool Available =>
            Application.platform == RuntimePlatform.Android && !Application.isEditor;

        private static AndroidJavaObject _activity;
        private static AndroidJavaObject _sanbuk;

        internal static AndroidJavaObject Activity
        {
            get
            {
                if (_activity == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    _activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                }

                return _activity;
            }
        }

        /// <summary>
        /// `object Sanbuk` in Kotlin is a singleton whose instance lives in a
        /// static INSTANCE field; its methods are instance methods on that.
        /// </summary>
        internal static AndroidJavaObject Sdk
        {
            get
            {
                if (_sanbuk == null)
                {
                    using var cls = new AndroidJavaClass("ir.sanbuk.sdk.Sanbuk");
                    _sanbuk = cls.GetStatic<AndroidJavaObject>("INSTANCE");
                }

                return _sanbuk;
            }
        }

        /// <summary>Run on Android's UI thread — where every View call belongs.</summary>
        internal static void OnUiThread(Action action)
        {
            Activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }));
        }

        /// <summary>
        /// Stands in for a Kotlin `(T) -> Unit` lambda.
        ///
        /// A Kotlin lambda is a kotlin.jvm.functions.Function1 once compiled,
        /// so that is the interface a proxy has to implement. It is not a
        /// pretty thing to depend on — it is Kotlin's calling convention rather
        /// than the SDK's published API — and the day the native SDK grows a
        /// plain listener interface for shells like this one, this class goes
        /// away and nothing above it changes.
        ///
        /// The result is discarded by the Kotlin caller (the lambda returns
        /// Unit), so returning null here is safe.
        /// </summary>
        internal sealed class Lambda1 : AndroidJavaProxy
        {
            // The Java side holds this proxy, but a reference from Java is
            // invisible to the C# collector: between handing the proxy over and
            // the ad coming back — a network round trip — nothing on the
            // managed side is holding it, and a collection in that window takes
            // the callback with it. The result is an ad that loads and then
            // never arrives, on a schedule nobody can reproduce. So a live
            // proxy holds itself, and lets go once it has been called.
            private static readonly HashSet<Lambda1> Live = new HashSet<Lambda1>();

            private readonly Action<AndroidJavaObject> _onCall;

            internal Lambda1(Action<AndroidJavaObject> onCall)
                : base("kotlin.jvm.functions.Function1")
            {
                _onCall = onCall;

                lock (Live)
                {
                    Live.Add(this);
                }
            }

            // Invoked from a Java thread. Nothing here may touch Unity.
            public AndroidJavaObject invoke(AndroidJavaObject argument)
            {
                lock (Live)
                {
                    Live.Remove(this);
                }

                MainThread.Run(() => _onCall(argument));

                return null;
            }
        }

        /// <summary>
        /// A Kotlin `String?` comes back as a C# null already; this exists for
        /// the other direction — a Java method that was never called because we
        /// are in the editor.
        /// </summary>
        internal static string CallString(AndroidJavaObject target, string method)
        {
            return target == null ? null : target.Call<string>(method);
        }
    }
}
