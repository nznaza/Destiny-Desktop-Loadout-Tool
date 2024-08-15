using Bluegrams.Application;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using static DestinyLoadoutTool.MainWindow;
using static DestinyLoadoutTool.WindowUserMessage;

namespace DestinyLoadoutTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, WindowMessage Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        //[DllImport("user32.dll")]
        //public static extern uint RegisterWindowMessage(string lpString);

        const string appName = "DestinyLoadoutTool";
        static bool createdNew;
        private static readonly Mutex _mutex = new Mutex(true, appName, out createdNew);

        protected override async void OnStartup(StartupEventArgs e)
        {
            PortableSettingsProvider.SettingsFileName = "settings.xml";
            PortableSettingsProviderBase.AllRoaming = true;
            //PortableSettingsProvider.SettingsDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "d2lt", Assembly.GetExecutingAssembly().GetName().Name);
            Directory.CreateDirectory(PortableSettingsProvider.SettingsDirectory);
            PortableSettingsProvider.ApplyProvider(Settings.Default);

            if (!createdNew)
            {
                Process me = Process.GetCurrentProcess();
                Process[] arrProcesses = Process.GetProcessesByName(me.ProcessName);

                if (arrProcesses.Length > 1)
                {
                    for (int i = 0; i < arrProcesses.Length; i++)
                    {
                        if (arrProcesses[i].Id != me.Id)
                        {
                            // get the window handle
                            IntPtr hWnd = arrProcesses[i].MainWindowHandle;
                            //uint _messageId = RegisterWindowMessage("DestinyLoadoutToolMessageIdentifier");
                            COPYDATASTRUCT copyData = new COPYDATASTRUCT
                            {
                                lpData = string.Join(" ", e.Args),
                                dwData = hWnd
                            };
                            copyData.cbData = copyData.lpData.Length + 1;
                            _ = SendMessage(hWnd, WindowMessage.WM_COPYDATA, IntPtr.Zero, ref copyData);

                            break;
                        }
                    }
                }
                //app is already running! Exiting the application  
                Application.Current.Shutdown();
                return;
            }
            MainWindow mainWindow = new MainWindow();
            string argCode = string.Join(" ", e.Args);
            if (!string.IsNullOrEmpty(argCode))
                await mainWindow.ValidateProtocolToken(argCode);

            mainWindow.Show();
            //base.OnStartup(e);
        }
    }
}
