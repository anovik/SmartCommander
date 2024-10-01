using System;

namespace SmartCommander.Plugins;

public static class PluginManager
{
    public static ListerPluginWrapper CreateListerWrapper(string Filename)
    {
        return new ListerPluginWrapper(Filename);
    }

    public static IntPtr CreateListerWindow(this ListerPluginWrapper listerWrapper, IntPtr parentWindowHandle, string fileToLoad)
    {
        int showFlags = 1;
        return listerWrapper.LoadFile(parentWindowHandle, fileToLoad, showFlags);
    }

}

