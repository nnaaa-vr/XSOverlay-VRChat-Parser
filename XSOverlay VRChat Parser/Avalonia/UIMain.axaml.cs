using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using XSOverlay_VRChat_Parser.Avalonia.ViewModels;
using XSOverlay_VRChat_Parser.Avalonia.Views;
using XSOverlay_VRChat_Parser.Models;

namespace XSOverlay_VRChat_Parser.Avalonia
{
    public class UIMain : Application
    {
        public static ConfigurationModel Configuration { get; set; }
        private static DateTime LastSaveRequest { get; set; }
        private static Task SaveTask { get; set; }

        public static void SaveConfigurationDebounced()
        {
            LastSaveRequest = DateTime.Now;

            if (SaveTask != null && SaveTask.IsCompleted)
            {
                SaveTask.Dispose();
                SaveTask = null;
            }

            if (SaveTask == null)
            {
                SaveTask = new Task(() =>
                {
                    while (LastSaveRequest.AddSeconds(1) > DateTime.Now)
                        Task.Delay(100).GetAwaiter().GetResult();
                    ConfigurationModel.Save(Configuration);
                });

                SaveTask.Start();
            }
        }

        public static void Start(string[] args, AppBuilder builder, ConfigurationModel _configuration)
        {
            Configuration = _configuration;

            builder.StartWithClassicDesktopLifetime(args);
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow() { DataContext = new MainWindowViewModel() };

            base.OnFrameworkInitializationCompleted();
        }
    }
}