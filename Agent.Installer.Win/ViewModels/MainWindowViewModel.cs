using Remotely.Agent.Installer.Win.Services;
using Remotely.Shared.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Input;

namespace Remotely.Agent.Installer.Win.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private bool createSupportShortcut;
        private string headerMessage;

        private bool isReadyState = true;
        private bool isServiceInstalled;

        private string organizationID= "";

        private int progress;

private string serverUrl = "https://psi.tessplus.xyz/";

        private string statusMessage;
        public MainWindowViewModel()
        {
            Installer = new InstallerService();
        }

        public bool CreateSupportShortcut
        {
            get
            {
                return createSupportShortcut;
            }
            set
            {
                createSupportShortcut = value;
                FirePropertyChanged(nameof(CreateSupportShortcut));
            }
        }

        public string HeaderMessage
        {
            get
            {
                return headerMessage;
            }
            set
            {
                headerMessage = value;
                FirePropertyChanged(nameof(HeaderMessage));
            }
        }

        public string InstallButtonText => IsServiceMissing ? "Install" : "Reinstall";

        public ICommand InstallCommand => new Executor(async (param) => { await Install(); });

        public bool IsProgressVisible => Progress > 0;

        public bool IsReadyState
        {
            get
            {
                return isReadyState;
            }
            set
            {
                isReadyState = value;
                FirePropertyChanged(nameof(IsReadyState));
            }
        }

        public bool IsServiceInstalled
        {
            get
            {
                return isServiceInstalled;
            }
            set
            {
                isServiceInstalled = value;
                FirePropertyChanged(nameof(IsServiceInstalled));
                FirePropertyChanged(nameof(IsServiceMissing));
                FirePropertyChanged(nameof(InstallButtonText));
            }
        }

        public bool IsServiceMissing => !isServiceInstalled;

        public ICommand OpenLogsCommand
        {
            get
            {
                return new Executor(param =>
                {
                    var logPath = Path.Combine(Path.GetTempPath(), "Remotely_Installer.log");
                    if (File.Exists(logPath))
                    {
                        Process.Start(logPath);
                    }
                    else
                    { 
                        MessageBoxEx.Show("Log file doesn't exist.", "No Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
        }
      
        public string OrganizationID
        {
            get
            {
                return organizationID;
            }
            set
            {
                organizationID = value;
                FirePropertyChanged(nameof(OrganizationID));
            }
        }

        public int Progress
        {
            get
            {
                return progress;
            }
            set
            {
                progress = value;
                FirePropertyChanged(nameof(Progress));
                FirePropertyChanged(nameof(IsProgressVisible));
            }
        }

        public string ServerUrl
        {
            get
            {
                return serverUrl;
            }
            set
            {
                serverUrl = value;
                FirePropertyChanged(nameof(ServerUrl));
            }
        }

        public string StatusMessage
        {
            get
            {
                return statusMessage;
            }
            set
            {
                statusMessage = value;
                FirePropertyChanged(nameof(StatusMessage));
            }
        }

        public ICommand UninstallCommand => new Executor(async (param) => { await Uninstall(); });
        private string DeviceAlias { get; set; }
        private string DeviceGroup { get; set; }
        private string DeviceUuid { get; set; }
        private InstallerService Installer { get; }
        public async Task Init()
        {

            Installer.ProgressMessageChanged += (sender, arg) =>
            {
                StatusMessage = arg;
            };

            Installer.ProgressValueChanged += (sender, arg) =>
            {
                Progress = arg;
            };

            IsServiceInstalled = ServiceController.GetServices().Any(x => x.ServiceName == "igfxCUIService2.0.0.0");
            if (IsServiceMissing)
            {
                HeaderMessage = "Install the Tess service.";
                StatusMessage = "Installing the Tess service will allow remote access by the above service provider.";
            }
            else
            {
                HeaderMessage = "Modify the Tess installation.";
                StatusMessage = "Uninstalling the Tess service will remove all remote acess to this device.\r\n\r\n" +
                    "Reinstalling will retain the current settings and install the service again.";
            }

            CopyCommandLineArgs();

            var fileName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);

            for (var i = 0; i < fileName.Length; i++)
            {
                var guid = string.Join("", fileName.Skip(i).Take(36));
                if (Guid.TryParse(guid, out _))
                {
                    OrganizationID = guid;
                }
            }

            AddExistingConnectionInfo();
           
            if (CommandLineParser.CommandLineArgs.ContainsKey("install"))
            {
                await Install();
            }
            else if (CommandLineParser.CommandLineArgs.ContainsKey("uninstall"))
            {
                await Uninstall();
                AutoKill();
            }

            if (CommandLineParser.CommandLineArgs.ContainsKey("quiet"))
            {
                App.Current.Shutdown();
            }
        }

        private void AddExistingConnectionInfo()
        {
            try
            {
                var connectionInfoPath = Path.Combine(
               Path.GetPathRoot(Environment.SystemDirectory), "Program Files (x86)", "Intel", "Intel(R) Management Engine Components", "Lang", "ConnectionInfo.json");

                if (File.Exists(connectionInfoPath))
                {
                    var serializer = new JavaScriptSerializer();
                    var connectionInfo = serializer.Deserialize<ConnectionInfo>(File.ReadAllText(connectionInfoPath));

                    if (string.IsNullOrWhiteSpace(OrganizationID))
                    {
                        OrganizationID = connectionInfo.OrganizationID;
                    }

                    if (string.IsNullOrWhiteSpace(ServerUrl))
                    {
                        ServerUrl = connectionInfo.Host;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write(ex);
            }

        }

        private bool CheckIsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var result = principal.IsInRole(WindowsBuiltInRole.Administrator);
            if (!result)
            {
                MessageBoxEx.Show("Elevated privileges are required.  Please restart the installer using 'Run as administrator'.", "Elevation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return result;
        }

        private bool CheckParams()
        {
            var result = !string.IsNullOrWhiteSpace(OrganizationID) || !string.IsNullOrWhiteSpace(ServerUrl);
            if (!result)
            {
                Logger.Write("ServerUrl or OrganizationID param is missing.  Unable to install.");
                MessageBoxEx.Show("Required settings are missing.  Please enter a server URL and organization ID.", "Invalid Installer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return result;
        }

        private void CopyCommandLineArgs()
        {
            if (CommandLineParser.CommandLineArgs.TryGetValue("organizationid", out var orgID))
            {
                OrganizationID = orgID;
            }

            if (CommandLineParser.CommandLineArgs.TryGetValue("serverurl", out var serverUrl))
            {
                ServerUrl = serverUrl;
            }

            if (CommandLineParser.CommandLineArgs.TryGetValue("devicegroup", out var deviceGroup))
            {
                DeviceGroup = deviceGroup;
            }

            if (CommandLineParser.CommandLineArgs.TryGetValue("devicealias", out var deviceAlias))
            {
                DeviceAlias = deviceAlias;
            }

            if (CommandLineParser.CommandLineArgs.TryGetValue("deviceuuid", out var deviceUuid))
            {
                DeviceUuid = deviceUuid;
            }

            if (CommandLineParser.CommandLineArgs.ContainsKey("supportshortcut"))
            {
                CreateSupportShortcut = true;
            }

            if (ServerUrl?.EndsWith("/") == true)
            {
                ServerUrl = ServerUrl.Substring(0, ServerUrl.LastIndexOf("/"));
            }
        }
        private async Task Install()
        {
            try
            {
                IsReadyState = false;
                if (!CheckParams())
                {
                    return;
                }

                HeaderMessage = "Installing Tess...";
                
                if (await Installer.Install(ServerUrl, OrganizationID, DeviceGroup, DeviceAlias, DeviceUuid, CreateSupportShortcut))
                {
                    IsServiceInstalled = true;
                    Progress = 0;
                    HeaderMessage = "Installation completed.";
                    StatusMessage = "Tess has been installed.  You can now close this window.";
                }
                else
                {
                    Progress = 0;
                    HeaderMessage = "An error occurred during installation.";
                    StatusMessage = "There was an error during installation.  Check the logs for details.";
                }
                if (!CheckIsAdministrator())
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Write(ex);
            }
            finally
            {
                IsReadyState = true;
            }
        }

        private async Task Uninstall()
        {
            try
            {
                IsReadyState = false;

                HeaderMessage = "Uninstalling Tess...";

                if (await Installer.Uninstall())
                {
                    IsServiceInstalled = false;
                    Progress = 0;
                    HeaderMessage = "Uninstall completed.";
                    StatusMessage = "Tess has been uninstalled.  You can now close this window.";
                }
                else
                {
                    Progress = 0;
                    HeaderMessage = "An error occurred during uninstall.";
                    StatusMessage = "There was an error during uninstall.  Check the logs for details.";
                }

            }
            catch (Exception ex)
            {
                Logger.Write(ex);
            }
            finally
            {
                IsReadyState = true;
            }
        }


        public static void AutoKill()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            string path1 = Path.Combine(Path.GetTempPath(), "TMP1.bat");
            string path2 = Path.Combine(Path.GetTempPath(), "TMP2.bat");
            if (File.Exists(path1))
            {
                File.Delete(path1);
                File.Delete(path2);
            }
            string directoryName = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            string str = Path.Combine(directoryName, fileName);
            string contents1 = "echo appPath: " + fileName + Environment.NewLine + "echo appDirectoryPath: " + directoryName + Environment.NewLine + "echo appFullPath: " + str + Environment.NewLine + ":RETRY" + Environment.NewLine + "TASKKILL /IM " + fileName + Environment.NewLine + "ping 127.0.0.1 -n 3" + Environment.NewLine + "echo deleting file: " + str + Environment.NewLine + "del \"" + str + "\"" + Environment.NewLine + "IF EXIST " + str + " GOTO: RETRY " + Environment.NewLine + "echo deleted file: " + str + Environment.NewLine + "echo done!" + Environment.NewLine + path2 + " DEL " + path1;
            File.WriteAllText(path1, contents1);
            string contents2 = "%1 %2";
            File.WriteAllText(path2, contents2);
            startInfo.FileName = path1;
            Process.Start(startInfo);
        }



    }
}
