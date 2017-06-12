using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageDenoising
{
    public class ImageInfo
    {
        public string ImgSrcName { get; set; }

        public string ImgNoiseName { get; set; }

        public string ImgName { get; set; }

        public string ImgUri { get; set; }

        public bool IsSrc { get; set; }

        public bool IsNoise { get; set; }

        public double? PSNR { get; set; }

        public double? EPI { get; set; }

        public double? AddPSNR { get; set; }

        public double? AddEPI { get; set; }

        public ImageInfo(string imgName,string tempPath)
        {
            ImgName = imgName;
            ImgUri = tempPath + imgName;
            IsSrc = true;
            IsNoise = false;
            ImgSrcName = null;
            ImgNoiseName = null;
            PSNR = null;
            EPI = null;
            AddPSNR = null;
            AddEPI = null;
        }
    }
}
