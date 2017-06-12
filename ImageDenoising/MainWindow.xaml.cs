using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Scripting.Hosting;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ImageDenoising
{
    enum PythonScriptType
    {
        AddSaltNoise,
        AddGaussionNoise
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _missionStarted = false;
        private readonly string _tempPath = Path.GetTempPath() + @"ImageDenoising\";
        private int _imageNo = 0;
        private readonly ScriptEngine _engine;
        private ImageInfo _srcImageInfo;
        private ImageInfo _currentlyImageInfo;
        private readonly List<ImageInfo> _imageInfos;


        private const string CmdPath = @"C:\Windows\System32\cmd.exe";

        private string GetCurrentlyImagePath()
        {
            return _currentlyImageInfo.ImgUri;
        }

        /// <summary>  
        /// 执行cmd命令  
        /// 多命令请使用批处理命令连接符：  
        /// <![CDATA[ 
        /// &:同时执行两个命令 
        /// |:将上一个命令的输出,作为下一个命令的输入 
        /// &&：当&&前的命令成功时,才执行&&后的命令 
        /// ||：当||前的命令失败时,才执行||后的命令]]>  
        /// </summary>  
        /// <param name="cmd"></param>
        private static Task<string> RunCmd(string cmd)
        {
            cmd = cmd.Trim().TrimEnd('&') + " &exit";
            return Task.Run(() =>
                {
                    using (var p = new Process())
                    {
                        p.StartInfo.FileName = CmdPath;
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardInput = true; //接受来自调用程序的输入信息  
                        p.StartInfo.RedirectStandardOutput = true; //由调用程序获取输出信息  
                        p.StartInfo.RedirectStandardError = true; //重定向标准错误输出  
                        p.StartInfo.CreateNoWindow = true; //不显示程序窗口  
                        p.Start(); //启动程序  
                        p.StandardInput.AutoFlush = true;
                        p.StandardInput.WriteLine(cmd);
                        p.StandardInput.AutoFlush = true;

                        //获取cmd窗口的输出信息  
                        var output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(); //等待程序执行完退出进程  
                        p.Close();
                        return output;
                    }
                }
            );

        }


        private void ClearTempFiles()
        {
            var directoryInfo = new DirectoryInfo(_tempPath);
            var fileinfo = directoryInfo.GetFileSystemInfos();
            foreach (var i in fileinfo)
            {
                try
                {
                    File.Delete(i.FullName);
                    AddLog("已清除文件" + i.Name);
                }
                catch (Exception exception)
                {
                    AddLog("清除文件" + i.Name + "出错 程序以跳过 可手动前往目录" + _tempPath + "删除" + exception.Message);
                }
            }
        }

        private void CloseAddNoise()
        {
            MenuItemAddNoise.IsEnabled = false;
            AddSaltImgNoise.IsEnabled = false;
            AddGaussionNoise.IsEnabled = false;
            AddMixedNoise.IsEnabled = false;
        }

        private void OpenAddNoise()
        {
            MenuItemAddNoise.IsEnabled = true;
            AddSaltImgNoise.IsEnabled = true;
            AddGaussionNoise.IsEnabled = true;
            AddMixedNoise.IsEnabled = true;
        }
        
        private void CloseDeNoise()
        {
            MenuItemDenoise.IsEnabled = false;
        }

        private void OpenDeNoise()
        {
            MenuItemDenoise.IsEnabled = true;
        }

        public MainWindow()
        {
            var scriptRuntime = ScriptRuntime.CreateFromConfiguration();
            _engine = scriptRuntime.GetEngine("python");
            var searchPath = new List<string>
            {
                "G:\\version1",
                "C:\\Users\\Anaconda2\\Lib",
                "C:\\Users\\Anaconda2\\Lib\\site-packages",
                "C:\\Users\\Anaconda2\\Lib\\site-packages\\skimage\\_shared",
                "C:\\Users\\Anaconda2\\Lib\\site-packages\\numpy",
                "C:\\Users\\Anaconda2\\Lib\\site-packages\\matplotlib",
                "C:\\Users\\Anaconda2\\Lib\\site-packages\\scikit_image-0.13.0-py2.7.egg-info",
                "C:\\Users\\Anaconda2\\Lib\\site-packages\\numpy-1.12.1-py2.7.egg-info",
                "C:\\Users\\Anaconda2\\Lib\\site-packages\\numpy\\core"
            };
            _engine.SetSearchPaths(searchPath);
            _imageInfos = new List<ImageInfo>();
            InitializeComponent();
        }

        private void UpdateViewByImageInfo(ImageInfo imageInfo)
        {
            if (imageInfo.IsNoise)
            {
                CloseAddNoise();
                OpenDeNoise();
            }
            if (imageInfo.IsSrc)
            {
                OpenAddNoise();
                CloseDeNoise();
            }

        }
        private void AddLog(string loginfo)
        {
            LogInfo.Document.Blocks.Add(new Paragraph(new Run(DateTime.Now + " " + loginfo)));
            LogInfo.ScrollToEnd();
        }

        private string GetPythonScriptName(PythonScriptType pythonScriptType)
        {
            switch (pythonScriptType)
            {
                case PythonScriptType.AddSaltNoise:
                    return "AddSaltNoiseScript";
                case PythonScriptType.AddGaussionNoise:
                    return "AddGaussionNoiseScript";
            }
            return null;
        }

        private bool ExecutePythonScript(PythonScriptType pythonScriptType, Args[] args)
        {
            var pythonScriptName = GetPythonScriptName(pythonScriptType);
            var source = _engine.CreateScriptSourceFromFile(".\\script\\" + pythonScriptName + ".py"); //设置脚本文件  
            var scope = _engine.CreateScope();

            //设置参数
            foreach (var arg in args)
            {
                scope.SetVariable(arg.ArgName, arg.ArgValue);
            }
            try
            {
                source.Execute(scope);
            }
            catch (Exception exception)
            {
                AddLog("" + exception.Message);
                return false;
            }
            //MessageBox.Show(scope.GetVariable("result").ToString());
            return bool.Parse(scope.GetVariable("result"));
        }

        private bool LoadTempFile(ImageInfo imageInfo)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(imageInfo.ImgUri);
                bi.EndInit();
                ImageSrc.Source = bi;
                _currentlyImageInfo = imageInfo;
                LabelCurrentlySafeName.Content = imageInfo.ImgName;
                UpdateViewByImageInfo(imageInfo);
            }
            catch (Exception e)
            {
                AddLog(e.Message);
                return false;
            }
            return true;
        }

        private bool CreateOrSaveTempFile(out string fileSafeName)
        {
            fileSafeName = Path.GetRandomFileName() + "_" + ++_imageNo + ".png";
            var fileName = _tempPath + fileSafeName;
            try
            {
                File.Create(fileName).Close();
                AddLog("创建临时文件" + fileName);
                File.SetAttributes(fileName, FileAttributes.Temporary);
            }
            catch (Exception e)
            {
                AddLog("临时文件创建失败" + e.Message);
                return false;
            }
            return true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogInfo.Document.Blocks.Clear();
            try
            {
                if (!Directory.Exists(_tempPath))
                {
                    Directory.CreateDirectory(_tempPath);
                    AddLog("临时文件夹不存在自动创建");
                }
                AddLog("系统启动完成... 工作临时目录：" + _tempPath);
            }
            catch (Exception exception)
            {
                AddLog("系统启动失败 系统临时文件目录可能被占用" + exception.Message);
            }
            ListViewImgList.ItemsSource = _imageInfos;
        }

        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            //ScriptRuntime scriptRuntime = ScriptRuntime.CreateFromConfiguration();
            //ScriptEngine rbEng = scriptRuntime.GetEngine("python");
            //ScriptSource source = rbEng.CreateScriptSourceFromFile("a.py");//设置脚本文件  
            //ScriptScope scope = rbEng.CreateScope();

            //try
            //{
            //    //设置参数  
            //    scope.SetVariable("arg1", 1);
            //    scope.SetVariable("arg2", 2);
            //}
            //catch (Exception)
            //{
            //    MessageBox.Show("输入有误。");
            //}

            //source.Execute(scope);
            //MessageBox.Show(scope.GetVariable("result").ToString());
        }

        private void StartNewMission_OnClick(object sender, RoutedEventArgs e)
        {
            if (_missionStarted)
            {
                var messageBoxResult = MessageBox.Show("已有新任务,确定要放弃现有任务而开始新任务吗？", "信息", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    MenuItemExitMission_OnClick(sender, e);
                }
                else
                    return;
            }
            var dialog = new OpenFileDialog
            {
                Title = "选择文件",
                Filter = "图像文件(*.bmp,*.jpg,*.png)|*bmp;*.jpg;*.png|所有文件|*.*",
                FileName = string.Empty,
                FilterIndex = 1,
                RestoreDirectory = true,
                DefaultExt = "jpg"
            };
            var result = dialog.ShowDialog();
            if (result != true) return;
            var imgdir = dialog.FileName;
            AddLog("打开源文件: " + dialog.SafeFileName);
            var tempFileSafeName = Path.GetRandomFileName() +"_src.png";
            var tempFileName = _tempPath + tempFileSafeName;
            try
            {
                File.Copy(imgdir, tempFileName, true);
                AddLog("创建临时文件" + tempFileName);
            }
            catch (Exception exception)
            {
                AddLog(exception.Message);
                return;
            }
            var imageinfo = new ImageInfo(tempFileSafeName, _tempPath)
            {
                ImgSrcName = tempFileSafeName,
            };
            _imageInfos.Add(imageinfo);
            ListViewImgList.Items.Refresh();
            if (!LoadTempFile(imageinfo)) return;
            _currentlyImageInfo = imageinfo;
            _srcImageInfo = imageinfo;
            _missionStarted = true;
            MenuItemExitMission.IsEnabled = true;
            OpenAddNoise();
        }

        private void MenuItemExitMission_OnClick(object sender, RoutedEventArgs e)
        {
            ImageSrc.Source = new BitmapImage();
            _missionStarted = false;
            AddLog("开始清空任务临时文件...");
            ClearTempFiles();
            _imageNo = 0;
            AddLog("清空任务临时文件完成，任务结束！");
            _imageInfos.Clear();
            ListViewImgList.Items.Refresh();
            LabelCurrentlySafeName.Content = "null";
            MenuItemExitMission.IsEnabled = false;
            CloseAddNoise();
            CloseDeNoise();
        }

        private void MenuItemExit_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (_missionStarted)
            {
                var result = MessageBox.Show("确定要退出吗？请确认处理后图像已被另存为，否则将会丢失！", "退出？", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.No || result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            ClearTempFiles();
        }

        private void MenuItemOpenTempPath_OnClick(object sender, RoutedEventArgs e)
        {
            var psi = new ProcessStartInfo("Explorer.exe") {Arguments = "/e,/root," + _tempPath};
            try
            {
                Process.Start(psi);
            }
            catch (Exception exception)
            {
                AddLog("打开临时文件夹出错" + exception.Message);
                throw;
            }
        }

        private double? GetPsnrFromOutPut(string outPut)
        {
            var r = new Regex(@"(?<=PSNR=)(\d+.\d+)");
            var m = r.Match(outPut);
            if (m.Success)
            {
                var psnr = double.Parse(m.Value);
                return Math.Round(psnr, 4);
            }
            return null;
        }

        private double? GetEpiFromOutPut(string outPut)
        {
            var r = new Regex(@"(?<=EPI=)(\d+.\d+)");
            var m = r.Match(outPut);
            if (m.Success)
            {
                var epi = double.Parse(m.Value);
                return Math.Round(epi, 4);
            }
            return null;
        }

        private async void AddSaltNoise_OnClick(object sender, RoutedEventArgs e)
        {
            var form = new AddNoise(NoiseType.SaltNoise);
            try
            {
                form.ShowDialog();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
            if (form.GetResult() != true) return;
            if (!CreateOrSaveTempFile(out string fileName)) return;
            AddLog("正在执行椒盐加噪操作 请等待...");
            var info = await RunCmd(@"cd script & python addSaltNoiseScript.py " + GetCurrentlyImagePath() + " " + _tempPath + fileName + " " +
                   form.TextBoxSaltRatio.Text.Trim());
            var psnr = GetPsnrFromOutPut(info);
            var epi = GetEpiFromOutPut(info);
            var newImg = new ImageInfo(fileName,_tempPath)
            {
                IsSrc = false,
                IsNoise = true,
                ImgSrcName = _currentlyImageInfo.ImgSrcName,
                ImgNoiseName = fileName,
                PSNR = psnr,
                EPI = epi
            };
            _imageInfos.Add(newImg);
            ListViewImgList.Items.Refresh();
            ListViewImgList.SelectedIndex = ListViewImgList.Items.Count - 1;
            AddLog("加噪完成！");
            LoadTempFile(newImg);
        }

        private void Test_OnClick(object sender, RoutedEventArgs e)
        {
            RunCmd(@"cd script & python 1.py");
        }

        private async void AddGaussionNoise_OnClick(object sender, RoutedEventArgs e)
        {
            var form = new AddNoise(NoiseType.GaussionNoise);
            form.ShowDialog();
            if (form.GetResult() != true) return;
            if (!CreateOrSaveTempFile(out string fileName)) return;
            AddLog("正在执行高斯加噪操作 请等待...");
            var info = await RunCmd(@"cd script & python addGaussionNoiseScript.py " + GetCurrentlyImagePath() + " " + _tempPath + fileName + " " +
                         form.TextBoxGaussionSigma.Text.Trim());
            AddLog("加噪完成！");
            var psnr = GetPsnrFromOutPut(info);
            var epi = GetEpiFromOutPut(info);
            var newImg = new ImageInfo(fileName, _tempPath)
            {
                IsSrc = false,
                IsNoise = true,
                ImgSrcName = _currentlyImageInfo.ImgSrcName,
                ImgNoiseName = fileName,
                PSNR = psnr,
                EPI = epi
            };
            _imageInfos.Add(newImg);
            ListViewImgList.Items.Refresh();
            ListViewImgList.SelectedIndex = ListViewImgList.Items.Count - 1;
            LoadTempFile(newImg);
        }

        private async void AddMixedNoise_OnClick(object sender, RoutedEventArgs e)
        {
            var form = new AddNoise(NoiseType.MixedNoise);
            form.ShowDialog();
            if (form.GetResult() != true) return;
            if (!CreateOrSaveTempFile(out string fileName)) return;
            AddLog("正在执行混合加噪操作 请等待...");
            var info = await RunCmd(@"cd script & python addMixedNoiseScript.py " + GetCurrentlyImagePath() + " " + _tempPath + fileName +
                         " " + form.TextBoxSaltRatio.Text.Trim() + " " + form.TextBoxGaussionSigma.Text.Trim());
            AddLog("加噪完成！");
            var psnr = GetPsnrFromOutPut(info);
            var epi = GetEpiFromOutPut(info);
            var newImg = new ImageInfo(fileName, _tempPath)
            {
                IsSrc = false,
                IsNoise = true,
                ImgSrcName = _currentlyImageInfo.ImgSrcName,
                ImgNoiseName = fileName,
                PSNR = psnr,
                EPI = epi
            };
            _imageInfos.Add(newImg);
            ListViewImgList.Items.Refresh();
            ListViewImgList.SelectedIndex = ListViewImgList.Items.Count - 1;
            LoadTempFile(newImg);
        }

        private void ListViewImgList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var imageInfo = (ImageInfo) ListViewImgList.SelectedItem;
            LoadTempFile(imageInfo);
        }

        /// <summary>
        /// 中值滤波
        /// </summary>
        private async void MenuItemMedianFilter_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CreateOrSaveTempFile(out string fileName)) return;
            AddLog("正在执行中值滤波操作 请等待...");
            var info = await RunCmd(@"cd script & python MedianFilterDeNoise.py " + GetCurrentlyImagePath() + " " +
                                    _tempPath + fileName);
            MessageBox.Show(info);
            AddLog("去噪完成！");
            var psnr = GetPsnrFromOutPut(info);
            var epi = GetEpiFromOutPut(info);
            var newImg = new ImageInfo(fileName, _tempPath)
            {
                IsSrc = false,
                IsNoise = true,
                ImgSrcName = _currentlyImageInfo.ImgSrcName,
                ImgNoiseName = fileName,
                PSNR = psnr,
                EPI = epi
            };
            _imageInfos.Add(newImg);
            ListViewImgList.Items.Refresh();
            ListViewImgList.SelectedIndex = ListViewImgList.Items.Count - 1;
            LoadTempFile(newImg);
        }

    }
}
