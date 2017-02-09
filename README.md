Here's the thing. [Apache Lucene](https://lucene.apache.org/) is an outstanding [information retrieval](https://en.wikipedia.org/wiki/Information_retrieval) system built on the Java runtime. There is a .Net alternative but the [Lucene.Net](https://lucenenet.apache.org/) team doing the line-by-line porting of Java code to C# code are to this date three major versions behind the Java team. Also, the Lucene code base is big and complex and difficult to understand. Maybe, the .Net runtime is better suited for IR than the JRE? This is a great opportunity to find out and to build a new information retrieval system.

# Resin

Resin is a modern information retrieval system, a document based search engine library and framework, and a Lucene clone.

## Documentation

##Here's a document.

	{
		"_id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

##Say you have a huge number of those.
	
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

## The Resin Project Mission

- [x] Layout basic architecture and infrastructure of a modern IR system - v0.9b
- [ ] Index and scan at least as fast as the lastest .Net version of Lucene - v1.0
- [ ] Reach almost feature parity with the lastest .Net version of Lucene - v1.9
- [ ] Index and scan at least as fast as the lastest JRE version of Lucene - v2.0
- [ ] Reach almost feature parity with the lastest JRE version of Lucene - v2.9
- [ ] Become the fastest independent IR system in the world - v3.0
- [ ] Become the most feature-complete indie IR system in the world - v.3.9
- [ ] Hand over to the next generation IR freaks

## Power to the people (Not a communist)

Great knowledge about IR can be found within the big tech coorporations of today, Google, Microsoft, Facebook. It is undemocratic that people can use the information available on the internet solely through the means of services that coorporations offer.

The internet's data is vast. Because it is so it can't be properly digested by humans unless a machine first makes sense of it. Google, Microsoft and Facebook have great machines. That's great. There should be alternative services though, free like Wikipedia but for search, and there should be business models where the personal integrity of users are not for sale. Something else can be for sale. Maybe the integrity of the businesses can be for sale instead, for a change.

Anyway, IR is perhaps the smallest and most fundemental component of AI.

AI should empower us all. It should not be monopolized on by coorporations. It should be free.

Peace

## Sir

[Sir](https://github.com/kreeben/sir) is an Elasticsearch clone built on Resin.
