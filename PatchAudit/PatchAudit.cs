using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace PatchAudit
{
    /// <summary>
    /// Diagnostic plugin. Reports how many DISTINCT methods each mod has Harmony-patched.
    ///
    /// Why distinct methods: MonoMod installs one native detour (and therefore one trampoline)
    /// per patched method, no matter how many mods stack prefixes/postfixes on it. Ten mods
    /// patching Player.Update cost one trampoline; one mod PatchAll-ing 400 methods costs 400.
    /// If you are hitting a trampoline / near-memory ceiling, the number that matters is the
    /// "exclusive" column - methods only that mod touches - because those are the only ones
    /// that get freed if you remove it.
    ///
    /// This plugin deliberately injects no IL2CPP types and applies no patches of its own,
    /// so it does not consume the budget it is measuring. It reads Harmony's managed-side
    /// registry from a background timer.
    /// </summary>
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class PatchAuditPlugin : BaseUnityPlugin
#elif IL2CPP
    public class PatchAuditPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        private static ConfigEntry<int> FirstReportDelay;
        private static ConfigEntry<int> RepeatInterval;
        private static ConfigEntry<int> TopContendedMethods;
        private static ConfigEntry<bool> ListEveryMethod;

        private static Timer timer;
        private static int reportNumber;
        private static int previousDistinctMethods = -1;
        private static readonly Dictionary<string, int> PreviousExclusive =
            new Dictionary<string, int>(StringComparer.Ordinal);

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif
            FirstReportDelay = Config.Bind("Timing", "FirstReportDelay", 45,
                "Seconds to wait before the first audit. Needs to be long enough for every other mod to finish patching - most patch during Load, but some patch lazily on first scene load.");

            RepeatInterval = Config.Bind("Timing", "RepeatInterval", 180,
                "Seconds between audits. Repeats are how you catch trampoline leaks: if the distinct-method count climbs over a session, something is re-patching without unpatching. Set to 0 for a single report.");

            TopContendedMethods = Config.Bind("Output", "TopContendedMethods", 10,
                "How many of the most-contended methods (patched by the greatest number of mods) to list. These cost one trampoline total, so they are cheap - listed to show where removing a mod will NOT help.");

            ListEveryMethod = Config.Bind("Output", "ListEveryMethod", false,
                "Dump the full signature of every patched method grouped by owner. Very verbose; useful once you know which mod to look at.");

            var period = RepeatInterval.Value > 0
                ? TimeSpan.FromSeconds(RepeatInterval.Value)
                : Timeout.InfiniteTimeSpan;

            timer = new Timer(_ => SafeReport(), null, TimeSpan.FromSeconds(FirstReportDelay.Value), period);

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded! First report in {FirstReportDelay.Value}s" +
                                 (RepeatInterval.Value > 0 ? $", repeating every {RepeatInterval.Value}s." : " (single report)."));
        }

#if MONO
        private void OnDestroy()
        {
            timer?.Dispose();
            timer = null;
        }
#elif IL2CPP
        public override bool Unload()
        {
            timer?.Dispose();
            timer = null;
            return base.Unload();
        }
