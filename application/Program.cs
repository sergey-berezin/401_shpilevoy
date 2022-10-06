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

                var img1 = await solution.GetEmbeddings("./face1.png", token);
                var img2 = await solution.GetEmbeddings("./face2.png", token);
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