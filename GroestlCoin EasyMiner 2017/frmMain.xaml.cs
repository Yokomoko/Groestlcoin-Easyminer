﻿using GroestlCoin_EasyMiner_2018.Properties;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Navigation;
using GroestlCoin_EasyMiner_2018.Business_Logic;
using Clipboard = System.Windows.Clipboard;
using HorizontalAlignment = System.Windows.HorizontalAlignment;


namespace GroestlCoin_EasyMiner_2018 {

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        public DialogResult Result;
        private readonly BackgroundWorker _amdBg = new BackgroundWorker();
        private readonly BackgroundWorker _cpuBg = new BackgroundWorker();
        private readonly BackgroundWorker _nVidiaBg = new BackgroundWorker();
        private bool _minerStarted;

        public MainWindow() {
            InitializeComponent();
            UxIntensityPopupText.Text = "Select lower values if you still want to use your PC.\nRaise the intensity if you are idle. (GPU Only)";

            ConfigureBackgroundWorkers();
            PopulatePage();
        }

        public event EventHandler CpuMinerClosed;

        public event EventHandler GpuMinerClosed;

        protected virtual void OnCpuMinerClosed(EventArgs e) {
            if (CpuMinerClosed != null) {
                MiningOperations.CpuStarted = false;
            }
            if (MiningOperations.CpuStarted || MiningOperations.GpuStarted) return;
            if (_minerStarted) {
                BtnStart_OnClick(null, null);
            }
        }

        protected virtual void OnGpuMinerClosed(EventArgs e) {
            MiningOperations.GpuStarted = false;
            if (MiningOperations.CpuStarted || MiningOperations.GpuStarted) return;
            if (_minerStarted) {
                BtnStart_OnClick(null, null);
            }
        }

