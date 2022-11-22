using ArcFaceComponent;

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
                var tensor_img1 = Component.GetTensorFromImage("./face1.png");
                var img1 = await solution.GetEmbeddings(tensor_img1, token);

                var tensor_img2 = Component.GetTensorFromImage("./face2.png");
                var img2 = await solution.GetEmbeddings(tensor_img2, token);
                return $"distance = {solution.Distance(img1, img2)}\nsimilarity = {solution.Similarity(img1, img2)}";
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