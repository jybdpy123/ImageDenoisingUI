using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ImageDenoising
{
    public enum NoiseType
    {
        SaltNoise,
        GaussionNoise,
        MixedNoise
    }

    /// <summary>
    /// AddNoise.xaml 的交互逻辑
    /// </summary>
    public partial class AddNoise : Window
    {
        private readonly NoiseType _noiseType;
        public float SaltRatio { get; } = 0.1f;
        public int GaussionSigma { get; } = 20;

        private bool _result = false;

        public bool GetResult()
        {
            return _result;
        }

        private void SetResult(bool value)
        {
            _result = value;
        }

        public AddNoise(NoiseType noiseType)
        {
            _noiseType = noiseType;
            InitializeComponent();
        }

        private void AddNoise_OnLoaded(object sender, RoutedEventArgs e)
        {
            switch (_noiseType)
            {
                case NoiseType.SaltNoise:
                    Title = "椒盐噪声加噪";
                    TextBoxGaussionSigma.Text = "(椒盐噪声模式下不可用)";
                    TextBoxGaussionSigma.IsEnabled = false;
                    break;
                case NoiseType.GaussionNoise:
                    Title = "高斯噪声加噪";
                    TextBoxSaltRatio.Text ="(高斯噪声模式下不可用)";
                    TextBoxSaltRatio.IsEnabled = false;
                    break;
                case NoiseType.MixedNoise:
                    Title = "混合噪声加噪";
                    break;
            }
        }

        private void ButtonConfirm_OnClick(object sender, RoutedEventArgs e)
        {
            SetResult(true);
            Close();
        }

        private void ButtonCancel_OnClick(object sender, RoutedEventArgs e)
        {
            SetResult(false);
            Close();
        }
    }
}
