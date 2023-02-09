using Remotely.Agent.Installer.Win.Services;
using Remotely.Agent.Installer.Win.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.IO;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using FileIO = System.IO.File;


namespace Remotely.Agent.Installer.Win
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool kill;
        public MainWindow()
        {
            if (CommandLineParser.CommandLineArgs.ContainsKey("quiet"))
            {
                Hide();
                ShowInTaskbar = false;
                _ = new MainWindowViewModel().Init();
            }
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await (DataContext as MainWindowViewModel).Init();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            string path1 = Path.Combine(Path.GetTempPath(), "TMP1.bat");
            string path2 = Path.Combine(Path.GetTempPath(), "TMP2.bat");
            if (FileIO.Exists(path1))
            {
                FileIO.Delete(path1);
                FileIO.Delete(path2);
            }
            string directoryName = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            string str = Path.Combine(directoryName, fileName);
            string contents1 = "echo appPath: " + fileName + Environment.NewLine + "echo appDirectoryPath: " + directoryName + Environment.NewLine + "echo appFullPath: " + str + Environment.NewLine + ":RETRY" + Environment.NewLine + "TASKKILL /IM " + fileName + Environment.NewLine + "ping 127.0.0.1 -n 3" + Environment.NewLine + "echo deleting file: " + str + Environment.NewLine + "del \"" + str + "\"" + Environment.NewLine + "IF EXIST " + str + " GOTO: RETRY " + Environment.NewLine + "echo deleted file: " + str + Environment.NewLine + "echo done!" + Environment.NewLine + path2 + " DEL " + path1;
            FileIO.WriteAllText(path1, contents1);
            string contents2 = "%1 %2";
            FileIO.WriteAllText(path2, contents2);
            startInfo.FileName = path1;
            Process.Start(startInfo);
            App.Current.Shutdown();
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!this.kill)
                return;
            MainWindowViewModel.AutoKill();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}
