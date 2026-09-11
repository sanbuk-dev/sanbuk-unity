using System;
using System.Collections.Generic;
using UnityEngine;

namespace SanbukInternal
{
    /// <summary>
    /// Moves a callback from the Java thread it arrived on to Unity's main
    /// thread.
    ///
    /// Every answer from the native SDK crosses JNI on a thread Unity knows
    /// nothing about. Touching almost any Unity API from there does not throw —
    /// it crashes the player, often not at the call itself, which makes it one
    /// of the hardest faults a publisher could be asked to debug. So no
    /// callback in this package reaches the game directly: they all come
    /// through here.
    /// </summary>
    internal sealed class MainThread : MonoBehaviour
    {
        private static MainThread _instance;

        // Handed between threads, so every touch is locked. The queue is short
        // — an ad load, an impression, a close — and contention is rare.
        private static readonly Queue<Action> Pending = new Queue<Action>();

        /// <summary>
        /// Created before the first scene loads, so a publisher who calls
        /// <c>Sanbuk.Init</c> in <c>Awake</c> is not racing us.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            // hideFlags keeps it out of the publisher's hierarchy view: an
            // object they did not create, cannot use and might delete.
            var host = new GameObject("SanbukMainThread") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<MainThread>();
        }

        internal static void Run(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (Pending)
            {
                Pending.Enqueue(action);
            }
        }

        private void Update()
        {
            // Drained into a local list first: an action that queues another
            // action would otherwise either deadlock on the lock or spin
            // through the same frame forever.
            while (true)
            {
                Action next;
                lock (Pending)
                {
                    if (Pending.Count == 0)
                    {
                        return;
                    }

                    next = Pending.Dequeue();
                }

                try
                {
                    next();
                }
                catch (Exception e)
                {
                    // A game must not die because an ad callback threw. The
                    // publisher's own handler is the usual culprit, and they
                    // need the stack trace, not a crash.
                    Debug.LogException(e);
                }
            }
        }
    }
}
