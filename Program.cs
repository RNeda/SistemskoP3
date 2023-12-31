﻿/*
Koristeći principe Reaktivnog programiranja i Yelp API, 
implementirati aplikaciju za analizu komentara za teretane za dati cenovni rang (price parametar). 
Za prikupljene komentare implementirati Topic Modeling uz pomoć OpenNLP biblioteke 
(koristiti C# verziju) illi SharpEntropy biblioteke. 
Prikazati dobijene rezultate.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpEntropy;
using Microsoft.ML;
using System.Diagnostics.Eventing.Reader;





public class TextData
{
    public string Text { get; set; }
}

public class TransformedTextData : TextData
{
    public float[] Features { get; set; }
}
class LatentDirichletAllocation
{
    private static ConcurrentBag<string> _comments = new ConcurrentBag<string>();
    private static int count = 0;
    public static PredictionEngine<TextData, TransformedTextData> PredictionEngine { get; set; }

    public static void ProccessData(ConcurrentBag<string> coms)
    {
        _comments.Clear();
        _comments = coms;
        count = _comments.Count;

    }
    public static void RunAnalysis(int topicNum)
    {
        var mlContext = new MLContext();

        var samples = new List<TextData>();
        for (int i = 0; i < count / 2; i++)
        {
            samples.Add(new TextData() { Text = _comments.ElementAt(i) });
        }

        var dataview = mlContext.Data.LoadFromEnumerable(samples);

        var pipeline = mlContext.Transforms.Text.NormalizeText("NormalizedText",
            "Text");
        /*.Append(mlContext.Transforms.Text.TokenizeIntoWords("Tokens",
            "NormalizedText"))
        .Append(mlContext.Transforms.Text.RemoveDefaultStopWords("Tokens"))
        .Append(mlContext.Transforms.Conversion.MapValueToKey("Tokens"))
        .Append(mlContext.Transforms.Text.ProduceNgrams("Tokens"))
        .Append(mlContext.Transforms.Text.LatentDirichletAllocation(
            "Features", "Tokens", numberOfTopics: topicNum));*/

        var transformer = pipeline.Fit(dataview);

        var predictionEngine = mlContext.Model.CreatePredictionEngine<TextData,
            TransformedTextData>(transformer);
        PredictionEngine = predictionEngine;
    }
    public static string GetPrediction(string text)
    {
        var prediction = PredictionEngine.Predict(new TextData() { Text = text });
        return PrintLdaFeatures(prediction);
    }

    private static string PrintLdaFeatures(TransformedTextData prediction)
    {
        string result = "";
        for (int i = 0; i < prediction.Features.Length; i++)
        {
            Console.Write($"{prediction.Features[i]:F4}  ");
            result += $"{prediction.Features[i]:F4}  ";
        }
        result += "\n";
        Console.WriteLine();
        return result;
    }
}


public class Gym
{
    public string id { get; set; }
    public string Naziv { get; set; }

    public Gym(string id, string naziv)
    {
        this.id = id;
        this.Naziv = naziv;
    }
}

public class GymStream : IObservable<Gym>
{
    public readonly Subject<Gym> gymSubject;
    public GymStream()
    {
        gymSubject = new Subject<Gym>();
    }
    public void GetGyms(int price)
    {
        string apiKey = "N_97QUgL7k9LRspAVMwPHKHH3gSOfg5usV-IYTOTVtdQEK2mwKEAalxnFj6dAfYdq6r74jvhN4c86EuezprGmmfKduOyV1GBXl5btQnsxqAbF8Mb-oO_TtHfQlObZHYx";
        HttpClient client = new HttpClient();
        var url = $"https://api.yelp.com/v3/businesses/search?categories=gyms&location=San Francisco, CA&price={price}";
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                JObject jsonResponse = JObject.Parse(content);
                JArray teretane = (JArray)jsonResponse["businesses"]!;

                foreach (var t in teretane)
                {
                    var id = t["id"]!.ToString();
                    var naziv = t["name"]!.ToString();
                    var novaTeretana = new Gym(id, naziv);
                    Console.WriteLine($"Teretana:  {naziv}");
                    HttpResponseMessage reviewResponse = await client.GetAsync($"https://api.yelp.com/v3/businesses/{id}/reviews");
                    string reviewContent = await reviewResponse.Content.ReadAsStringAsync();

                    JObject reviewJsonResponse = JObject.Parse(reviewContent);
                    JArray reviews = (JArray)reviewJsonResponse["reviews"]!;

                    foreach (var r in reviews)
                    {
                        var text = r["text"]!.ToString();
                        Console.WriteLine(text);
                        Console.WriteLine("\n");
                    }
                }
                gymSubject.OnCompleted();
            }
            catch (Exception ex)
            {
                gymSubject.OnError(ex);
            }
        });
    }

    public IDisposable Subscribe(IObserver<Gym> observer)
    {
        return gymSubject.Subscribe(observer);
    }
}
public class GymObserver : IObserver<Gym>
{
    private readonly string name;
    private ConcurrentBag<Task> _tasks = new ConcurrentBag<Task>();
    private ConcurrentBag<string> _comments = new ConcurrentBag<string>();

    private ISubject<string> _subject;
    private object _lock = new object();
    public GymObserver(string name)
    {
        this.name = name;
    }
    public void OnNext(Gym teretana)
    {
        Console.WriteLine($"{name}: {teretana.Naziv}!");
    }
    public void OnError(Exception e)
    {
        Console.WriteLine($"{name}: Doslo je do greske: {e.Message}");
    }
    public void OnCompleted()
    {
        Console.WriteLine($"{name}: Uspesno vraceni svi komentari.");
        DoTopicModeling();
    }

    private void DoTopicModeling()
    {
        try
        {
            Console.WriteLine($"Topic modeling for {name}:\n");
            LatentDirichletAllocation.ProccessData(_comments);
            LatentDirichletAllocation.RunAnalysis(4);

            foreach (var comment in _comments)
            {
                string topic = LatentDirichletAllocation.GetPrediction(comment);
                _subject.OnNext(topic);
            }
        }
        catch (Exception e)
        {
            lock (_lock)
            {
                Console.WriteLine(e.Message + "in analysis");
                _subject.OnError(e);
            }
        }
        _subject.OnCompleted();

    }
}

public class Program
{
    public static void Main()
    {
        //kreiramo stream
        var gymStream = new GymStream();

        //dodajemo nekoliko observera
        var observer1 = new GymObserver("Observer 1");
        var observer2 = new GymObserver("Observer 2");
        var observer3 = new GymObserver("Observer 3");

        var strim = gymStream;
        var subscription1 = strim.Subscribe(observer1);
        var subscription2 = strim.Subscribe(observer2);
        var subscription3 = strim.Subscribe(observer3);


        int price;
        Console.WriteLine("Enter your wanted price range:");
        price = Int32.Parse(Console.ReadLine());
        gymStream.GetGyms(price);
        Console.ReadLine();

        subscription1.Dispose();
        subscription2.Dispose();
        subscription3.Dispose();


    }
}
