using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Alexandria.Parser.Models;
using Alexandria.Parser.Models.Content;
using System.Xml.Linq;

namespace Alexandria.Parser
{
    public static class Parser
    {
        public static async Task<Book> OpenBookAsync(string filePath)
        {
            using (ZipArchive archive = ZipFile.Open(filePath, ZipArchiveMode.Read))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Container));
                var containerFile = archive.GetEntry(@"META-INF/container.xml");
                using (StreamReader reader = new StreamReader(containerFile.Open()))
                {
                    var c = (Container)serializer.Deserialize(reader);
                    var contentFilePath = c.Rootfiles.FirstOrDefault().FullPath;
                    var contentFile = archive.GetEntry(contentFilePath);

                    var contentFileDirectory = Path.GetDirectoryName(contentFilePath);

                    using (StreamReader contentFilereader = new StreamReader(contentFile.Open()))
                    {
                        var cfSerializer = new XmlSerializer(typeof(Package));
                        var package = (Package)cfSerializer.Deserialize(contentFilereader);
                        var pages = new List<string>();
                        foreach (var manifest in package.ManifestItems)
                        {
                            var page = archive.GetEntry(string.IsNullOrEmpty(contentFileDirectory) ? manifest.Href : $"{contentFileDirectory}/{manifest.Href}");
                            var read = new StreamReader(page.Open());
                            var content = await read.ReadToEndAsync();
                            pages.Add(content);

                        }
                        return new Book(package.Metadata.Titles, package.Metadata.Authors, pages);
                    }
                }
            }
        }
    }
}
