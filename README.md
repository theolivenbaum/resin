<a name="top" id="top"></a>
# Resin

[Launch a search server in seconds](#inproc), with Solr-like capabilities but with zero config. [Consume](#usage) its API from javascript or C#, or use the [CLI](#cli) to build, query and analyze your index. 

Solve your full-text search problem or your big data analysis task with an intuitive tool for information retrieval with an extensible model, a strong architecture and a tiny bit of infrastructure. Resin is stream-lined, free of legacy code and java inheritance and aims to simplify what it is what makes Lucene, Solr and Elasticsearch great: speed of indexing and query execution, relevance, reliablility, and cost, but also: 

* zero-config (`git clone`, `build`, `start server`)
* great API (in-proc and HTTP, with optional .net client)
* CLI

Resin is an extensible and multi-cultural search framework written specifically for .net. It has great analysis skills and fast response times even to complex queries. 

Resin's document-centric nature (shared by its cousins from javaland) makes it effectively a schema-less document-oriented json database with ad-hoc quering.

Read about the dependencies you need to [worry about](#dependencies) before launching your beefed-up search server that serves up 100M documents and that matches Google's capabilities in scale and performance. Spoiler alert: there are none. Instead, read it to learn about the open and closed-source projects Resin depends on.

* _[Quick usage guide](#usage)_
* _[Relevance (tf-idf)](#relevance)_
* _[The CLI](#cli)_
* _[Backlog](#roadmap)_
* _[At large scale](#scale)_
* _[Dependencies](#dependencies)_
* _[File format](#fileformat)_

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
	// rnh --url http://localhost:1234/
	// You have just started a light-weight search server with solr-like capabilities. Enjoy!
	
	var searcher = new SearchClient("wikipedia", "http://localhost:1234/");
	var result = searcher.Search("label:universe");
	
	// To shut down the server, in the same cmd window, type "stop" and press enter.
	// To restart it (release its caches), type "restart" instead.

#####JQuery

	$("#searchbtn").click(function() {
	
	  var token = $("#query").val();
	  if (query.length == 0) return;
	
	  var url = "http://localhost:1234/wikipedia/?callback=?";
	  var q = "label:" + token;
	  var params = {"query":q, "page":"0", "size":"20"};
	  var result = $("#result");
	  result.empty();
	
	  $.getJSON(url, params, function(data) {
	    for(var i = 0;i < data.docs.length;i++){
	      result.append( "<li>" + data.docs[i].label + "</li>");
	    }
	  });
	});

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
		// This loads and caches the term indices for the "_id" and "label" fields:
		
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
  
(*) A [Trie](https://github.com/kreeben/resin/blob/master/Resin/Trie.cs) has a leading role in the querying routine.  

<a name="relevance" id="relevance"></a>
##Relevance

The scoring [implemented here](https://github.com/kreeben/resin/blob/master/Resin/Tfidf.cs) follows the [tf-idf](https://en.wikipedia.org/wiki/Tf%E2%80%93idf) scheme for a probabilistic inverse document frequency:

`IDF = log ( numDocs - docFreq / docFreq)`

with a sqrt and log-normalized term frequency `1+log(sqrt(TF))`.

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
##Backlog

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

TODO: handle deletes, optimize

####Multi-index searching
Handle queries that span two or more indices.

####Out-of-proc indexing
As of now, indices are created locally.

####Tooling
Refine the CLI to a state where you're starting to wish MS SQL Server Management Studio was a command line tool. Oh, the power you shall have.

####Facetting
Help!

<a name="scale" id="scale"></a>
##Scaling

Increase responses per second with more RAM and more caching abilities, faster CPU's and solid disks.

####Extreme scaling

Buy Gb network and use bare-metal.

####Google scale

Service-orient the different parts of Resin and make query parsing, calculating of execution plans, scanning, scoring and resolving of documents each a service of its own. Distribute those services across a machine park, grouped by function. Have redundance and scalability within each group: service-orientation with [Nancy](https://github.com/NancyFx/Nancy), messaging with [protobuf-net](https://github.com/mgravell/protobuf-net), System.String and GC provided by Microsoft. ;)

<a name="dependencies" id="dependencies"></a>
##Dependencies

.net 4.5.1:

* Microsoft.CSharp
* System
* System.Core
* System.Xml
* System.Net.Http

Packages:  

* [nancyfx](https://github.com/NancyFx/Nancy)
* [protobuf-net](https://github.com/mgravell/protobuf-net)
* [json.net](https://github.com/JamesNK/Newtonsoft.Json)
* [nunit](https://www.nuget.org/packages/NUnit/)
* [log4net](https://www.nuget.org/packages/log4net/)

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

To the [top&#10548;](#top)
