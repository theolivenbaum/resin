# Resin

Resin is a vector space model implementation, a modern type search and analytics framework and a document based IR library with fast fuzzy and prefix querying and with customizable tokenizers and scoring (tf-idf included, other schemes supported).

Resin outperforms [the market leader's](https://lucenenet.apache.org/) querying and indexing speed making it [the fastest](https://github.com/kreeben/resin/wiki/Lucene-vs-Resin-1.0-RC2) IR system on the .net plaform. 

## Resin as a database

What you need is somewhere to store your big data, e.g. the log files from your web site or the training corpus of a NLP machine, compressed but in a format where your data is still queryable.

If you represent the corpus in a trie, which has the natural ability of compression, then you have queryability on a normalized and compressed version of the data. What you have is a search engine.

Now you need to solve compression of the documents in the state they were in before you normalized and compressed them because that version of the data is what will be requested when you respond to queries. 

That data can also be represented in a trie. By chopping the text up at only one point, the space in between words, and storing the pieces and their casing state together with their position in the document, you have a compressed form that when decompressed has a state identical to its initial state. What you now have is a database, or more precisely, a reason to use your search engine as a database.

In a relational database data is stored in tables and indexes in trees. In a search engine a compressed form of the data is stored in indices and how they store the actual data is irrelevant. In Resin, indices and documents (the actual data) are tries, both first-class citizens.

Resin is like a relational database where you only use indexing feature of the database. You store data in indices and pointers to that data also in indices. What you end up with are pointers to pointers that in the end locate a document, an inverted index that can recreate the data in its initial state and query it, fast, through exact, fuzzy, prefix and range searches and with string, numbers, geolocations or dates.

## Versions

Resin 1.0 will be released shortly. Resin's API and file format should be considered unstable until release candidate 3. Coming featues are indexing support for IComparable instead of just strings, and improved compression of documents by representing them as tries.

Resin will be available on Core 2.0 Preview before Q3 2017

## Supported .net version

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
