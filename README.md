<a name="top" id="top"></a>
# Resin

_A speedy, light-weight, schema-less search server and framework with zero config._

* _[Introduction](#intro)_
* _[Quick usage guide](#usage)_
* _[Data availability](#data-availability)_
* _[Relevance (tf-idf)](#relevance)_
* _[Backlog](#roadmap)_
* _[At large scale](#scale)_
* _[Dependencies](#dependencies)_
* _[File format](#fileformat)_

<a name="intro" id="intro"></a>
##Introduction
[Launch a search server in seconds](#inproc), with Solr-like capabilities but with zero config. [Consume](#usage) its entire API from [javascript](#jquery) or C#, or use the CLI to build, query and analyze your index. 

Solve your full-text search problem or your big data analysis task with an intuitive tool for information retrieval armed with an extensible model and a strong architecture. Resin is stream-lined, free of legacy code and java inheritance and aims to simplify what it is that make Lucene, Solr and Elasticsearch great: __speed of indexing and query execution, relevance, reliablility__, but also: 

* zero-config (`git clone`, `build`, `start server`)
* easy-to-use API (in-proc and HTTP, with optional .net client or from [javascript](#jquery))
* CLI tooling
* multi-cultural

Resin's document-centric nature (shared by its cousins from javaland) makes it effectively _a schema-less [document-oriented json database](https://en.wikipedia.org/wiki/Document-oriented_database) with ad-hoc full-text quering_.

Learn about the open-source projects Resin [depends on](#dependencies).

<a name="usage" id="usage"></a>
##Quick usage guide
####Here's a document

	{
		"_id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

Fields prefixed with `_` are not analyzed. The `_id` field is mandatory.  

####Here's a huge number of documents
	
	var docs = GetHugeNumberOfDocs();

####Add them to a Resin index
#####In-proc
	var dir = @"C:\Users\Yourname\Resin\wikipedia";
	using (var writer = new IndexWriter(dir, new Analyzer()))
	{
		foreach (var doc in docs)
		{
			writer.Write(doc);
		}
	}

#####Client
	
	// To start the server, in a cmd window:
	// cd path_to_resin
	// rnh --url http://localhost:1234/

	using (var client = new WriterClient("wikipedia", url))
	{
		client.Write(docs);
	}
        
	// To shut down the server, in the same cmd window, type "stop" and press enter.
	// To restart it (release its caches), enter "restart" instead.

####Query that index (matching the whole term)
<a name="inproc" id="inproc"></a>
#####In-proc

	var searcher = new Searcher(dir);
	var result = searcher.Search("label:universe");

#####Client
	
	var searcher = new SearchClient("wikipedia", "http://localhost:1234/");
	var result = searcher.Search("label:universe");

<a name="jquery" id="jquery"></a>
#####JQuery

	$("#searchbtn").click(function() {
	
	  var token = $("#query").val();
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
	var scanner = new FieldScanner(dir);
	var termsOrderedByFreq = scanner.GetAllTokens(field).OrderByDescending(t=>t.Count).ToList();
	
	File.WriteAllLines(Path.Combine(dir, "_" + field + ".txt"), termsOrderedByFreq
		.Select(t=>string.Format("{0} {1}", t.Token, t.Count)));

####Reading and writing
Each write session is an automic operation. During writes, the last known baseline is still readable and consistent with its initial state.

A write is a commit. Each commit is an index. An index is a set of deletions and document upserts. Newer commits are treated as changesets to older commits. 

Writing to an empty directory will create a commit and a baseline. Subsequent commits can be applied to the last baseline by calling Optimizer.Rebase(). That state can be made into a new baseline by calling Optimizer.Save().

The "_id" field of a document is mandatory. A document may be queried by its ID immediately after a write but until the commit has been applied to the baseline and a new baseline has been created queries towards other fields are not possible.

Your directory will eventually contain commits that have already been applied. Those that are older than the last baseline can be deleted by calling Optimizer.Truncate().

If you do not truncate, you can do this: Optimizer.RewindTo(fileNameOfCommit);

When refreshing a Searcher (i.e. creating a new instance), the newest baseline as well as uncommited documents queryable by their ID can be read.

<a name="relevance" id="relevance"></a>
##Relevance

The default scoring implementation follows a [tf-idf](https://en.wikipedia.org/wiki/Tf%E2%80%93idf) scheme for a probabilistic inverse document frequency:

`IDF = log ( numDocs - docFreq / docFreq)`

with a slightly normalized term frequency `sqrt(TF)`. 

Call Searcher.Search with `returnTrace:true` to include an explanation along with the result of how the scoring was calculated.

<a name="data-availability" id="data-availability"></a>
##Data availability

####Actors included in the Resin Data Availability Scheme
[IndexWriter](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs)  
[Searcher](https://github.com/kreeben/resin/blob/master/Resin/Searcher.cs)   
[Optimizer](https://github.com/kreeben/resin/blob/master/Resin/Optimizer.cs)  

<a name="roadmap" id="roadmap"></a>
##Backlog

####Query language interpreter
AND, OR, NOT (+ -), prefix* and fuzzy~ [implemented here](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs).

TODO: nested clauses, phrases

####Specify which doc fields to return in the response
Cache doc fields instead of whole docs.

####Fuzzy
Levenshtein Trie scan implemented [here](https://github.com/kreeben/resin/blob/master/Resin/Trie.cs#L55), inspired by [this paper](http://julesjacobs.github.io/2015/06/17/disqus-levenshtein-simple-and-fast.html).

TODO: override default similarity in query: `label:starr~0.8`.

####Phrase and range query
Implement token position as a custom metric. Use that metric to score documents in a range query based on how far the tokens are from each other. Then implement phrase query by parsing `fielName:this is a phrase` into `fieldName:[this TO is] + fieldName:[is TO a] fieldName:[a TO phrase]`

####Multi-index searching
Handle queries that span two or more indices.

####Tooling
Refine the CLI to a state where you're starting to wish MS SQL Server Management Studio was a command line tool. The power you shall have.

####Facetting
Killer feature!

####Windows service
Implement host as service.

####Extensibility
Allow use of custom IScorer implementations.

Make the infrastructure for analysis extensible so that one can implement custom metrics, e.g. doc length, avg. word length or any other piece of data other than the term freq that is already being stored. Custom data ends up in .dfo files (doc info files) and is available in the querying pipeline, e.g. within the context of an IScorer.

####Scoring
Refine the scoring. Implement BM25 and different flavors of TFIDF. Use custom metrics to support them.

<a name="scale" id="scale"></a>
##Scaling

Increase responses per second with more RAM and more caching abilities, faster CPU's and solid disks.

####Extreme scaling

Buy Gb network and use bare-metal.

####Bing scale

Service-orient the different parts of Resin and make query parsing, calculating of execution plans, scanning, scoring and resolving of documents each a service of its own. Distribute those services across a machine park, grouped by function. Have redundance and scalability within each group: service-orientation with [Nancy](https://github.com/NancyFx/Nancy), messaging with [protobuf-net](https://github.com/mgravell/protobuf-net), System.String and GC provided by Microsoft. ;)

<a name="dependencies" id="dependencies"></a>
##Dependencies

.net 4.5.1:

* Microsoft.CSharp
* System
* System.Configuration
* System.Core
* System.Net.Http
* System.Net.Http.Formatting
* System.Xml

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
