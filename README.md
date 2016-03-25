<a name="top" id="top"></a>
# Resin
Fast full-text search that is not built on Lucene.

####Querying a Resin index containing 1M Wikipedia documents
![alt text](https://github.com/kreeben/resin/blob/master/how-fast.png "Resin is this fast with 1M wikipedia docs")  

__[Quick usage guide](#usage)__

__[Relevance (tf-idf)](#relevance)__

__[Why so few classes?](#citizens)__

__[The CLI](#cli)__

__[Roadmap](#roadmap)__

__[At large scale](#scale)__


Use [freely](https://github.com/kreeben/resin/blob/master/LICENSE) and register [issues here](https://github.com/kreeben/resin/issues).

Contribute frequently. Go directly to an [introduction](#citizens) of the parts that make up Resin.  

Use the [CLI](#cli) to build, query and analyze your index.  

<a name="usage" id="usage"></a>
##Quick usage guide

####Here's an interesting document

	{
	  "Fields": {
		"id": [
		  "Q1"
		],
		"label": [
		  "universe"
		],
		"description": [
		  "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy"
		],
		"aliases": [
		  "cosmos The Universe existence space outerspace"
		]
	  },
	  "Id": 1
	}

####Here's a huge number of documents

	var docs = GetHugeNumberOfDocs();

####Add them to a Resin index

	var dir = @"c:\MyResinIndices\0";
	using (var writer = new IndexWriter(dir, new Analyzer()))
	{
		foreach (var doc in docs)
		{
			writer.Write(doc);
		}
	}

####Query that index (matching the whole term)

	using (var searcher = new Searcher(dir))
	{
		var result = searcher.Search("label:universe");
	}

####Prefix query

	var result = searcher.Search("label:univ*");

####Fuzzy

	var result = searcher.Search("label:univ~");

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

	var result = searcher.Search("id:Q1");
	var doc = result.Docs.First();

####All fields queryable, whole document returned

	var result = searcher.Search("id:Q1");
	var doc = result.Docs.First();
	var aliases = doc.Fields["aliases"].First(); // Each field contains a list of values
	// Print "cosmos The Universe existence space outerspace"
	Console.WriteLine(aliases);

####Analyze your index

	var field = "label";
	var scanner = new Scanner(dir);
	var tokens = scanner.GetAllTokens(field).OrderByDescending(t=>t.Count).ToList();
	File.WriteAllLines(Path.Combine(dir, "_" + field + ".txt"), tokens
		.Select(t=>string.Format(
			"{0} {1}", t.Token, t.Count)));

####Resin is fast because

	using (var searcher = new Searcher(dir)) // Initializing the searcher loads the document index
	{
		// This loads and caches the term indices for the "id" and "label" fields
		var result = searcher.Search("id:Q1 label:Q1");
		
		// This executes the query.
		// Resin loads the doc from disk and caches it
		var doc1 = result.Docs.First();
		
		// The following query requires 
		// - a hashtable lookup towards the field file index to find the field ID
		// - a hashtable lookup towards the term index to find the doc IDs
		// - for each doc ID: a hashtable lookup towards the doc cache
		var docs = searcher.Search("label:universe").Docs.ToList();
		
		// The following prefix query requires 
		// - a hashtable lookup towards the field file index to find the field ID
		// - a Trie scan (*) to find matching terms
		// - for each term: a hashtable lookup towards the term index to find the doc IDs
		// - append the results of the scan (as if the tokens are joined by "OR")
		// - for each doc ID: one hashtable lookup towards the doc cache
		docs = searcher.Search("label:univ*").Docs.ToList();
		
	}// Caches are released	 
  
(*) The [Trie](https://github.com/kreeben/resin/blob/master/Resin/Trie.cs).  

<a name="relevance" id="relevance"></a>
##Relevance

Expect the scoring [implemented here](https://github.com/kreeben/resin/blob/master/Resin/Tfidf.cs) to follow the [tf-idf](https://en.wikipedia.org/wiki/Tf%E2%80%93idf) scheme:

`IDF = log ( numDocs / docFreq + 1) + 1`

and with the standard Lucene augumented term frequency `sqrt(TF)` (a formula [about to become legacy](http://opensourceconnections.com/blog/2015/10/16/bm25-the-next-generation-of-lucene-relevation/) for them):

`score = IDF*sqrt(TF)`

<a name="citizens" id="citizens"></a>
##Resin's first class citizens

To be able to call ourselves a full-text search framework we need something that can analyze text, an [Analyzer](https://github.com/kreeben/resin/blob/master/Resin/Analyzer.cs). Also, something that can write index files and store documents, an [IndexWriter](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs), [FieldWriter](https://github.com/kreeben/resin/blob/master/Resin/FieldWriter.cs) and a [DocumentWriter](https://github.com/kreeben/resin/blob/master/Resin/DocumentWriter.cs). 

We will need to be able to parse multi-criteria queries such as "title:Rambo +title:Blood", in other words a [QueryParser](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs). The important questions for the parser to answer are what fields do we need to scan and what are the tokens that should match. A space character between two query terms such as the space  between "Rambo title:" in the query `title:Rambo title:Blood` will be interpreted as `OR`, a plus sign as `AND`, a minus sign as `NOT`. In other words that query will be parsed into "please find documents that has both rambo AND blood in the title", or in a more machine-like language `scan the field named title for the tokens rambo and blood and return the intersection of their postings`.

An [IndexReader](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and a [FieldReader](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) will make it possible for a [Scanner](https://github.com/kreeben/resin/blob/master/Resin/Scanner.cs) to get a list of document IDs containing the tokens at hand, then [scoring](https://github.com/kreeben/resin/blob/master/Resin/Tfidf.cs) them. It should then be possible to return the whole set or page that set (skip and take) before fetching the documents from cache or disk.

####The Analyzer

	public IEnumerable<string> Analyze(string value)
	{
	    var token = new List<char>();
	    foreach (var c in value.ToLower(_culture))
	    {
	        if (IsSeparator(c))
	        {
	            if (token.Count > 0)
	            {
	                var tok = new string(token.ToArray());
	                if (!_stopwords.Contains(tok)) yield return tok;
	                token.Clear();
	            }
	        }
	        else
	        {
	            token.Add(c);
	        }
	    }
	    if (token.Count > 0)
	    {
	        var tok = new string(token.ToArray());
	        yield return tok;
	    }
	}
	
	private bool IsSeparator(char c)
	{
	    return
	        char.IsControl(c) ||
	        char.IsPunctuation(c) ||
	        char.IsSeparator(c) ||
	        char.IsWhiteSpace(c) ||
	        _tokenSeparators.Contains(c);
	}

The least thing we can do in an Analyzer is to inspect each character of each token it's been given. We could let .net do that for us (string.Split) or we can sweep over the string ourselves.

Once we have a character in our hands we need to figure out if it's information or if it's something that separates two tokens or if it's noice.

When we have assembled something that to us looks like a clean, normalized, noice-free token, before we include it in the result, we  check to see if this token is something that you consider to be irrelevant, in which case we shall give that string to the garbage collector and forget it ever existed. The GC might think that's a perfectly good string and it shall keep it. I don't know. But we don't want it. In fact, if we ever see that string again, in a query, we will once again throw it to the garbage. GC will say, perhaps, "hey, there's that perfectly good string again. What's goin' on?".

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

	// docid/fields/values
	private readonly IDictionary<int, IDictionary<string, IList<string>>> _docs;

Here is a document on its own:

	// fields/values
	IDictionary<string, IList<string>> doc;

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

	// tokens/docids/term frequency
	private readonly IDictionary<string, IDictionary<int, int>> _tokens;
	
	// the char's of the tokens, arranged in a tree structure
	private readonly Trie _trie;
	
A field reader can do this:

	var tokens = reader.GetAllTokens();
	var tokensThatStartsWith = reader.GetTokens(prefix);
	var docPos = reader.GetDocPosition(string token);

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/FieldReaderTests.cs)

#### Scanner

After a good parsing we get back a list of terms. A term is a field and a token, e.g. "title:rambo". 

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

TODO: nested clauses

####Fuzzy
Levenshtein Trie scan implemented [here](https://github.com/kreeben/resin/blob/master/Resin/Trie.cs#L55), inspired by [this paper](http://julesjacobs.github.io/2015/06/17/disqus-levenshtein-simple-and-fast.html).

TODO: specify similarity in query as number of allowed [edits](https://en.wikipedia.org/wiki/Levenshtein_distance).

####Scoring
Refine the scoring. The current scoring scheme is [tf-idf](https://en.wikipedia.org/wiki/Tf%E2%80%93idf).The Lucene core team has just recently grown out of tf-idf and now like [bm25](http://opensourceconnections.com/blog/2015/10/16/bm25-the-next-generation-of-lucene-relevation/) better.

####Writing to an index in use, solving concurrency issues
Lucene does it well because of it's file format and because it's indices are easily mergable.

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

To the [top&#10548;](#top)
