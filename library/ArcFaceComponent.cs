using System;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ArcFaceComponent;

public class Component : IDisposable
{
    private static readonly string ARCFACE_MODEL_RESOURCE = "library.arcfaceresnet100-8.onnx";

    //for work with NN
    private InferenceSession session;

    //constructor
    public Component()
    {
        //load model from EmbeddedResource
        var assembly = typeof(Component).Assembly;
        using var modelStream = assembly.GetManifestResourceStream(ARCFACE_MODEL_RESOURCE);
        if (modelStream == null)
            throw new Exception("embedded resource is not loaded");

        //create session
        using var memoryStream = new MemoryStream();
        modelStream.CopyTo(memoryStream);

        session = new InferenceSession(memoryStream.ToArray());

        if (session == null)
            throw new Exception("session is null");
    }

    public float Distance(float[] v1, float[] v2) => Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());

    public float Similarity(float[] v1, float[] v2) => v1.Zip(v2).Select(p => p.First * p.Second).Sum();

    public static DenseTensor<float> GetTensorFromImage(string img_path) => ImageTransformer.ImageToTensor(img_path);

    //utils
    float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x * x).Sum());

    float[] Normalize(float[] v)
    {
        var len = Length(v);
        return v.Select(x => x / len).ToArray();
    }


    public async Task<float[]> GetEmbeddings(DenseTensor<float> image, CancellationToken token)
    {
        return await Task<float[]>.Factory.StartNew(() =>
        {
            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("data", image) };

            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            //common object session
            //IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = null;
            lock (session)
            {
                Console.WriteLine($"{image} take model");
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = session.Run(inputs);

                Console.WriteLine($"{image} release model");
                return Normalize(outputs.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray());
            }



        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void Dispose()
    {
        session.Dispose();
    }


}
