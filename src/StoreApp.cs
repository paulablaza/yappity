using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Yappity
{
    // True when Yappity runs from its Microsoft Store (MSIX) package. Windows installs, updates,
    // uninstalls and starts the Store copy, so Yappity's own install and startup code stays out of the way.
    internal static class StoreApp
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);

        const int NoPackage = 15700; // APPMODEL_ERROR_NO_PACKAGE
        static int state = -1;

        public static bool IsPackaged
        {
            get
            {
                if (state < 0)
                {
                    try
                    {
                        int length = 0;
                        state = GetCurrentPackageFullName(ref length, null) == NoPackage ? 0 : 1;
                    }
                    catch (Exception) { state = 0; }
                }
                return state == 1;
            }
        }

        // The Store copy starts with Windows through the package's startup task, which people
        // turn on and off in Windows Settings rather than in Yappity.
        public static void OpenStartupSettings()
        {
            System.Diagnostics.Process.Start("ms-settings:startupapps");
        }
    }
}
