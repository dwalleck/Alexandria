using Alexandria.Parser.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;

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
                    Console.WriteLine(c.Rootfiles.FirstOrDefault().FullPath);
                }
            }
            
        }
    }
}
