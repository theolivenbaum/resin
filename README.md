# Resin
Resin is a auto-indexing document-based search engine with querying support for term, fuzzy, prefix, phrase and range. Analyzers, tokenizers and scoring schemes are customizable. 

## Auto-indexing
Auto-indexing refers to storing and indexing a document being one and the same operation.

## A smarter index
Resin is a tree like most other indices. Resin indices are fast to write to and read from and support near (as in "almost match") and prefix which is out-of-scope for most database index types.

More formally, Resin's a doubly chained character trie optimized for fast disk-based tree traversing.

Apart from offering fast lookups Resin also scores documents based on their relevance. Relevance in turn is based on the distance from a document and a query in [vector space](https://en.wikipedia.org/wiki/Vector_space_model).

## Supports any scoring scheme
To support the default tf-idf scoring scheme Resin stores term counts. Resin supports any scoring scheme and also gives you the ability to store additional document/sentence/token meta-data your model might need (up-coming feature in RC4). That data will be delivered to you neatly as a field on the document posting. In your custom IScoringScheme you then base your per-document posting calculations on that instead of just the term count.

## Key-value store with score
If you skip the scoring, i.e. create a searcher without a scorer, then Resin is a auto-indexing key-value store, where keys are strings and values are the string representation of any object.

## Fast at indexing and querying
In many scenarios Resin is already faster than the [market "leader"](https://lucenenet.apache.org/) when it comes down to querying and indexing speed, making it a [in-many-scenarios-fastest](https://github.com/kreeben/resin/wiki/Lucene-vs-Resin-1.0-RC2) information retrieval system on the .net plaform and certainly a good choice if you're on dotnet core being there is no real alternative if you're looking for a in-process alternative. [Here](https://github.com/kreeben/sir) is an out-of-process implementation of Resin.

If you have a scenario where you feel Resin should do better, this is important information for me. Please let me know. I'm both curious about those special cases and I'd love to optimize for them.

## Not based on Lucene
[Half a decade](https://blogs.apache.org/lucenenet/entry/lucene_net_3_0_3) has passed since what we in the .net community consider to be state-of-the-art search tech was built.

Who could use a modern and powerful search engine based on sound mathematics that's open source, extensible and built on Core, though?

## Stable (API and file format) in RC4
Resin's API and file format should be considered unstable until release candidate 4.

## Supported .net version
Resin is built for dotnet Core 1.1.

## Download
Latest release is [here](https://github.com/kreeben/resin/releases/latest) but I suggest you clone the source or [download the latest source as a zip file](https://github.com/kreeben/resin/archive/master.zip), build and run. 

## Start with the CLI
The command line tool, rn.exe, can be executed after you have built the source.
1. Find the folder where you have the Resin source code. 
2. Right-click on that folder while pressing Shift on your keyboard to open a command prompt. 
3. Use it like I do in this [benchmark test](https://github.com/kreeben/resin/wiki/Lucene-vs-Resin-1.0-RC1).

## Help out?
Definitely start [here](https://github.com/kreeben/resin/issues).

## Documentation
### A document (serialized).

	{
		"id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

### Batch many of those together and store them on disk.

	var docs = GetWikipedia();
	var dir = @"C:\Users\Yourname\Resin\wikipedia";
	using (var upsert = new DocumentUpsertOperation(dir, new Analyzer(), compression:true, primaryKey:"id", docs))
	{
		upsert.Commit();
	}
	
### Store documents that are encoded in a stream source.

	using (var docs = new FileStream(fileName))
	using (var upsert = new StreamUpsertOperation(dir, new Analyzer(), compression:true, primaryKey:"id", docs))
	{
		upsert.Commit();
	}
	
	// Implement the base class UpsertOperation to use whatever document source you need.
	
### Query the index.
<a name="inproc" id="inproc"></a>

	varr result = new Searcher(dir).Search("label:good bad~ description:leone", page:0, size:15);
	
	// Document fields and scores, i.e. the aggregated tf-idf weights a document recieve from a simple 
	// or compound query, are included in the result:
	
	var scoreOfFirstDoc = result.Docs.First().Fields["__score"];
	var label = result.Docs.First().Fields["label"];

[More documentation here](https://github.com/kreeben/resin/wiki). 
