
using System;
using System.IO;
using System.Linq;


namespace Alexandria.ConsoleTest
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            var files = Directory.GetFiles(@"C:\Users\dwall\OneDrive\e-books", "*.epub", SearchOption.AllDirectories);
            //string zipPath = @"C:\Users\dwall\repos\epub-examples\wok.zip";
            
            foreach (var file in files)
            {
                var book = await Parser.Parser.OpenBookAsync(file);
                Console.WriteLine($"{book.Titles.FirstOrDefault()}");
                Console.WriteLine($"Number of titles: {book.Titles.Count()}");
                Console.WriteLine($"Written by: {book.Authors.FirstOrDefault()}");
                Console.WriteLine($"Number of authors: {book.Authors.Count()}");
            }
        }
    }
}