        private void BtnStart_OnClick(object sender, RoutedEventArgs e) {
            _minerStarted = !_minerStarted;
            if (_minerStarted) {
                List<string> errors;
                if (!ValidateSettings(out errors)) {
                    MessageBox.Show(this,
                        $"Unable to start miner, please rectify the following issues and try again:{Environment.NewLine + string.Join(Environment.NewLine, errors)} ");
                    return;
                }
                SaveSettings();

                BtnStart.Content = "Stop Mining";

                UxLogsExpander.Visibility = Visibility.Visible;

                UxCpuTgl.IsEnabled = false;
                uxIntervalSlider.IsEnabled = false;
                uxnVidiaRb.IsEnabled = false;
                uxnAMDRb.IsEnabled = false;
                TxtAddress.IsEnabled = false;
                TxtPool.IsEnabled = false;
                TxtUsername.IsEnabled = false;
                TxtPassword.IsEnabled = false;
                UxIntensityTxt.IsEnabled = false;
                RbUsedwarfPool.IsEnabled = false;
                RbCustomPool.IsEnabled = false;
                UxAddressLabel.IsEnabled = false;
                GpuIntensityLbl.IsEnabled = false;
                UxIntensityHelp.Opacity = 0.56;
                ProgressBar.Visibility = Visibility.Visible;

                UxStandardSettings.IsExpanded = true;
                UxAdvancedSettings.IsExpanded = false;
                UxLogsExpander.IsExpanded = true;
                UxGetWalletAddressTxt.IsEnabled = false;

                if (UxCpuTgl.IsChecked == true) {
                    _cpuBg.RunWorkerAsync();
                    uxCpuMiningLogGroup.Visibility = Visibility.Visible;
                }
                if (uxnAMDRb.IsChecked == true) {
                    _amdBg.RunWorkerAsync();
                    uxGpuMiningLog.Visibility = Visibility.Collapsed;
                }
                if (uxnVidiaRb.IsChecked == true) {
                    _nVidiaBg.RunWorkerAsync();
                    uxGpuMiningLog.Visibility = Visibility.Visible;
                }
                ProgressBar.IsIndeterminate = true;
            }
            else {
                BtnStart.Content = "Start Mining";

                KillProcesses();

                uxIntervalSlider.IsEnabled = true;
                UxCpuTgl.IsEnabled = true;
                uxnVidiaRb.IsEnabled = true;
                uxnAMDRb.IsEnabled = true;
                TxtAddress.IsEnabled = true;
                TxtPool.IsEnabled = true;
                TxtUsername.IsEnabled = true;
                TxtPassword.IsEnabled = true;
                UxIntensityTxt.IsEnabled = true;
                RbUsedwarfPool.IsEnabled = true;
                RbCustomPool.IsEnabled = true;
                UxAddressLabel.IsEnabled = true;
                GpuIntensityLbl.IsEnabled = true;
                UxGetWalletAddressTxt.IsEnabled = true;
                UxIntensityHelp.Opacity = 1;

                UxLogsExpander.IsExpanded = false;
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void ConfigureBackgroundWorkers() {
            var executingAssembly = Directory.GetCurrentDirectory();
            _cpuBg.WorkerSupportsCancellation = true;
            _nVidiaBg.WorkerSupportsCancellation = true;
            _amdBg.WorkerSupportsCancellation = true;



            #region Complete Tasks

            _cpuBg.DoWork += (sender, args) => {
                if (_cpuBg.CancellationPending) {
                    args.Cancel = true;
                    return;
                }
                if (File.Exists(executingAssembly + @"\Resources\Miners\CPU Miner\minerd.exe")) {
                    using (var process = new Process()) {
                        ProcessStartInfo info = new ProcessStartInfo {
                            FileName = "cmd.exe",
                            Arguments =
                                "/C " + "\"" + executingAssembly + @"\Resources\Miners\CPU Miner\minerd.exe" + "\"" +
                                $@" -a groestl -o stratum+tcp://{MiningOperations.MiningPoolAddress.ToLower()
                                    .Replace("stratum+tcp://", "")
                                    .Trim()} -u {MiningOperations.MiningPoolUsername.Trim()} -p {MiningOperations
                                    .MiningPoolPassword.Trim()}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        process.StartInfo = info;
                        process.EnableRaisingEvents = true;
                        process.ErrorDataReceived += (o, eventArgs) => {
                            try {
                                Dispatcher.Invoke(() => {
                                    var lines =
                                        uxCpuLog.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                            .ToList();

                                    if (lines.Count() == 30) {
                                        lines.RemoveAt(0);
                                    }
                                    lines.Add(eventArgs.Data);
                                    uxCpuLog.Text = string.Join(Environment.NewLine, lines);

                                    uxCpuScroller.ScrollToVerticalOffset(uxGpuScroller.ExtentHeight);
                                });
                            }
                            catch (Exception ex) {
                                MessageBox.Show("Error " + ex.Message + Environment.NewLine + Environment.NewLine +
                                                ex.StackTrace);
                            }

                        };
                        process.Start();
                        process.BeginErrorReadLine();
                        MiningOperations.CpuStarted = true;
                        process.WaitForExit();
                        Dispatcher.Invoke(() => OnCpuMinerClosed(new EventArgs()));
                    }
                }
                else {
                    Dispatcher.Invoke(() => {
                        MessageBox.Show(
                            "minerd.exe file not found. Please check your antivirus settings, re-run the installer and select repair");
                        OnCpuMinerClosed(new EventArgs());
                    });
                }
            };

            _amdBg.DoWork += (sender, args) => {
                if (_amdBg.CancellationPending) {
                    args.Cancel = true;
                    return;
                }
                var path = executingAssembly + @"\Resources\Miners\AMD Miner\sgminer.exe";
                var folderNames = path.Split('\\');

                folderNames = folderNames.Select(fn => (fn.Contains(' ')) ? $"\"{fn}\"" : fn)
                                         .ToArray();

                var fullPathWithQuotes = string.Join("\\", folderNames);

                using (var process = new Process()) {
                    ProcessStartInfo info = new ProcessStartInfo {
                        FileName = "cmd.exe",
                        Arguments =
                            $"/C {fullPathWithQuotes} -g 4 -w 64 -k groestlcoin --no-submit-stale -o \"{MiningOperations.MiningPoolAddress.ToLower().Replace("stratum+tcp://", "").Trim()}\" -u {MiningOperations.MiningPoolUsername.Trim()} -p {MiningOperations.MiningPoolPassword.Trim()}  -I {MiningOperations.MiningIntensity} --text-only",
                        //  RedirectStandardOutput = true,
                        //  RedirectStandardError = true,
                        CreateNoWindow = false,
                        UseShellExecute = false
                    };
                    process.StartInfo = info;
                    //  MessageBox.Show(info.Arguments);
                    process.EnableRaisingEvents = true;
                    #region Writing to local window
                    //process.OutputDataReceived += (o, eventArgs) => {
                    //    try {
                    //        Dispatcher.Invoke(() => {
                    //            var lines =
                    //                uxGpuLog.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                    //                    .ToList();

                    //            if (lines.Count() == 30) {
                    //                lines.RemoveAt(0);
                    //            }
                    //            lines.Add(eventArgs.Data);
                    //            uxGpuLog.Text = string.Join(Environment.NewLine, lines);

                    //            uxGpuScroller.ScrollToVerticalOffset(uxGpuScroller.ExtentHeight);
                    //        });
                    //    }
                    //                        catch (Exception e) {
                    //#if DEBUG
                    //                            MessageBox.Show("Errr: " + e.Message);
                    //                            //Do Nothing
                    //#endif
                    //                        }
                    //                    };
                    //                    process.ErrorDataReceived += (o, eventArgs) => {
                    //                        try {
                    //                            Dispatcher.Invoke(() => {
                    //                                var lines = uxGpuLog.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();

                    //                                if (lines.Count() == 30) {
                    //                                    lines.RemoveAt(0);
                    //                                }
                    //                                lines.Add(eventArgs.Data);
                    //                                uxGpuLog.Text = string.Join(Environment.NewLine, lines);

                    //                                uxGpuScroller.ScrollToVerticalOffset(uxGpuScroller.ExtentHeight);
                    //                            });
                    //                        }
                    //                        catch (Exception e) {
                    //#if DEBUG
                    //                            MessageBox.Show("Errr: " + e.Message);
                    //                            //Do Nothing
                    //#endif
                    //                        }
                    //                    };
                    #endregion
                    process.Start();
                    MiningOperations.GpuStarted = true;
                    //    process.BeginErrorReadLine();
                    process.WaitForExit();
                    Dispatcher.Invoke(() => OnGpuMinerClosed(new EventArgs()));
                }
            };

            _nVidiaBg.DoWork += (sender, args) => {
                if (_nVidiaBg.CancellationPending) {
                    args.Cancel = true;
                    return;
                }
                var intensity = MiningOperations.MiningIntensity < 8 ? 8 : MiningOperations.MiningIntensity;

                using (var process = new Process()) {
                    ProcessStartInfo info = new ProcessStartInfo {
                        FileName = "cmd.exe",
                        Arguments =
                            "/C " + "\"" + executingAssembly + @"\Resources\Miners\nVidia Miner\ccminer.exe" + "\"" +
                            $@" -a groestl  -i {intensity} -o stratum+tcp://{MiningOperations.MiningPoolAddress.ToLower().Replace("stratum+tcp://", "").Trim()} -u {MiningOperations
                                .MiningPoolUsername.Trim()} -p {MiningOperations.MiningPoolPassword.Trim()}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    process.StartInfo = info;
                    process.EnableRaisingEvents = true;
                    process.OutputDataReceived += (o, eventArgs) => {
                        try {
                            Dispatcher.Invoke(() => {
                                var lines = uxGpuLog.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();

                                if (lines.Count() == 30) {
                                    lines.RemoveAt(0);
                                }
                                lines.Add(eventArgs.Data);
                                uxGpuLog.Text = string.Join(Environment.NewLine, lines);

                                uxGpuScroller.ScrollToVerticalOffset(uxGpuScroller.ExtentHeight);
                            });
                        }
                        catch (Exception ex) {
                            MessageBox.Show("Error " + ex.Message + Environment.NewLine + Environment.NewLine + ex.StackTrace);
                        }

                    };
                    process.Start();
                    MiningOperations.GpuStarted = true;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    try {
                        Dispatcher.Invoke(() => OnGpuMinerClosed(new EventArgs()));
                    }
                    catch (Exception ex) {
                        MessageBox.Show("Error " + ex.Message + Environment.NewLine + Environment.NewLine + ex.StackTrace);
                    }
                }
            };
        }

        #endregion Complete Tasks

        private void PoolRadioOptions(object sender, RoutedEventArgs e) {
            if (RbUsedwarfPool == null || RbCustomPool == null) return;
            if (RbUsedwarfPool.IsChecked == true) {
                WpCustom1.Visibility = Visibility.Collapsed;
                WpCustom2.Visibility = Visibility.Collapsed;
                uxViewdwarfPool.Visibility = Visibility.Visible;
            }
            if (RbCustomPool.IsChecked != true) return;
            WpCustom1.Visibility = Visibility.Visible;
            WpCustom2.Visibility = Visibility.Visible;
            uxViewdwarfPool.Visibility = Visibility.Collapsed;
        }

        private void PopulatePage() {
            uxIntervalSlider.Value = Settings.Default.MineIntensity;
            TxtAddress.Text = string.IsNullOrEmpty(Settings.Default.GrsWalletAddress) ? MiningOperations.GetAddress() : Settings.Default.GrsWalletAddress;
            RbUsedwarfPool.IsChecked = Settings.Default.UseDwarfPool;
            RbCustomPool.IsChecked = !Settings.Default.UseDwarfPool;
            TxtPool.Text = Settings.Default.MiningPoolAddress;
            TxtUsername.Text = Settings.Default.MiningPoolUsername;
            TxtPassword.Text = Settings.Default.MiningPoolPassword;
            UxIntensityTxt.Text = Settings.Default.MineIntensity.ToString();
            UxCpuTgl.IsChecked = Settings.Default.CPUMining;
            uxnVidiaRb.IsChecked = (MiningOperations.GpuMiningSettings)Settings.Default.GPUMining == MiningOperations.GpuMiningSettings.NVidia;
            uxnAMDRb.IsChecked = (MiningOperations.GpuMiningSettings)Settings.Default.GPUMining == MiningOperations.GpuMiningSettings.Amd;
        }

        private void SaveSettings() {
            Settings.Default.GrsWalletAddress = TxtAddress.Text;
            Settings.Default.UseDwarfPool = RbUsedwarfPool.IsChecked == true;
            Settings.Default.MiningPoolAddress = TxtPool.Text;
            Settings.Default.MiningPoolUsername = TxtUsername.Text;
            Settings.Default.MiningPoolPassword = TxtPassword.Text;
            Settings.Default.MineIntensity = int.Parse(UxIntensityTxt.Text);
            Settings.Default.CPUMining = UxCpuTgl.IsChecked == true;

            if (uxnVidiaRb.IsChecked == true) {
                Settings.Default.GPUMining = (byte)MiningOperations.GpuMiningSettings.NVidia;
            }
            else if (uxnAMDRb.IsChecked == true) {
                Settings.Default.GPUMining = (byte)MiningOperations.GpuMiningSettings.Amd;
            }
            else {
                Settings.Default.GPUMining = (byte)MiningOperations.GpuMiningSettings.None;
            }
            Settings.Default.Save();
        }

        private void UxAdvancedSettings_OnExpanded(object sender, RoutedEventArgs e) {
            UxLogsExpander.IsExpanded = false;
            UxStandardSettings.IsExpanded = false;
        }

        private void UxStandardSettings_OnExpanded(object sender, RoutedEventArgs e)
        {
            if (UxAdvancedSettings != null)
            {
                UxAdvancedSettings.IsExpanded = false;
            }
        }

        private void UxAmdRb_OnChecked(object sender, RoutedEventArgs e) {
            if (uxnVidiaRb.IsChecked != true) return;
            uxnVidiaRb.Checked -= UxnVidiaRb_OnChecked;
            uxnVidiaRb.IsChecked = false;
        }

        private void UxGetWalletAddressTxt_Click(object sender, RoutedEventArgs e) {
            if (!MiningOperations.WalletFileExists) {
                StartingGuide guide = new StartingGuide {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                };
                guide.FromMainWindow = true;
                guide.ShowDialog();
            }
            else {
                if (TxtAddress.Text == MiningOperations.GetAddress()) return;
                var messageBoxResult = MessageBox.Show(this, "Warning: Resetting your mining address will reset your rewards. Are you sure?", "Address Warning", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes) {
                    TxtAddress.Text = MiningOperations.GetAddress();
                }
            }
        }

        private void KillProcesses() {
            var processes = Process.GetProcessesByName("minerd");
            foreach (var process in processes) {
                process.Kill();
            }
            processes = Process.GetProcessesByName("ccminer");
            foreach (var process in processes) {
                process.Kill();
            }
            processes = Process.GetProcessesByName("sgminer");
            foreach (var process in processes) {
                process.Kill();
            }
        }
        private void UxIntensityHelp_OnMouseEnter(object sender, MouseEventArgs e) {
            UxIntensityPopup.IsOpen = true;
        }

        private void UxIntensityHelp_OnMouseLeave(object sender, MouseEventArgs e) {
            UxIntensityPopup.IsOpen = false;
        }

        private void UxLogsExpander_OnExpanded(object sender, RoutedEventArgs e) {
            UxAdvancedSettings.IsExpanded = false;
        }

        private void UxnVidiaRb_OnChecked(object sender, RoutedEventArgs e) {
            if (uxnAMDRb.IsChecked != true) return;
            uxnAMDRb.Checked -= UxAmdRb_OnChecked;
            uxnAMDRb.IsChecked = false;
            uxnAMDRb.Checked += UxAmdRb_OnChecked;
        }

        private bool ValidateSettings(out List<string> errors) {
            errors = new List<string>();

            if (string.IsNullOrEmpty(TxtAddress.Text)) {
                errors.Add("Please specify an address before starting to mine.");
            }
            if (RbUsedwarfPool.IsChecked != false) return !errors.Any();
            if (string.IsNullOrEmpty(TxtAddress.Text)) {
                errors.Add("Please specify a mining pool address or use Dwarfpool");
            }
            if (string.IsNullOrEmpty(TxtUsername.Text)) {
                errors.Add("Please specify a mining pool username or use Dwarfpool");
            }
            if (string.IsNullOrEmpty(TxtPassword.Text)) {
                errors.Add("Please specify a mining pool password or use Dwarfpool");
            }
            if (uxnAMDRb.IsChecked == false && uxnVidiaRb.IsChecked == false && UxCpuTgl.IsChecked == false) {
                errors.Add("Please select what to mine with (CPU, AMD / nVidia)");
            }
            return !errors.Any();
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            KillProcesses();
        }

        private void UxIntensityTxt_OnPreviewTextInput(object sender, TextCompositionEventArgs e) {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void RangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (UxIntensityTxt != null) {
                UxIntensityTxt.Text = e.NewValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void UxViewDwarfPoolHl_OnRequestNavigate(object sender, RequestNavigateEventArgs e) {
            System.Diagnostics.Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri + TxtAddress.Text));
            e.Handled = true;
        }

        private void UxCpuLog_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            try {
                Clipboard.SetText(uxCpuLog.Text);
                MessageBox.Show(this, "Copied to Clipboard");
            }
            catch {

            }

        }

        private void UxGpuLog_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            try {
                Clipboard.SetText(uxGpuLog.Text);
                MessageBox.Show(this, "Copied to Clipboard");
            }
            catch {

            }


        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            this?.DragMove();
        }
    }
}