using NAudio.CoreAudioApi;

namespace MIC
{
    public partial class TrayMicControllerForm : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private bool isMuted = false;

        // Store references to your two icons
        private Icon activeIcon;
        private Icon mutedIcon;

        public TrayMicControllerForm()
        {
            StartupManager.SetStartup(true);
            // Hide the main form window and keep it out of the taskbar
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            // Load your custom icons (ensure these files exist in your output directory, or load from embedded resources)
            LoadIcons();

            // Initialize right-click context menu
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Toggle Mute", null, (s, e) => ToggleMicrophoneMute());
            trayMenu.Items.Add("Exit", null, (s, e) => { trayIcon.Visible = false; Application.Exit(); });

            // Initialize system tray icon
            trayIcon = new NotifyIcon();
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;

            // Toggle microphone on left-click of the tray icon
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ToggleMicrophoneMute();
                }
            };

            // Sync initial state and set correct icon on startup
            RefreshMicrophoneState();
        }

        private void LoadIcons()
        {
            HighQualityIconConverter.ConvertPngToMultiResIco("mic_active.png", "mic_active.ico");
            HighQualityIconConverter.ConvertPngToMultiResIco("mic_muted.png", "mic_muted.ico");
            try
            {
                // Option A: Load from physical files in your app folder
                if (File.Exists("mic_active.ico") && File.Exists("mic_muted.ico"))
                {
                    activeIcon = new Icon("mic_active.ico");
                    mutedIcon = new Icon("mic_muted.ico");
                }
                else
                {
                    // Fallback to system icons if custom .ico files aren't found yet
                    activeIcon = SystemIcons.Application;
                    mutedIcon = SystemIcons.Error;
                }
            }
            catch
            {
                activeIcon = SystemIcons.Application;
                mutedIcon = SystemIcons.Error;
            }
        }

        private void ToggleMicrophoneMute()
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    using (var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
                    {
                        bool currentMute = mic.AudioEndpointVolume.Mute;
                        mic.AudioEndpointVolume.Mute = !currentMute;
                        isMuted = !currentMute;

                        UpdateTrayUI();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not toggle microphone: {ex.Message}", "Audio Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshMicrophoneState()
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    using (var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
                    {
                        isMuted = mic.AudioEndpointVolume.Mute;
                        UpdateTrayUI();
                    }
                }
            }
            catch
            {
                trayIcon.Text = "Microphone Controller (Error)";
                trayIcon.Icon = activeIcon;
            }
        }

        private void UpdateTrayUI()
        {
            if (isMuted)
            {
                trayIcon.Icon = mutedIcon;
                trayIcon.Text = "Microphone: MUTED";
            }
            else
            {
                trayIcon.Icon = activeIcon;
                trayIcon.Text = "Microphone: ACTIVE";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trayIcon?.Dispose();
                trayMenu?.Dispose();
                activeIcon?.Dispose();
                mutedIcon?.Dispose();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }


        public static class IconConverterUtility
        {
            public static void ConvertPngToIco(string pngPath, string icoPath)
            {
                // 1. Load the PNG image
                using (Bitmap bitmap = new Bitmap(pngPath))
                {
                    // 2. Resize or ensure standard icon dimensions if necessary (e.g., 256x256)
                    using (Bitmap iconBitmap = new Bitmap(bitmap, new Size(256, 256)))
                    {
                        // 3. Save as .ico using icon handle
                        IntPtr hIcon = iconBitmap.GetHicon();
                        using (Icon icon = Icon.FromHandle(hIcon))
                        {
                            using (FileStream fs = new FileStream(icoPath, FileMode.Create))
                            {
                                icon.Save(fs);
                            }
                        }
                    }
                }
            }
        }
        //private NotifyIcon trayIcon;
        //private ContextMenuStrip trayMenu;
        //private bool isMuted = false;
        //public TrayMicControllerForm()
        //{
        //    InitializeComponent();
        //    this.WindowState = FormWindowState.Minimized;
        //    this.ShowInTaskbar = false;

        //    // Initialize right-click context menu
        //    trayMenu = new ContextMenuStrip();
        //    trayMenu.Items.Add("Toggle Mute", null, (s, e) => ToggleMicrophoneMute());
        //    trayMenu.Items.Add("Exit", null, (s, e) => { trayIcon.Visible = false; Application.Exit(); });

        //    // Initialize system tray icon
        //    trayIcon = new NotifyIcon();
        //    trayIcon.Text = "Microphone Controller";
        //    trayIcon.Icon = SystemIcons.Application; // Replace with a custom icon if desired
        //    trayIcon.ContextMenuStrip = trayMenu;
        //    trayIcon.Visible = true;

        //    // Toggle microphone on left-click of the tray icon
        //    trayIcon.MouseClick += (s, e) =>
        //    {
        //        if (e.Button == MouseButtons.Left)
        //        {
        //            ToggleMicrophoneMute();
        //        }
        //    };

        //    // Sync initial state on startup
        //    RefreshMicrophoneState();
        //}
        //private void ToggleMicrophoneMute()
        //{
        //    try
        //    {
        //        using (var enumerator = new MMDeviceEnumerator())
        //        {
        //            // Access the default microphone (Capture device)
        //            using (var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
        //            {
        //                bool currentMute = mic.AudioEndpointVolume.Mute;
        //                mic.AudioEndpointVolume.Mute = !currentMute;
        //                isMuted = !currentMute;

        //                // Update tooltip text to show current status
        //                trayIcon.Text = isMuted ? "Microphone: MUTED" : "Microphone: ACTIVE";
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Could not toggle microphone: {ex.Message}", "Audio Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}

        //private void RefreshMicrophoneState()
        //{
        //    try
        //    {
        //        using (var enumerator = new MMDeviceEnumerator())
        //        {
        //            using (var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
        //            {
        //                isMuted = mic.AudioEndpointVolume.Mute;
        //                trayIcon.Text = isMuted ? "Microphone: MUTED" : "Microphone: ACTIVE";
        //            }
        //        }
        //    }
        //    catch
        //    {
        //        trayIcon.Text = "Microphone Controller";
        //    }
        //}

        //protected override void Dispose(bool disposing)
        //{
        //    if (disposing && (components != null))
        //    {
        //        trayIcon?.Dispose();
        //        trayMenu?.Dispose(); 
        //        components.Dispose();
        //    }
        //    base.Dispose(disposing);
        //    //if (disposing)
        //    //{
        //    //    trayIcon?.Dispose();
        //    //    trayMenu?.Dispose();
        //    //}
        //    //base.Dispose(disposing);
        //}
    }
}
