# Resin
Resin can analyze, index and store documents and has querying support for term, fuzzy and prefix. It's a document-based search engine and analytics tool. Analyzers, tokenizers and scoring schemes are customizable. 

## It's a smarter index
Resin can be seen as an index of the same kind you attach to relational database tables when you want to make reading from them fast. Resin indices are fast to write and read from and support near (as in "almost match") which is out-of-scope for most database index types.

Apart from offering fast lookups, like a database index, Resin also scores documents based on their relevance. Relevance in turn is based on the distance from a document and a query in vector space.

## Supports any scoring scheme
To support the default tf-idf scoring scheme Resin stores term counts. Resin supports any scoring scheme and also gives you the ability to store additional document/sentence/token meta-data your model might need (up-coming feature in RC4). That data will be delivered to you neatly as a field on the document posting. In your custom IScoringScheme you then base your per-document posting calculations on that instead of just the term count.

## Don't score
If you skip the scoring, i.e. create a searcher without a scorer, then Resin is a auto-indexing key-value store, where keys are strings and values are the string representation of any object.

## Fast at indexing and querying
In many scenarios Resin is already faster than the [market leader](https://lucenenet.apache.org/) when it comes down to querying and indexing speed, making it a [in-many-scenarios-fastest](https://github.com/kreeben/resin/wiki/Lucene-vs-Resin-1.0-RC2) information retrieval system on the .net plaform and certainly a good choice if you're on dotnet core being there is no real alternative. 

If you have a scenario where you feel Resin should do better, this is important information for me. Please let me know. I'm both curious about those special cases and I'd love to optimize for them.

## Deeply influenced but not based on Lucene
[Half a decade](https://blogs.apache.org/lucenenet/entry/lucene_net_3_0_3) has passed since what we in the .net community consider to be state-of-the-art search tech was built.

Who could use a modern and powerful search engine based on sound mathematics that's open source, extensible and built on Core, though?

## Stable (API and file format) in RC4
Resin's API and file format should be considered unstable until release candidate 4. Coming features are indexing support for numbers and dates as well as support for range queries.

## Supported .net version
Resin is built for dotnet Core 1.1.

## Download
Latest release is [here](https://github.com/kreeben/resin/releases/latest)

## Help out?
Definitely start [here](https://github.com/kreeben/resin/issues).

## Documentation
### A document.

	{
		"_id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

### Index many like that.

	var docs = GetWikipedia();
	var dir = @"C:\Users\Yourname\Resin\wikipedia";
	using (var upsert = new DocumentUpsertOperation(dir, new Analyzer(), compression:true, primaryKey:"_id", docs))
	{
		upsert.Commit();
	}
	
### Documents as a stream

	using(var docs = new FileStream(fileName))
	using (var upsert = new StreamUpsertOperation(dir, new Analyzer(), compression:true, primaryKey:"_id", docs))
	{
		upsert.Commit();
	}
	
	// Implement the base class UpsertOperation to use whatever document source you need.
	
### Query the index.
<a name="inproc" id="inproc"></a>

	varr result = new Searcher(dir).Search("label:good bad~ description:leone", page:0, size:15);
	
	// Document scores, i.e. the aggregated tf-idf weights a document recieve from a simple 
	// or compound query, are included in the result:
	
	var scoreOfFirstDoc = result.Docs.First().Fields["__score"];

[More documentation here](https://github.com/kreeben/resin/wiki). 

### Roadmap

- [x] Layout basic architecture and infrastructure of a modern IR system - v0.9b
- [x] Query faster than Lucene - v1.0 RC1
- [x] Index faster than Lucene - v1.0 RC2
- [x] ___Compress denser than Lucene - v1.0 RC3___
- [ ] Range query, grouping of query statements - 1.0
- [ ] Build Sir, a distributed search engine

### Sir

[Sir](https://github.com/kreeben/sir) is Resin distributed, a search engine, map/reduce system and long-term data storage solution in one, a Elasticsearch+Hadoop replacement.
