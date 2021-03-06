using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models
{
    public class Book
    {
        public Book(string[] titles, string[] authors, List<string> chapters)
        {
            Titles = new List<string>(titles);
            Chapters = chapters;
            Authors = new List<string>(authors);
        }

        public List<string> Titles { get; set; }

        public List<string> Chapters { get; set; }

        public List<string> Authors { get; set; }
    }
}