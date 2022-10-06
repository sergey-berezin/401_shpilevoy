using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ArcFaceComponent;


public static class ImageTransformer
{
    private static readonly int TargetWidth = 112;
    private static readonly int TargetHeight = 112;

    static public DenseTensor<float> ImageToTensor(string img_path)
    {
        var img = Image.Load<Rgb24>(img_path);
        img.Mutate(x =>
        {
            x.Resize(new ResizeOptions
            {
                Size = new Size(TargetWidth, TargetHeight),
                Mode = ResizeMode.Crop // Сохраняем пропорции обрезая лишнее
            });
        });

        var w = img.Width;
        var h = img.Height;
        var t = new DenseTensor<float>(new[] { 1, 3, h, w });

        img.ProcessPixelRows(pa =>
        {
            for (int y = 0; y < h; y++)
            {
                Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    t[0, 0, y, x] = pixelSpan[x].R;
                    t[0, 1, y, x] = pixelSpan[x].G;
                    t[0, 2, y, x] = pixelSpan[x].B;
                }
            }
        });

        return t;
    }
}