using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Contracts;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace WpfClient
{
    public partial class DatabaseWindow : Window
    {
        //список информации про изображения
        public ObservableCollection<ImageInfo> ImagesCollection { get; } = new();
        public Service service;
        public DatabaseWindow(Service s)
        {
            service = s;
            ImagesCollection.CollectionChanged += delegate (object? sender, NotifyCollectionChangedEventArgs e) { RaisePropertyChanged(nameof(ImagesCollection)); };

            renderImages();

            InitializeComponent();
            this.DataContext = this;

            
        }

        private async void renderImages()
        {
            
            var imgs = await service.GetAllImages();
            foreach (var imageInfo in imgs)
                ImagesCollection.Add(imageInfo);
            
            return;
                
        }

        private async void DeleteImageClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var imageInfo = ImagesCollection[ImagesCollectionListBox.SelectedIndex];
                var res = await service.DeleteImage(imageInfo.Id);
                if (res == null)
                {
                    MessageBox.Show("Error");
                    return;
                }

                if (!res)
                {
                    MessageBox.Show("Image wasnt delete");
                    return;
                }
                ImagesCollection.Remove(imageInfo);

            }
            catch (Exception e1)
            {
                if (ImagesCollectionListBox.SelectedIndex == -1)
                    MessageBox.Show("Select image to delete");
                else MessageBox.Show(e1.Message);
            }
        }

        private async void DeleteAllImagesClick(object sender, RoutedEventArgs e)
        {
            try
            {
                
                var res = await service.DeleteAllImages();
                if (res == null)
                {
                    MessageBox.Show("Error");
                    return;
                }

                if (!res)
                {
                    MessageBox.Show("Images weren't delete");
                    return;
                }
                ImagesCollection.Clear();

            }
            catch (Exception e1)
            {
                MessageBox.Show(e1.Message);
            }
        }

        private void ListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImagesCollectionListBox.SelectedIndex < 0) DeleteFromDbButton.IsEnabled = false;
            else DeleteFromDbButton.IsEnabled = true;
        }

        //событие изменения данных
        public event PropertyChangedEventHandler? PropertyChanged;
        public void RaisePropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void ImagesCollectionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}

