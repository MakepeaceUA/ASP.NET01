using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace GeometryApp
{
    public interface ICanvasShape
    {
        string Name { get; }
        string Render();
        void Display(IOutput output);
    }
    public class ShapeModel
    {
        public string Type { get; set; } = "";
    }

    public class Circle : ICanvasShape
    {
        public string Name => "Круг";
        public string Render() => "   ***   \n *     * \n *     * \n   ***   ";

        public void Display(IOutput output)
        {
            output.WriteLine($"Фигура: {Name}");
            output.WriteLine(Render());
        }
    }

    public class Square : ICanvasShape
    {
        public string Name => "Квадрат";
        public string Render() => "*****\n*   *\n*   *\n*****";

        public void Display(IOutput output)
        {
            output.WriteLine($"Фигура: {Name}");
            output.WriteLine(Render());
        }
    }

    public class Triangle : ICanvasShape
    {
        public string Name => "Треугольник";
        public string Render() => "  *  \n * * \n*****";

        public void Display(IOutput output)
        {
            output.WriteLine($"Фигура: {Name}");
            output.WriteLine(Render());
        }
    }
    public interface IOutput
    {
        void WriteLine(string line);
    }
    public class ConsoleOutput : IOutput
    {
        public void WriteLine(string line) => Console.WriteLine(line);
    }

    public class FileOutput : IOutput, IDisposable
    {
        private readonly StreamWriter writer;
        public FileOutput(string path)
        {
            writer = new StreamWriter(path, false);
        }

        public void WriteLine(string line)
        {
            writer.WriteLine(line);
        }

        public void Dispose()
        {
            writer?.Close();
            writer?.Dispose();
        }
    }

    public interface IShapeSerializer
    {
        void Serialize(List<ICanvasShape> shapes, string filePath);
        List<ICanvasShape> Deserialize(string filePath);
    }
    public class JsonShapeSerializer : IShapeSerializer
    {
        public void Serialize(List<ICanvasShape> shapes, string filePath)
        {
            var models = new List<ShapeModel>();
            foreach (var shape in shapes)
                models.Add(new ShapeModel { Type = shape.GetType().Name });

            var json = JsonSerializer.Serialize(models);
            File.WriteAllText(filePath, json);
        }

        public List<ICanvasShape> Deserialize(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var models = JsonSerializer.Deserialize<List<ShapeModel>>(json);
            return ShapeFactory.CreateShapes(models);
        }
    }

    public class XmlShapeSerializer : IShapeSerializer
    {
        public void Serialize(List<ICanvasShape> shapes, string filePath)
        {
            var models = new List<ShapeModel>();
            foreach (var shape in shapes)
                models.Add(new ShapeModel { Type = shape.GetType().Name });

            var serializer = new XmlSerializer(typeof(List<ShapeModel>));
            using var fs = new FileStream(filePath, FileMode.Create);
            serializer.Serialize(fs, models);
        }

        public List<ICanvasShape> Deserialize(string filePath)
        {
            var serializer = new XmlSerializer(typeof(List<ShapeModel>));
            using var fs = new FileStream(filePath, FileMode.Open);
            var models = (List<ShapeModel>)serializer.Deserialize(fs)!;
            return ShapeFactory.CreateShapes(models);
        }
    }

    public static class ShapeFactory
    {
        public static ICanvasShape CreateShape(string type) =>
            type switch
            {
                nameof(Circle) => new Circle(),
                nameof(Square) => new Square(),
                nameof(Triangle) => new Triangle(),
                _ => throw new InvalidOperationException($"Неизвестный тип фигуры: {type}")
            };

        public static List<ICanvasShape> CreateShapes(IEnumerable<ShapeModel> models)
        {
            var list = new List<ICanvasShape>();
            foreach (var model in models)
            {
                list.Add(CreateShape(model.Type));
            }
            return list;
        }
    }

    public interface IShapeStorageService
    {
        void Save(List<ICanvasShape> shapes, string filePath);
        List<ICanvasShape> Load(string filePath);
    }

    public class FileShapeStorageService : IShapeStorageService
    {
        private readonly IShapeSerializer serializer;

        public FileShapeStorageService(IShapeSerializer serializer)
        {
            this.serializer = serializer;
        }

        public void Save(List<ICanvasShape> shapes, string filePath) => serializer.Serialize(shapes, filePath);
        public List<ICanvasShape> Load(string filePath) => serializer.Deserialize(filePath);
    }

    public class ShapeService
    {
        private readonly IShapeStorageService StorageService;
        private readonly IServiceProvider Provider;

        public ShapeService(IShapeStorageService storageService, IServiceProvider provider)
        {
            StorageService = storageService;
            Provider = provider;
        }

        public void Run(string format, string outputType)
        {
            var shapes = new List<ICanvasShape> { new Circle(), new Square(), new Triangle() };

            string DataFile = $"shapes.{format}";
            StorageService.Save(shapes, DataFile);
            Console.WriteLine($"Фигуры сохранены в файл: {Path.GetFullPath(DataFile)}");

            var LoadedShapes = StorageService.Load(DataFile);

            string RenderFile = "render.txt";
            IOutput output = outputType == "file"
                ? new FileOutput(RenderFile)
                : Provider.GetRequiredService<IOutput>();

            foreach (var shape in LoadedShapes)
                shape.Display(output);

            if (output is IDisposable disposable)
                disposable.Dispose();

            if (outputType == "file")
                Console.WriteLine($"Рендер сохранён в файл: {Path.GetFullPath(RenderFile)}");
        }
    }
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Выберите формат сериализации (json/xml):");
            string? format = Console.ReadLine()?.Trim().ToLower();

            Console.WriteLine("Куда вывести результат? (console/file):");
            string? outputType = Console.ReadLine()?.Trim().ToLower();

            var services = new ServiceCollection();

            services.AddSingleton<IOutput, ConsoleOutput>();

            switch (format)
            {
                case "xml":
                    services.AddSingleton<IShapeSerializer, XmlShapeSerializer>();
                    break;
                case "json":
                default:
                    services.AddSingleton<IShapeSerializer, JsonShapeSerializer>();
                    break;
            }

            services.AddSingleton<IShapeStorageService, FileShapeStorageService>();
            services.AddTransient<ShapeService>();

            var provider = services.BuildServiceProvider();

            var service = provider.GetRequiredService<ShapeService>();
            service.Run(format ?? "json", outputType ?? "console");

            Console.WriteLine("\nГотово. Нажмите любую клавишу...");
            Console.ReadKey();
        }
    }
}
