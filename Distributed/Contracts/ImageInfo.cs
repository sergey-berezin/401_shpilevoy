

namespace Contracts
{
    //информация про изображение
    public class ImageInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string Hash { get; set; }
        public byte[] Embedding { get; set; }

        public ImageDetails Details { get; set; } //reference navigation property
    }

}


