
using System.Security.Cryptography;

namespace Contracts
{
    //само изображение
    public class ImageDetails
    {
        public static string GetHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(data).Select(x => x.ToString("X2")));
            }
        }

        public int Id { get; set; }
        public byte[] Data { get; set; }
    }
}

