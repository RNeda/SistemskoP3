﻿/*
Koristeći principe Reaktivnog programiranja i Yelp API, 
implementirati aplikaciju za analizu komentara za teretane za dati cenovni rang (price parametar). 
Za prikupljene komentare implementirati Topic Modeling uz pomoć OpenNLP biblioteke 
(koristiti C# verziju) illi SharpEntropy biblioteke. 
Prikazati dobijene rezultate.
 */




using System;
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
using Yelp.Api;
using Yelp.Api.Models;
using OpenNLP.Tools.Util;
using OpenNLP.Tools.SentenceDetect;
using OpenNLP.Tools.Tokenize;
using OpenNLP;
//using SharpEntropy.IO;
using System.Diagnostics.Eventing.Reader;


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
    public async Task GetGyms(int price)
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
                //Console.WriteLine($"{teretane}");
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
                    //Console.WriteLine("komentari: " + reviews);
                    /*List<string> komentari = new List<string>();
                    foreach (var r in reviews)
                    {

                        var text = r["text"]!.ToString();
                        komentari.Add(text);
                        Console.WriteLine(text);
                    }
                    Console.WriteLine("");


                    
                }
                gymSubject.OnCompleted();*/
                    List<string> komentari = new List<string>();
                    foreach (var r in reviews)
                    {
                        var text = r["text"]!.ToString();
                        komentari.Add(text);
                        Console.WriteLine(text);
                    }

                    // Perform topic modeling
                    
                    if (komentari.Count > 0)
                    {
                        var trainer = new GisTrainer();

                        // Create a training event reader
                        var trainingData = new SharpEntropy.TrainingEvent[komentari.Count];
                        //var komarray = komentari.ToArray();
                        for (int i = 0; i < komentari.Count; i++)
                        {
                            string[] niz = komentari[i].Split(' ');
                            trainingData[i] = new SharpEntropy.TrainingEvent("topic", niz);
                        }
                        var reader = new TrainingEventReader(trainingData);


                        // Train the model
                        var model = trainer.TrainModel(reader);

                        // Get the topics for the comments
                        var topics = model.GetBestOutcomeLabels(komentari.ToArray());

                        // Print the topics
                        Console.WriteLine("Topics:");
                        for (int i = 0; i < komentari.Count; i++)
                        {
                            Console.WriteLine($"{komentari[i]} - {topics[i]}");
                        }
                    }
                    Console.WriteLine("");
                }

                gymSubject.OnCompleted();



            }
            catch (Exception ex)
            {
                gymSubject.OnError(ex);
            }
        });
    }
    /*
    List<string> topics = PerformTopicModeling(komentari);
    Console.WriteLine($"Topics: {string.Join(", ", topics)}");

    Console.WriteLine("");
}

gymSubject.OnCompleted();



}
catch (Exception ex)
{
gymSubject.OnError(ex);
}
});
}

private List<string> PerformTopicModeling(List<string> comments)
{
List<string> topics = new List<string>();

// Sentence detection
EnglishMaximumEntropySentenceDetector sentenceDetector = new EnglishMaximumEntropySentenceDetector(""); //nzm da li je dobro da se prosledi ""
List<string[]> sentences = new List<string[]>();
foreach (var comment in comments)
{
string[] sentenceTokens = sentenceDetector.SentenceDetect(comment);
sentences.Add(sentenceTokens);
}

// Tokenization
EnglishMaximumEntropyTokenizer tokenizer = new EnglishMaximumEntropyTokenizer("");
List<string[]> tokens = new List<string[]>();
foreach (var sentence in sentences)
{
string[] sentenceTokens = sentence;
for (int i = 0; i < sentenceTokens.Length; i++)
{
sentenceTokens[i] = tokenizer.Tokenize(sentenceTokens[i]);
}
tokens.Add(sentenceTokens);
}

// Topic modeling
EventStream eventStream = new BasicEventStream(tokens);
ITrainingDataReader reader = new PlainTextByLineDataReader(eventStream);
ITrainingData trainingData = new BasicTrainingData(reader);
IMaxentModel model = new SharpEntropy.GisModel(new SharpEntropy.IO.BinaryGisModelReader("EnglishSD.nbin"));
IMaxentTagger tagger = new MaxentTagger(model);

foreach (var sentenceTokens in tokens)
{
string[] tags = tagger.Tag(sentenceTokens);
for (int i = 0; i < tags.Length; i++)
{
if (tags[i] == "NN") // Noun tags
{
    topics.Add(sentenceTokens[i]);
}
}
}
return topics;
}
    */
    public IDisposable Subscribe(IObserver<Gym> observer)
    {
        return gymSubject.Subscribe(observer);
    }
}

public class GymObserver : IObserver<Gym>
    {
        private readonly string name;
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
        }
    }

    public class Program
    {
        public static void Main()
        {
            var gymStream = new GymStream();

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

