using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileOrganizerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private FileSystemWatcher _fileWatcher;

        private string _watchDirectory;
        private Dictionary<string, string> _categories;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            LoadSettings();
        }

        private void LoadSettings()
        {
            string rawPath = _configuration.GetValue<string>("FileOrganizer:TargetDirectory");

            if (string.IsNullOrEmpty(rawPath))
            {
                rawPath = @"%USERPROFILE%\Downloads";
                _logger.LogWarning("Path not found in appsettings. Using default: {rawPath}", rawPath);
            }

            _watchDirectory = Environment.ExpandEnvironmentVariables(rawPath);

            var categoriesConfig = _configuration.GetSection("FileOrganizer:Categories")
                                                 .Get<Dictionary<string, string>>();

            if (categoriesConfig == null)
            {
                categoriesConfig = new Dictionary<string, string>
                {
                    { ".jpg", "Images" }, { ".png", "Images" }, { ".pdf", "Documents" },
                    { ".docx", "Documents" }, { ".mp4", "Videos" }, { ".zip", "Compressed Files" },
                    { ".exe", "Executables" }
                };
                _logger.LogWarning("No categories found in appsettings.json. Using default categories.");
            }

            _categories = new Dictionary<string, string>(categoriesConfig, StringComparer.OrdinalIgnoreCase);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Directory.Exists(_watchDirectory))
            {
                _logger.LogError("The directory {_watchDirectory} was not found.", _watchDirectory);
                return;
            }

            _logger.LogInformation("Watching directory: {_watchDirectory}", _watchDirectory);

            _fileWatcher = new FileSystemWatcher(_watchDirectory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _fileWatcher.Created += OnFileCreated;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("New file detected: {File}", e.Name);
            Task.Run(() => ProcessFileAsync(e.FullPath));
        }

        private async Task ProcessFileAsync(string path)
        {
            int tries = 0;
            int maxTries = 10;
            int cooldown = 2000;

            while (tries < maxTries)
            {
                try
                {
                    string extension = Path.GetExtension(path);
                    string fileName = Path.GetFileName(path);

                    if (string.IsNullOrEmpty(extension)) return;

                    string directoryName = _categories.ContainsKey(extension) ? _categories[extension] : "Others";
                    string destinationPath = Path.Combine(_watchDirectory, directoryName);

                    if (!Directory.Exists(destinationPath))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }

                    string finalDestinationPath = Path.Combine(destinationPath, fileName);

                    if (!File.Exists(finalDestinationPath))
                    {
                        File.Move(path, finalDestinationPath);
                        _logger.LogInformation("File moved: {fileName} -> {directoryName}", fileName, directoryName);
                    }
                    else
                    {
                        _logger.LogWarning("The file {fileName} already exists in the destination.", fileName);
                    }

                    break;
                }
                catch (IOException)
                {
                    tries++;
                    _logger.LogWarning("The file is in use. Trying again in {cooldown}s ({tries}/{maxTries})...", (cooldown / 1000), tries, maxTries);
                    await Task.Delay(cooldown);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unknown error while trying to move {path}.", path);
                    break;
                }
            }

            if (tries == maxTries)
            {
                _logger.LogError("Failed to move {path} after {Max} tries.", path, maxTries);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping service...");
            _fileWatcher?.Dispose();
            await base.StopAsync(stoppingToken);
        }
    }
}