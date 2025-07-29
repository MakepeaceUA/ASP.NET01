using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleApp58
{
    public interface IAnimal
    {
        string Name { get; }
        string Sound { get; }
        void Display();
    }

    public class AnimalModel
    {
        public string Type { get; set; } = "";
    }

    public class Dog : IAnimal
    {
        public string Name => "Собака";
        public string Sound => "Гав-гав";
        public void Display() => Console.WriteLine($"{Name}: {Sound}");
    }

    public class Cat : IAnimal
    {
        public string Name => "Кошка";
        public string Sound => "Мяу";
        public void Display() => Console.WriteLine($"{Name}: {Sound}");
    }

    public class Cow : IAnimal
    {
        public string Name => "Корова";
        public string Sound => "Мууу";
        public void Display() => Console.WriteLine($"{Name}: {Sound}");
    }

    public interface IAnimalSerializer
    {
        void Serialize(List<IAnimal> animals, string filePath);
        List<IAnimal> Deserialize(string filePath);
    }

    public class JsonAnimalSerializer : IAnimalSerializer
    {
        public void Serialize(List<IAnimal> animals, string filePath)
        {
            var models = new List<AnimalModel>();
            foreach (var animal in animals)
                models.Add(new AnimalModel { Type = animal.GetType().Name });

            var json = JsonSerializer.Serialize(models);
            File.WriteAllText(filePath, json);
        }

        public List<IAnimal> Deserialize(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var models = JsonSerializer.Deserialize<List<AnimalModel>>(json);
            return AnimalFactory.CreateAnimals(models);
        }
    }

    public class XmlAnimalSerializer : IAnimalSerializer
    {
        public void Serialize(List<IAnimal> animals, string filePath)
        {
            var models = new List<AnimalModel>();
            foreach (var animal in animals)
                models.Add(new AnimalModel { Type = animal.GetType().Name });

            var serializer = new XmlSerializer(typeof(List<AnimalModel>));

            using var fs = new FileStream(filePath, FileMode.Create);
            serializer.Serialize(fs, models);
        }

        public List<IAnimal> Deserialize(string filePath)
        {
            var serializer = new XmlSerializer(typeof(List<AnimalModel>));
            using var fs = new FileStream(filePath, FileMode.Open);
            var models = (List<AnimalModel>)serializer.Deserialize(fs)!;
            return AnimalFactory.CreateAnimals(models);
        }
    }

    public static class AnimalFactory
    {
        public static IAnimal CreateAnimal(string type) =>
            type switch
            {
                nameof(Dog) => new Dog(),
                nameof(Cat) => new Cat(),
                nameof(Cow) => new Cow(),
                _ => throw new InvalidOperationException($"Неизвестный тип животного: {type}")
            };

        public static List<IAnimal> CreateAnimals(IEnumerable<AnimalModel> models)
        {
            var list = new List<IAnimal>();
            foreach (var model in models)
            {
                list.Add(CreateAnimal(model.Type));
            }
            return list;
        }
    }

    public interface IAnimalStorageService
    {
        void SaveAnimals(List<IAnimal> animals, string filePath);
        List<IAnimal> LoadAnimals(string filePath);
    }
    public class FileAnimalStorageService : IAnimalStorageService
    {
        private readonly IAnimalSerializer _serializer;
        public FileAnimalStorageService(IAnimalSerializer serializer) => _serializer = serializer;

        public void SaveAnimals(List<IAnimal> animals, string filePath) => _serializer.Serialize(animals, filePath);

        public List<IAnimal> LoadAnimals(string filePath) => _serializer.Deserialize(filePath);
    }

    public class AnimalService
    {
        private readonly IAnimalStorageService StorageService;

        public AnimalService(IAnimalStorageService service)
        {
            StorageService = service;
        }

        public void Run(string filePath)
        {
            var animals = new List<IAnimal> { new Dog(), new Cat(), new Cow() };

            StorageService.SaveAnimals(animals, filePath);
            Console.WriteLine($"Файл сохранён по пути: {Path.GetFullPath(filePath)}");
            Console.WriteLine("Животные сохранены.\n");

            var LoadedAnimals = StorageService.LoadAnimals(filePath);
            Console.WriteLine("Загруженные животные:");
            foreach (var animal in LoadedAnimals) 
            {
                animal.Display();
            }
                
        }
    }

    class Program
    {
        static void Main()
        {
            Console.WriteLine("Выберите формат для сохранения животных (json / xml):");
            string? input = Console.ReadLine()?.Trim().ToLower();

            var services = new ServiceCollection();

            string extension;
            switch (input)
            {
                case "json":
                    services.AddSingleton<IAnimalSerializer, JsonAnimalSerializer>();
                    extension = ".json";
                    break;

                case "xml":
                    services.AddSingleton<IAnimalSerializer, XmlAnimalSerializer>();
                    extension = ".xml";
                    break;

                default:
                    Console.WriteLine("Неверный формат. Используется JSON по умолчанию.");
                    services.AddSingleton<IAnimalSerializer, JsonAnimalSerializer>();
                    extension = ".json";
                    break;
            }

            services.AddSingleton<IAnimalStorageService, FileAnimalStorageService>();
            services.AddTransient<AnimalService>();

            var provider = services.BuildServiceProvider();

            string filePath = $"animals{extension}";

            var AnService = provider.GetRequiredService<AnimalService>();
            AnService.Run(filePath);

            Console.ReadKey();
        }
    }
}