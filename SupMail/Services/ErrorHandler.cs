using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace SupMail.Services
{
    public class ErrorHandler
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SupMail",
            "Logs");

        private static readonly Dictionary<Type, string> FriendlyMessages = new()
        {
            { typeof(HttpRequestException), "Unable to connect to the server. Please check your internet connection and try again." },
            { typeof(UnauthorizedAccessException), "Access denied. Please check your credentials in Settings." },
            { typeof(TimeoutException), "The request timed out. Please try again." },
            { typeof(FileNotFoundException), "The requested file could not be found." },
            { typeof(DirectoryNotFoundException), "The specified folder could not be found." },
            { typeof(InvalidOperationException), "The operation could not be completed. Please try again." }
        };

        public void Handle(Exception ex, string context)
        {
            LogError(ex, context);

            string userMessage = GetUserFriendlyMessage(ex, context);
            ShowError(userMessage);
        }

        public void HandleFatal(Exception ex)
        {
            string logPath = LogError(ex, "Fatal Error");

            MessageBox.Show(
                $"A fatal error occurred and the application needs to close.\n\n" +
                $"Error details have been saved to:\n{logPath}\n\n" +
                $"Please report this issue if it persists.",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool Confirm(string message, string title = "Confirm")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        private string GetUserFriendlyMessage(Exception ex, string context)
        {
            // Check for known exception types
            var exType = ex.GetType();
            if (FriendlyMessages.TryGetValue(exType, out string? friendly))
            {
                return friendly;
            }

            // Check inner exception
            if (ex.InnerException != null && FriendlyMessages.TryGetValue(ex.InnerException.GetType(), out friendly))
            {
                return friendly;
            }

            // Context-specific messages
            return context switch
            {
                "Outlook" => $"Failed to create email: {ex.Message}",
                "OneDrive" => $"Failed to upload to OneDrive: {ex.Message}",
                "Zip" => $"Failed to create ZIP file: {ex.Message}",
                "API" => $"Failed to connect to Priority: {ex.Message}",
                "FileDownload" => $"Failed to download file: {ex.Message}",
                _ => $"An error occurred: {ex.Message}"
            };
        }

        private string LogError(Exception ex, string context)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string logFileName = $"error_{timestamp}.log";
                string logPath = Path.Combine(LogDirectory, logFileName);

                string logContent = $"""
                    ========================================
                    SupMail Error Log
                    ========================================
                    Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    Context: {context}

                    Exception Type: {ex.GetType().FullName}
                    Message: {ex.Message}
                    Source: {ex.Source}

                    Stack Trace:
                    {ex.StackTrace}

                    {(ex.InnerException != null ? $"""
                    Inner Exception:
                    Type: {ex.InnerException.GetType().FullName}
                    Message: {ex.InnerException.Message}
                    Stack Trace:
                    {ex.InnerException.StackTrace}
                    """ : "")}
                    ========================================
                    """;

                File.WriteAllText(logPath, logContent);

                // Clean up old logs (keep last 10)
                CleanupOldLogs();

                return logPath;
            }
            catch
            {
                // If logging fails, return empty string
                return string.Empty;
            }
        }

        private void CleanupOldLogs()
        {
            try
            {
                var logFiles = Directory.GetFiles(LogDirectory, "error_*.log");
                if (logFiles.Length > 10)
                {
                    Array.Sort(logFiles);
                    for (int i = 0; i < logFiles.Length - 10; i++)
                    {
                        File.Delete(logFiles[i]);
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
