﻿using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Animators;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic.FileIO;
using System.Collections.Generic;
using SkiaSharp;
using MetadataExtractor;

namespace Dynaframe3
{
    public class MainWindow : Window
    {
        Image frontImage;
        Image backImage;
        TextBlock tb;
        Bitmap bitmapNew;
        Window mainWindow;
        Panel mainPanel;
        Process videoProcess; // handle to the video Player

        // Engines
        PlayListEngine playListEngine;

        // Transitions used for animating the fades
        DoubleTransition fadeTransition;

        SimpleHTTPServer server;

        /// <summary>
        /// This controls the time between 'slides'
        /// </summary>
        // Timer which controls 'slides'
        // set to a low number to force a quick 'first slide' to appear
        System.Timers.Timer slideTimer = new System.Timers.Timer(500);

        // Track state of the engine
        DateTime lastUpdated = DateTime.Now;
        DateTime timeStarted = DateTime.Now;
        bool IsPaused = false;


        Transform rotationTransform;

        public MainWindow()
        {
            playListEngine = new PlayListEngine();
            InitializeComponent();
            SetupWebServer();
        }

        private void InitializeComponent()
        {
            CommandProcessor.GetMainWindowHandle(this);

            AvaloniaXamlLoader.Load(this);
            this.KeyDown += MainWindow_KeyDown;
            this.Closed += MainWindow_Closed;

            // setup transitions and animations
            DoubleTransition windowTransition = new DoubleTransition();
            windowTransition.Duration = TimeSpan.FromMilliseconds(2000);
            windowTransition.Property = Window.OpacityProperty;

            DoubleTransition panelTransition = new DoubleTransition();
            panelTransition.Duration = TimeSpan.FromMilliseconds(1200);
            panelTransition.Property = Panel.OpacityProperty;


            fadeTransition = new DoubleTransition();
            fadeTransition.Easing = new QuadraticEaseIn();
            fadeTransition.Duration = TimeSpan.FromMilliseconds(AppSettings.Default.FadeTransitionTime);
            fadeTransition.Property = Image.OpacityProperty;

            mainWindow = this.FindControl<Window>("mainWindow");
            mainWindow.Transitions = new Transitions();
            mainWindow.Transitions.Add(windowTransition);

            mainWindow.SystemDecorations = SystemDecorations.None;
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            mainPanel = this.FindControl<Panel>("mainPanel");
            mainPanel.Transitions = new Transitions();
            mainPanel.Transitions.Add(panelTransition);
            if (AppSettings.Default.Rotation != 0)
            {
                RotateMainPanel();
            }

            tb = this.FindControl<TextBlock>("tb");
            tb.Foreground = Brushes.AliceBlue;
            tb.Text = "Loading images...";
            tb.FontFamily = new FontFamily("Terminal");
            tb.FontWeight = FontWeight.Bold;
            tb.FontSize = AppSettings.Default.InfoBarFontSize;
            tb.Transitions = new Transitions();
            tb.Transitions.Add(fadeTransition);
            tb.Padding = new Thickness(30);

            frontImage = this.FindControl<Image>("Front");
            backImage = this.FindControl<Image>("Back");

            string intro;
            if ((AppSettings.Default.Rotation == 0) || AppSettings.Default.Rotation == 180)
            {
                intro = Environment.CurrentDirectory + "/images/background.jpg";
            }
            else
            {
                intro = Environment.CurrentDirectory + "/images/vertbackground.jpg";
            }
             
            bitmapNew = new Bitmap(intro);

            frontImage.Source = bitmapNew;

            frontImage.Stretch = AppSettings.Default.ImageStretch;
            backImage.Stretch = AppSettings.Default.ImageStretch;

            DoubleTransition transition2 = new DoubleTransition();
            transition2.Easing = new QuadraticEaseIn();
            transition2.Duration = TimeSpan.FromMilliseconds(1600);
            transition2.Property = Image.OpacityProperty;

            frontImage.Transitions = new Transitions();
            frontImage.Transitions.Add(fadeTransition);
            backImage.Transitions = new Transitions();
            backImage.Transitions.Add(transition2);

            slideTimer.Elapsed += Timer_Tick;

            AppSettings.Default.ReloadSettings = true; 
            slideTimer.Start();
            Timer_Tick(null, null);
            Logger.LogComment("Initialized");
            

        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            ((ClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current.ApplicationLifetime).Shutdown(0);
        }


        public void GoToNextImage()
        {
            tb.Transitions.Clear();
            playListEngine.GoToNext();
            PlayImageFile(true);
            lastUpdated = DateTime.Now;
            tb.Transitions.Add(fadeTransition);
        }
        public void GoToPreviousImage()
        {
            tb.Transitions.Clear();
            playListEngine.GoToPrevious();
            PlayImageFile(true);
            lastUpdated = DateTime.Now;
            tb.Transitions.Add(fadeTransition);
        }
        public void GoToFirstImage()
        {
            tb.Transitions.Clear();
            GetFiles();
            PlayImageFile(true);
            lastUpdated = DateTime.Now;
            tb.Transitions.Add(fadeTransition);
        }
        public void Pause()
        {
            if (IsPaused)
            {
                IsPaused = false;
            }
            else
            {
                IsPaused = true;
            }
        }

        private void MainWindow_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
        {
           
            // Exit on Escape or Control X (Windows and Linux Friendly)
            if ((e.Key == Avalonia.Input.Key.Escape) || ((e.KeyModifiers == Avalonia.Input.KeyModifiers.Control) && (e.Key == Avalonia.Input.Key.X)))
            {
                slideTimer.Stop();
                this.Close();
                server.Stop();
            }

            if (e.Key == Avalonia.Input.Key.T)
            {
                if (mainWindow.Opacity != 1)
                {
                    mainWindow.Opacity = 1;
                }
                else
                {
                    mainWindow.Opacity = 0;
                }
            }
            if (e.Key == Avalonia.Input.Key.U)
            {
                if (mainPanel.Opacity != 1)
                {
                    mainPanel.Opacity = 1;
                }
                else
                {
                    mainPanel.Opacity = 0;
                }
            }


            if (e.Key == Avalonia.Input.Key.F)
            {
                AppSettings.Default.InfoBarState = AppSettings.InfoBar.FileInfo;
            }
            if (e.Key == Avalonia.Input.Key.I)
            {
                AppSettings.Default.InfoBarState = AppSettings.InfoBar.IP;
            }
            if (e.Key == Avalonia.Input.Key.C)
            {
                AppSettings.Default.InfoBarState = AppSettings.InfoBar.DateTime;
            }
            if (e.Key == Avalonia.Input.Key.H)
            {
                tb.Opacity = 0;
                AppSettings.Default.InfoBarState = AppSettings.InfoBar.OFF;
            }

            if (e.Key == Avalonia.Input.Key.Right)
            {
                GoToNextImage();
            }
            if (e.Key == Avalonia.Input.Key.Left)
            {
                GoToPreviousImage();
            }

            UpdateInfoBar();
            
        }

        /// <summary>
        /// The main processing loop. Will have to break this down at some point.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (IsPaused)
            {
                UpdateInfoBar();
                // Note: Do not stop the timer...we need it to 'recheck'
                return;
            }

            if (AppSettings.Default.ReloadSettings)
            {
                slideTimer.Stop();
                Logger.LogComment("Reload settings was true");
                AppSettings.Default.ReloadSettings = false;
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshSettings();
                });
                slideTimer.Start();
            }

