# ResinDB
ResinDB is a in-process document database with full-text search and loosely coupled storage engine. ResinDB may be used as a database, or as an index to your database or key/value store.

[Resin system architecture documentation](https://github.com/kreeben/resin/blob/master/docs/Resin%20overview.pdf)

## Reads are purely disk-based
When compared to a DBMS, e.g. SQL Server, a ResinDB instance is "always-off" as opposed to a SQL Server instance being "always-on". There is no state or in-memory datastructure that needs to be rebuilt before ResinDB can respond to a query.

The default index type is a fast disk-based and bitmapped left-child-right-sibling character trie.

## No schema
Store documents with variable number columns/fields. 

Group similar documents into separate stores or have them all in a big store.

## Column-oriented indexing
By default Resin creates and maintains an index per document field. 

You can opt out of indexing (analyzing) and storing of fields.

## Row-based compression
With Resin's default storage engine you have the option of compressing your data with either QuickLZ or GZip. For unstructured data this leaves a smaller footprint on disk and enables faster writes.

Compression is row-based. Querying performance affected very little. It is the contents of the document storage file that is compressed and that file is touched after the index lookup and the scoring. 

## Full-text search
Querying support includes term, fuzzy, prefix, phrase and range. 

## Vector space bag-of-words model
Scores are calculated using the default scoring scheme which is a vector space/tf-idf bag-of-words model.

Scoring is column-oriented. Analyzed fields participate in the scoring.

## Map/reduce
Query: "What is a cat?"

Parse into document: [what,is,a,cat]

Scan index: what  
Scan index: is  
Scan index: a  
Scan index: cat  

Found documents: 

[(i), (have), a, cat],   
[what, (if), (i), (am), a, cat]  

Normalize to fit into 4-dimensional space:  
[what,is,a,cat]  
[null, null, a, cat],  
[what, null, a, cat]  

Give each word a weight (tf-idf):  
[0.2, 0.1, 0.1, 3],  
[0,        0, 0.1, 3],   
[0.2,     0, 0.1, 3]   

Map the query and the documents in vector space, sort by the documents' (Euclidean) distance from the query document, paginate and as a final step, fetch documents from the filesystem. 

## Pluggable storage engine
Implement your own storage engine through the IDocumentStoreWriter, IDocumentStoreReadSessionFactory, IDocumentStoreReadSession and IDocumentStoreDeleteOperation interfaces.

## Flexible and extensible
Analyzers, tokenizers and scoring schemes are customizable.

Are you looking for something other than a document database or a search engine? Database builders or architects looking for Resin's indexing capabilities specifically and nothing but, can either 
- integrate as a store plug-in
- send documents to the default storage engine storing a single unique key per document but analyzing everything (and then querying the index like you normally would to resolve the primary key)

## Merge and truncate
Multiple simultaneous writes are allowed. When they happen instead of appending to the main log the index forks into two or more branches and the document file fragments into two or more files. 

Querying is performed over multiple branches but takes a hit performance-wise when there are many.

A new segment is a minor performance hit.

Issuing multiple merge operations on a directory will lead to forks becoming merged (in order according to their wall-clock timestamp) and segments becoming truncated. Merge and truncate truncate operation wipe away unusable data and lead to increased querying performance and a smaller disk foot-print.

Merging two forks leads to a defragmented but segmented index.

Writing to a store uncompeted yields a segmented index.

Issuing a merge operation on a single segmented index results in a unisegmented index. If the merge operation was uncompeted the store will now have a single branch/single segment index.

## Supported .net version
Resin is built for dotnet Core 1.1.

## Usage
### CLI
Clone the source or [download the latest source as a zip file](https://github.com/kreeben/resin/archive/master.zip), build and run the CLI (rn.bat) with the following arguments:

	rn query --dir c:\resin\data\wikipedia -q "label:the good the bad the ugly" -p 0 -s 10
	rn write --file c:\temp\0wikipedia.json --dir c:\resin\data\wikipedia --skip 0 --take 1000000
	rn delete --ids "Q1476435" --dir c:\resin\data\wikipedia
	rn merge --dir c:\resin\data\wikipedia
### API
#### A document (serialized).

	{
		"id": "Q1",
		"label":  "universe",
		"description": "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy",
		"aliases": "cosmos The Universe existence space outerspace"
	}

_Download Wikipedia as JSON [here](https://dumps.wikimedia.org/wikidatawiki/entities/)._

#### Store and index documents

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

#### Query the index.
<a name="inproc" id="inproc"></a>

	var result = new Searcher(dir).Search("label:good bad~ description:leone", page:0, size:15);

	// Document fields and scores, i.e. the aggregated tf-idf weights a document recieve from a simple 
	// or compound query, are included in the result:

	var scoreOfFirstDoc = result.Docs[0].Score;
	var label = result.Docs[0].Fields["label"];
	var primaryKey = result.Docs[0].Fields["id"];

## Help out?
Awesome! Start [here](https://github.com/kreeben/resin/issues).
