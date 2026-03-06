using System.ComponentModel;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using vibrance.GUI.common;
using vibrance.GUI.NVIDIA;

namespace vibrance.GUI
{
    static class Program
    {
        private const string ErrorGraphicsAdapterUnknown = "Failed to determine your Graphic GraphicsAdapter type. Make sure you have installed a proper NVIDIA driver. Intel laptops are not supported as stated on the website. When installing your GPU driver did not work, please contact @juvlarN at twitter. Press Yes to open twitter in your browser now. Error: ";
        private const string MessageBoxCaption = "vibranceGUI Error";

        [STAThread]
        static void Main(string[] args)
        {
            File.WriteAllText("debug_startup.log", "1. Start Main.\n");
            try 
            {
                RunApp(args);
                File.AppendAllText("debug_startup.log", "99. End Main normally.\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText("debug_startup.log", "CRITICAL ERROR: " + ex.ToString() + "\n");
            }
        }

        static void RunApp(string[] args)
        {
            File.AppendAllText("debug_startup.log", "2. RunApp started.\n");
            bool result = false;
            Mutex mutex = new Mutex(true, "vibranceGUI~Mutex", out result);
            if (!result)
            {
                MessageBox.Show("You can run vibranceGUI only once at a time!", MessageBoxCaption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }


            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            NativeMethods.SetDllDirectory(CommonUtils.GetVibrance_GUI_AppDataPath());

            GraphicsAdapter adapter = GraphicsAdapterHelper.GetAdapter();
            Form vibranceGui = null;

            if (adapter == GraphicsAdapter.Nvidia)
            {
                const string nvidiaAdapterName = "vibranceDLL.dll";
                string resourceName = $"{typeof(Program).Namespace}.NVIDIA.{nvidiaAdapterName}";
                CommonUtils.LoadUnmanagedLibraryFromResource(
                    Assembly.GetExecutingAssembly(),
                    resourceName,
                    nvidiaAdapterName);
                Marshal.PrelinkAll(typeof(NvidiaDynamicVibranceProxy));

                vibranceGui = new VibranceGUI(
                    (x, y) => new NvidiaDynamicVibranceProxy(x, y),
                    NvidiaDynamicVibranceProxy.NvapiDefaultLevel,
                    NvidiaDynamicVibranceProxy.NvapiDefaultLevel,
                    NvidiaDynamicVibranceProxy.NvapiMaxLevel,
                    NvidiaDynamicVibranceProxy.NvapiDefaultLevel,
                    x => NvidiaVibranceValueWrapper.Find(x).Percentage);
            }
            else if (adapter == GraphicsAdapter.Unknown)
            {
                string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                if (MessageBox.Show(ErrorGraphicsAdapterUnknown + errorMessage,
                    MessageBoxCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start("https://twitter.com/juvlarN");
                }
                return;
            }
            if (args.Contains("-minimized"))
            {
                File.AppendAllText("debug_startup.log", "8. Minimizing.\n");
                vibranceGui.WindowState = FormWindowState.Minimized;
                ((VibranceGUI)vibranceGui).SetAllowVisible(false);
            }
            vibranceGui.Text += String.Format(" ({0}, {1})", adapter.ToString().ToUpper(), Application.ProductVersion);

            File.AppendAllText("debug_startup.log", "9. Calling Application.Run\n");
            Application.Run(vibranceGui);
            GC.KeepAlive(mutex);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);
    }

    internal static class CommonUtils
    {
        public static string GetVibrance_GUI_AppDataPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vibranceGUI");
        }

        public static void LoadUnmanagedLibraryFromResource(Assembly assembly, string resourceName, string dllName)
        {
            string appDataPath = GetVibrance_GUI_AppDataPath();
            Directory.CreateDirectory(appDataPath);
            string dllPath = Path.Combine(appDataPath, dllName);

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return;
                using (FileStream fileStream = new FileStream(dllPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }
    }
}
