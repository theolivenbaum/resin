<a name="top" id="top"></a>
# Resin

_If it's not zero-config it's not Resin._  
_- Creators of Resin_

Solve your full-text search problem or your big data analysis task with Resin, a code base derived from iteratively refactoring Lucene.Net down to what is now __a fast and light-weighted search framework written specifically for .net__ with great analysis skills and fast response times even to complex queries. Resin is multi-cultural and deeply inspired by Lucene but leaves [legacy code and java inheritance](https://lucenenet.apache.org/) behind and finally makes it possible for .net programmers to be able to use [cutting-edge search tech](https://lucene.apache.org/).

First and foremost: read about the dependencies you need to [worry about](#dependencies) before launching your beefed-up search server that serves up 100M documents and that matches Google's capabilities in scale and performance. Spoiler alert: there are none. But read it anyway.

* _[Quick usage guide](#usage)_
* _[Relevance (tf-idf)](#relevance)_
* _[Why so few classes?](#citizens)_
* _[The CLI](#cli)_
* _[Roadmap](#roadmap)_
* _[At large scale](#scale)_
* _[File format](#fileformat)_
* _[Dependencies](#dependencies)_

_Functionality that relies heavily on conventions are  
bound to be unfitting for some and is therefore in need  
of a little bit of config, which is perfectly fine,  
as long as that function is not included in Resin._  
_Thank you! :)_  
_- Consumers of Resin_

Use [freely](https://github.com/kreeben/resin/blob/master/LICENSE) and register [issues here](https://github.com/kreeben/resin/issues).

Contribute frequently. Go directly to an [introduction](#citizens) of the parts that make up Resin.  

Code or use the [CLI](#cli) to build, query and analyze your index.  

[Fire up a search server](#inproc) in seconds, with Solr-like capabilities but with zero config:

	cd path_to_resin
	rnh.exe --url http://localhost:1234/
  
<a name="usage" id="usage"></a>
##Quick usage guide

####Here's a document

	{
		"_id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

Fields prefixed with `_` are not [analyzed](#citizens). The `_id` field is mandatory.  

####Here's a huge number of documents
	
	var docs = GetHugeNumberOfDocs();

####Add them to a Resin index

	var dir = @"C:\Users\Yourname\Resin\wikipedia";
	using (var writer = new IndexWriter(dir, new Analyzer()))
	{
		foreach (var doc in docs)
		{
			writer.Write(doc);
		}
	}

####Query that index (matching the whole term)
<a name="inproc" id="inproc"></a>
#####In-proc

	var searcher = new Searcher(dir);
	var result = searcher.Search("label:universe");

#####Server
	
	// To start the server, in a cmd window:
	// cd path_to_resin
	// rnh.exe --url http://localhost:1234/
	// You have just started a light-weight search server with solr-like capabilities. Enjoy!
	
	var searcher = new SearchClient("wikipedia", "http://localhost:1234/");
	var result = searcher.Search("label:universe");
	
####Prefix query

	var result = searcher.Search("label:univ*");

####Fuzzy

	var result = searcher.Search("label:univerze~");

####And

	var result = searcher.Search("label:universe +aliases:cosmos");

####Or

	var result = searcher.Search("label:universe aliases:cosmos");

####Not

	var result = searcher.Search("label:universe -aliases:cosmos");

####Only a few at a time
	
	var page = 0;
	var size = 10;
	var result = searcher.Search("label:univ*", page, size);
	var totalNumberOfHits = result.Total;
	var docs = result.Docs.ToList(); // Will contain a maximum of 10 docs

####Only one

	var result = searcher.Search("_id:Q1");
	var doc = result.Docs.First();

####All fields queryable, whole document returned

	// Find document "Q1" and print "cosmos The Universe existence space outerspace"
	var result = searcher.Search("_id:Q1");
	var doc = result.Docs.First();
	Console.WriteLine(doc.Fields["aliases"]);

####Analyze your index

	var field = "label";
	var scanner = new Scanner(dir);
	var termsOrderedByFreq = scanner.GetAllTokens(field).OrderByDescending(t=>t.Count).ToList();
	
	File.WriteAllLines(Path.Combine(dir, "_" + field + ".txt"), termsOrderedByFreq
		.Select(t=>string.Format("{0} {1}", t.Token, t.Count)));

####Querying is fast because

	using (var searcher = new Searcher(dir)) // Initializing the searcher loads the document index
	{
		// This loads and caches the term indices for the "id" and "label" fields:
		
		var result = searcher.Search("_id:Q1 label:Q1");
		
		// This executes the query. Resin loads the doc from disk and caches it:
		
		var doc1 = result.Docs.First();
		
		// The following query requires 
		//
		// - a hashtable lookup towards the field file index to find the field ID
		// - a hashtable lookup towards the term index to find the doc IDs
		// - for each doc ID: a hashtable lookup towards the doc cache
		
		var docs = searcher.Search("label:universe").Docs.ToList();
		
		// The following prefix query requires 
		//
		// - a hashtable lookup towards the field file index to find the field ID
		// - a Trie scan (*) to find matching terms
		// - for each term: a hashtable lookup towards the term index to find the doc IDs
		// - append the results of the scan (as if the tokens are joined by "OR")
		// - for each doc ID: one hashtable lookup towards the doc cache
		
		docs = searcher.Search("label:univ*").Docs.ToList();
		
	}// Caches are released	 
  
(*) The [Trie](https://github.com/kreeben/resin/blob/master/Resin/Trie.cs) has a leading role in the querying routine.  

<a name="relevance" id="relevance"></a>
##Relevance

The scoring [implemented here](https://github.com/kreeben/resin/blob/master/Resin/Tfidf.cs) follows the [tf-idf](https://en.wikipedia.org/wiki/Tf%E2%80%93idf) scheme:

`IDF = log ( numDocs / docFreq + 1) + 1`

with the standard Lucene augumented term frequency `sqrt(TF)` (but they're [leaving us in the dust](http://opensourceconnections.com/blog/2015/10/16/bm25-the-next-generation-of-lucene-relevation/)):

`score = IDF*sqrt(TF)`

<a name="citizens" id="citizens"></a>
##Resin's first class citizens

To be able to call ourselves a full-text search framework we need something that can analyze text, an [Analyzer](https://github.com/kreeben/resin/blob/master/Resin/Analyzer.cs). Also, something that can write index files and store documents, an [IndexWriter](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs), [FieldWriter](https://github.com/kreeben/resin/blob/master/Resin/FieldWriter.cs) and a [DocumentWriter](https://github.com/kreeben/resin/blob/master/Resin/DocumentWriter.cs). 

We will need to be able to parse multi-criteria queries such as "title:Rambo +title:Blood", in other words a [QueryParser](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs). The important questions for the parser to answer are what fields do we need to scan and what are the tokens that should match. A space character between two query terms such as the space  between "Rambo title:" in the query `title:Rambo title:Blood` will be interpreted as `OR`, a plus sign as `AND`, a minus sign as `NOT`. In other words that query will be parsed into "please find documents that has both rambo AND blood in the title", or in a more machine-like language `scan the field named title for the tokens rambo and blood and return the intersection of their postings`.

An [IndexReader](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and a [FieldReader](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) will make it possible for a [FieldScanner](https://github.com/kreeben/resin/blob/master/Resin/FieldScanner.cs) to get a list of document IDs containing the tokens at hand and [scoring](https://github.com/kreeben/resin/blob/master/Resin/Tfidf.cs) them. We can return the whole set or a paginated subset before fetching the documents from cache or disk.

####The Analyzer

	public IEnumerable<string> Analyze(string value)
	{
	    if (value == null) yield break;
	    int token = 0;
	    var lowerStr = value.ToLower(_culture);
	    for (int i = 0; i < lowerStr.Length; ++i)
	    {
	        if (!IsSeparator(lowerStr[i])) continue;
	        if (token < i)
	        {
	            var tok = lowerStr.Substring(token, i - token);
	            if (!_stopwords.Contains(tok)) yield return tok;
	        }
	        token = i + 1;
	    }
	    if (token < lowerStr.Length)
	    {
	        yield return lowerStr.Substring(token);
	    }
	}

The least thing we can do in an Analyzer is to inspect each character of each token it's been given. We could let .net do that for us (string.Split) or we can sweep over the string ourselves.

Once we have a character in our hands we need to figure out if it's information or if it's something that separates two tokens or if it's noice.

By tokenizing the text of a field we make the individual tokens insensitive to casing, queryable. Had we not only exact matches to the verbatim text can be made at runtime, if we want the querying to go fast. The query "title:Rambo" would produce zero documents (no movie in the whole world actually has the title "Rambo") but querying "title:Rambo\\: First Blood" would produce one hit. 

But only if you are scanning a database of Swedish movie titles because the original movie title was "First Blood". Swedish Media Institue (it's called something else, sorry, I forget) changed the title to the more declarative "Rambo: First Blood". This is probably what happened:

Guy: Americans have sent us new movie.

Boss: What's it called?

g: First blood!

b: First blood? What kind of silly name is that? Who's it about?

g: Well, there's this guy, Rambo, he...

b: Rambo! What great name! Put THAT in title. I'm feeling also this might be franchise.

Another thing we hope to achieve by analyzing text is to normalize between the words used when querying and the words in the documents so that matches can be produced consistently. 

##### Deep analysis
The analysis you want to do both at indexing and querying time is to acctually try to understand the contents of the text, that a "Tree" is the same thing as a "tree" and a component of "trees". What if you could also pick up on themes and subcontexts?

What we are doing however in Analyzer.cs is very rudimentary type of analysis. We are simply identifying the individual words. We could go further, investigate if any of those words are kind of the same, because although "trees" != "tree" their concepts intersect so much so that in the interest of full-text search they could and maybe should be one and the same concept. Anyway, identifying and normalizing the words will be fine for now.

[Code](https://github.com/kreeben/resin/blob/master/Resin/Analyzer.cs) and [tests](https://github.com/kreeben/resin/blob/master/Tests/AnalyzerTests.cs)

####FieldWriter
Tokens are stored in a field file. A field file is an index of all the tokens in a field. Tokens are stored together with postings. Postings are pointers to documents. Our postings contain the document ID and how many times the token exists within that document, its _term frequency_.

That means that if we know what field file to look in, we can find the answer to the query "title:rambo" by opening the file, deserialize the contents into this:

	// tokens/docids/term frequency
	private readonly IDictionary<string, IDictionary<int, int>> _tokens = DeserializeFieldFile(fileName);
	
	// ...and then we can find the document IDs. This operation does not take long.
	IDictionary<int, int> postings;
	if (!_tokens.TryGetValue(token, out postings))
	{
		return null;
	}
	return postings;

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldWriter.cs) and [tests](https://github.com/kreeben/resin/blob/master/Tests/FieldWriterTests.cs)

####DocumentWriter

Documents (and field structures) are persisted on disk as [protobuf](https://en.wikipedia.org/wiki/Protocol_Buffers) messages:

	using (var fs = File.Create(fileName))
	{
	    Serializer.Serialize(fs, docs); // protobuf-net serialization
	}

The in-memory equivalent of a document file is this:

	// docid/fields/value
	private readonly IDictionary<int, IDictionary<string, string>> _docs;

Here is a document on its own:

	// fields/values
	IDictionary<string, string> doc;

[Code](https://github.com/kreeben/resin/blob/master/Resin/DocumentWriter.cs)

####IndexWriter

Store the documents. But also analyze them and create field files that are queryable. There's not much to it:

	public void Write(Document doc)
	{
	    foreach (var field in doc.Fields)
	    {
	        foreach (var value in field.Value)
	        {
	            // persist the value of the field, as-is, by writing to a document file

	            var tokens = _analyzer.Analyze(value);
	            foreach(var token in tokens)
	            {
	            	// store the doc ID, token and its position in a field file
	            }
	        }
	    }
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/IndexWriterTests.cs)

#### QueryParser
With our current parser we can interpret "title:Rambo", also `title:first title:blood`. The last query is what lucene decompiles this query into: `title:first blood`. We will try to mimic this later on but for now let's work with the decompiled format.

	var q = query.Split(' ').Select(t => t.Split(':'));

[Code](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/QueryParserTests.cs)

####FieldReader

The complete in-memory representation of the field file:

	// terms/docids/term frequency
	private readonly IDictionary<string, IDictionary<int, int>> _terms;
	
	// the char's of the terms, arranged in a tree structure
	private readonly Trie _trie;
	
A field reader can do this:

	var tokens = reader.GetAllTokens();
	var tokensThatStartWith = reader.GetTokens(prefix);
	var tokensThatResemble = reader.GetSimilar(word, edits:2);
	var docPos = reader.GetPostings(term);

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/FieldReaderTests.cs)

#### Token and Term

A token is a clean, noice-free, lower-cased or otherwise noramlized piece of text. A term is a token and a field. Tokens are stored within a field file. Terms are what your parsed queries consist of.

A token:

	rambo

A term:

	title:rambo

#### Scanner

After a good parsing we get back a list of terms.

All that we know, as a search framework, is called "a lexicon".

At the back of that lexicon is an index, the field file. A scanner scans the index and if it finds a match it returns the doc IDs and the term frequencies: 

	public IEnumerable<DocumentScore> GetDocIds(Term term)
	{
		int fieldId;
		if (_fieldIndex.TryGetValue(term.Field, out fieldId))
		{
		  var reader = GetReader(term.Field);
		  if (reader != null)
		  {
		      if (term.Prefix)
		      {
		          return GetDocIdsByPrefix(term, reader);
		      }
		      return GetDocIdsExact(term, reader);
		  }
		}
		return Enumerable.Empty<DocumentScore>();
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/Scanner.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/ScannerTests.cs)

#### IndexReader

The IndexReader needs a scanner. The results of a scan is a list of document ids. IndexReader scores the hits by calculating the [tf-idf](https://en.wikipedia.org/wiki/Tf%E2%80%93idf) for the terms in the query:

	public IEnumerable<DocumentScore> GetScoredResult(IEnumerable<Term> terms)

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/IndexReaderTests.cs)

####Searcher

Finally, the searcher, a helper that takes an IndexReader and a QueryParser, accepting unparsed queries, lazily returning a list of documents:

	public IEnumerable<Document> Search(string query)
	{
		var terms = _parser.Parse(query);
		var scored = _reader.GetScoredResult(terms).OrderByDescending(d=>d.TermFrequency).ToList();
		var docs = scored.Select(s => _reader.GetDocFromDisk(s));
		return new Result { Docs = docs, Total = scored.Count };
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/Searcher.cs) 

<a name="cli" id="cli"></a>
## Test spin

1. Download a Wikipedia JSON dump [here](https://dumps.wikimedia.org/wikidatawiki/entities/)
2. Use the [WikipediaJsonParser](https://github.com/kreeben/resin/blob/master/Resin.WikipediaJsonParser/Program.cs) to extract as many documents as you want. In a cmd window:

	cd path_to_resin_repo
	
	rnw c:\downloads\wikipedia.json 0 1000000

	This will generate a new file: wikipedia_resin.json. 0 documents was skipped and 1M taken from the wikipedia file.

3. Create an index:
	
	rn write --file c:\downloads\wikipedia_resin.json --dir c:\temp\resin\wikipedia --skip 0 --take 1000000

4. After 3 minutes or so, do this:  

	rn query --dir c:\temp\resin\wikipedia -q "label:ringo"

![alt text](https://github.com/kreeben/resin/blob/master/screenshot.PNG "I have an SSD. The index was warmed up prior to the query.")

A little less than a millisecond apparently. A couple of orders of magitude faster than Lucene. Here's what went down:

	var q = args[Array.IndexOf(args, "-q") + 1];
	var timer = new Stopwatch();
	using (var s = new Searcher(dir))
	{
	    for (int i = 0; i < 1; i++)
	    {
	        s.Search(q).Docs.ToList(); // this heats up the "label" field and pre-caches the documents
	    }
	    timer.Start();
	    var docs = s.Search(q).Docs.ToList(); // Fetch docs from cache
	    var elapsed = timer.Elapsed.TotalMilliseconds;
	    var position = 0;
	    foreach (var doc in docs)
	    {
	        Console.WriteLine(string.Join(", ", ++position, doc.Fields["id"][0], doc.Fields["label"][0]));
	    }
	    Console.WriteLine("{0} results in {1} ms", docs.Count, elapsed);
	}

Here is another test, this time the documents aren't pre-cached in the warmup:

	var q = args[Array.IndexOf(args, "-q") + 1];
	var timer = new Stopwatch();
	using (var s = new Searcher(dir))
	{
	    for (int i = 0; i < 1; i++)
	    {
	        s.Search(q); // warm up the "label" field
	    }
	    timer.Start();
	    var docs = s.Search(q).Docs.ToList(); // Fetch docs from disk
	    var elapsed = timer.Elapsed.TotalMilliseconds;
	    var position = 0;
	    foreach (var doc in docs)
	    {
	        Console.WriteLine(string.Join(", ", ++position, doc.Fields["id"][0], doc.Fields["label"][0]));
	    }
	    Console.WriteLine("{0} results in {1} ms", docs.Count, elapsed);
	}

![alt text](https://github.com/kreeben/resin/blob/master/screenshot3.PNG "Docs weren't in the cache.")

<a name="roadmap" id="roadmap"></a>
##Roadmap

####Query language interpreter
AND, OR, NOT (+ -), prefix* and fuzzy~ [implemented here](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs).

TODO: nested clauses, phrases

####Fuzzy
Levenshtein Trie scan implemented [here](https://github.com/kreeben/resin/blob/master/Resin/Trie.cs#L55), inspired by [this paper](http://julesjacobs.github.io/2015/06/17/disqus-levenshtein-simple-and-fast.html).

TODO: override default similarity in query: `label:starr~0.8`.

####Scoring
Refine the scoring. The current scoring scheme is [tf-idf](https://en.wikipedia.org/wiki/Tf%E2%80%93idf).The Lucene core team has just recently grown out of tf-idf and now like [bm25](http://opensourceconnections.com/blog/2015/10/16/bm25-the-next-generation-of-lucene-relevation/) better.

####Write concurrency
Implemented [here](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs#L81) at indexing time and [here](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs#L53) at querying time. Each write session creates a new, automic index. When refreshing the index reader, new indices are merged with earlier generations and then made searchable as if they were one index.

TODO: handle deletes

####Multi-index searching
Handle queries that span two or more indices.

<a name="scale" id="scale"></a>
##Scaling

Increase responses per second with more RAM and more caching abilities, faster CPU's and solid disks. 

####Extreme scaling

Buy Gb network and use bare-metal.
Shard and connect each subindex to each other in a network of nodes. Call me, I'll show you.

####Google scale

Service-orient the different parts of Resin and make query parsing, calculating of execution plans, scanning, scoring and resolving of documents each a service of its own. Distribute those services across a machine park, grouped by function. Have redundance and scalability within each group. Here is where .Net and Resin will show its real strengths. Service-orientation with [Nancy](https://github.com/NancyFx/Nancy), messaging with [protobuf-net](https://github.com/mgravell/protobuf-net), GC provided by Microsoft. ;)

<a name="fileformat" id"=fileformat></a>
##Resin file format

All files are protobuf messages.

####Index (generation 0)
0.ix

####Field index
*.fix

####Fields (terms and postings)
*.f  
*.f.tri

####Document index
*.dix

####Documents
*.d
  
<a name="dependencies" id="dependencies"></a>
##Dependencies

Garbage collected by Microsoft .net 4.5.1.

Resin infrastructure:  

* nancy
* protobuf-net
* easyhttp
* jsonfx
  
To the [top&#10548;](#top)