            // Check to see if the directories flag was modified
            if (AppSettings.Default.RefreshDirctories)
            {
                slideTimer.Stop();
                Logger.LogComment("Refresh Directories was true");
                AppSettings.Default.RefreshDirctories = false;
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GetFiles();
                });
                lastUpdated = lastUpdated.Subtract(TimeSpan.FromMilliseconds(AppSettings.Default.SlideshowTransitionTime));
                slideTimer.Start();
            }
            
            UpdateInfoBar();


            if ((DateTime.Now.Subtract(lastUpdated).TotalMilliseconds > AppSettings.Default.SlideshowTransitionTime))
            {
                lastUpdated = DateTime.Now;
                playListEngine.GoToNext();
                Logger.LogComment("Next file is: " + playListEngine.CurrentPlayListItem.Path);
                try
                {
                    // TODO: Try to 'peek' at next file, if video, then slow down more
                    if (playListEngine.CurrentPlayListItem.ItemType == PlayListItemType.Video)
                    {
                        KillVideoPlayer();
                        PlayVideoFile();
                    }
                    else
                    {
                        PlayImageFile(false);
                        KillVideoPlayer(); // if a video is playing, get rid of it now that we've swapped images
                    }
                }
                catch (InvalidOperationException)
                { 
                    // We expect this if a process is no longer around
                }
                catch (Exception exc)
                {
                    Debug.WriteLine("ERROR: Exception processing file.." + exc.ToString());
                    Logger.LogComment("ERROR: Exception processing file.." + exc.ToString());
                }
            }
        }

        private void UpdateInfoBar()
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            { 
                tb.FontFamily = AppSettings.Default.DateTimeFontFamily;
                if (DateTime.Now.Subtract(timeStarted).TotalSeconds < AppSettings.Default.NumberOfSecondsToShowIP)
                {
                    tb.Text = Helpers.GetIPString();
                    tb.Opacity = 1;
                }
                else
                {
                    switch (AppSettings.Default.InfoBarState)
                    {
                        case (AppSettings.InfoBar.DateTime):
                            {
                                tb.Opacity = 1;
                                tb.Text = DateTime.Now.ToString(AppSettings.Default.DateTimeFormat);
                                break;
                            }
                        case (AppSettings.InfoBar.FileInfo):
                            {
                                tb.Opacity = 1;
                                FileInfo f = new FileInfo(playListEngine.CurrentPlayListItem.Path);
                                string fData = f.Name;
                                // IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(f.FullName);

                                tb.Text = f.Name;
                                break;
                            }
                        case (AppSettings.InfoBar.IP):
                            {
                                tb.Opacity = 1;
                                tb.Text = Helpers.GetIPString();
                                break;
                            }
                        case (AppSettings.InfoBar.OFF):
                            {
                                tb.Opacity = 0;
                                break;
                            }
                        default:
                            {
                                tb.Text = "";
                                break;
                            }
                    } // end switch

                    if ((IsPaused) && (AppSettings.Default.InfoBarState != AppSettings.InfoBar.OFF))
                    {
                        tb.Text += " (Paused)";
                    }

                } // end if
            });
        }
        private void PlayImageFile(bool fast)
        {
            Logger.LogComment("PlayImageFile() called");
           
           // Step 1: Set the background image to the new one
           // fade the top out, revealing the bottom
           Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                mainWindow.Opacity = 1;
                try
                {
                    Logger.LogComment("Beginning to load next file: " + playListEngine.CurrentPlayListItem.Path);
                    
                    bitmapNew = new Bitmap(playListEngine.CurrentPlayListItem.Path);

                    backImage.Source = bitmapNew;
                    backImage.Opacity = 1;
                    frontImage.Opacity = 0;
                    mainWindow.WindowState = WindowState.FullScreen;
                }
                catch (Exception exc)
                {
                    Logger.LogComment("ERROR: Exception: " + exc.ToString());
                }
            });

            // We sleep on this thread to let the transition occur fully
            if (!fast)
            {
                Thread.Sleep(AppSettings.Default.FadeTransitionTime);
            }


            // At this point the 'bottom' image is opaque showing the new image
            // We set the top to that image, and fade it in
            // we temporarily clear the transiton out so it's instant
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    frontImage.Transitions.Clear();
                    frontImage.Source = backImage.Source;
                    frontImage.Opacity = 1;
                    frontImage.Transitions.Add(fadeTransition);
                }
                catch (Exception exc)
                {
                    tb.Text = "Error: " + exc.Message;
                }
                backImage.Opacity = 0;
            });
        }
        private void PlayVideoFile()
        {
            Logger.LogComment("Entering PlayVideoFile");
            ProcessStartInfo pInfo = new ProcessStartInfo();
            pInfo.WindowStyle = ProcessWindowStyle.Maximized;

            // TODO: Parameterize omxplayer settings
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Logger.LogComment("Linux Detected, setting up OMX Player");
                pInfo.FileName = "omxplayer";
                Logger.LogComment("Setting up Appsettings...");
                pInfo.Arguments = AppSettings.Default.OXMOrientnation + " --aspect-mode " + AppSettings.Default.VideoStretch;

                // Append volume command argument
                if (!AppSettings.Default.VideoVolume)
                {
                    pInfo.Arguments += " --vol -6000 ";
                }

                pInfo.Arguments += "\"" + playListEngine.CurrentPlayListItem.Path + "\""; 
                Logger.LogComment("DF Playing: " + playListEngine.CurrentPlayListItem.Path);
                Logger.LogComment("OMXPLayer args: " + pInfo.Arguments);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pInfo.UseShellExecute = true;
                pInfo.FileName = "wmplayer.exe";
                pInfo.Arguments = "\"" + playListEngine.CurrentPlayListItem.Path + "\"";
                pInfo.Arguments += " /fullscreen";
                Console.WriteLine("Looking for media in: " + pInfo.Arguments);
            }


            videoProcess = new Process();
            videoProcess.StartInfo = pInfo;
            Logger.LogComment("Starting player...");
            videoProcess.Start();
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                mainPanel.Opacity = 0;
            });
           
            System.Threading.Thread.Sleep(1100);
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                mainWindow.Opacity = 0;
            });

            int timer = 0;
            Logger.LogComment("Entering Timerloop");
            while ((videoProcess != null) && (!videoProcess.HasExited))
            {
                timer += 1;
                System.Threading.Thread.Sleep(300);
                if (timer > 400)
                {
                    // timeout to not 'hang'
                    // TODO: Add a setting for this
                    break;
                }
               
            }
            Logger.LogComment("Video has exited!");
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                mainPanel.Opacity = 1;
                mainWindow.Opacity = 1;
            });

        }

        private void KillVideoPlayer()
        {
            try
            {
                if (videoProcess != null)
                {
                    try
                    {
                        videoProcess.CloseMainWindow();
                        videoProcess = null;
                    }
                    catch (InvalidOperationException)
                    {
                        // expected if the process isn't there.
                    }
                    catch (Exception exc)
                    {
                        Debug.WriteLine("Tried and failed to kill video process..." + exc.ToString());
                        Logger.LogComment("Tried and failed to kill video process. Excpetion: " + exc.ToString());
                    }
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // OMXPlayer processes can be a bit tricky. to kill them we use
                    // killall - 9 omxplayer.bin

                    Helpers.RunProcess("killall", "-9 omxplayer.bin");
                    videoProcess = null;

                }
                else
                {
                    videoProcess.Close();
                    videoProcess.Dispose();
                    videoProcess = null;
                }
            }
            catch (Exception)
            { 
                // Swallow. This may no longer be there depending on what kills it (OMX player will exit if the video
                // completes for instance
            }
        }
        private bool GetFiles()
        {
            Logger.LogComment("GetFiles called!");
            playListEngine.GetPlayListItems();
            return true;
        }   
        public void SetupWebServer()
        {
            string current = System.IO.Directory.GetCurrentDirectory();
            server = new SimpleHTTPServer(current + "//web", 8000);
        }

        /// <summary>
        /// This gets called each clock cycle, and is responsible for 'refreshing' settings, fixing up rotation rendering, etc.
        /// </summary>
        private void RefreshSettings()
        {
            Logger.LogComment("Refresh settings was called");
            Helpers.DumpAppSettingsToLogger();
            Logger.LogComment("Current opacity: " + mainWindow.Opacity);
           
            // Infobar is the text at the bottom (default 100)
            tb.FontSize = AppSettings.Default.InfoBarFontSize;

            // update stretch if changed
            frontImage.Stretch = AppSettings.Default.ImageStretch;
            backImage.Stretch = AppSettings.Default.ImageStretch;


            RotateMainPanel();


            if (AppSettings.Default.Clock)
            {
                tb.Opacity = 1;
            }

            // update any fade settings
            fadeTransition.Duration = TimeSpan.FromMilliseconds(AppSettings.Default.FadeTransitionTime);
        }
        /// <summary>
        /// Rotates the Main Panel to match the orientation specified in appsettings
        /// </summary>
        private void RotateMainPanel()
        {
            int degrees = AppSettings.Default.Rotation;
            double w = mainWindow.Width;
            double h = mainWindow.Height;

            // if the screen is rotated, and we haven't rendered anything yet, then we'll get back NaN when trying to 
            // calculate for rotation. Look for this and set some default guesses to get us by the first rendering.
            if (Double.IsNaN(w) && (degrees == 90) || (degrees == 270))
            {
                mainWindow.Width = 1920;
                mainWindow.Height = 1080;
                // Screen hasn't rendered yet...force a resolution
                w = 1080;
                h = 1920;
                mainWindow.InvalidateMeasure();
                Logger.LogComment("Tried to fix rendering");
            }

            rotationTransform = new RotateTransform(degrees);

            if ((degrees == 90) || (degrees == 270))
            {
                mainPanel.Width = h;
                mainPanel.Height = w;
                Logger.LogComment("Rotation 90 degrees. calculated  W: " + w + " H: " + h);
                Logger.LogComment("Rotation 90 degrees. Panel  Width: " + mainPanel.Width + " Height: " + mainPanel.Height);
                Logger.LogComment("Rotation 90 degrees. Window Width: " + mainWindow.Width + " Height: " + mainWindow.Height);
            }
            else
            {
                mainPanel.Width = w;
                mainPanel.Height = h;
            }
            mainPanel.RenderTransform = rotationTransform;
            mainWindow.InvalidateMeasure();
            AppSettings.Default.OXMOrientnation = "--orientation " + degrees.ToString();

        }
    }
}
