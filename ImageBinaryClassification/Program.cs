using System;
using System.IO;
using System.Linq;
using ImageBinaryClassificationML.Model;


namespace ImageBinaryClassification
{
    class Program
    {
        static void Main(string[] args)
        {
            // args[0] - path to the model ZIP-file
            // args[1] - path to the directory containing test set images

            if (args.Length < 2)
                throw new Exception($"ERROR: Too few command-line arguments ({args.Length})!");

            var model_path = args[0];
            var testset_directory = args[1];

            var files = Directory.EnumerateFiles(testset_directory, "*.jpg", SearchOption.AllDirectories).ToArray();
            using(var prediction_engine = ConsumeModel.CreatePredictionEngine(model_path))
            {
                foreach (var file in files)
                {
                    var input = new ModelInput() { ImageSource = file };
                    var prediction = prediction_engine.Predict(input);
                    Console.WriteLine($"{file.Replace(testset_directory, "")} --> {prediction.Prediction} ({prediction.Score[0]:F2} / {prediction.Score[1]:F2})");
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
