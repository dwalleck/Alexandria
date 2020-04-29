using Alexandria.Parser.Models;
using Alexandria.Parser.Models.Content;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;

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
                        Console.WriteLine("Done");
                    }
                }
            }
            
        }
    }
}
