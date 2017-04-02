# Resin

Resin is a vector space model implementation, a modern type search and analytics framework and a document based IR library with fast fuzzy and prefix querying and with customizable tokenizers and scoring (tf-idf included, other schemes supported).

Resin outperforms [the market leader's](https://lucenenet.apache.org/) querying and indexing speed making it [the fastest](https://github.com/kreeben/resin/wiki/Lucene-vs-Resin-1.0-RC2) IR system on the .net plaform (available Q3 on Core 2.0).

## .net version

4.6.1

## Download

Latest release is [here](https://github.com/kreeben/resin/releases/latest)

## Documentation

### A document.

	{
		"_id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

### Many like that.
	
	var docs = GetWikipediaAsJson();

### Index them.

	var dir = @"C:\Users\Yourname\Resin\wikipedia";
	using (var write = new WriteOperation(dir, new Analyzer()))
	{
		write.Write(docs);
	}

### Query the index.
<a name="inproc" id="inproc"></a>

	// Resin will scan a disk based trie for terms that are an exact match,
	// a near match or is prefixed with the query term/-s.
	
	// A set of postings is produced for each query statement.
	// A final answer is compiled by reducing the query tree into one node, postings into one set.
		
	// Postings are resolved into top scoring documents. A total hit count is also included.
	// Paging is fast using the built-in paging mechanism.
	
	var result = new Searcher(dir).Search("label:good bad~ description:leone");
	
	// Document scores, i.e. the aggregated tf-idf weights a document recieve from a simple or compound query,
	// are also included in the result:
	
	var scoreOfFirstDoc = result.Docs.First().Fields["__score"];

[More documentation here](https://github.com/kreeben/resin/wiki). 

### Roadmap

- [x] Layout basic architecture and infrastructure of a modern IR system - v0.9b
- [x] ___Be fast - v1.0 rc1___
- [ ] Create http-transportable query language where you can express table joins, or adopt [GrapohQL](http://graphql.org/) - v2.0
- [ ] Make all parts of Resin distributable - v3.0
- [ ] Build Sir

### Sir

[Sir](https://github.com/kreeben/sir) is an Elasticsearch clone with improved querying capabilities, built on Resin.
