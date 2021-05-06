using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Octokit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
                EventLogAppend($"[{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00}] <UPDATE> Checking for updates for package updater...\r\n");


                bool updaterHasUpdate = false;

                try
                {
                    updaterHasUpdate = PackageUpdateManager.IsUpdaterUpdateAvailable(PackageUpdateManager.GetUpdaterVersion()).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    EventLogAppend($"[{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00}] <ERROR> An error occurred while checking for updates for the update package: {ex.Message}\r\n");
                    return;
                }

                if (updaterHasUpdate)
                {
                    EventLogAppend($"[{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00}] <UPDATE> Downloading updater package...\r\n");
                    EventLogAppend($"[{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00}] <UPDATE> Unpacking updater package...\r\n");
                }
                else
                    EventLogAppend($"[{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00}] <UPDATE> No package updater update was found.\r\n");
            });
        }

        private void QueryUpdateTick(object timerState)
        {
            try
            {
                lastIsUpdateAvailable = PackageUpdateManager.IsParserUpdateAvailable(ConfigurationModel.CurrentVersion).GetAwaiter().GetResult();

                DateTime now = DateTime.Now;

                if (lastIsUpdateAvailable)
                    EventLogAppend($"[{now.Hour:00}:{now.Minute:00}:{now.Second:00}] <UPDATE> An update is available!\r\n");
            }
            catch (Exception ex)
            {
                DateTime now = DateTime.Now;
                EventLogAppend($"[{now.Hour:00}:{now.Minute:00}:{now.Second:00}] <ERROR> Failed to check for updates: {ex.Message}\r\n");
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