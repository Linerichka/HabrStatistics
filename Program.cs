using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ParquetSharp;
using ParquetSharp.IO;
using Microsoft.ML;

namespace HabrMetaAnalysis;

class Program
{
    public const string DataPath = @"\Res\";
    public const int StartArticleId = 753292; 
    public const int EndArticleId = 870084;   
    const int WorkerCount = 5;

    static string[] DataBase = new[]
    {
        @"\train-00000-of-00005.parquet",
        @"\train-00001-of-00005.parquet",
        @"\train-00002-of-00005.parquet",
        @"\train-00003-of-00005.parquet",
        @"\train-00004-of-00005.parquet",
    };

    const string ModelPath = @"Res\ModelSdca";

    static MLContext MlContext
    {
        get
        {
            if (_mlContext == null) _mlContext = new MLContext();
            return _mlContext;
        }
    }

    static MLContext _mlContext;

    public static PredictionEngine<Input, OutPut> PredictionEngine
    {
        get
        {
            if (_predictionEngine == null || TrainedModel == null)
            {
                TrainedModel = MlContext.Model.Load(ModelPath, out var _);
                _predictionEngine = MlContext.Model.CreatePredictionEngine<Input, OutPut>(TrainedModel);
            }

            return _predictionEngine;
        }
    }
    static PredictionEngine<Input, OutPut> _predictionEngine;
    public static ITransformer TrainedModel;
    
    
    static void Main(string[] args)
    {
        //DownloadArticles();
        //TrainModel();
        
        
        var a = new ArticlesManager();
        a.Load();
        //a.CalculateArticleTimeInDraft();
        //a.Save();
        //a.CreateAndSave(force: false);
        a.SaveToExcel();
        var analysis = new Analysis();
        analysis.Run();
        
        
        //CheckAccuracy();
        return;
    }

    public static void DownloadArticles()
    {
        List<Task> tasks = new List<Task>(WorkerCount);
        
        {
            var lastStartArticleId = StartArticleId;
            var countToWorker = (EndArticleId - StartArticleId) / WorkerCount;
            int i = 0;
            while (lastStartArticleId < EndArticleId)
            {
                var endArticleId = lastStartArticleId + countToWorker;
                var downloader = new ArticlesDownloader(lastStartArticleId, endArticleId, i);
                tasks.Add(Task.Run(() => downloader.DownloadNewArticle()));
                i++;
                lastStartArticleId += countToWorker;
            }
        }
        Task.WaitAll(tasks.ToArray());
    }
    
    public class Input
    {
        public string Text;
    }
    public class OutPut
    {
        public float Score;
    }

    public class TrainInput
    {
        public float Label;
        public string Text;
    }
    
    private static void TrainModel()
    {
        if (File.Exists(ModelPath))
        {
            return;
        }

        ParquetFileReader[] readers = new ParquetFileReader[DataBase.Length];

        for (int i = 0; i < DataBase.Length; i++)
        {
            readers[i] = new ParquetFileReader(DataBase[i]);
        }

        
        var pipeline = MlContext.Transforms.Text.FeaturizeText("Features", nameof(TrainInput.Text))
            .Append(MlContext.Regression.Trainers.Sdca(labelColumnName: "Label", featureColumnName: "Features"));

        var rowGroups = readers[0].FileMetaData.NumRowGroups;
        List<TrainInput> trainInputs = new(65536);
        for (int rowGroup = 0; rowGroup < rowGroups; ++rowGroup)
        {
            Console.WriteLine($"Completed group '{rowGroup}' of '{rowGroups}'. Percent: '{((float)rowGroup) / ((float)rowGroups) * 100f}'");
            foreach (var r in readers)
            {
                using var rowGroupReader = r.RowGroup(rowGroup);
                var groupNumRows = checked((int)rowGroupReader.MetaData.NumRows);

                var texts = rowGroupReader.Column(0).LogicalReader<string>().ReadAll(groupNumRows);
                var isGens = rowGroupReader.Column(1).LogicalReader<long?>().ReadAll(groupNumRows);

                for (int i = 0; i < groupNumRows; i++)
                {
                    var text = texts[i];
                    bool isGen = isGens[i] == 1;

                    if (isGen)
                    {
                        trainInputs.Add(new TrainInput { Text = text, Label = 255f  });
                    }
                    else
                    {
                        trainInputs.Add(new TrainInput { Text = text, Label = 0f  });
                    }
                }
            }
            
        }
        
        var trainData = MlContext.Data.LoadFromEnumerable(trainInputs);
        var model = pipeline.Fit(trainData);
        MlContext.Model.Save(model, trainData.Schema, ModelPath);
        trainInputs.Clear();

        Console.WriteLine("Model trained and saved successfully.");

        foreach (var reader in readers)
        {
            reader.Close();
        }
        
    }

    private static List<TrainInput> TakeRandomTrainData()
    {
        ParquetFileReader[] readers = new ParquetFileReader[DataBase.Length];

        for (int i = 0; i < DataBase.Length; i++)
        {
            readers[i] = new ParquetFileReader(DataBase[i]);
        }
        

        var rowGroups = readers[0].FileMetaData.NumRowGroups;
        List<TrainInput> trainInputs = new(512);
        for (int rowGroup = 0; rowGroup < 10; ++rowGroup)
        {
            foreach (var r in readers)
            {
                using var rowGroupReader = r.RowGroup(rowGroup);
                var groupNumRows = checked((int)rowGroupReader.MetaData.NumRows);

                var texts = rowGroupReader.Column(0).LogicalReader<string>().ReadAll(groupNumRows);
                var isGens = rowGroupReader.Column(1).LogicalReader<long?>().ReadAll(groupNumRows);

                for (int i = 0; i < groupNumRows; i++)
                {
                    var text = texts[i];
                    bool isGen = isGens[i] == 1;

                    if (isGen)
                    {
                        trainInputs.Add(new TrainInput { Text = text, Label = 255f });
                    }
                    else
                    {
                       trainInputs.Add(new TrainInput { Text = text, Label = 0f  });
                    }
                }
            }
            
        }
        

        foreach (var reader in readers)
        {
            reader.Close();
        }
        return trainInputs;
    }

    private static void CheckAccuracy()
    {
        var trainData = MlContext.Data.LoadFromEnumerable(TakeRandomTrainData());
        var predictions = TrainedModel.Transform(trainData);
        
        var metrics = MlContext.Regression.Evaluate(predictions, labelColumnName: nameof(TrainInput.Label));

        Console.WriteLine($"R^2: {metrics.RSquared:0.###}");
        Console.WriteLine($"RMSE: {metrics.RootMeanSquaredError:0.###}");
        Console.WriteLine($"MAE: {metrics.MeanAbsoluteError:0.###}");
        Console.WriteLine($"MSE: {metrics.MeanSquaredError:0.###}");
    }
}