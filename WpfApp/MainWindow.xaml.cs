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
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
namespace WpfApp
{
	public partial class MainWindow : Window
	{
		//данные для отображения
		public ViewModel ViewData = new();

		//нейронка
		public ArcFaceComponent.Component ArcFaceModel = new();

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
            get {
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

		private ImageInfo[] GetImagesFromDb(List<byte[]> imagesBytes)
		{
            //получаем хеши
            var hashes = new string[imagesBytes.Count];
            for (int i = 0; i < hashes.Length; i++)
                hashes[i] = ImageDetails.GetHash(imagesBytes[i]);

            //ищем в базе по хешу
            var imagesFromDb = new ImageInfo[hashes.Length];
            using (var db = new ImagesContext())
            {
                for (int i = 0; i < hashes.Length; i++)
                {
                    var q = db.ImagesInfo
                    .Where(x => x.Hash == hashes[i])
                    .Include(x => x.Details)
                    .Where(x => Equals(x.Details.Data, imagesBytes[i]));

                    if (q.Any())
                    {
                        //MessageBox.Show("image from db");
                        imagesFromDb[i] = q.First();
                    }
                }
            }
			return imagesFromDb;
        }

		private void LaunchTasks(
            ref float[][] imgEmbeddings,
            ref List<Task<Task<(float[], int)>>> tasks,
            List<byte[]> imagesBytes)
		{
			//ищем картинки в базе
            var imagesFromDb = GetImagesFromDb(imagesBytes); 

            for (int i = 0; i < imagesBytes.Count; i++)
            {
                if (imagesFromDb[i] != null) //изображение было в базе
                {
                    imgEmbeddings[i] = new float[imagesFromDb[i].Embedding.Length / 4];
                    Buffer.BlockCopy(imagesFromDb[i].Embedding, 0, imgEmbeddings[i], 0, imagesFromDb[i].Embedding.Length);
                }
                else
                {
                    //получаем тензор
                    var image_tensor = ArcFaceComponent.Component.GetTensorFromImage(
                        SixLabors.ImageSharp.Image.Load<Rgb24>(imagesBytes[i])
                    );

                    //запускаем асинхронные таски
                    var idx = i;
                    var img_embd = Task.Factory.StartNew(async () =>
                    {
                        var emb = await ArcFaceModel.GetEmbeddings(image_tensor, token);
                        return (emb, idx);
                    });

                    //складываем в список для отслеживания завершения
                    tasks.Add(img_embd);
                }
                
            }
        }

        private async void CalculateClick(object sender, RoutedEventArgs e)
        {
            if (ViewData.CalculationEnable && AllParameteresValid)
            {
				ResetToken();
				ViewData.Distance = 0;
				ViewData.Similarity = 0;

				pbStatus.Value = 0;

                ViewData.CalculationEnable = false;
                ViewData.Cancellable = true;

                bool sameImages = ViewData.ImagePath1 == ViewData.ImagePath2;

                //загружаем выбранные картинки
                var imagesBytes = new List<byte[]>{
                    File.ReadAllBytes(ViewData.ImagePath1),
                };
                if (!sameImages) imagesBytes.Add(File.ReadAllBytes(ViewData.ImagePath2));

                var imgEmbeddings = new float[imagesBytes.Count][];
				var tasks = new List<Task<Task<(float[], int)>>>();

                //получаем данные из базы + запускаем таски если не нашли данные
				LaunchTasks(ref imgEmbeddings, ref tasks, imagesBytes);
                
				while (tasks.Count > 0)
				{
					//если вернулся - либо закончил - либо отменился
                    var finished = await Task.WhenAny(tasks);
					var calcResult = await await finished;
					var imageIdx = calcResult.Item2;

                    if (token.IsCancellationRequested)
					{
						tasks.Clear();
                        pbStatus.Value = 0;
                        ViewData.Distance = -1;
						ViewData.Similarity = -1;
						MessageBox.Show("All calculations canceled");
						break;
					}
					else 
					{
						imgEmbeddings[imageIdx] = calcResult.Item1;

                        //записываем результат в базу
                        using (var db = new ImagesContext())           
                        {
                            var byteArray = new byte[calcResult.Item1.Length * 4];
                            Buffer.BlockCopy(imgEmbeddings[imageIdx], 0, byteArray, 0, byteArray.Length);

                            var newImageDetails = new ImageDetails { Data = imagesBytes[imageIdx] };
							ImageInfo newImage = new ImageInfo
							{
								Name = (imageIdx == 0) ? Path.GetFileName(ViewData.ImagePath1) : Path.GetFileName(ViewData.ImagePath2),
                                Embedding = byteArray,
                                Details = newImageDetails,
                                Hash = ImageDetails.GetHash(newImageDetails.Data),
                            };
                            db.Add(newImage);
                            db.SaveChanges();
                        }
                        pbStatus.Value += 49;
					}
                    tasks.Remove(finished);
                }

                //посчитать метрики
                await Task.Factory.StartNew(() =>
				{
                    ViewData.Distance = ArcFaceModel.Distance(imgEmbeddings[0], sameImages ? imgEmbeddings[0] : imgEmbeddings[1]);
                    ViewData.Similarity = ArcFaceModel.Similarity(imgEmbeddings[0], sameImages ? imgEmbeddings[0] : imgEmbeddings[1]);
				});

                pbStatus.Value = 100;
                ViewData.Cancellable = false; 
            }
        }

        private void CancelCalculationsClick(object sender, RoutedEventArgs e)
        {
			token_src.Cancel();
        }

        private void OpenDatabaseClick(object sender, RoutedEventArgs e)
        {
            DatabaseWindow windowDatabase = new DatabaseWindow();
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
	

	