using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
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

        // Persistent audio objects for live event monitoring
        private MMDeviceEnumerator deviceEnumerator;
        private MMDevice defaultMic;

        public TrayMicControllerForm()
        {
            StartupManager.SetStartup(true);

            // Hide the main form window and keep it out of the taskbar
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            // Load or generate custom icons
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

            // Setup real-time Windows audio tracking and sync initial state
            InitializeAudioListener();
        }

        private void InitializeAudioListener()
        {
            try
            {
                deviceEnumerator = new MMDeviceEnumerator();
                defaultMic = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);

                // Sync initial state
                isMuted = defaultMic.AudioEndpointVolume.Mute;
                UpdateTrayUI();

                // Subscribe to NAudio's built-in volume/mute change event
                defaultMic.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotificationReceived;
            }
            catch
            {
                trayIcon.Text = "Microphone Controller (Error)";
                trayIcon.Icon = activeIcon;
            }
        }

        // Triggered automatically by Windows whenever mic volume or mute changes externally
        private void OnVolumeNotificationReceived(AudioVolumeNotificationData data)
        {
            // Ensure UI updates run safely back on the main UI thread
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<AudioVolumeNotificationData>(OnVolumeNotificationReceived), data);
                return;
            }

            isMuted = data.Muted;
            UpdateTrayUI();
        }

        private void LoadIcons()
        {
            try
            {
                if (!File.Exists("mic_active.ico") && File.Exists("mic_active.png"))
                {
                    IconConverterUtility.ConvertPngToMultiResIco("mic_active.png", "mic_active.ico");
                }

                if (!File.Exists("mic_muted.ico") && File.Exists("mic_muted.png"))
                {
                    IconConverterUtility.ConvertPngToMultiResIco("mic_muted.png", "mic_muted.ico");
                }

                if (File.Exists("mic_active.ico") && File.Exists("mic_muted.ico"))
                {
                    activeIcon = new Icon("mic_active.ico");
                    mutedIcon = new Icon("mic_muted.ico");
                }
                else
                {
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
                if (defaultMic != null)
                {
                    defaultMic.AudioEndpointVolume.Mute = !defaultMic.AudioEndpointVolume.Mute;
                }
                else
                {
                    using (var enumerator = new MMDeviceEnumerator())
                    {
                        using (var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
                        {
                            mic.AudioEndpointVolume.Mute = !mic.AudioEndpointVolume.Mute;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not toggle microphone: {ex.Message}", "Audio Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                try
                {
                    if (defaultMic != null)
                    {
                        defaultMic.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotificationReceived;
                        defaultMic.Dispose();
                    }
                    deviceEnumerator?.Dispose();
                }
                catch { }

                trayIcon?.Dispose();
                trayMenu?.Dispose();
                activeIcon?.Dispose();
                mutedIcon?.Dispose();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }

        // Helper class for registry startup management
        public static class StartupManager
        {
            private static readonly string AppName = "MicControllerApp";

            public static void SetStartup(bool enable)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (enable)
                        {
                            key.SetValue(AppName, $"\"{Application.ExecutablePath}\"");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
                catch { }
            }
        }

        // Multi-resolution high-quality icon converter utility
        public static class IconConverterUtility
        {
            private static readonly int[] IconSizes = { 16, 24, 32, 48, 64, 128, 256 };

            public static void ConvertPngToMultiResIco(string pngPath, string icoPath)
            {
                using (var sourceBitmap = new Bitmap(pngPath))
                {
                    using (var fs = new FileStream(icoPath, FileMode.Create))
                    {
                        using (var writer = new BinaryWriter(fs))
                        {
                            int imageCount = IconSizes.Length;

                            writer.Write((short)0);
                            writer.Write((short)1);
                            writer.Write((short)imageCount);

                            int currentOffset = 6 + (16 * imageCount);
                            var pngDataList = new byte[imageCount][];

                            for (int i = 0; i < imageCount; i++)
                            {
                                int size = IconSizes[i];
                                using (var resizedBmp = ResizeImage(sourceBitmap, size, size))
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        resizedBmp.Save(ms, ImageFormat.Png);
                                        pngDataList[i] = ms.ToArray();
                                    }
                                }
                            }

                            for (int i = 0; i < imageCount; i++)
                            {
                                int size = IconSizes[i];
                                byte[] pngData = pngDataList[i];

                                writer.Write((byte)(size == 256 ? 0 : size));
                                writer.Write((byte)(size == 256 ? 0 : size));
                                writer.Write((byte)0);
                                writer.Write((byte)0);
                                writer.Write((short)1);
                                writer.Write((short)32);
                                writer.Write((int)pngData.Length);
                                writer.Write((int)currentOffset);

                                currentOffset += pngData.Length;
                            }

                            for (int i = 0; i < imageCount; i++)
                            {
                                writer.Write(pngDataList[i]);
                            }
                        }
                    }
                }
            }

            private static Bitmap ResizeImage(Image image, int width, int height)
            {
                var destRect = new Rectangle(0, 0, width, height);
                var destImage = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using (var wrapMode = new ImageAttributes())
                    {
                        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                    }
                }

                return destImage;
            }
        }
    }
}

//using NAudio.CoreAudioApi;

//namespace MIC
//{
//    public partial class TrayMicControllerForm : Form
//    {
//        private NotifyIcon trayIcon;
//        private ContextMenuStrip trayMenu;
//        private bool isMuted = false;

//        // Store references to your two icons
//        private Icon activeIcon;
//        private Icon mutedIcon;

//        public TrayMicControllerForm()
//        {
//            StartupManager.SetStartup(true);
//            // Hide the main form window and keep it out of the taskbar
//            this.WindowState = FormWindowState.Minimized;
//            this.ShowInTaskbar = false;

//            // Load your custom icons (ensure these files exist in your output directory, or load from embedded resources)
//            LoadIcons();

//            // Initialize right-click context menu
//            trayMenu = new ContextMenuStrip();
//            trayMenu.Items.Add("Toggle Mute", null, (s, e) => ToggleMicrophoneMute());
//            trayMenu.Items.Add("Exit", null, (s, e) => { trayIcon.Visible = false; Application.Exit(); });

//            // Initialize system tray icon
//            trayIcon = new NotifyIcon();
//            trayIcon.ContextMenuStrip = trayMenu;
//            trayIcon.Visible = true;

//            // Toggle microphone on left-click of the tray icon
//            trayIcon.MouseClick += (s, e) =>
//            {
//                if (e.Button == MouseButtons.Left)
//                {
//                    ToggleMicrophoneMute();
//                }
//            };

//            // Sync initial state and set correct icon on startup
//            RefreshMicrophoneState();
//        }

//        private void LoadIcons()
//        {
//            HighQualityIconConverter.ConvertPngToMultiResIco("mic_active.png", "mic_active.ico");
//            HighQualityIconConverter.ConvertPngToMultiResIco("mic_muted.png", "mic_muted.ico");
//            try
//            {
//                // Option A: Load from physical files in your app folder
//                if (File.Exists("mic_active.ico") && File.Exists("mic_muted.ico"))
//                {
//                    activeIcon = new Icon("mic_active.ico");
//                    mutedIcon = new Icon("mic_muted.ico");
//                }
//                else
//                {
//                    // Fallback to system icons if custom .ico files aren't found yet
//                    activeIcon = SystemIcons.Application;
//                    mutedIcon = SystemIcons.Error;
//                }
//            }
//            catch
//            {
//                activeIcon = SystemIcons.Application;
//                mutedIcon = SystemIcons.Error;
//            }
//        }

//        private void ToggleMicrophoneMute()
//        {
//            try
//            {
//                using (var enumerator = new MMDeviceEnumerator())
//                {
//                    using (var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
//                    {
//                        bool currentMute = mic.AudioEndpointVolume.Mute;
//                        mic.AudioEndpointVolume.Mute = !currentMute;
//                        isMuted = !currentMute;

//                        UpdateTrayUI();
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"Could not toggle microphone: {ex.Message}", "Audio Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
//            }
//        }

//        private void RefreshMicrophoneState()
//        {
//            try
//            {
//                using (var enumerator = new MMDeviceEnumerator())
//                {
//                    using (var mic = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
//                    {
//                        isMuted = mic.AudioEndpointVolume.Mute;
//                        UpdateTrayUI();
//                    }
//                }
//            }
//            catch
//            {
//                trayIcon.Text = "Microphone Controller (Error)";
//                trayIcon.Icon = activeIcon;
//            }
//        }

//        private void UpdateTrayUI()
//        {
//            if (isMuted)
//            {
//                trayIcon.Icon = mutedIcon;
//                trayIcon.Text = "Microphone: MUTED";
//            }
//            else
//            {
//                trayIcon.Icon = activeIcon;
//                trayIcon.Text = "Microphone: ACTIVE";
//            }
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (disposing)
//            {
//                trayIcon?.Dispose();
//                trayMenu?.Dispose();
//                activeIcon?.Dispose();
//                mutedIcon?.Dispose();
//                if (components != null) components.Dispose();
//            }
//            base.Dispose(disposing);
//        }




//    }
//}
