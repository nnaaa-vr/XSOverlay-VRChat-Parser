using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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

        private DispatcherTimer LogUpdateTimer;
        private DispatcherTimer QueryUpdateUITimer;
        private DispatcherTimer QueryUpdateUIOnLaunchTimer;

        private volatile bool lastIsUpdateAvailable = false;
        private Timer CheckUpdateTimer;

        private static bool ScrollDelayToggle = false;

        private static bool IsParserStandalone = false;

        private GitHubUpdater Updater;

        public MainWindow()
        {
            InitializeComponent();
            GitHubLink = this.FindControl<Button>("GitHubLink");
            EventLog = this.FindControl<TextEditor>("EventLog");

            this.PointerPressed += MainWindow_PointerPressed;
            GitHubLink.Click += GitHubLink_Click;

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

            QueryUpdateUITimer = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background, new EventHandler(QueryUpdateUI));
            QueryUpdateUITimer.Start();

            QueryUpdateUIOnLaunchTimer = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Background, new EventHandler(QueryUpdateUI));
            QueryUpdateUIOnLaunchTimer.Start();
        }

        private void QueryUpdateTick(object timerState)
        {
            if (Updater == null)
            {
                // Kind of a hacky way to detect whether or not it's a standalone or framework-dependent build, but it works.
                IsParserStandalone = File.Exists(ConfigurationModel.GetLocalResourcePath("\\createdump.exe"));

                Updater = new GitHubUpdater("nnaaa-vr", "nnaaa-vr", "XSOverlay-VRChat-Parser",
                    delegate (string[] names)
                    {
                        names = names.Where(x => !x.Contains("prerelease")
                                            && !x.Contains("alpha")
                                            && !x.Contains("beta")
                                            && !x.Contains("rc")
                                            && !x.Contains("-")
                                            ).ToArray();

                        for (int i = 0; i < names.Length; i++)
                            names[i] = names[i].Replace("v", "");

                        float[] asFloats = Array.ConvertAll<string, float>(names, float.Parse);

                        int idxMaxFloat = 0;
                        float lastMaxFloat = 0.0f;
                        for (int i = 0; i < asFloats.Length; i++)
                            if (asFloats[i] > lastMaxFloat)
                            {
                                lastMaxFloat = asFloats[i];
                                idxMaxFloat = i;
                            }

                        return 'v' + names[idxMaxFloat];
                    },
                    delegate (string[] names)
                    {
                        if (IsParserStandalone)
                            names = names.Where(x => x.Contains("_Standalone")).ToArray();

                        return names.Where(x => x.Contains(".zip")).FirstOrDefault();
                    },
                    delegate (string tag)
                    {
                        return float.Parse(tag.Replace("v", ""));
                    });
            }

            try
            {
                lastIsUpdateAvailable = Updater.IsUpdateAvailable(ConfigurationModel.CurrentVersion).GetAwaiter().GetResult();

                DateTime now = DateTime.Now;

                if(lastIsUpdateAvailable)
                    EventLogAppend($"[{now.Hour:00}:{now.Minute:00}:{now.Second:00}] <INFO> An update is available!");
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
            {
                ((MainWindowViewModel)DataContext).IsUpdateAvailable = true;
                QueryUpdateUITimer.IsEnabled = false;
            }

            QueryUpdateUIOnLaunchTimer.IsEnabled = false;
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