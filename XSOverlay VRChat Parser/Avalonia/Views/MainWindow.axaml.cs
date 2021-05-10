using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XSOverlay_VRChat_Parser.Avalonia.ViewModels;
using XSOverlay_VRChat_Parser.Helpers;
using XSOverlay_VRChat_Parser.Models;

namespace XSOverlay_VRChat_Parser.Avalonia.Views
{
    public class MainWindow : AcrylicWindow
    {
        public static MainWindow MainWindowRef;

        public static ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
        private readonly TextEditor EventLog;
        private readonly Button GitHubLink;
        private readonly Button UpdateButton;

        private DispatcherTimer LogUpdateTimer;

        private volatile bool lastIsUpdateAvailable = false;
        private Timer CheckUpdateTimer;

        private static bool ScrollDelayToggle = false;
        private PackageUpdateManager PackageUpdateManager;

        public MainWindow()
        {
            InitializeComponent();
            GitHubLink = this.FindControl<Button>("GitHubLink");
            UpdateButton = this.FindControl<Button>("UpdateButton");
            EventLog = this.FindControl<TextEditor>("EventLog");

            PackageUpdateManager = new PackageUpdateManager();

            this.PointerPressed += MainWindow_PointerPressed;
            GitHubLink.Click += GitHubLink_Click;
            UpdateButton.Click += UpdateButton_Click;

            XmlReader reader = XmlReader.Create("Resources\\EventLogSH.xshd");
            HighlightingManager.Instance.RegisterHighlighting("EventLogSH", null, HighlightingLoader.Load(reader, HighlightingManager.Instance));

            MainWindowRef = this;

            // This is broken right now. Issue: https://github.com/AvaloniaUI/AvaloniaEdit/issues/133
            //EventLog.Options.EnableHyperlinks = true;
            //EventLog.Options.RequireControlModifierForHyperlinkClick = false;

            EventLog.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("EventLogSH");

            CheckUpdateTimer = new Timer(new TimerCallback(QueryUpdateTick), null, 0, 1000 * 60 * 5);

            LogUpdateTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background,
                new EventHandler(delegate (Object o, EventArgs ea)
                {
                    if (MessageQueue.Count > 0)
                    {
                        string message = string.Empty;
                        while (MessageQueue.TryDequeue(out message))
                        {
                            if (message.Contains("<UPDATE>"))
                                QueryUpdateUI(null, null);

                            EventLog.AppendText(message);
                        }

                        ScrollDelayToggle = true;
                    }
                    else if (ScrollDelayToggle)
                    {
                        EventLog.ScrollToEnd();
                        ScrollDelayToggle = false;
                    }
                }));

            LogUpdateTimer.Start();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;

            Task.Run(() =>
            {
                Log.Update("Cleaning up temp directory...");
                bool cleaned = PackageUpdateManager.CleanTemp();

                if (!cleaned)
                {
                    Log.Error("Failed to clean up temp directory!");
                    return;
                }

                //Log.Update("Checking for updates for package updater...");

                bool updaterHasUpdate = false, parserHasUpdate = false, updaterIsStaged = false, parserIsStaged = false;

                //try
                //{
                //    updaterHasUpdate = PackageUpdateManager.IsUpdaterUpdateAvailable(PackageUpdateManager.GetUpdaterVersion()).GetAwaiter().GetResult();
                //}
                //catch (Exception ex)
                //{
                //    Log.Error("An error occurred while checking for updates for the update package.");
                //    Log.Exception(ex);
                //    return;
                //}

                //if (updaterHasUpdate)
                //{
                //    Log.Update("Downloading updater package...");
                //    bool downloadedUpdaterUpdate = PackageUpdateManager.DownloadLatestUpdater().GetAwaiter().GetResult();

                //    if (downloadedUpdaterUpdate)
                //    {
                //        Log.Update("Unpacking updater...");
                //        bool unpackedUpdater = PackageUpdateManager.UnpackUpdater();

                //        if (unpackedUpdater) {
                //            updaterIsStaged = true;
                //            Log.Update("Unpacked updater successfully.");
                //        }
                //        else
                //            Log.Error("Failed to unpack updater!");
                //    }
                //    else
                //        Log.Error("Failed to download updater update.");
                //}
                //else
                //    Log.Update("No package updater update was found.");


                Log.Update("Verifying parser still has update...");

                try
                {
                    parserHasUpdate = PackageUpdateManager.IsParserUpdateAvailable(ConfigurationModel.CurrentVersion).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Error("An error occurred while checking for updates for the parser package.");
                    Log.Exception(ex);
                    return;
                }

                if (!parserHasUpdate)
                {
                    Log.Update("Parser no longer appears to have an update available. Was it rolled back?");
                    return;
                }

                Log.Update("A parser package update is available. Downloading...");

                bool downloadedParserUpdate = false;

                try
                {
                    downloadedParserUpdate = PackageUpdateManager.DownloadLatestParser().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to download parser package!");
                    Log.Exception(ex);
                    return;
                }

                if (!downloadedParserUpdate)
                {
                    Log.Error("Failed to download parser package.");
                    return;
                }

                Log.Update("Downloaded parser package successfully. Unpacking...");
                bool unpackedParser = PackageUpdateManager.UnpackParser();

                if (!unpackedParser)
                {
                    Log.Error("Failed to unpack parser!");
                    return;
                }
                else
                    parserIsStaged = true;

                Log.Update("Unpacked parser successfully.");

                if((parserHasUpdate && parserIsStaged) || (updaterHasUpdate && updaterIsStaged))
                {
                    Log.Update("Update staging is complete. Beginning update process...");
                }
            });
        }

        private void QueryUpdateTick(object timerState)
        {
            try
            {
                lastIsUpdateAvailable = PackageUpdateManager.IsParserUpdateAvailable(ConfigurationModel.CurrentVersion).GetAwaiter().GetResult();

                DateTime now = DateTime.Now;

                if (lastIsUpdateAvailable)
                    Log.Update("An update is available!");
            }
            catch (Exception ex)
            {
                DateTime now = DateTime.Now;
                Log.Error("Failed to check for updates.");
                Log.Exception(ex);
            }

            if (lastIsUpdateAvailable)
                CheckUpdateTimer.Dispose();
        }

        private void QueryUpdateUI(object o, EventArgs ea)
        {
            if (lastIsUpdateAvailable)
                ((MainWindowViewModel)DataContext).IsUpdateAvailable = true;
        }

        private void GitHubLink_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/nnaaa-vr/XSOverlay-VRChat-Parser", UseShellExecute = true, RedirectStandardOutput = false });
        }

        private void MainWindow_PointerPressed(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            Point mousePosition = e.GetCurrentPoint(MainWindowRef).Position;

            if (mousePosition.Y <= 30 && mousePosition.X >= 475)
                return;

            if (mousePosition.Y <= 90)
                this.BeginMoveDrag(e);
        }

        public static void EventLogAppend(string message)
        {
            MessageQueue.Enqueue(message);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}