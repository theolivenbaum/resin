# ResinDB
In-process document database with pluggable storage engine and full-text search.

## No schema
Your document store can contain documents with variable columns/fields. 

## Vector space bag-of-words model
Scores are calculated using a vector space/tf-idf bag-of-words model.

## Auto-index
By default Resin indexes all fields on all documents. You can opt out of indexing and storing of fields.

## Full-text search
Resin can traverse its index as a Levenshtein-powered automaton. Querying support includes term, fuzzy, prefix, phrase and range. Analyzers, tokenizers and scoring schemes are customizable.

## Disk-based tree traversal
The index is a disk-based left-child-right-sibling character trie. Indices and document stores are very fast to write to and read from.

## Compression
With Resin's default storage engine you have the option of compressing your data with either QuickLZ or GZip. For unstructured data compression leaves a smaller footprint on disk and enables faster writes.

## Pluggable storage engine
Implement your own storage engine through the IDocumentStoreWriter, IDocumentStoreReadSessionFactory, IDocumentStoreReadSession and IDocumentStoreDeleteOperation interfaces.

## Flexible and extensible
Are you looking for something other than a document database or a search engine? Database builders or architects looking for Resin's indexing capabilities specifically and nothing but, can either 
- integrate as a store plug-in
- send documents to the default storage engine storing a single unique key per document but analyzing everything (and then querying the index like you normally would to resolve the primary key)

## Query execution
  
![query](/docs/query.png)

## Log-structured writing

### Write
  
![query](/docs/write.png)

### Delete
  
![query](/docs/delete.png)

### Merge and truncate
Multiple simultaneous writes are allowed. When they happen instead of appending to the main log the index forks into two or more branches. 

Querying is performed over multiple branches but takes a hit performance wise when there are many.

A new segment is a minor or no performance hit.

Issuing multiple merge operations on a directory will lead to forks becoming merged (in order according to their wall-clock timestamp) and then segments becoming truncated. A truncate operation wipes away unusable data leading to increased querying performance and a smaller disk foot-print.

Merging two forks leads to a defragmented but segmented index.

Writing to a store uncompeted yields a segmented index.

Merging a single segmented index results in a unisegmented index.

Attempting to merge a unisegmented index does nothing.

### Rewrite to compress/deflate
If you changed your mind regarding your compression strategy, issue a merge operation on a segmented or unisegmented index specifying the preferred compression rate to rewrite the store.

Note: rewriting a branched store leads to the main trunk being truncated and rewritten but the branches left untouched. Branches may be merged afterwards. Also note that branches may have different compression rates. 

## Supported .net version
Resin is built for dotnet Core 1.1.

## Download
Clone the source or [download the latest source as a zip file](https://github.com/kreeben/resin/archive/master.zip), build and run the CLI or look at the code in the CLI Program.cs to see how querying and writing was implemented.

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

### Store and index documents

	var docs = GetDocumentsTypedAsDictionaries();
	var dir = @"C:\MyStore";
	
	// From memory
	using (var firstBatchBocuments = new InMemoryDocumentStream(docs))
	using (var writer = new UpsertOperation(dir, new Analyzer(), Compression.NoCompression, primaryKey:"id", firstBatchBocuments))
	{
		long versionId = writer.Write();
	}
	
	// From stream
	using (var secondBatchDocuments = new JsonDocumentStream(fileName))
	using (var writer = new UpsertOperation(dir, new Analyzer(), Compression.NoCompression, primaryKey:"id", secondBatchDocuments))
	{
		long versionId = writer.Write();
	}

	// Implement the base class DocumentStream to use whatever source you need.

### Query the index.
<a name="inproc" id="inproc"></a>

	var result = new Searcher(dir).Search("label:good bad~ description:leone", page:0, size:15);

	// Document fields and scores, i.e. the aggregated tf-idf weights a document recieve from a simple 
	// or compound query, are included in the result:

	var scoreOfFirstDoc = result.Docs[0].Score;
	var label = result.Docs[0].Fields["label"];
	var primaryKey = result.Docs[0].Fields["id"];

[More documentation here](https://github.com/kreeben/resin/wiki). 
