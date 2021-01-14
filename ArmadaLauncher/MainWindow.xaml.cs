using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace ArmadaLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate,
        install,
        update
    }

    public static class FileSizeFormatter
    {
        static readonly string[] suffixes =
        { "Bytes", "KB", "MB", "GB", "TB", "PB" };
        public static string FormatSize(Int64 bytes)
        {
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }
    }

    public partial class MainWindow : Window
    {
        private Version onlineVersion;
        private string versionFile;
        private string gameZip;
        private string gameExe;
        private float downloadPercentageValue;
        private string currentLuancherDirectory;
        private string currentGameDirectory;
        private bool directorySelected = false;
        private string GameInfo;
        private bool gameInstalled;
        private bool gameInfoFileCreated;
        private static readonly string[] suffixes =
            { " Bytes", " KB", " MB", " GB", " TB", " PB"
        };

        private LauncherStatus _status;

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                case LauncherStatus.ready:

                    MoreOptionsDropdown.Visibility = Visibility.Visible;
                    OpenDirectoryButton.Visibility = Visibility.Visible;
                    PatchNotes.Visibility = Visibility.Visible;
                    REmoveGameButton.Visibility = Visibility.Visible;
                    PlayButtonNormal.Visibility = Visibility.Visible;
                    PlayButtonNormal.Content = "Launch";
                    break;

                case LauncherStatus.failed:
                    MessageBox.Show("Update Failed - Retry");
                    break;

                case LauncherStatus.downloadingGame:
                    ProgressBarText.Text = "Downloading Game";
                    MoreOptionsDropdown.Visibility = Visibility.Hidden;
                    break;

                case LauncherStatus.downloadingUpdate:
                    MoreOptionsDropdown.IsExpanded = false;
                    MoreOptionsDropdown.Visibility = Visibility.Hidden;
                    _status = LauncherStatus.downloadingUpdate;
                    break;

                case LauncherStatus.install:

                    _status = LauncherStatus.install;
                    MoreOptionsDropdown.IsExpanded = false;
                    MoreOptionsDropdown.Visibility = Visibility.Hidden;
                    break;

                case LauncherStatus.update:

                    break;
                default:
                    break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            ArmadaProgressBar.Visibility = Visibility.Hidden;
            UpdateButton.Visibility = Visibility.Hidden;
            ProgressBarText.Visibility = Visibility.Hidden;

            currentLuancherDirectory = Directory.GetCurrentDirectory();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckIfGameIsInstalled();

            // CheckForUpdates();
        }

        private void CheckIfGameIsInstalled()
        {
            if (File.Exists(currentLuancherDirectory + @"\GameInfo.ini"))
            {
                gameInstalled = true;

                versionFile = currentLuancherDirectory + @"\GameInfo.ini";

                CheckForUpdates();

            }
            else
            {
                gameInstalled = false;
            }
        }

        private void FirstTiemInstall(object sender, RoutedEventArgs e)
        {
            using (FileStream writer = File.Create(
                          currentLuancherDirectory + @"\GameInfo.ini"))
            {
                string[] lines = { "", "" };

                foreach (var line in lines)
                {
                    byte[] info = new UTF8Encoding(true).GetBytes(line);
                    writer.Write(info, 0, info.Length);
                }

                writer.Close();
            }


            gameInfoFileCreated = true;
            InstallGameFiles(false, Version.zero);
        }

        private void InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            try
            {
                using (var folderBrowser = new FolderBrowserDialog())
                {
                    folderBrowser.Description = "Please choose were to install Tails Adventure: Armada";

                    DialogResult result = folderBrowser.ShowDialog();

                    if (!string.IsNullOrWhiteSpace(folderBrowser.SelectedPath))
                    {
                        string chosenDirectory = folderBrowser.SelectedPath;

                        GameInfo = currentLuancherDirectory + @"\GameInfo.ini";

                        currentGameDirectory = chosenDirectory;

                        using (StreamWriter writer = File.AppendText(GameInfo))
                        {
                            string[] lines = { "[GameDirectory]", chosenDirectory };

                            foreach (var line in lines)
                            {
                                if (line.Contains("[GameDirectory]"))
                                {
                                    writer.Write(line + "\n");
                                }
                                else
                                {
                                    writer.WriteLine("name=" + line);
                                }
                            }

                            writer.Close();
                        }

                        gameZip = currentLuancherDirectory + @"\TailsAdventureArmada-windows.Zip";

                        directorySelected = true;
                    }
                }

                if (directorySelected)
                {


                    WebClient webClient = new WebClient();

                    if (_isUpdate)
                    {
                        Status = LauncherStatus.downloadingUpdate;
                    }
                    else
                    {
                        OpenDirectoryButton.Visibility = Visibility.Hidden;
                        Status = LauncherStatus.downloadingGame;
                        _onlineVersion = new Version(webClient.DownloadString("https://www.tailsadventure.com/downloads/windows/game-version.txt"));
                    }

                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                    webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
                    webClient.DownloadFileAsync(new Uri("https://www.tailsadventure.com/downloads/windows/TailsAdventureArmada-windows.Zip"), gameZip, _onlineVersion);
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files: {ex.Message}");
            }
        }

        private void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                string[] lines = File.ReadAllLines(versionFile);

                string tmp = string.Empty;

                for (int i = 0; i < lines.Length; ++i)
                {
                    if (lines[i].StartsWith("number"))
                    {
                        tmp = lines[i].Split("="[0])[1].Trim();
                    }
                }

                Version localVersion = new Version(tmp);

                VersionText.Text = "Alpha-Version: " + localVersion.ToString();

                try
                {
                    WebClient webClient = new WebClient();
                    onlineVersion = new Version(
                        webClient.DownloadString(
                            "https://www.tailsadventure.com/downloads/windows/game-version.txt"));

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallButton.Visibility = Visibility.Hidden;
                        MoreOptionsDropdown.Visibility = Visibility.Visible;
                        PlayButtonNormal.Visibility = Visibility.Visible;
                        UpdateButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        InstallButton.Visibility = Visibility.Hidden;

                        Status = LauncherStatus.ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for game updates -- Please yell ata the devs: {ex.Message}");
                }
            }
            else
            {
                PlayButtonNormal.Visibility = Visibility.Hidden;
                InstallButton.Visibility = Visibility.Visible;
            }
        }

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            PlayButtonNormal.Visibility = Visibility.Hidden;
            REmoveGameButton.Visibility = Visibility.Hidden;
            OpenDirectoryButton.Visibility = Visibility.Hidden;
            TimeEstimateText.Visibility = Visibility.Visible;
            DownloadValueText.Visibility = Visibility.Visible;
            FileSizeText.Visibility = Visibility.Visible;
            ArmadaProgressBar.Visibility = Visibility.Visible;
            InstallButton.Visibility = Visibility.Hidden;
            ProgressBarText.Visibility = Visibility.Visible;
            ArmadaProgressBar.Value = e.ProgressPercentage;
            downloadPercentageValue = e.ProgressPercentage;

            if (_status == LauncherStatus.downloadingUpdate)
            {
                UpdateButton.Visibility = Visibility.Hidden;

                ProgressBarText.Text =
                        "Downloading Update  " +
                        downloadPercentageValue.ToString() + "%";

                var fileSize =
                    FileSizeFormatter.FormatSize(
                        e.TotalBytesToReceive);

                var fileProgressInBytes = FileSizeFormatter.FormatSize(
                    e.BytesReceived);

                double TimeRemaining = (e.TotalBytesToReceive -
                    e.BytesReceived)
                    * e.ProgressPercentage / e.BytesReceived;

                FileSizeText.Text = "FILE SIZE:  " + fileSize;

                DownloadValueText.Text = "DOWNLOADING:  " +
                    fileProgressInBytes + " / " + fileSize;

                PatchNotes.Visibility = Visibility.Hidden;

                GenTimeSpanFromSeconds(TimeRemaining);

                if (downloadPercentageValue >= 99)
                {
                    _status = LauncherStatus.install;
                }

            }
            else if (_status == LauncherStatus.install)
            {
                ProgressBarText.Text = "Installing...";
            }
            else if (_status == LauncherStatus.ready)
            {

                OpenDirectoryButton.Visibility = Visibility.Visible;
                FileSizeText.Visibility = Visibility.Hidden;
                TimeEstimateText.Visibility = Visibility.Visible;
                DownloadValueText.Visibility = Visibility.Visible;

            }
            else if (_status == LauncherStatus.downloadingGame)
            {
                ProgressBarText.Text =
                       "Downloading Game  " +
                       downloadPercentageValue.ToString() + "%";

                var fileSize =
                    FileSizeFormatter.FormatSize(
                        e.TotalBytesToReceive);

                var fileProgressInBytes = FileSizeFormatter.FormatSize(
                    e.BytesReceived);

                double TimeRemaining = (e.TotalBytesToReceive -
                    e.BytesReceived)
                    * e.ProgressPercentage / e.BytesReceived;

                FileSizeText.Text = "FILE SIZE:  " + fileSize;

                DownloadValueText.Text = "DOWNLOADING:  " +
                    fileProgressInBytes + " / " + fileSize;

                PatchNotes.Visibility = Visibility.Hidden;

                GenTimeSpanFromSeconds(TimeRemaining);

                if (downloadPercentageValue >= 99)
                {
                    _status = LauncherStatus.install;
                }
            }
        }

        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                FileSizeText.Visibility = Visibility.Hidden;
                UpdateButton.Visibility = Visibility.Hidden;
                PlayButtonNormal.Visibility = Visibility.Visible;
                REmoveGameButton.Visibility = Visibility.Visible;
                OpenDirectoryButton.Visibility = Visibility.Visible;
                PatchNotes.Visibility = Visibility.Visible;
                TimeEstimateText.Visibility = Visibility.Hidden;
                DownloadValueText.Visibility = Visibility.Hidden;
                ArmadaProgressBar.Visibility = Visibility.Hidden;
                ProgressBarText.Visibility = Visibility.Hidden;
                string onlineVersion = ((Version)e.UserState).ToString();

                if (Directory.Exists(currentGameDirectory + @"\TailsAdventureArmada-windows"))
                {
                    Directory.Delete(currentGameDirectory + @"\TailsAdventureArmada-windows", true);
                }

                ZipFile.ExtractToDirectory(gameZip, currentGameDirectory, Encoding.UTF8);

                DirectoryInfo dirInfo = new DirectoryInfo(currentGameDirectory)
                {
                    Attributes = FileAttributes.Normal
                };

                versionFile = currentLuancherDirectory + @"\GameInfo.ini";

                using (StreamWriter writer = File.AppendText(versionFile))
                {
                    string[] lines = { "\n" + "[Version]", onlineVersion };

                    foreach (var line in lines)
                    {
                        if (line.Contains("[Version]"))
                        {
                            writer.Write(line + "\n");
                        }
                        else
                        {
                            writer.WriteLine("number=" + line);
                        }
                    }
                    writer.Close();
                }

                VersionText.Text = "Alpha-Version: " + onlineVersion;

                File.Delete(gameZip);

                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing download: {ex.Message}");
            }
        }

        private void GenTimeSpanFromSeconds(double seconds)
        {
            // Create a TimeSpan object and TimeSpan string from 
            // a number of seconds.
            TimeSpan interval = TimeSpan.FromSeconds(seconds);
            string timeInterval = interval.ToString();

            // Pad the end of the TimeSpan string with spaces if it 
            // does not contain milliseconds.
            int pIndex = timeInterval.IndexOf(':');
            pIndex = timeInterval.IndexOf('.', pIndex);
            if (pIndex < 0) timeInterval += "        ";

            TimeEstimateText.Text = "Time Remaining:  " + timeInterval;


        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // Begin dragging the window
            this.DragMove();
        }

        private void CloseApplication(object sender, RoutedEventArgs e)
        {
            Close();
            directorySelected = false;
        }

        private void OpenLinkButton(object sender, RoutedEventArgs e)
        {
            var tag = ((Button)sender).Tag;

            if (tag.ToString() == "Discord")
            {
                Process.Start("https://www.discord.gg/t7pxjqj");
            }
            else if (tag.ToString() == "Youtube")
            {
                Process.Start("https://www.youtube.com/ProjectArmada");
            }
            else if (tag.ToString() == "Twitter")
            {
                Process.Start("https://www.twitter.com/tailsarmada");
            }
            else if (tag.ToString() == "Website")
            {
                Process.Start("https://www.tailsadventure.com");
            }
            else if (tag.ToString() == "PatchNotes")
            {
                Process.Start("https://www.tailsadventure.com/downloads/updates/patchnotes");

            }
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void StartCheckForUpdate(object sender, RoutedEventArgs e)
        {
            InstallGameFiles(true, onlineVersion);
        }

        private string GameDirectory()
        {
            string formatedValue = string.Empty;

            if (File.Exists(
                currentLuancherDirectory + @"\GameInfo.ini"))
            {
                string[] lines = File.ReadAllLines(versionFile);
                string tmp = string.Empty;
                for (int i = 0; i < lines.Length; ++i)
                {
                    if (lines[i].StartsWith("DirectoryName="))
                    {
                        formatedValue = lines[i].Split("="[0])[1].Trim();
                    }
                }
            }

            return formatedValue;
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            versionFile = currentLuancherDirectory + @"\GameInfo.ini";

            string[] lines = File.ReadAllLines(versionFile);
            string tmp = string.Empty;
            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].StartsWith("name="))
                {
                    tmp = lines[i].Split("="[0])[1].Trim();
                }
            }

            gameExe = tmp + @"\TailsAdventureArmada-windows\TailsAdventureArmada-windows.exe";

            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe)
                {
                    WorkingDirectory = GameDirectory() + gameExe
                };
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }

        private void OpenInstallDirectory(object sender, RoutedEventArgs e)
        {
            Process.Start(currentLuancherDirectory);
        }

        private void UninstallGame(object sender, RoutedEventArgs e)
        {
            string[] lines = File.ReadAllLines(versionFile);

            string tmp = string.Empty;

            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].StartsWith("name"))
                {
                    tmp = lines[i].Split("="[0])[1].Trim();
                }
            }

            if (Directory.Exists(tmp + @"\TailsAdventureArmada-windows"))
            {
                Directory.Delete(tmp + @"\TailsAdventureArmada-windows", true);

                if (File.Exists(currentLuancherDirectory + @"\GameInfo.ini"))
                {
                    File.Delete(currentLuancherDirectory + @"\GameInfo.ini");
                }

                MoreOptionsDropdown.IsExpanded = false;
                MoreOptionsDropdown.Visibility = Visibility.Hidden;
                PatchNotes.Visibility = Visibility.Hidden;
                OpenDirectoryButton.Visibility = Visibility.Hidden;
                REmoveGameButton.Visibility = Visibility.Hidden;

                CheckForUpdates();
            }
        }

        private void OpenGameDirectory(object sender, RoutedEventArgs e)
        {
            string[] lines = File.ReadAllLines(versionFile);

            string tmp = string.Empty;

            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].StartsWith("name"))
                {
                    tmp = lines[i].Split("="[0])[1].Trim();
                }
            }

            Process.Start(tmp + @"\TailsAdventureArmada-windows");
        }

    }

    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

        internal Version(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }
        internal Version(string _version)
        {
            string[] versionStrings = _version.Split('.');
            if (versionStrings.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            major = short.Parse(versionStrings[0]);
            minor = short.Parse(versionStrings[1]);
            subMinor = short.Parse(versionStrings[2]);
        }

        internal bool IsDifferentThan(Version _otherVersion)
        {
            if (major != _otherVersion.major)
            {
                return true;
            }
            else
            {
                if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else
                {
                    if (subMinor != _otherVersion.subMinor)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }
    }
}
