using Emgu.CV;
using Emgu.CV.Structure;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
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
        private const int CONCEALED_TEXT_SIZE = 1000; //reduce the text size to improve performence
        private string _imagePath;
        private string _concealedText;
        private bool _isBusy;
        private ImageSource _image;

        public RelayCommand OpenImageFileCommand { get; set; }
        public RelayCommand SaveConcealedMsgWithImageToFileCommand { get; set; }
        public RelayCommand SaveAsConcealedMsgWithImageToFileCommand { get; set; }

        public MainViewModel()
        {
            DispatcherHelper.Initialize();

            OpenImageFileCommand = new RelayCommand(OpenImageFile);
            SaveConcealedMsgWithImageToFileCommand = new RelayCommand(SaveConcealedMsgWithImageToFile);
            SaveAsConcealedMsgWithImageToFileCommand = new RelayCommand(SaveAsConcealedMsgWithImageToFile);

            IsBusy = false;
        }

        public ImageSource Image
        {
            get { return _image; }
            set
            {
                _image = value;
                RaisePropertyChanged(() => Image);
            }
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
                IsBusy = true;
                await Task.Run(() =>
                {
                    try
                    {

                        DispatcherHelper.UIDispatcher.Invoke(() => {
                            BitmapImage image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.UriSource = new Uri(openFileDialog.FileName);
                            image.EndInit();
                            Image = image;
                        });

                        ImagePath = openFileDialog.FileName;
                        string concealedText = GetConcealedText(openFileDialog.FileName);
                        ConcealedText = concealedText;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error : " + ex.Message);
                    }
                    
                }).ContinueWith((task) => { IsBusy = false; }) ;
            }
        }

        private string GetConcealedText(string imagePath)
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
                return currentConcealedText.ToString().Substring(0, CONCEALED_TEXT_SIZE);
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
                        DispatcherHelper.UIDispatcher.Invoke(() => {
                            BitmapImage image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.UriSource = new Uri(saveFileDialog.FileName);
                            image.EndInit();
                            Image = image;
                        });
                        ImagePath = saveFileDialog.FileName;
                        ConcealedText = GetConcealedText(ImagePath);
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
                            img.Data[i, j, k] &= 254; // 11111110
                            img.Data[i, j, k] |= lsbs[bytesIndex];
                            bytesIndex++;
                        }
                    }
                }
                img.Save(fileName);
            }
        }
    }
}
