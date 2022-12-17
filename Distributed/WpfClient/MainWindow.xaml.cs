using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf;
using System.Linq;
using Contracts;

using SixLabors.ImageSharp.PixelFormats;
using System.Net.NetworkInformation;


namespace WpfClient
{
    public partial class MainWindow : Window
    {
        //данные для отображения
        public ViewModel ViewData = new();
        public Service service = new ();

        //токен отмены
        private CancellationTokenSource token_src;
        private CancellationToken token;

        //главное окно
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = ViewData;
        }

        //диалог с выбором папки
        public void BrowseFolderPath(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Select a folder with images.";

            Button? button = sender as Button;
            if (button == null) throw new ArgumentOutOfRangeException("unknown sender");

            if ((bool)dialog.ShowDialog(this))
            {
                var path = dialog.SelectedPath;

                if (button.Name == "ButtonBrowsePath1")
                {
                    ViewData.FolderPath1 = path;
                    addFilesToCollection(ViewData.FilesList1, path);
                }
                else if (button.Name == "ButtonBrowsePath2")
                {
                    ViewData.FolderPath2 = path;
                    addFilesToCollection(ViewData.FilesList2, path);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("unknown name of sender");
                }
            }
        }

        private void addFilesToCollection(ObservableCollection<FileItem> coll, string folder_path)
        {
            coll.Clear();

            string[] paths = Directory.GetFiles(folder_path);
            foreach (string path in paths)
            {
                var fileItem = new FileItem(path);
                if (fileItem.Ext == ".jpg" || fileItem.Ext == ".png")
                    coll.Add(fileItem);
            }
        }


        private void FileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewData.CalculationEnable = true;
            pbStatus.Value = 0;
            var list_box = sender as ListBox;
            var selected_item = list_box!.SelectedItem as FileItem;

            if (list_box.Name == "ListBoxFiles1")
                ViewData.ImagePath1 = selected_item!.Path;

            else if (list_box.Name == "ListBoxFiles2")
                ViewData.ImagePath2 = selected_item!.Path;
        }


        private bool AllParameteresValid
        {
            get
            {
                if (ViewData.FilesList1.Count == 0 || ViewData.FilesList2.Count == 0)
                {
                    MessageBox.Show("Select folder with images", "Images folder not detected", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                else if (ViewData.ImagePath1 == "" || ViewData.ImagePath2 == "")
                {
                    MessageBox.Show("You need select one image from every list", "Images not selected", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                return true;
            }

        }


        private void ResetToken()
        {
            token_src = new CancellationTokenSource();
            token = token_src.Token;
        }

        
        private async void CalculateClick(object sender, RoutedEventArgs e)
        {
            if (!ViewData.CalculationEnable || !AllParameteresValid)
                return;

            ResetToken();
            ViewData.Distance = 0;
            ViewData.Similarity = 0;

            pbStatus.Value = 0;

            ViewData.CalculationEnable = false;
            ViewData.Cancellable = true;

            bool sameImages = ViewData.ImagePath1 == ViewData.ImagePath2;

            //загружаем выбранные картинки
            var imgs = new List<ImageMinInfo>{
                new ImageMinInfo{
                    Data = File.ReadAllBytes(ViewData.ImagePath1),
                    Name = Path.GetFileName(ViewData.ImagePath1)
                } 
            };
            if (!sameImages) imgs.Add(
                new ImageMinInfo
                {
                    Data = File.ReadAllBytes(ViewData.ImagePath2),
                    Name = Path.GetFileName(ViewData.ImagePath2)
                }
            );


            var imageIDs = await service.PostImages(imgs, token);

            if (imageIDs == null)
            {
                MessageBox.Show("Calculations are cancaled");
                return;
            }
            pbStatus.Value = 49;

            var distAndSim = await service.GetCompare(imageIDs[0], sameImages ? imageIDs[0] : imageIDs[1]);
            if (distAndSim == null)
            {
                {
                    MessageBox.Show("cant get dist and sim from server");
                    return;
                }
            }
            ViewData.Distance = distAndSim.Distance;
            ViewData.Similarity = distAndSim.Similarity;
            pbStatus.Value = 100;
            ViewData.Cancellable = false;

        }

        private void CancelCalculationsClick(object sender, RoutedEventArgs e)
        {
            token_src.Cancel();
        }

        private async void OpenDatabaseClick(object sender, RoutedEventArgs e)
        {
            DatabaseWindow windowDatabase = new DatabaseWindow(service);
            windowDatabase.Show();
        }
    }

    public class NumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                var v = (float)value;
                return v == -1 ? "Canceled" : v.ToString();
            }
            catch
            {
                return "ConvertationError";
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}



