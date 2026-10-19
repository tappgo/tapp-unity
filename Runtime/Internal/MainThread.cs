using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace TappGo.Internal
{
    /// <summary>
    /// Runs work on Unity's main thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Native replies arrive on whatever thread finished the job — iOS answers Live Activity calls off the main
    /// thread, and will keep doing so. Touching any <c>UnityEngine</c> API from there, including
    /// <c>Debug.Log</c>, is undefined behaviour, so every callback the SDK hands back goes through here first.
    /// </para>
    /// <para>
    /// One hidden pump for the whole package: the host adds no prefab and no component, and there is exactly
    /// one place to look when a callback doesn't arrive. The same pump will serve the Android path, which is
    /// what keeps one C# code path across both platforms.
    /// </para>
    /// </remarks>
    internal sealed class MainThread : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> Pending = new ConcurrentQueue<Action>();
        private static MainThread _instance;

        /// <summary>
        /// Creates the pump before the first scene loads.
        /// </summary>
        /// <remarks>
        /// <c>HideAndDontSave</c> keeps it out of the Hierarchy and out of saved scenes — it is Tapp's plumbing,
        /// not part of the game's content, and a stray object in a scene file is a diff nobody wants to review.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Boot()
        {
            if (_instance != null)
            {
                return;
            }

            // Outside Play mode there is no Update loop for a pump to drain in, and DontDestroyOnLoad throws
            // outright. Editor code legitimately calls into the SDK — the build hook reads settings, a test
            // exercises the facade — so this is a supported state, not one to assert against. Work posted here
            // simply waits in the queue, which is the honest outcome: in the Editor nothing native ever answers.
            if (!Application.isPlaying)
            {
                return;
            }

            var host = new GameObject("[Tapp]") { hideFlags = HideFlags.HideAndDontSave };
            _instance = host.AddComponent<MainThread>();
            DontDestroyOnLoad(host);
        }

        /// <summary>
        /// Queues <paramref name="work"/> for the next frame on the main thread.
        /// </summary>
        /// <remarks>
        /// Safe to call from any thread, and safe to call before <see cref="Boot"/> has run: the queue is
        /// static, so anything posted early is drained once the pump exists rather than lost.
        /// </remarks>
        internal static void Post(Action work)
        {
            if (work == null)
            {
                return;
            }

            Pending.Enqueue(work);
        }

        private void Update()
        {
            while (Pending.TryDequeue(out var work))
            {
                // One throwing callback must not stop the rest of the queue draining, and must not surface as a
                // Tapp failure — it is the host's own code that threw.
                try
                {
                    work();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
