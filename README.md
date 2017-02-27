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

##A huge number.
	
	var docs = GetWikipediaAsJson();

##Index them.

	var dir = @"C:\Users\Yourname\Resin\wikipedia";
	using (var write = new WriteOperation(dir, new Analyzer(), docs))
	{
		write.Execute();
	}

##Query the index.
<a name="inproc" id="inproc"></a>

	var searcher = new Searcher(dir);
	var result = searcher.Search("description:matter or energy");

[More here](https://github.com/kreeben/resin/wiki). 

## Contribute

Here are some [issues](https://github.com/kreeben/resin/issues) that need to be sorted.

Pull requests are accepted.

## Resin Project Milestones

- [x] ___Layout basic architecture and infrastructure of a modern IR system - v0.9b___
- [ ] Scan at least as fast as .Net version of Lucene - v1.0
- [ ] Index at least as fast as .Net version of Lucene - v1.1
- [ ] Merge best parts of the Lucene query language and the Elasticsearch DSL into RQL (Resin query language) - v1.2
- [ ] Handover control of roadmap to the community - v1.3 - v1.8
- [ ] Reach almost feature parity with .Net version of Lucene - v1.9
- [ ] Scan at least as fast as JRE version of Lucene - v2.0
- [ ] Index at least as fast as JRE version of Lucene - v2.1
- [ ] Merge applicable parts of SQL into RQL - v2.2
- [ ] Handover control of roadmap to the community - v2.3 - v2.7
- [ ] Reach almost feature parity with JRE version of Lucene - v2.8
- [ ] Become the fastest independent IR system in the world - v2.9
- [ ] Tooling.

## Sir

[Sir](https://github.com/kreeben/sir) is an Elasticsearch clone built on Resin.