#endif

        private static void SafeReport()
        {
            try
            {
                Report();
            }
            catch (Exception e)
            {
                // Never let the audit take the game down - that would be a poor showing for a diagnostic.
                PluginLogger.LogError($"Audit failed: {e}");
            }
        }

        private static void Report()
        {
            reportNumber++;

            // method -> set of owner ids that patched it
            var ownersByMethod = new Dictionary<MethodBase, HashSet<string>>();
            // owner id -> methods it touches
            var methodsByOwner = new Dictionary<string, HashSet<MethodBase>>(StringComparer.Ordinal);
            // owner id -> total individual patches (prefixes + postfixes + transpilers + finalizers)
            var patchCountByOwner = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var method in Harmony.GetAllPatchedMethods().ToList())
            {
                if (method == null)
                    continue;

                Patches info;
                try
                {
                    info = Harmony.GetPatchInfo(method);
                }
                catch (Exception e)
                {
                    PluginLogger.LogWarning($"Could not read patch info for {Describe(method)}: {e.Message}");
                    continue;
                }

                if (info == null)
                    continue;

                var owners = new HashSet<string>(info.Owners ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
                if (owners.Count == 0)
                    continue;

                ownersByMethod[method] = owners;

                foreach (var owner in owners)
                {
                    if (!methodsByOwner.TryGetValue(owner, out var set))
                        methodsByOwner[owner] = set = new HashSet<MethodBase>();
                    set.Add(method);
                }

                Tally(patchCountByOwner, info.Prefixes);
                Tally(patchCountByOwner, info.Postfixes);
                Tally(patchCountByOwner, info.Transpilers);
                Tally(patchCountByOwner, info.Finalizers);
            }

            var distinctMethods = ownersByMethod.Count;

            var rows = methodsByOwner
                .Select(kvp => new OwnerRow
                {
                    Owner = kvp.Key,
                    Total = kvp.Value.Count,
                    Exclusive = kvp.Value.Count(m => ownersByMethod[m].Count == 1),
                    Patches = patchCountByOwner.TryGetValue(kvp.Key, out var c) ? c : 0
                })
                .OrderByDescending(r => r.Exclusive)
                .ThenByDescending(r => r.Total)
                .ThenBy(r => r.Owner, StringComparer.Ordinal)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"===== PatchAudit report #{reportNumber} =====");
            sb.AppendLine($"Distinct patched methods (~ one native detour each): {distinctMethods}");
            sb.AppendLine($"Patch owners: {rows.Count}");
            sb.AppendLine();
            sb.AppendLine("  exclusive     shared      total    patches  owner");

            foreach (var row in rows)
            {
                sb.AppendLine(string.Format("  {0,9}  {1,9}  {2,9}  {3,9}  {4}",
                    row.Exclusive, row.Total - row.Exclusive, row.Total, row.Patches, row.Owner));
            }

            sb.AppendLine();
            sb.AppendLine("  exclusive = methods only this owner patches. Removing it frees this many trampolines.");
            sb.AppendLine("  shared    = methods another mod also patches. Removing it frees nothing here.");

            AppendContendedMethods(sb, ownersByMethod);
            AppendDelta(sb, rows, distinctMethods);

            if (ListEveryMethod.Value)
                AppendFullMethodList(sb, methodsByOwner);

            PluginLogger.LogMessage(sb.ToString());

            previousDistinctMethods = distinctMethods;
            PreviousExclusive.Clear();
            foreach (var row in rows)
                PreviousExclusive[row.Owner] = row.Exclusive;
        }

        private static void AppendContendedMethods(StringBuilder sb, Dictionary<MethodBase, HashSet<string>> ownersByMethod)
        {
            var top = TopContendedMethods.Value;
            if (top <= 0)
                return;

            var contended = ownersByMethod
                .Where(kvp => kvp.Value.Count > 1)
                .OrderByDescending(kvp => kvp.Value.Count)
                .Take(top)
                .ToList();

            if (contended.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine($"Most contended methods (top {contended.Count}):");
            foreach (var kvp in contended)
                sb.AppendLine($"  {kvp.Value.Count,3} owners  {Describe(kvp.Key)}");
        }

        private static void AppendDelta(StringBuilder sb, List<OwnerRow> rows, int distinctMethods)
        {
            if (previousDistinctMethods < 0)
                return;

            var growth = distinctMethods - previousDistinctMethods;

            sb.AppendLine();
            sb.AppendLine($"Since last report: {previousDistinctMethods} -> {distinctMethods} distinct methods ({growth:+#;-#;0}).");

            if (growth == 0)
                return;

            foreach (var row in rows)
            {
                if (!PreviousExclusive.TryGetValue(row.Owner, out var before))
                    before = 0;

                var delta = row.Exclusive - before;
                if (delta != 0)
                    sb.AppendLine($"  {before} -> {row.Exclusive} ({delta:+#;-#;0})  {row.Owner}");
            }

            if (growth > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  Trampoline count is still climbing after startup. Something is re-patching");
                sb.AppendLine("  without unpatching - often a scene-load or save-load handler. That is a leak,");
                sb.AppendLine("  and it means your ceiling is a matter of session length, not just mod count.");
            }
        }

        private static void AppendFullMethodList(StringBuilder sb, Dictionary<string, HashSet<MethodBase>> methodsByOwner)
        {
            sb.AppendLine();
            sb.AppendLine("Full method list:");

            foreach (var kvp in methodsByOwner.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.AppendLine();
                sb.AppendLine($"  [{kvp.Key}]");
                foreach (var name in kvp.Value.Select(Describe).OrderBy(n => n, StringComparer.Ordinal))
                    sb.AppendLine($"    {name}");
            }
        }

        private static void Tally(Dictionary<string, int> counts, IEnumerable<Patch> patches)
        {
            if (patches == null)
                return;

            foreach (var patch in patches)
            {
                var owner = patch.owner ?? "<unknown>";
                counts.TryGetValue(owner, out var current);
                counts[owner] = current + 1;
            }
        }

        private static string Describe(MethodBase method)
        {
            try
            {
                var declaring = method.DeclaringType?.FullName ?? "<global>";
                var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                return $"{declaring}::{method.Name}({parameters})";
            }
            catch
            {
                return method.ToString();
            }
        }

        private class OwnerRow
        {
            public string Owner;
            public int Exclusive;
            public int Total;
            public int Patches;
        }
    }
}
