# Resin
Resin is a vector space model, a search/analytics framework and a document store that offers compression. Querying support includes exact, fuzzy and prefix, soon also range (up-coming feature in RC 4), and comes with customizable tokenizers and scoring schemes. 

## Query language
The current query language is a copy of [Lucene's](https://lucene.apache.org/core/2_9_4/queryparsersyntax.html) (minus range and grouping). 

On the roadmap is an extended query language with support for write, read and merge operations and the ability to express range, grouping, index joins (the equivalent of a table join in SQL) and database joins. As these features mature they will end up as commands in the query language. 

## It's an index (that you can read)
From an angle Resin is an index of the same kind you attach to database tables when you want to make reading from them fast. Certain types of database indices are as full-featured as Resin indices are but usually you'll use one without the support for near (as in "almost") matches to achieve decent write speeds, which leaves your toolbox empty of two of Resin's expert features, the LcrsTrie and its disk-based equivalent, the LcrsNode.

## Supports any scoring scheme
Out-of-the-box, to support the default tf-idf scoring scheme Resin will store term counts. To support any scoring scheme Resin gives you the ability to store any additional data (up-coming feature in RC4). That data will be delivered to you neatly as a field on the document posting. In your custom IScoringScheme you then base your per-document posting calculations on that data instead of just the term count.

## Fast at indexing and querying
In many scenarios Resin is already faster than the [market leader](https://lucenenet.apache.org/) when it comes down to querying and indexing speed, making it a [in-many-scenarios-fastest](https://github.com/kreeben/resin/wiki/Lucene-vs-Resin-1.0-RC2) information retrieval system on the .net plaform. 

When Resin is not faster than Lucene, most of the times it's because it hasn't yet been optimized nor has it been micro-optimized for that particular scenario ;). If you have a scenario where you feel Resin should do better, this is important to me. Let me know.

## Deeply influenced by but not based on a java port
Five years ago the .net community created the search engine, Lucene 3.0.3, we are still using today.

Who could use a modern and powerful search engine based on sound mathematics that's open source, extensible and built on Core, though?

## Philosophy

There shall be nothing in its architecture and infrastructure nor anything in the platform it was built on or in the way the project is managed that stops Resin from being the fastest and most precise search engine on the planet.

## Stable API and file format in RC3
Resin's API and file format should be considered unstable until release candidate 3. Coming features are indexing support for IComparable instead of just strings, improved compression of documents by representing them as tries, and updates/merges of documents.

## Supported .net version
Resin is built for 4.6.1 but have no dependancies on any Core-incompatible technology so will be available on both frameworks soon.

## Download
Latest release is [here](https://github.com/kreeben/resin/releases/latest)

## Help out?
Start [here](https://github.com/kreeben/resin/issues).

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
	
	// At each EndOfWord node there is a pointer to a set of postings.
		
	// The postings are resolved into top scoring documents. A total hit count is also included.
	
	// Paging is fast using the built-in paging mechanism.
	
	var result = new Searcher(dir).Search("label:good bad~ description:leone");
	
	// Document scores, i.e. the aggregated tf-idf weights a document recieve from a simple or compound query,
	// are also included in the result:
	
	var scoreOfFirstDoc = result.Docs.First().Fields["__score"];

[More documentation here](https://github.com/kreeben/resin/wiki). 

### Roadmap

- [x] Layout basic architecture and infrastructure of a modern IR system - v0.9b
- [x] Query faster than Lucene - v1.0 RC1
- [x] ___Index faster than Lucene - v1.0 RC2___
- [ ] Compress better than Lucene - v1.0 RC3
- [ ] Migrate to Core - v1.0
- [ ] Build Sir, a distributed search engine

### Sir

[Sir](https://github.com/kreeben/sir) is a distributed search engine, map/reduce system and long-term data storage solution in one with aspirations of being a Elasticsearch+Hadoop replacement and the end-of-the road for your data, not by being a cemetary as most archiving solutions of today but by compressing data in a way where it is very much alive and responsive to querying even at vast scales. 

Don't just archive your data. Surf on top of it or find yourself engulfed by it.
