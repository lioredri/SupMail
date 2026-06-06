using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SupMail.Models;
using SupMail.Services;
using SupMail.Views;

namespace SupMail
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            var services = new ServiceCollection();

            // Register services
            services.AddSingleton<ErrorHandler>();
            services.AddSingleton<OneDriveService>();
            services.AddTransient<OutlookService>();
            services.AddTransient<ZipService>();
            services.AddTransient<PriorityApiService>();

            // Register windows
            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();

            // Register factory for AttachmentActionWindow (needs runtime AttachmentContext)
            services.AddTransient<Func<AttachmentContext, AttachmentActionWindow>>(sp =>
                context => new AttachmentActionWindow(
                    context,
                    sp.GetRequiredService<OutlookService>(),
                    sp.GetRequiredService<ZipService>(),
                    sp.GetRequiredService<OneDriveService>(),
                    sp.GetRequiredService<ErrorHandler>()));

            Services = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var errorHandler = Services.GetRequiredService<ErrorHandler>();

            // Handle UI thread exceptions
            DispatcherUnhandledException += (s, args) =>
            {
                errorHandler.Handle(args.Exception, "Unhandled UI Error");
                args.Handled = true;
            };

            // Handle non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    errorHandler.HandleFatal(ex);
                }
            };

            // Show main window through DI
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
    }
}