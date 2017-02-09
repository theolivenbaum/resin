# Resin

Resin is a modern information retrieval system, a document based search engine library and framework, and a Lucene clone.

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
	using (var writer = new IndexWriter(dir, new Analyzer()))
	{
		writer.Write(docs);
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

- [x] Layout basic architecture and infrastructure of a modern IR system - v0.9b
- [ ] Index and scan at least as fast as .Net version of Lucene - v1.0
- [ ] Reach almost feature parity with .Net version of Lucene - v1.9
- [ ] Index and scan at least as fast as JRE version of Lucene - v2.0
- [ ] Reach almost feature parity with JRE version of Lucene - v2.9
- [ ] Become the fastest independent IR system in the world - v3.0

## Sir

[Sir](https://github.com/kreeben/sir) is an Elasticsearch clone built on Resin.
