# EPUB Parser/Reader Lifecycle Architecture

## Overview

This document maps the complete lifecycle of an EPUB parser/reader and identifies key optimizations and packages that would enhance each phase.

## Lifecycle Phases

### Phase 1: File Access & Extraction 📦

**What Happens:**

- EPUB file is opened (it's a ZIP archive)
- META-INF/container.xml is read to locate content.opf
- Resources are cataloged but not necessarily extracted

**Current Implementation:**

- Using `System.IO.Compression` with full extraction to memory

**Recommended Improvements from text_manipulation.md:**

1. **SharpZipLib** - Stream-based processing

   ```csharp
   // Instead of extracting everything:
   using (var zipFile = new ZipFile(epubPath))
   {
       // Stream individual entries as needed
       var entry = zipFile.GetEntry("META-INF/container.xml");
       using var stream = zipFile.GetInputStream(entry);
       // Process without full extraction
   }
   ```

2. **Memory-Mapped Files** - For large EPUBs

   ```csharp
   using var mmf = MemoryMappedFile.CreateFromFile(epubPath);
   using var accessor = mmf.CreateViewAccessor();
   // Read portions without loading entire file
   ```

### Phase 2: Metadata Parsing & Structure Building 🏗️

**What Happens:**

- Parse content.opf for metadata, manifest, spine
- Build navigation from NCX (EPUB2) or NAV (EPUB3)
- Create internal book structure

**Current Implementation:**

- XML parsing with `System.Xml.Serialization`
- Multiple passes through content

**Recommended Improvements:**

- Keep current XML approach (it's appropriate)
- Add validation with **XmlSchemaValidator**

### Phase 3: Content Processing & Text Extraction 📝

**What Happens:**

- Parse HTML/XHTML chapter content
- Extract plain text for searching
- Process embedded styles

**Current Implementation:**

- Custom regex-based HTML parsing (problematic!)

**Recommended Improvements from text_manipulation.md:**

1. **AngleSharp** (already planned) - Proper HTML/CSS parsing
2. **ExCSS** - Parse and manipulate stylesheets

   ```csharp
   var parser = new ExCSS.StylesheetParser();
   var stylesheet = await parser.ParseAsync(cssContent);
   // Modify styles, extract rules, etc.
   ```

3. **HtmlSanitizer** - Security for untrusted EPUBs

   ```csharp
   var sanitizer = new HtmlSanitizer();
   sanitizer.AllowedTags.Add("img");
   sanitizer.AllowedAttributes.Add("class");
   var safe = sanitizer.Sanitize(chapterHtml);
   ```

### Phase 4: Search Index Building 🔍

**What Happens:**

- Build searchable index of content
- Create word frequency maps
- Prepare for fast searching

**Current Implementation:**

- No indexing, real-time search only

**Recommended Improvements from text_manipulation.md:**

1. **Lucene.NET** - For advanced search capabilities

   ```csharp
   public class EpubSearchIndexer
   {
       private readonly RAMDirectory directory;
       private readonly IndexWriter writer;

       public void IndexChapter(string chapterId, string content)
       {
           var doc = new Document();
           doc.Add(new StringField("id", chapterId, Field.Store.YES));
           doc.Add(new TextField("content", content, Field.Store.YES));
           writer.AddDocument(doc);
       }

       public SearchResults Search(string query)
       {
           var parser = new QueryParser("content", analyzer);
           var q = parser.Parse(query);
           var searcher = new IndexSearcher(DirectoryReader.Open(directory));
           var hits = searcher.Search(q, 10);
           // Return results with highlighting
       }
   }
   ```

2. **Inverted Index** (from text_manipulation.md suggestion)

   ```csharp
   public class InvertedIndex
   {
       private Dictionary<string, HashSet<ChapterLocation>> wordIndex;

       public void BuildIndex(Chapter chapter)
       {
           var words = TokenizeText(chapter.PlainText);
           foreach (var (word, position) in words)
           {
               var key = word.ToLowerInvariant();
               if (!wordIndex.ContainsKey(key))
                   wordIndex[key] = new HashSet<ChapterLocation>();

               wordIndex[key].Add(new ChapterLocation
               {
                   ChapterId = chapter.Id,
                   Position = position,
                   Context = ExtractContext(chapter.PlainText, position)
               });
           }
       }
   }
   ```

3. **Fastenshtein** - For fuzzy search

   ```csharp
   public class FuzzySearch
   {
       public IEnumerable<string> FindSimilar(string query, int maxDistance = 2)
       {
           var lev = new Fastenshtein.Levenshtein(query);
           return wordIndex.Keys
               .Where(word => lev.DistanceFrom(word) <= maxDistance)
               .OrderBy(word => lev.DistanceFrom(word));
       }
   }
   ```

### Phase 5: Reading & Navigation 📖

**What Happens:**

- Display current chapter
- Handle navigation (TOC, next/prev)
- Track reading position

**Current Implementation:**

- Basic chapter loading and navigation

**Recommended Improvements:**

1. **Lazy Loading with Caching**

   ```csharp
   public class ChapterCache
   {
       private readonly IMemoryCache cache;

       public async Task<ProcessedChapter> GetChapterAsync(string id)
       {
           return await cache.GetOrCreateAsync($"chapter_{id}", async entry =>
           {
               entry.SlidingExpiration = TimeSpan.FromMinutes(10);
               var raw = await LoadRawChapter(id);
               return await ProcessChapter(raw);
           });
       }
   }
   ```

2. **Predictive Preloading**

   ```csharp
   public async Task PreloadAdjacentChapters(string currentId)
   {
       var current = GetChapterIndex(currentId);
       // Preload next and previous chapters
       _ = Task.Run(() => GetChapterAsync(GetChapterId(current + 1)));
       _ = Task.Run(() => GetChapterAsync(GetChapterId(current - 1)));
   }
   ```

### Phase 6: Search Execution 🔎

**What Happens:**

- Accept search queries
- Search across chapters
- Return results with context
- Support highlighting

**Current Implementation:**

- Simple string.Contains search

**Recommended Improvements from text_manipulation.md:**

1. **Microsoft.Recognizers.Text** - Natural language queries

   ```csharp
   public SearchQuery ParseQuery(string input)
   {
       var results = DateTimeRecognizer.RecognizeDateTime(input, Culture.English);
       // "Find chapters from last week" -> date range query

       var numbers = NumberRecognizer.RecognizeNumber(input, Culture.English);
       // "Chapter 5" -> specific chapter search
   }
   ```

2. **RE2.Net** - Safe regex searching

   ```csharp
   public class RegexSearch
   {
       public IEnumerable<Match> SafeRegexSearch(string pattern, string content)
       {
           // RE2 prevents catastrophic backtracking
           var re2 = new RE2(pattern);
           return re2.FindAll(content);
       }
   }
   ```

### Phase 7: Persistence & State Management 💾

**What Happens:**

- Save reading progress
- Store bookmarks and annotations
- Cache parsed content
- Maintain library metadata

**Current Implementation:**

- No persistence currently

**Recommended Improvements from text_manipulation.md:**

1. **LiteDB** - Embedded NoSQL database

   ```csharp
   public class BookLibraryRepository
   {
       private readonly LiteDatabase db;

       public class BookMetadata
       {
           public string Id { get; set; }
           public string Title { get; set; }
           public DateTime LastOpened { get; set; }
           public double ProgressPercent { get; set; }
           public List<Bookmark> Bookmarks { get; set; }
           [BsonIgnore]
           public BsonDocument SearchIndex { get; set; } // Store Lucene index
       }

       public void SaveProgress(string bookId, ReadingProgress progress)
       {
           var collection = db.GetCollection<BookMetadata>();
           collection.Update(bookId, book =>
           {
               book.ProgressPercent = progress.Percent;
               book.LastOpened = DateTime.Now;
           });
       }
   }
   ```

2. **Microsoft.Extensions.Caching.Memory** - Runtime caching

   ```csharp
   services.AddMemoryCache(options =>
   {
       options.SizeLimit = 100_000_000; // 100MB cache
   });
   ```

### Phase 8: Advanced Features 🚀

**Additional Capabilities from text_manipulation.md:**

1. **DiffPlex** - Compare versions/annotations

   ```csharp
   public class AnnotationDiffer
   {
       public InlineDiffViewModel ShowAnnotationChanges(string original, string annotated)
       {
           var differ = new Differ();
           var diff = differ.CreateDiffs(original, annotated);
           return InlineDiffBuilder.BuildDiffModel(original, annotated, diff);
       }
   }
   ```

2. **SimMetrics.NET** - Find similar passages

   ```csharp
   public class SimilaritySearch
   {
       public IEnumerable<SimilarPassage> FindSimilar(string passage)
       {
           var metric = new JaroWinkler();
           return allPassages
               .Select(p => new { Passage = p, Score = metric.GetSimilarity(passage, p.Text) })
               .Where(x => x.Score > 0.8)
               .OrderByDescending(x => x.Score);
       }
   }
   ```

## Recommended Package Stack

Based on the lifecycle analysis and text_manipulation.md suggestions:

### Core (Must Have)

```xml
<!-- Archive handling -->
<PackageReference Include="SharpZipLib" Version="*" />

<!-- HTML/CSS processing -->
<PackageReference Include="AngleSharp" Version="*" />
<PackageReference Include="ExCSS" Version="*" />
<PackageReference Include="HtmlSanitizer" Version="*" />

<!-- Storage -->
<PackageReference Include="LiteDB" Version="*" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="*" />
```

### Search & Text (Highly Recommended)

```xml
<!-- Search engine -->
<PackageReference Include="Lucene.Net" Version="*" />
<PackageReference Include="Lucene.Net.Analysis.Common" Version="*" />
<PackageReference Include="Lucene.Net.Highlighter" Version="*" />

<!-- Fuzzy matching -->
<PackageReference Include="Fastenshtein" Version="*" />

<!-- Natural language -->
<PackageReference Include="Microsoft.Recognizers.Text" Version="*" />
```

### Advanced Features (Nice to Have)

```xml
<!-- Text comparison -->
<PackageReference Include="DiffPlex" Version="*" />
<PackageReference Include="SimMetrics.Net" Version="*" />

<!-- Performance -->
<PackageReference Include="Microsoft.IO.RecyclableMemoryStream" Version="*" />

<!-- Safe regex -->
<PackageReference Include="RE2.Net" Version="*" />
```

## Architecture Benefits

This lifecycle-aware architecture provides:

1. **Performance**: Stream-based processing, lazy loading, efficient caching
2. **Scalability**: Can handle large EPUBs and libraries
3. **Features**: Advanced search, fuzzy matching, natural language queries
4. **Persistence**: Proper state management and progress tracking
5. **Security**: HTML sanitization for untrusted content
6. **Maintainability**: Clear separation of concerns per lifecycle phase

## Implementation Priority

1. **Phase 1**: SharpZipLib for better ZIP handling
2. **Phase 3**: AngleSharp for HTML (already planned)
3. **Phase 4**: Lucene.NET for search indexing
4. **Phase 7**: LiteDB for persistence
5. **Phase 6**: Fastenshtein for fuzzy search
6. **Others**: Add as features are needed
