using Alexandria.Parser.Models;
using Alexandria.Parser.Models.Content;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Alexandria.ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string zipPath = @"C:\Users\dwall\repos\epub-examples\wok.zip";
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Container));
                var containerFile = archive.GetEntry(@"META-INF/container.xml");
                using (StreamReader reader = new StreamReader(containerFile.Open()))
                {
                    var c = (Container)serializer.Deserialize(reader);
                    var contentFilePath = c.Rootfiles.FirstOrDefault().FullPath;
                    var contentFile = archive.GetEntry(contentFilePath);

                    using (StreamReader contentFilereader = new StreamReader(contentFile.Open()))
                    {   
                        var cfSerializer = new XmlSerializer(typeof(Package));
                        var package = (Package)cfSerializer.Deserialize(contentFilereader);
                        var pages = new List<string>();
                        foreach (var manifest in package.ManifestItems)
                        {
                            var page = archive.GetEntry(manifest.Href);
                            var read = new StreamReader(page.Open());
                            var content = read.ReadToEnd();
                            pages.Add(content);
                            
                        }
                        Console.ReadLine();
                    }
                }
            }
            
        }
    }
}
