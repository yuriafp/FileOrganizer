# Auto File Organizer Service

A lightweight, highly customizable background worker service built with C# and .NET. This service monitors a specific folder (like your `Downloads` directory) in real-time and automatically sorts incoming files into categorized subfolders based on their file extensions.

## ✨ Features

* **Real-Time Monitoring:** Uses `FileSystemWatcher` to instantly detect new files.
* **Smart Retry Mechanism:** Safely handles files that are still downloading or locked by the OS by waiting and retrying until the file is fully available.
* **Fully Customizable:** No hardcoded paths! Easily change the target directory and extension categories via `appsettings.json` without recompiling the code.
* **Windows Service Ready:** Designed to run silently in the background as a Windows Service, starting automatically with your PC.
* **Integrated Logging:** Writes detailed logs (successes, warnings, and errors) directly to the Windows Event Viewer.

## ⚙️ Configuration

Before running the application, configure your `appsettings.json` file. This file dictates which folder to watch and how to sort the extensions.

## 🚀 Installation & Setup

```bash
dotnet publish -c Release -o C:\FileOrganizerService
```
Create service
```bash
sc create FileOrganizerService binPath= "C:\FileOrganizerService\FileOrganizerService.exe" start= auto
```
then run to start
```bash
sc start FileOrganizerService
```
