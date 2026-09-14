using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.Services;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using BillsEasy.Infrastructure.Services;
using BillsEasy.Presentation.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BillsEasy.Presentation.Wpf;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private static string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BillsEasy", "app_runtime.log");

    private static void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("=== Application OnStartup Started ===");
        base.OnStartup(e);

        // Global unhandled exception catching
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (s, ev) =>
        {
            Log($"UnobservedTaskException: {ev.Exception}");
            ev.SetObserved();
        };

        try
        {
            var services = new ServiceCollection();

            // 1. Storage Location
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BillsEasy");
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            var dbPath = Path.Combine(appDataDir, "billseasy.db");
            var connectionString = $"Data Source={dbPath}";
            Log($"Database path: {dbPath}");

            // 2. Register Infrastructure & Persistence
            services.AddSingleton<ISqliteDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
            services.AddSingleton<DatabaseInitializer>();
            services.AddSingleton<ICompanyRepository, CompanyRepository>();
            services.AddSingleton<IFinancialYearRepository, FinancialYearRepository>();
            services.AddSingleton<IAccountGroupRepository, AccountGroupRepository>();
            services.AddSingleton<ILedgerRepository, LedgerRepository>();
            services.AddSingleton<ICurrentUserService, StandaloneCurrentUserService>();
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<ICompanyContextService, CompanyContextService>();

            // 3. Register Application Services
            services.AddTransient<IAccountGroupService, AccountGroupService>();
            services.AddTransient<AccountGroupService>();
            services.AddTransient<ILedgerService, LedgerService>();
            services.AddTransient<LedgerService>();
            services.AddTransient<CompanyService>();
            services.AddTransient<FinancialYearService>();

            // 4. Register ViewModels & Views
            services.AddTransient<AccountGroupListViewModel>();
            services.AddTransient<LedgerListViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();
            Log("Service provider built successfully.");

            // 5. Initialize Database Schema synchronously before showing window
            var dbInit = ServiceProvider.GetRequiredService<DatabaseInitializer>();
            dbInit.InitializeAsync().GetAwaiter().GetResult();
            Log("Database schema initialized.");

            // 6. Launch Main Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = ServiceProvider.GetRequiredService<MainWindowViewModel>();
            MainWindow = mainWindow;
            mainWindow.Loaded += (s, ev) => Log("MainWindow.Loaded event fired.");
            mainWindow.Activated += (s, ev) => Log("MainWindow.Activated event fired.");
            mainWindow.Closing += (s, ev) => Log($"MainWindow.Closing event fired (Cancel={ev.Cancel}).");
            mainWindow.Closed += (s, ev) => Log("MainWindow.Closed event fired.");
            mainWindow.Show();
            Log($"MainWindow.Show() called. Window IsVisible={mainWindow.IsVisible}, IsLoaded={mainWindow.IsLoaded}, Visibility={mainWindow.Visibility}");
        }
        catch (Exception ex)
        {
            Log($"FATAL Startup Exception: {ex}");
            MessageBox.Show(
                $"Fatal Error during BillsEasy startup:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "BillsEasy Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log($"=== OnExit called with ExitCode: {e.ApplicationExitCode} ===");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"DispatcherUnhandledException: {e.Exception}");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "BillsEasy Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log($"AppDomain UnhandledException: {e.ExceptionObject}");
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"Fatal unhandled domain error:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "BillsEasy Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}