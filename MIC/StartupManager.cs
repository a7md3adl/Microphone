using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;

namespace MIC
{
    public static class StartupManager
    {
        private static readonly string AppName = "MicControllerApp";

        public static void SetStartup(bool enable)
        {
            // Target the current user's registry run key (no Administrator privileges required)
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (enable)
                {
                    // Add the full path to the currently running executable
                    key.SetValue(AppName, $"\"{Application.ExecutablePath}\" --no-startup-window");
                }
                else
                {
                    // Remove the key if disabled
                    key.DeleteValue(AppName, false);
                }
            }
        }

        public static bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                object value = key?.GetValue(AppName);
                return value != null;
            }
        }
    }
}
