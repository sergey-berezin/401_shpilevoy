using ArcFaceComponent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

//sync
try
{
    var token = new CancellationTokenSource().Token;
    using (var solution = new Component())
    {
        var t =
            Enumerable
            .Range(0, 5)
            .Select(async i =>
            {
                var img1 = Image.Load<Rgb24>("./face1.png");
                var tensor_img1 = Component.GetTensorFromImage(img1);
                var img1_emb = await solution.GetEmbeddings(tensor_img1, token);

                var img2 = Image.Load<Rgb24>("./face2.png");
                var tensor_img2 = Component.GetTensorFromImage(img2);
                var img2_emb = await solution.GetEmbeddings(tensor_img2, token);
                return $"distance = {solution.Distance(img1_emb, img2_emb)}\nsimilarity = {solution.Similarity(img1_emb, img2_emb)}";
            })
            .ToArray();

        var wait = await Task.WhenAll(t);
        foreach (var str in wait)
        {
            Console.WriteLine(str);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}