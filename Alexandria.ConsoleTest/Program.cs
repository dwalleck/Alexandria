
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;


namespace Alexandria.ConsoleTest
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            var files = Directory.GetFiles(@"C:\Users\dwall\OneDrive\e-books", "*.epub", SearchOption.AllDirectories);
            string zipPath = @"C:\Users\dwall\repos\epub-examples\wok.zip";
            
            foreach (var file in files)
            {
                var book = await Parser.Parser.OpenBookAsync(zipPath);
                var chapterText = book.Chapters[13];
                var pos = chapterText.IndexOf("<body");
                var end = chapterText.IndexOf("body>");
                //chapterText.AsMemory().
                var text = chapterText.AsMemory().Slice(pos, end - pos + 5);
                var x = XElement.Parse(text.ToString());
                var cont = x.ToString();
                var pageSize = 3000;
                var tagLength = 4;
                var currentPosition = pageSize;
                // Loop through pages in the chapter
                while (currentPosition <= text.Length)
                {
                    Console.WriteLine("New Page");
                    var lastFullPos = text.ToString().LastIndexOf("</p>", currentPosition, currentPosition - tagLength);
                    var page = text.Slice(0, lastFullPos + tagLength);
                    Console.WriteLine(page);
                    currentPosition = currentPosition + lastFullPos + 1;
                }

                
                
                
                // var nextStart = lastFullPos + 4;
                // var newLast = text.ToString().LastIndexOf("</p>", lastFullPos + 4 + 3000, 2994);
                // page = text.Slice(lastFullPos + 4, newLast + 5);
                // Console.WriteLine(page);

                /*Console.WriteLine($"{book.Titles.FirstOrDefault()}");
                Console.WriteLine($"Number of titles: {book.Titles.Count()}");
                Console.WriteLine($"Written by: {book.Authors.FirstOrDefault()}");
                Console.WriteLine($"Number of authors: {book.Authors.Count()}");*/
                Console.ReadLine();
            }
        }
    }
}
