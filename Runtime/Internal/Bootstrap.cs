using UnityEngine;

namespace TappGo.Internal
{
    /// <summary>
    /// Configures the SDK before the first scene loads, unless the host opted out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosts call <c>configure</c> on every launch anyway, so doing it for them removes the most common
    /// integration mistake — forgetting it, then finding out weeks later that no Live Activity ever rendered.
    /// It is a local operation with no network, so there is nothing to defer.
    /// </para>
    /// <para>
    /// Opt out with <c>autoConfigureOnLaunch</c> when configuration has to follow your own login flow, and call
    /// <see cref="Tapp.Configure"/> yourself. A missing settings asset is silent here on purpose: a project
    /// that hasn't set Tapp up yet should not have its console filled at every launch. The build hook is what
    /// refuses to ship such a project.
    /// </para>
    /// </remarks>
    internal static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureIfWanted()
        {
            // Nothing to configure where there is no native SDK, and nothing worth saying about it either.
            // A shipped Android player used to report a native version and then "Configured on launch" —
            // two lines describing work that did not happen, at the host's expense, for a platform this
            // version does not support. The Editor keeps all of it: there, the lines are the answer to why
            // nothing renders in Play mode.
#if !UNITY_IOS && !UNITY_EDITOR
            return;
#else
            var settings = TappGoSettings.Load();
            if (settings == null || !settings.autoConfigureOnLaunch)
            {
                return;
            }

            // The first thing support asks for, and the one question a host cannot answer from its own logs:
            // which native binary is actually loaded. It needs no configuration and cannot fail for anything the
            // host did, so it separates "the integration is wrong" from "the framework never loaded" — and in
            // the Editor it says `editor-stub`, which is the answer to "why is nothing rendering".
            var version = Tapp.SdkVersion();
            Debug.Log(version.Ok
                ? $"[Tapp] native {version.Value}"
                : $"[Tapp] native version unavailable: {version.Code}");

            // Logged here rather than in Tapp.Configure, because here is where the result is genuinely dropped:
            // nobody is holding it to check. A host that calls Configure itself gets the result back and can
            // decide, and logging there too would hand them an error they had already handled.
            //
            // Only the failures this package decides itself — no settings asset, an App Group that isn't one —
            // are logged by Tapp.Configure. A failure that comes back from native is not, and that is the one
            // that matters: a well-formed but wrong App Group passes validation, reaches the device, and fails
            // there. Without this line the launch log reports the native version and then says nothing at all,
            // which reads exactly like success while no Live Activity will ever render.
            var result = Tapp.Configure(settings);
            if (result.Ok)
            {
                Debug.Log("[Tapp] Configured on launch.");
            }
            else
            {
                Debug.LogError($"[Tapp] Configure failed on launch — {result}");
            }
#endif
        }
    }
}
