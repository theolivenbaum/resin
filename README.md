# Resin
Resin is a in-process document database and word2vec implementation with full-text disk-based index, word vector space and tf-idf-driven scoring, Levenshtein-powered automaton (-ish) tree traversing and with querying support for term, fuzzy, prefix, phrase and range. Analyzers, tokenizers and scoring schemes are customizable.

With Resin's default storage engine you have the option of compressing your data with either QuickLZ or GZip. For unstructured data compression leaves a smaller footprint on disk and enables faster writes.

The index is a disk-based left-child-right-sibling character trie. Indices and document store are very fast to write to and read from.

The document storage engine is pluggable. Implement your own storage engine through the IDocumentStoreWriter, IDocumentStoreReadSessionFactory, IDocumentStoreReadSession and IDocumentStoreDeleteOperation interfaces.

The contracts for a custom storage engine does not dictate its write model. Resin achieves read and write consistency through the use of timestamps and snapshots, its native document storage likewise. A custom engine can follow this principle but may also choose other read/write models.

Database builders in need of Resin's indexing capabilities specifically and nothing but, can either 
- integrate as a store pluggin
- implement a null (or lesser) store and query the index (like you normally would) to resolve documents by e.g. primary key
- send documents to the default storage engine with only PK as stored field (and then query the index to resolve it)

## Supported .net version
Resin is built for dotnet Core 1.1.

## Download
Clone the source or [download the latest source as a zip file](https://github.com/kreeben/resin/archive/master.zip), build and run the CLI or look at the code in the CLI Program.cs to see how querying and writing was implemented.

## Demo
[searchpanels.com](http://searchpanels.com)  

## Help out?
Awesome! Start [here](https://github.com/kreeben/resin/issues).

## Documentation
### A document (serialized).

	{
		"id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

_Download Wikipedia as JSON [here](https://dumps.wikimedia.org/wikidatawiki/entities/)._

### Store documents residing in memory (compression is optional).

	var docs = GetWikipedia();
	var dir = @"C:\wikipedia";
	
	using (var documents = new InMemoryDocumentStream(docs))
	using (var writer = new UpsertOperation(dir, new Analyzer(), Compression.Lz, primaryKey:"id", documents))
	{
		long versionId = writer.Write();
	}
	
### Store JSON documents encoded in a stream.

	using (var documents = new JsonDocumentStream(fileName, skip, take))
	using (var writer = new UpsertOperation(dir, new Analyzer(), Compression.NoCompression, primaryKey:"id", documents))
	{
		long versionId = writer.Write();
	}

	// Implement the base class DocumentStream to use whatever source you need.

It's perfectly fine to mix compressed and non-compressed batches inside a document store (directory).
	
### Query the index.
<a name="inproc" id="inproc"></a>

	var result = new Searcher(dir).Search("label:good bad~ description:leone", page:0, size:15);

	// Document fields and scores, i.e. the aggregated tf-idf weights a document recieve from a simple 
	// or compound query, are included in the result:

	var scoreOfFirstDoc = result.Docs[0].Fields["__score"];
	var label = result.Docs[0].Fields["label"];

[More documentation here](https://github.com/kreeben/resin/wiki). 
