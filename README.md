# Resin

Resin is a vector space model implementation, a modern type information retrieval system and a document based search engine library with fast fuzzy and prefix querying and with customizable tokenizers and scoring (tf-idf included, other schemes supported).

## .net version

4.6.1

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
	using (var write = new WriteOperation(dir, new Analyzer(), docs))
	{
		write.Execute();
	}

### Query the index.
<a name="inproc" id="inproc"></a>

	// Resin will scan a disk based trie for terms that are an exact match,
	// a near match or is prefixed with the query term/-s.
	
	// Postings for each term are fetched and scored.
	// A set of postings is produced for each query statement.
	// A final answer is compiled by reducing the postings to one set.
		
	// The top scoring documents are returned with a number describing the total amount of documents in the final set,
	// so that you can fetch them all if you will, by the built in paging mechanism.
	
	var result = new Searcher(dir).Search("label:good bad~ description:leone");
	
	// Document scores, i.e. the aggregated tf-idf weights a document recieve from a simple or compound query,
	// is also included in the result:
	
	var scoreOfFirstDoc = result.Docs.First().Fields["__score"];

[More documentation here](https://github.com/kreeben/resin/wiki). 

### Roadmap

- [x] Layout basic architecture and infrastructure of a modern IR system - v0.9b
- [x] ___Scan as fast as Lucene - v1.0rc1___
- [ ] Merge best parts of the Lucene query language and the Elasticsearch DSL into RQL (Resin query language) - v2.0
- [ ] Make all parts of Resin distributable - v3.0
- [ ] Tooling.

### Implementations

[Sir](https://github.com/kreeben/sir) is an Elasticsearch clone with improved querying capabilities, built on Resin.
