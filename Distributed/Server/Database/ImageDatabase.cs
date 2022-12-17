using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Contracts;


using System.Linq;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;
using System.Net.NetworkInformation;
using SixLabors.ImageSharp;

namespace Server.Database
{
    // база данных
    public class ImagesContext : DbContext
    {
        public DbSet<ImageInfo> ImagesInfo { get; set; }
        public DbSet<ImageDetails> ImagesDetails { get; set; }

        public ImagesContext() => Database.EnsureCreated();

        protected override void OnConfiguring(DbContextOptionsBuilder o)
        {
            o.UseSqlite("Data Source=images.db");
        }
    }


    //интерфейс
    public interface IImageDb
    {
        public Task<int[]> PostImages(List<ImageMinInfo> imgs, CancellationToken token);
        public Task<Metrics> GetCompareImages(int id1, int id2);
        public Task<List<ImageInfo>> GetImages();
        public Task<bool> DeleteAllImages();
        public Task<bool> DeleteImage(int id);
    }


    public class VinylImageDb : IImageDb
    {
        public async Task<int[]> PostImages(List<ImageMinInfo> imgs, CancellationToken token)
        {

            using (var arcFaceModel = new ArcFaceComponent.Component())
            {
                var imgsBytes = imgs.Select(x => x.Data).ToList();
                var imgsNames = imgs.Select(x => x.Name).ToList();

                //получаем данные из базы + запускаем таски если не нашли данные
                var tasksAndBoolAr = launchTasks(imgsBytes, arcFaceModel, token);
                var tasks = tasksAndBoolAr.Item1.ToList();
                var indexingArray = new List<Task<float[]>>(tasks);
                var imageWasInDbBoolAr = tasksAndBoolAr.Item2;

                while (tasks.Any())
                {
                    //если вернулся - либо закончил - либо отменился
                    var finishedTask = await Task.WhenAny(tasks);
                    var imageIdx = indexingArray.IndexOf(finishedTask);
                    var calcResult = await finishedTask;

                    if (token.IsCancellationRequested)
                    {
                        tasks.Clear();
                        return Array.Empty<int>();
                    }
                    else
                    {
                        if (!imageWasInDbBoolAr[imageIdx])
                        {
                            //записываем результат в базу
                            using (var db = new ImagesContext())
                            {
                                var emb = new byte[calcResult.Length * 4];
                                Buffer.BlockCopy(calcResult, 0, emb, 0, emb.Length);

                                var newImageDetails = new ImageDetails { Data = imgsBytes[imageIdx] };
                                ImageInfo newImage = new ImageInfo
                                {
                                    Name = imgsNames[imageIdx],
                                    Embedding = emb,
                                    Details = newImageDetails,
                                    Hash = ImageDetails.GetHash(newImageDetails.Data),
                                };
                                db.Add(newImage);
                                db.SaveChanges();
                            }
                        }
                    }
                    tasks.Remove(finishedTask);
                }

                return imgsBytes.Select(img =>
                {
                    var hash = ImageDetails.GetHash(img);
                    using (var db = new ImagesContext())
                    {
                        return db.ImagesInfo
                        .Where(info => info.Hash == hash)
                        .Select(info => info.Id)
                        .First();
                    }
                }).ToArray();
            }
        }


        public Task<Metrics> GetCompareImages(int id1, int id2)
        {
            return Task.Factory.StartNew(() =>
            {
                using (var db = new ImagesContext())
                {
                    var emb1Bytes = db.ImagesInfo
                    .Where(info => info.Id == id1)
                    .Select(info => info.Embedding)
                    .First();

                    var emb2Bytes = db.ImagesInfo
                    .Where(info => info.Id == id2)
                    .Select(info => info.Embedding)
                    .First();

                    if (emb1Bytes == null || emb2Bytes == null) throw new ArgumentOutOfRangeException("unexisting ids");

                    var emb1 = convertBytesToFLoats(emb1Bytes);
                    var emb2 = convertBytesToFLoats(emb2Bytes);

                    var dist = ArcFaceComponent.Component.Distance(emb1, emb2);
                    var sim = ArcFaceComponent.Component.Similarity(emb1, emb2);

                    
                    return new Metrics() { Distance = dist, Similarity = sim };
        }
            });
            
        }


        public Task<List<ImageInfo>> GetImages()
        {
            return Task.Factory.StartNew(() =>
            {
                using (var db = new ImagesContext())
                {
                    return db.ImagesInfo
                    .Include(x => x.Details)
                    .Select(x => x).ToList();
                }
            });
        }

        public Task<bool> DeleteAllImages()
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    using (var db = new ImagesContext())
                    {
                        foreach (var image in db.ImagesDetails)
                            db.ImagesDetails.Remove(image);

                        foreach (var image in db.ImagesInfo)
                            db.ImagesInfo.Remove(image);

                        db.SaveChanges();
                        return true;
                    }
                }
                catch
                {
                    return false;
                } 
            });
        }


        public Task<bool> DeleteImage(int id)
        {
            return Task.Factory.StartNew(() =>
            {
                
                using (var db = new ImagesContext())
                {
                    var imageToDelete = db.ImagesInfo.Where(x => x.Id == id).Include(x => x.Details).First();
                    if (imageToDelete == null) return false;

                    db.ImagesDetails.Remove(imageToDelete.Details);
                    db.ImagesInfo.Remove(imageToDelete);
                    db.SaveChanges();
                    return true;
                }
                
                
            });
        }


        private float[] convertBytesToFLoats(byte[] arr)
        {
            var f = new float[arr.Length / 4];
            Buffer.BlockCopy(arr, 0, f, 0, arr.Length);
            return f;
        }


        private ImageInfo[] getImagesFromDb(List<byte[]> imagesBytes)
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
                        imagesFromDb[i] = q.First();
                    }
                }
            }
            return imagesFromDb;
        }

        //true в соответсвующей клетке bool[] если изображение из базы, false если нет
        private (Task<float[]>[], bool[]) launchTasks(
            List<byte[]> imagesBytes,
            ArcFaceComponent.Component arcFaceModel,
            CancellationToken token)
        {
            //ищем картинки в базе
            var imagesFromDb = getImagesFromDb(imagesBytes);
            var imageFromDbBoolAr = imagesFromDb.Select(x => x != null).ToArray();

            var tasks = imagesFromDb.Zip(imagesBytes, (imageInfo, imageBytes) =>
            {
                if (imageInfo != null) //изображение было в базе
                {
                    var imgEmbeddings = new float[imageInfo.Embedding.Length / 4];
                    Buffer.BlockCopy(imageInfo.Embedding, 0, imgEmbeddings, 0, imageInfo.Embedding.Length);
                    return Task<float[]>.FromResult(imgEmbeddings);
                }
                else
                {
                    //получаем тензор
                    var image_tensor = ArcFaceComponent.Component.GetTensorFromImage(
                        SixLabors.ImageSharp.Image.Load<Rgb24>(imageBytes)
                    );

                    //запускаем асинхронные таски
                    return arcFaceModel.GetEmbeddings(image_tensor, token);
                }
            }
            ).ToArray();
            return (tasks, imageFromDbBoolAr);

        }


  
    }
}
