# ResinDB

![ResinDB](/docs/resindb.png)

ResinDB, a full-text search engine/document database, is designed to be used as a fast data store, a cache-replacement or as an index to your database/store.

ResinDB's architecture can be compared to that of LevelDB or SQL Server LocalDB in that they all run in-process.

## Usage
### CLI
Clone the source or [download the latest source as a zip file](https://github.com/kreeben/resin/archive/master.zip), build and run the CLI (rn.bat) with the following arguments:

	rn write --file source_filename --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take] [--gzip] [--lz]  
	rn query --dir store_directory -q query_statement [-p page_number] [-s page_size]  
	rn delete --ids comma_separated_list_of_ids --dir store_directory  
	rn merge --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take]  
	rn rewrite --file rdoc_filename --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take] [--gzip] [--lz]
  
E.g.:

	rn write --file c:\temp\wikipedia.json --dir c:\resin\data\wikipedia --pk "id" --skip 0 --take 1000000
	rn query --dir c:\resin\data\wikipedia -q "label:the good the bad the ugly" -p 0 -s 10
	rn delete --ids "Q1476435" --dir c:\resin\data\wikipedia
	rn merge --dir c:\resin\data\wikipedia --pk "id" --skip 0 --take 1000000
	
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

	var docs = GetDocuments();
	var dir = @"c:\resin\data\mystore";
	
	// From memory
	using (var firstBatchDocuments = new InMemoryDocumentStream(docs))
	using (var writer = new UpsertOperation(dir, new Analyzer(), Compression.NoCompression, primaryKey:"id", firstBatchDocuments))
	{
		long versionId = writer.Write();
	}
	
	// From stream
	using (var secondBatchDocuments = new JsonDocumentStream(fileName))
	using (var writer = new UpsertOperation(dir, new Analyzer(), Compression.NoCompression, primaryKey:"id", secondBatchDocuments))
	{
		long versionId = writer.Write();
	}

	// Implement the base class DocumentStream to use any type of data in any format you need as your data source.

#### Query the index.

	var result = new Searcher(dir).Search("label:good bad~ description:leone", page:0, size:15);

	// Document fields and scores, i.e. the aggregated tf-idf weights a document recieve from a simple 
	// or compound query, are included in the result:

	var scoreOfFirstDoc = result.Docs[0].Score;
	var label = result.Docs[0].Fields["label"];
	var primaryKey = result.Docs[0].Fields["id"];

## Reads are purely disk-based
Resin is a library, not a service. It runs inside of your application domain. 

ResinDB has been optimized to be able to immediately respond to queries without having to first rebuild data structures in-memory. 

The default index type is a fast disk-based and bitmapped left-child-right-sibling character trie.

ResinDB's read/write model allow for multi-threaded read and write access to the data files. Writing is append-only. Reading is snapshot-based.

## No input/output schema
Store documents with variable number columns/fields. 

Group similar documents into separate stores or have them all in a big store.

The document model supports flat or graph-like business entities.

In queries, reference document fields by a simple or complex key/path.

## Column-oriented indexing
By default Resin creates and maintains an index per document field. 

You can opt out of indexing (analyzing) and storing of fields.

## Row-based compression
With Resin's default storage engine you have the option of compressing your data with either QuickLZ or GZip. For unstructured data this leaves a smaller footprint on disk and enables faster writes.

Compression is row-based. Querying performance affected very little. It is the contents of the document storage file that is compressed and that file is touched after the index lookup and the scoring. 

## Full-text search
Querying support includes term, fuzzy, prefix, phrase and range. 

## Word vector space model
Scores are calculated using the default scoring scheme which is a vector space/tf-idf bag-of-words model.

Analyzed fields participate in the scoring.

## Map/reduce
You define your set of items (documents) by formulating a query composed of one or more term-based questions. Then you specify an aggregating function (in our case, a scoring mechanism). Then you run that function over your set. As a final step you reduce your term-based answers into one answer, paginate and fetch your items from your store.

E.g.:

__Question__: "What is a cat?"

__Parse into document__: [what,is,a,cat]

__Scan index__: what  
__Scan index__: is  
__Scan index__: a  
__Scan index__: cat  

__Found documents__: 

[(i), (have), a, cat],   
[what, (if), (i), (am), a, cat]  

__Normalize to fit into 4-dimensional space__:  
[what,is,a,cat]  
[null, null, a, cat],  
[what, null, a, cat]  

__Give each word a weight (tf-idf)__:  
[0.2, 0.1, 0.1, 3],  
[null, null, 0.1, 3],   
[0.2, null, 0.1, 3]   

Map the query and the documents in vector space, sort by the documents' (Euclidean) distance from the query, paginate and as a final step, fetch documents from the filesystem. 

__Answer__: Something you can have or possibly be.

## Pluggable storage engine
Implement your own storage engine through the IDocumentStoreWriter, IDocumentStoreReadSessionFactory, IDocumentStoreReadSession and IDocumentStoreDeleteOperation interfaces.

## Merge and truncate
Writing to a store uncontended yields a new index segment. Multiple simultaneous writes are allowed. When they happen the index forks into two or more branches and the document file fragments into two or more files. 

Querying is performed over multiple branches but takes a hit performance-wise when there are many.

A new segment is a minor performance hit.

Issuing multiple merge operations on a directory will lead to forks becoming merged (in order according to their wall-clock timestamp) and segments becoming truncated. Merge and truncate truncate operation wipe away unusable data and lead to increased querying performance and a smaller disk foot-print.

Merging two forks leads to a single multi-segmented index.

Issuing a merge operation on a single multi-segmented index results in a unisegmented index. If the merge operation was uncontended the store will now have a single branch/single segment index.

## Flexible and extensible
Analyzers, tokenizers and scoring schemes are customizable.

Are you looking for something other than a document database or a search engine? Database builders or architects looking for Resin's indexing capabilities specifically and nothing but, can either 
- integrate as a store plug-in
- let Resin maintain a full-text index storing nothing but identifyers from your store (i.e. the master data is in your store and querying is done towards a Resin index)

## Supported .net version
Resin is built for dotnet Core 1.1.

## Help out
Start [here](https://github.com/kreeben/resin/issues).
