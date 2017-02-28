# Resin

Resin is a modern information retrieval system and a document based search engine library with fast fuzzy and prefix querying and with customizable scoring (tf-idf and other schemes supported).

## Documentation

##A document.

	{
		"_id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

##Many documents.
	
	var docs = GetWikipediaAsJson();

##Index them.

	var dir = @"C:\Users\Yourname\Resin\wikipedia";
	using (var write = new WriteOperation(dir, new Analyzer(), docs))
	{
		write.Execute();
	}

##Query the index.
<a name="inproc" id="inproc"></a>

	// Resin will scan a disk based binary search tree (a doubly-chained tree)
	// for terms that are an exact match, a near match or is prefixed with the query terms.
	
	// Resin spawns a tree scanning thread for each query term.
	// Postings are fetched for all terms found from the scans.
	// Resin produces a set of postings for each query statement.
	// These are reduced to one set of postings.
	// The reduced set is then scored.
	
	// The top scoring documents are returned with a number describing the total amount of documents,
	// so that you can fetch them by the built in paging mechanism.
	
	var result = new Searcher(dir).Search("description:all matter~ energy* -aliases:astrology");

[More here](https://github.com/kreeben/resin/wiki). 

## Roadmap

- [x] ___Layout basic architecture and infrastructure of a modern IR system - v0.9b___
- [ ] Scan as fast as .Net version of Lucene - v1.0
- [ ] Index at least as fast as .Net version of Lucene - v1.1
- [ ] Merge best parts of the Lucene query language and the Elasticsearch DSL into RQL (Resin query language) - v1.2
- [ ] Reach almost feature parity with .Net version of Lucene - v1.9
- [ ] Scan as fast as JRE version of Lucene - v2.0
- [ ] Index at least as fast as JRE version of Lucene - v2.1
- [ ] Tooling.

## Implementations

[Sir](https://github.com/kreeben/sir) is an Elasticsearch clone built on Resin.
