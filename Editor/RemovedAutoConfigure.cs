using System.Collections.Generic;
using TappGo.Editor.Modules;
using UnityEngine;

namespace TappGo.Editor
{
    /// <summary>
    /// Warns a build whose settings asset still says Tapp configures itself — which it no longer does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before 2.1.0 a <c>TappGoSettings.autoConfigureOnLaunch</c> switch, on by default, had Tapp call
    /// <c>Tapp.Configure</c> before the first scene loaded. It is gone: Tapp now does nothing until the host
    /// calls it, as on native iOS and Android. A game that relied on the switch still builds cleanly and ships
    /// with Tapp never started — and with no error anywhere, because nothing ran to report one. This is the only
    /// place that can say so before it ships.
    /// </para>
    /// <para>
    /// Read from the asset as text, the way <see cref="TappModuleSettings.Legacy"/> recovers the fields the
    /// package split removed: the field is gone from the type, but Unity leaves it on disk until the asset is
    /// next saved. So this is best-effort — once the asset is saved the line and the warning both go — and a
    /// warning rather than a failure, because a host that already calls <c>Configure</c> has nothing to fix.
    /// </para>
    /// </remarks>
    internal static class RemovedAutoConfigure
    {
        /// <summary>The removed field, as Unity spelled it in the asset.</summary>
        internal const string Field = "autoConfigureOnLaunch";

        internal const string Message =
            "[Tapp] Tapp no longer configures itself on launch — call Tapp.Configure() at startup, or nothing of " +
            "Tapp's runs in your game. Your TappGoSettings asset still has the removed \"Auto configure on launch\" " +
            "switch on; once your code calls Configure, change any value in Project Settings → Tapp to re-save " +
            "the asset and clear this warning.";

        /// <summary>Logs <see cref="Message"/> when the settings asset on disk still has the switch on.</summary>
        internal static void WarnIfStillRelied()
        {
            if (Relied(TappModuleSettings.LegacyOnDisk()))
            {
                Debug.LogWarning(Message);
            }
        }

        /// <summary>
        /// The reading half of <see cref="WarnIfStillRelied"/>, apart so a test can hand it a fixture.
        /// </summary>
        internal static bool Relied(IReadOnlyDictionary<string, string> fields)
        {
            return fields.TryGetValue(Field, out var value) && value == "1";
        }
    }
}
