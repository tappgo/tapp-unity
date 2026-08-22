using TappGo;
using UnityEngine;

namespace TappGo.Samples
{
    /// <summary>
    /// The whole Live Activity lifecycle, in the order a game uses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drop this on any GameObject and wire the four methods to buttons. Note what the sample does <i>not</i>
    /// do: it never builds content, never stores an identifier, and never checks whether an activity is running
    /// before updating it. All three are the SDK's job.
    /// </para>
    /// <para>
    /// In the Editor every call logs and returns without rendering anything — Live Activities exist only on a
    /// device.
    /// </para>
    /// </remarks>
    public sealed class ActivityDemo : MonoBehaviour
    {
        [Tooltip("The live activity id from your Tapp back office.")]
        public string activityId = "match-4471";

        [Tooltip("The screen to open on. Configured server-side alongside the activity.")]
        public string firstScreen = "screen-1";

        [Tooltip("The screen to show when the match finishes. Its own card, not this one moved.")]
        public string resultsScreen = "screen-2";

        private void Start()
        {
            var version = Tapp.SdkVersion();
            Debug.Log($"[Demo] Tapp native version: {version.Value}");
        }

        /// <summary>Starts the activity, with a 90-minute clock.</summary>
        public void StartMatch()
        {
            Tapp.StartLiveActivity(
                new TappActivityRequest
                {
                    Id = activityId,
                    EntryId = firstScreen,
                    // How long the design's countdowns run, from the moment the activity starts — a duration,
                    // not a deadline. It marks the content stale when it runs out; it ends nothing.
                    Seconds = 90 * 60,
                },
                result =>
                {
                    if (result.Ok)
                    {
                        Debug.Log($"[Demo] Started ({result.Value}).");
                        return;
                    }

                    // Expected outcomes, not bugs: the player may have Live Activities switched off, or this
                    // activity may already be running from an earlier session.
                    if (result.Code == TappErrorCode.LiveActivitiesUnavailable)
                    {
                        Debug.Log("[Demo] Player has Live Activities disabled — nothing to show.");
                        return;
                    }

                    Debug.LogWarning($"[Demo] Start failed — {result}");
                });
        }

        /// <summary>Takes the match card down and puts the results one up in its place.</summary>
        /// <remarks>
        /// Two calls, because a card never changes screen: the screen it starts on is its address for life, so
        /// showing another one is a second card. Ended first here only because a match and its result shouldn't
        /// both be on the Lock Screen — leave the end out and the two run side by side.
        /// </remarks>
        public void ShowResults()
        {
            // No check that the match card is still running: the player may have dismissed it a moment ago, and
            // ending a card that isn't there succeeds and does nothing.
            Tapp.EndLiveActivity(activityId, firstScreen, completion: result =>
            {
                if (!result.Ok)
                {
                    Debug.LogWarning($"[Demo] End failed — {result}");
                }

                Tapp.StartLiveActivity(
                    new TappActivityRequest { Id = activityId, EntryId = resultsScreen },
                    started =>
                    {
                        if (!started.Ok)
                        {
                            Debug.LogWarning($"[Demo] Results failed — {started}");
                        }
                    });
            });
        }

        /// <summary>Ends the results card, leaving it on the Lock Screen for another 30 minutes.</summary>
        public void EndMatch()
        {
            // The screen is named, like everywhere else: this ends that one card and leaves any other screen of
            // the campaign running. The delay is a duration from the end, not a deadline, and it is one shot —
            // iOS caps it at four hours without saying so, and once the activity ends nothing can revise it.
            Tapp.EndLiveActivity(activityId, resultsScreen, 30 * 60, result =>
            {
                if (!result.Ok)
                {
                    Debug.LogWarning($"[Demo] End failed — {result}");
                }
            });
        }

        /// <summary>Reconciles after a relaunch — activities outlive the process.</summary>
        public void Reconcile()
        {
            var active = Tapp.ActiveLiveActivities();
            if (!active.Ok)
            {
                Debug.LogWarning($"[Demo] Could not read activities — {active}");
                return;
            }

            foreach (var info in active.Value)
            {
                // IsFinished, not `State == Ended`: an unrecognised state from a newer iOS counts as still
                // running, and treating it as finished would stop us updating something on screen.
                // The screen comes back too, which is what makes a card found this way drivable: id and screen
                // together are what an update or an end takes, so nothing has to survive the relaunch here.
                Debug.Log($"[Demo] {info} (finished: {info.State.IsFinished})");
            }
        }
    }
}
