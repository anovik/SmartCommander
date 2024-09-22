using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCommander.TcPlugins
{
    public static  class PluginManager
    {
        public static ListerPluginWrapper CreateListerWrapper()
        {
            string pluginPath = "C:\\totalcmd\\plugins\\CodeViewer\\CodeViewer.wlx64";
            return new ListerPluginWrapper(pluginPath);
        }

        public static IntPtr CreateListerWindow(this ListerPluginWrapper listerWrapper,IntPtr parentWindowHandle,string fileToLoad)
        {
            int showFlags = 1;
            return listerWrapper.LoadFile(parentWindowHandle, fileToLoad, showFlags);
        }

    }
}
