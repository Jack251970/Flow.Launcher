﻿using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.ExternalPlugins.Environments;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        public static PublicAPIInstance API { get; private set; }
        private const string Unique = "Flow.Launcher_Unique_Application_Mutex";
        private static bool _disposed;
        private Settings _settings;

        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
        }

        private async void OnStartupAsync(object sender, StartupEventArgs e)
        {
            await Stopwatch.NormalAsync("|App.OnStartup|Startup cost", async () =>
            {
                // Initialize settings
                var storage = new FlowLauncherJsonStorage<Settings>();
                _settings = storage.Load();
                _settings.Initialize(storage);
                _settings.WMPInstalled = WindowsMediaPlayerHelper.IsWindowsMediaPlayerInstalled();

                // Configure the dependency injection container
                var host = Host.CreateDefaultBuilder()
                    .UseContentRoot(AppContext.BaseDirectory)
                    .ConfigureServices(services => services
                        .AddSingleton(_ => _settings)
                        .AddSingleton<Updater>()
                        .AddSingleton<Portable>()
                        .AddSingleton<SettingWindowViewModel>()
                        .AddSingleton<IAlphabet, PinyinAlphabet>()
                        .AddSingleton<StringMatcher>()
                        .AddSingleton<IPublicAPI, PublicAPIInstance>()
                        .AddSingleton<MainViewModel>()
                    ).Build();
                Ioc.Default.ConfigureServices(host.Services);

                Ioc.Default.GetRequiredService<Updater>().Initialize(Launcher.Properties.Settings.Default.GithubRepo);

                Ioc.Default.GetRequiredService<Portable>().PreStartCleanUpAfterPortabilityUpdate();

                Log.Info("|App.OnStartup|Begin Flow Launcher startup ----------------------------------------------------");
                Log.Info($"|App.OnStartup|Runtime info:{ErrorReporting.RuntimeInfo()}");

                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();

                var imageLoadertask = ImageLoader.InitializeAsync();

                AbstractPluginEnvironment.PreStartPluginExecutablePathUpdate(_settings);

                var stringMatcher = Ioc.Default.GetRequiredService<StringMatcher>();
                StringMatcher.Instance = stringMatcher;
                stringMatcher.UserSettingSearchPrecision = _settings.QuerySearchPrecision;

                InternationalizationManager.Instance.Settings = _settings;
                InternationalizationManager.Instance.ChangeLanguage(_settings.Language);

                PluginManager.LoadPlugins(_settings.PluginSettings);

                API = Ioc.Default.GetRequiredService<IPublicAPI>() as PublicAPIInstance;

                Http.API = API;
                Http.Proxy = _settings.Proxy;

                await PluginManager.InitializePluginsAsync(API);
                await imageLoadertask;

                var mainVM = Ioc.Default.GetRequiredService<MainViewModel>();
                var window = new MainWindow(_settings, mainVM);

                Log.Info($"|App.OnStartup|Dependencies Info:{ErrorReporting.DependenciesInfo()}");

                Current.MainWindow = window;
                Current.MainWindow.Title = Constant.FlowLauncher;

                HotKeyMapper.Initialize(mainVM);

                // main windows needs initialized before theme change because of blur settings
                ThemeManager.Instance.Settings = _settings;
                ThemeManager.Instance.ChangeTheme(_settings.Theme);

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                RegisterExitEvents();

                AutoStartup();
                AutoUpdates();

                API.SaveAppAllSettings();
                Log.Info(
                    "|App.OnStartup|End Flow Launcher startup ----------------------------------------------------  ");
            });
        }

        private void AutoStartup()
        {
            // we try to enable auto-startup on first launch, or reenable if it was removed
            // but the user still has the setting set
            if (_settings.StartFlowLauncherOnSystemStartup && !Helper.AutoStartup.IsEnabled)
            {
                try
                {
                    Helper.AutoStartup.Enable();
                }
                catch (Exception e)
                {
                    // but if it fails (permissions, etc) then don't keep retrying
                    // this also gives the user a visual indication in the Settings widget
                    _settings.StartFlowLauncherOnSystemStartup = false;
                    Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"),
                        e.Message);
                }
            }
        }

        //[Conditional("RELEASE")]
        private void AutoUpdates()
        {
            _ = Task.Run(async () =>
            {
                if (_settings.AutoUpdates)
                {
                    // check update every 5 hours
                    var timer = new PeriodicTimer(TimeSpan.FromHours(5));
                    await Ioc.Default.GetRequiredService<Updater>().UpdateAppAsync(API);

                    while (await timer.WaitForNextTickAsync())
                        // check updates on startup
                        await Ioc.Default.GetRequiredService<Updater>().UpdateAppAsync(API);
                }
            });
        }

        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
            Current.Exit += (s, e) => Dispose();
            Current.SessionEnding += (s, e) => Dispose();
        }

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private void RegisterDispatcherUnhandledException()
        {
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
        }

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private static void RegisterAppDomainExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;
        }

        public void Dispose()
        {
            // if sessionending is called, exit proverbially be called when log off / shutdown
            // but if sessionending is not called, exit won't be called when log off / shutdown
            if (!_disposed)
            {
                API.SaveAppAllSettings();
                _disposed = true;
            }
        }

        public void OnSecondAppStarted()
        {
            Ioc.Default.GetRequiredService<MainViewModel>().Show();
        }
    }
}
