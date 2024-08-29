﻿using System;
using System.Configuration;
using System.IO;
using System.Windows.Forms;
using ARKBreedingStats.uiControls;
using ARKBreedingStats.utils;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ARKBreedingStats
{
    static class Program
    {
        // P/Invoke declarations for setting DPI awareness
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);

        private const int PROCESS_DPI_UNAWARE = 0;
        private const int PROCESS_SYSTEM_DPI_AWARE = 1;
        private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;

        [STAThread]
        static void Main()
        {
#if !DEBUG
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
#endif

            var args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "cleanupupdater":
                        FileService.TryDeleteFile(Path.Combine(Path.GetTempPath(), Updater.Updater.UpdaterExe));
                        break;
                    case "-setlanguage":
                        var language = args.Length > ++i ? args[i] : null;
                        Properties.Settings.Default.language = language;
                        break;
                }
            }
            // Set DPI Awareness programmatically
            if (Environment.OSVersion.Version >= new Version(6, 2)) // Windows 8 or later
            {
                SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE);
            }
            else
            {
                SetProcessDPIAware(); // For older versions of Windows
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Create and configure the main form
            Form1 mainForm = new Form1
            {
                Font = new Font(Properties.Settings.Default.DefaultFontName, Properties.Settings.Default.DefaultFontSize),
                // Adjust the form's AutoScaleMode for DPI-aware behavior
                AutoScaleMode = AutoScaleMode.Dpi
            };
            
            Application.Run(mainForm);
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            if (e is ConfigurationErrorsException ex)
            {
                if (ex.InnerException is ConfigurationErrorsException configEx)
                {
                    switch (MessageBox.Show("Error while accessing the configuration file.\n\n" +
                            $"Message:\n{e.Message}\n" +
                            $"{configEx.Message}\n" +
                            $"File: {configEx.Filename}\n\n" +
                            "Ark Smart Breeding will stop now.\n" +
                            "Should the file be deleted? This might fix it.\n" +
                            "The library file remains untouched.",
                            $"Error reading configuration file - {Utils.ApplicationNameVersion}", MessageBoxButtons.YesNo, MessageBoxIcon.Error))
                    {
                        case DialogResult.Yes:
                            File.Delete(configEx.Filename);
                            //Properties.Settings.Default.Reload();
                            break;
                    }
                }
                else
                {
                    string folder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            System.Reflection.Assembly.GetExecutingAssembly().EntryPoint.ReflectedType.Namespace);
                    MessageBoxes.ShowMessageBox("Error while accessing the configuration file.\n\n" +
                            $"Message:\n{e.Message}\n\n" +
                            "Ark Smart Breeding will stop now.\n" +
                            $"You can try to delete/rename the folder\n{folder}",
                            "Error reading configuration file");
                }
                Environment.Exit(0);
            }
            else
            {
                if (System.Diagnostics.Debugger.IsAttached) throw e;
                string message = e.Message
                    + "\n\n" + e.GetType() + " in " + e.Source + " (" + Utils.ApplicationNameVersion + ")"
                    + "\n\nMethod throwing the error: " + e.TargetSite.DeclaringType?.FullName + "." + e.TargetSite.Name
                    + "\n\nStackTrace:\n" + e.StackTrace
                    + (e.InnerException != null ? "\n\nInner Exception:\n" + e.InnerException.Message : string.Empty)
                    ;

                CustomMessageBox.Show(message, "Unhandled Exception", "OK", icon: MessageBoxIcon.Error,
                    showCopyToClipboard: true);
            }
        }
    }
}
