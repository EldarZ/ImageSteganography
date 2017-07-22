using Emgu.CV;
using Emgu.CV.Structure;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageSteganography
{
    public class MainViewModel : ViewModelBase
    {
        private string _imagePath;
        private string _concealedText;
        private bool _isBusy;

        public RelayCommand OpenImageFileCommand { get; set; }
        public RelayCommand SaveConcealedMsgWithImageToFileCommand { get; set; }
        public RelayCommand SaveAsConcealedMsgWithImageToFileCommand { get; set; }

        public MainViewModel()
        {
            OpenImageFileCommand = new RelayCommand(OpenImageFile);
            SaveConcealedMsgWithImageToFileCommand = new RelayCommand(SaveConcealedMsgWithImageToFile);
            SaveAsConcealedMsgWithImageToFileCommand = new RelayCommand(SaveAsConcealedMsgWithImageToFile);

            IsBusy = false;
        }

        public string ImagePath
        {
            get { return _imagePath; }
            set
            {
                _imagePath = value;
                RaisePropertyChanged(() => ImagePath);
            }
        }

        public string ConcealedText
        {
            get { return _concealedText; }
            set
            {
                _concealedText = value;
                RaisePropertyChanged(() => ConcealedText);
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                RaisePropertyChanged(() => IsBusy);
            }
        }

        private async void OpenImageFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "bmp files (*.bmp)|*.bmp|All files (*.*)|*.*";
            var result = openFileDialog.ShowDialog();
            if (result == true)
            {
                ImagePath = openFileDialog.FileName;
                IsBusy = true;
                await Task.Run(() =>
                {
                    try
                    {
                        UpdateConcealedText(openFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error : " + ex.Message);
                    }
                });
                IsBusy = false;
            }
        }

        private void UpdateConcealedText(string imagePath)
        {
            using (Image<Bgr, Byte> img = new Image<Bgr, Byte>(imagePath))
            {
                const byte lsbMask = 1;
                byte currentByte = 0;
                int byteIndex = 0;
                StringBuilder currentConcealedText = new StringBuilder();
                for (int i = 0; i < img.Height; i++)
                {
                    for (int j = 0; j < img.Width; j++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            byte value = img.Data[i, j, k];
                            byte lsb = (byte)(value & lsbMask);
                            currentByte = (byte)(currentByte | ((byte)(lsb << byteIndex)));
                            byteIndex++;

                            if (byteIndex == 8)
                            {
                                currentConcealedText.Append(Convert.ToChar(currentByte));
                                currentByte = 0;
                                byteIndex = 0;
                            }
                        }
                    }
                }
                img.Dispose();
                ConcealedText = currentConcealedText.ToString();
            }
        }

        private async void SaveConcealedMsgWithImageToFile()
        {
            IsBusy = true;
            await Task.Run(() =>
            {
                try
                {
                    SaveCurrentImageToFile(ImagePath);
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Error : " + ex.Message);
                }
            });
            IsBusy = false;
        }

        private async void SaveAsConcealedMsgWithImageToFile()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "bmp files (*.bmp)|*.bmp";
            if (saveFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                await Task.Run(() =>
                {
                    try
                    {
                        SaveCurrentImageToFile(saveFileDialog.FileName);
                        ImagePath = saveFileDialog.FileName;
                        UpdateConcealedText(ImagePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error : " + ex.Message);
                    }
                });
                IsBusy = false;
            }
        }

        private void SaveCurrentImageToFile(string fileName)
        {
            const byte lsbMask = 1;
            List<byte> lsbs = new List<byte>();
            char[] concealedTextChars = ConcealedText.ToCharArray();
            for (int i = 0; i < concealedTextChars.Length; i++)
            {
                byte currentValue = (byte)concealedTextChars[i];
                for (int j = 0; j < 8; j++)
                {
                    byte lsb = (byte)(currentValue & lsbMask);
                    lsbs.Add(lsb);
                    currentValue = (byte)(currentValue >> 1);
                }
            }

            int bytesIndex = 0;
            using (Image<Bgr, Byte> img = new Image<Bgr, Byte>(ImagePath))
            {
                for (int i = 0; i < img.Height && bytesIndex < lsbs.Count; i++)
                {
                    for (int j = 0; j < img.Width && bytesIndex < lsbs.Count; j++)
                    {
                        for (int k = 0; k < 3 && bytesIndex < lsbs.Count; k++)
                        {
                            img.Data[i, j, k] &= (byte)(254); // 11111110
                            img.Data[i, j, k] |= lsbs[bytesIndex];
                            bytesIndex++;
                        }
                    }
                }
                img.Save(fileName);
                img.Dispose();
            }
        }
    }
}
