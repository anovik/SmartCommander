using Serilog;
using SmartCommander.Models;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace SmartCommander.Plugins;

// The lister plugins loaded in this app session. UI-thread-only (no locking).
//
// Each configured DLL is loaded lazily, once, on the first F3 that needs it, and kept until exit:
// Total Commander keeps them loaded too, and unloading a DLL whose window procedure may still be
// referenced is the riskier choice. A plugin removed in Options simply stops being offered files.
// A DLL that fails to load is logged once and not retried this session.
[SupportedOSPlatform("windows")]
internal static class ListerPluginRegistry
{
    // Value null = failed to load.
    private static readonly Dictionary<string, ListerPlugin?> _plugins = new(StringComparer.OrdinalIgnoreCase);

    public static bool HasConfiguredPlugins => OptionsModel.Instance.ListerPlugins.Count > 0;

    // Plugins whose detect string matches the file, in the Options list order.
    public static List<ListerPlugin> GetCandidates(string filePath, long size, byte[] header)
    {
        var context = DetectContext.ForFile(filePath, size, header);
        var candidates = new List<ListerPlugin>();
        foreach (string dllPath in OptionsModel.Instance.ListerPlugins)
        {
            ListerPlugin? plugin = GetOrLoad(dllPath);
            if (plugin != null && plugin.Accepts(context))
            {
                candidates.Add(plugin);
            }
        }
        return candidates;
    }

    private static ListerPlugin? GetOrLoad(string dllPath)
    {
        if (_plugins.TryGetValue(dllPath, out ListerPlugin? cached))
        {
            return cached;
        }
        ListerPlugin? plugin = null;
        try
        {
            plugin = ListerPlugin.Load(dllPath);
            Log.Information("Loaded lister plugin {Plugin}", dllPath);
        }
        catch (Exception ex)
        {
            // Third-party DLL: anything can go wrong (missing file, 32-bit, missing exports).
            Log.Warning(ex, "Could not load lister plugin {Plugin}; ignored for this session", dllPath);
        }
        _plugins[dllPath] = plugin;
        return plugin;
    }
}
