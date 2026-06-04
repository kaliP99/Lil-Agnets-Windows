using System;
using System.Windows.Forms;

namespace LilAgentsWindows
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Restore saved theme + accent color before any forms open
            var config = AppConfig.Load();
            ThemeManager.LoadFromConfig(config);

            Application.Run(new TrayApplicationContext());
        }
    }
}