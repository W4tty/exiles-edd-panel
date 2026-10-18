using System;
using System.IO;
using System.Linq;
using System.Reflection;
using static EDDDLLInterfaces.EDDDLLIF;

namespace ExilesMonitorPanel
{
    // EDDiscovery scans its DLL folder, loads every .NET assembly it finds and looks for one class whose
    // name ends in "EDDClass". It creates one instance and calls EDDInitialise on it; that is where the
    // panel is registered. Nothing else is needed from the DLL side: EDD creates the panel control itself
    // when the user opens the tab.
    public class ExilesMonitorEDDClass
    {
        public const string PanelId = "ExilesMonitor-Panel-1";   // stable id: EDD maps it to a panel number kept in its database
        public const string DllVersion = "1.0.0.0";

        public static EDDCallBacks Callbacks;
        public static bool HasCallbacks;
        public static string HostVersion = "";

        static ExilesMonitorEDDClass()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveHostAssembly;
        }

        // EDDiscovery ships Microsoft.Web.WebView2.Core and .WinForms (strong named). This DLL is compiled
        // against the build EDDiscovery 19.1.11 carries. If a later EDDiscovery ships a newer build, the
        // exact-version bind fails first; this hands back whatever build the host has, by simple name.
        private static Assembly ResolveHostAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                if (!name.StartsWith("Microsoft.Web.WebView2", StringComparison.OrdinalIgnoreCase))
                    return null;
                Assembly loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
                if (loaded != null)
                    return loaded;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            }
            catch
            {
                return null;
            }
        }

        // vstr: "<EDD version>;HOSTNAME=EDDiscovery;JOURNALVERSION=7;CALLBACKVERSION=4;CALLVERSION=7;PANELCALLBACKVERSION=1"
        // Return "!message" to refuse, or our version number.
        public string EDDInitialise(string vstr, string dllfolder, EDDCallBacks cb)
        {
            Callbacks = cb;
            HasCallbacks = true;
            HostVersion = (vstr ?? "").Split(';').FirstOrDefault() ?? "";

            int panelVersion = Flag(vstr, FLAG_PANELCALLBACKVERSION);
            if (cb.ver < 3 || cb.AddPanel == null || panelVersion < 1)
                return "!Exiles Monitor needs an EDDiscovery with DLL panel support (16.1 or later)";

            try
            {
                cb.AddPanel(PanelId, typeof(MonitorPanel), "Exiles Monitor", "ExilesMonitor",
                            "Exiles squad monitor (reports.theexiles.gg), follows the system you are in", PanelIcon.Create());
            }
            catch (Exception ex)
            {
                return "!Exiles Monitor panel could not register: " + ex.Message;
            }

            System.Diagnostics.Trace.WriteLine("Exiles Monitor panel " + DllVersion + " registered from " + dllfolder + " (host " + HostVersion + ")");
            return DllVersion;
        }

        public void EDDTerminate()
        {
        }

        // Writes to the EDD log panel when the host offers it; always to the trace file.
        public static void Log(string text)
        {
            System.Diagnostics.Trace.WriteLine(text);
            try
            {
                if (HasCallbacks && Callbacks.ver >= 3 && Callbacks.WriteToLog != null)
                    Callbacks.WriteToLog(text);
            }
            catch
            {
            }
        }

        private static int Flag(string vstr, string flag)
        {
            foreach (string part in (vstr ?? "").Split(';'))
            {
                if (part.StartsWith(flag, StringComparison.OrdinalIgnoreCase) && int.TryParse(part.Substring(flag.Length), out int v))
                    return v;
            }
            return 0;
        }
    }
}
