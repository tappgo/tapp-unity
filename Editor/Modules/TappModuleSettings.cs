using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace TappGo.Editor.Modules
{
    /// <summary>
    /// Creates a product package's settings asset, carrying over what a pre-split project already set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For Tapp's own packages, not for host code.</b> Before Tapp was split into packages, every product's
    /// fields lived in the one <c>TappGoSettings.asset</c>. Unity does not erase a field a type no longer
    /// declares until the asset is next saved, so the old values are still on disk in an upgraded project —
    /// and reading them here is what makes the upgrade keep a host's ids rather than ask for them again.
    /// </para>
    /// <para>
    /// The asset is read as text, deliberately. The fields are gone from the type, so no serialized property
    /// can reach them, and the YAML Unity writes for a flat <c>ScriptableObject</c> is a line per field.
    /// </para>
    /// <para>
    /// <b>"Until the asset is next saved" is not long.</b> Editing any core field saves it, and so does
    /// following the removed-auto-configure warning — both things a host does before pressing a product's
    /// Create button as readily as after. So <see cref="LegacySettingsKeeper"/> copies the pre-split asset
    /// aside the moment before that save, and <see cref="Legacy"/> reads the copy for whatever the asset no
    /// longer holds. The order a host does things in stops deciding whether their app id survives.
    /// </para>
    /// </remarks>
    public static class TappModuleSettings
    {
        /// <summary>Where every Tapp settings asset lives: the folder <c>Resources.Load</c> reads.</summary>
        public const string Folder = "Assets/Resources";

        internal const string LegacyAsset = Folder + "/TappGoSettings.asset";

        /// <summary>
        /// Where <see cref="LegacySettingsKeeper"/> keeps the pre-split asset. Under <c>Library</c>: it is this
        /// machine's upgrade aid, not something to commit — the settings a product creates from it are.
        /// </summary>
        internal const string LegacyCopy = "Library/Tapp/TappGoSettings.pre-split.asset";

        /// <summary>
        /// Loads the asset at <c>Assets/Resources/&lt;name&gt;.asset</c>, creating it when
        /// <paramref name="create"/> is set and it does not exist.
        /// </summary>
        /// <param name="seed">Fills a newly created asset — from <see cref="Legacy"/>, typically.</param>
        public static TSettings LoadOrCreate<TSettings>(string name, bool create, Action<TSettings> seed = null)
            where TSettings : ScriptableObject
        {
            var path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TSettings>(path);
            if (existing != null || !create)
            {
                return existing;
            }

            Directory.CreateDirectory(Folder);
            var created = ScriptableObject.CreateInstance<TSettings>();
            seed?.Invoke(created);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>
        /// The fields a pre-split <c>TappGoSettings.asset</c> carried, by name. Empty when there was no such
        /// asset. A list field reads as its elements joined by <c>\n</c>.
        /// </summary>
        /// <remarks>
        /// Read from the asset on disk, and — for what a save has since dropped from it — from the copy
        /// <see cref="LegacySettingsKeeper"/> took the moment before that save.
        /// </remarks>
        public static IReadOnlyDictionary<string, string> Legacy()
        {
            return Legacy(LegacyAsset, LegacyCopy);
        }

        /// <summary><see cref="Legacy()"/> for an asset and a kept copy at paths a test chooses.</summary>
        internal static Dictionary<string, string> Legacy(string asset, string copy)
        {
            return Merge(ReadFields(asset), ReadKept(copy));
        }

        /// <summary>
        /// Only what the asset on disk carries now. What asks "does the host's asset still say this?" reads
        /// this one — the kept copy would answer yes for ever.
        /// </summary>
        internal static Dictionary<string, string> LegacyOnDisk()
        {
            return ReadFields(LegacyAsset);
        }

        private static Dictionary<string, string> ReadFields(string path)
        {
            return File.Exists(path)
                ? ParseLegacy(File.ReadAllText(path))
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// The kept copy's fields, or none when it cannot be read. It is an upgrade aid: a copy that is locked
        /// or damaged means a product's settings start empty, not that creating them fails.
        /// </summary>
        private static Dictionary<string, string> ReadKept(string path)
        {
            try
            {
                return ReadFields(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// What the asset holds, then whatever only the kept copy still has. The asset wins where both have a
        /// field: it is the newer of the two.
        /// </summary>
        internal static Dictionary<string, string> Merge(
            IReadOnlyDictionary<string, string> onDisk, IReadOnlyDictionary<string, string> kept)
        {
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in kept)
            {
                merged[field.Key] = field.Value;
            }

            foreach (var field in onDisk)
            {
                merged[field.Key] = field.Value;
            }

            return merged;
        }

        /// <summary>
        /// Whether <paramref name="yaml"/> carries a field <see cref="TappGoSettings"/> no longer declares —
        /// which is what a save is about to erase, and so what makes the asset worth copying first.
        /// </summary>
        internal static bool CarriesDroppedFields(string yaml)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in typeof(TappGoSettings).GetFields(instance))
            {
                declared.Add(field.Name);
                foreach (FormerlySerializedAsAttribute former in
                         field.GetCustomAttributes(typeof(FormerlySerializedAsAttribute), false))
                {
                    declared.Add(former.oldName);
                }
            }

            foreach (var key in ParseLegacy(yaml).Keys)
            {
                // m_Script, m_Name and the rest are Unity's own lines, present in every asset.
                if (!key.StartsWith("m_", StringComparison.Ordinal) && !declared.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Copies the asset aside if it is still a pre-split one. Called the moment before Unity saves it.
        /// </summary>
        /// <remarks>
        /// Never throws: this runs inside a save of the host's own asset, and failing to keep an upgrade aid
        /// is not a reason to disturb it.
        /// </remarks>
        internal static void KeepLegacyCopy()
        {
            KeepLegacyCopy(LegacyAsset, LegacyCopy);
        }

        /// <summary><see cref="KeepLegacyCopy()"/> for an asset and a copy at paths a test chooses.</summary>
        internal static void KeepLegacyCopy(string asset, string copy)
        {
            try
            {
                if (!File.Exists(asset))
                {
                    return;
                }

                // An asset that already matches the type has nothing a save can take, and writing it over the
                // copy would replace the old values with none.
                var yaml = File.ReadAllText(asset);
                if (!CarriesDroppedFields(yaml))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.WriteAllText(copy, yaml);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Tapp] Could not keep a copy of the pre-split TappGoSettings.asset, so a product's settings " +
                    $"created after this save start empty rather than from your old values. ({exception.Message})");
            }
        }

        /// <summary>The parsing half of <see cref="Legacy"/>, apart so a test can hand it a fixture.</summary>
        internal static Dictionary<string, string> ParseLegacy(string yaml)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            string list = null;
            foreach (var raw in yaml.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (list != null && line.StartsWith("  - ", StringComparison.Ordinal))
                {
                    var item = Unquote(line.Substring(4));
                    fields[list] = fields[list].Length == 0 ? item : fields[list] + "\n" + item;
                    continue;
                }

                list = null;
                if (!line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith("   ", StringComparison.Ordinal))
                {
                    continue;
                }

                var colon = line.IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }

                var key = line.Substring(2, colon - 2);
                var value = line.Substring(colon + 1).Trim();
                if (value.Length == 0)
                {
                    list = key;
                    fields[key] = "";
                }
                else if (value != "[]")
                {
                    fields[key] = Unquote(value);
                }
            }

            return fields;
        }

        private static string Unquote(string value)
        {
            value = value.Trim();
            return value.Length >= 2 && (value[0] == '\'' || value[0] == '"') && value[value.Length - 1] == value[0]
                ? value.Substring(1, value.Length - 2)
                : value;
        }
    }

    /// <summary>
    /// Keeps a copy of a pre-split <c>TappGoSettings.asset</c> the moment before Unity saves it again.
    /// </summary>
    /// <remarks>
    /// Saving is what erases the fields the split took out of the type, and with them the values a product's
    /// Create button would have carried over. Unity calls this for every save in the project, so it looks at
    /// the paths and does nothing unless Tapp's own asset is among them.
    /// </remarks>
    internal sealed class LegacySettingsKeeper : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (SavesTheSettings(paths))
            {
                TappModuleSettings.KeepLegacyCopy();
            }

            return paths;
        }

        /// <summary>Whether Tapp's own settings asset is among the paths Unity is about to save.</summary>
        internal static bool SavesTheSettings(string[] paths)
        {
            foreach (var path in paths)
            {
                var saved = path.Replace('\\', '/');
                if (string.Equals(saved, TappModuleSettings.LegacyAsset, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
