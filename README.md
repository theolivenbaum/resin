# ResinDB

[![NuGet Version](https://img.shields.io/badge/nuget-v1.0.0.0-blue.svg)](https://www.nuget.org/packages/ResinDB)
[![Gitter](https://img.shields.io/gitter/room/nwjs/nw.js.svg)](https://gitter.im/ResinDB/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link)

ResinDB is a cross-platform document database and search engine with a query language, API and CLI. A performant database with a unique feature set:  

Feature | ResinDB | Lucene | SQL Server LocalDB | LevelDB | RocksDB
--- | --- | --- | --- | --- | ---
Runs in-process | &#9989; | &#9989; | &#9989; | &#9989; | &#9989;
Is schema-less | &#9989; | &#9989; |   | &#9989; | &#9989;
Can compress data | &#9989; | &#9989; |  | &#9989; | &#9989;
Runs on Windows and Linux | &#9989; | &#9989; |   |   | &#9989;
Has a query language | &#9989; | &#9989; | &#9989; |   |  
Is full-text search engine | &#9989; | &#9989; |   |   |   |  
Has latch-free writing | &#9989; |   |   |   |  
Has pluggable storage engine | &#9989; |   |   |   |  

## Use cases

ResinDB is designed to be used as  

- a disk-based replacement for your in-memory business entity cache
- a training data vector store
- a index of your data
- a component of a distributed database
- a framework for experimenting with scoring models 
- a search engine

ResinDB's architecture can be compared to that of Lucene, LevelDB or SQL Server LocalDB in that it runs in-process. What sets ResinDB apart is its full-text search index, its scoring mechanisms and its latch-free writing.

## DocumentTable

A document table is a table where  

- each row has a variable amount of named columns
- each column is a variable length byte array

A normal table, such as one from a RDBM system, does not allow storing of data in this fashion. A document table is a specialized table made for document database use cases.

On disk a document table can be represented as a file with a header and a body where the header is a column name index and where each row contains alternating keyID and value blocks, one pair for each of its columns. A value block is a byte array prepended with a size byte array. The max size of a value byte array is sizeof(int).

The name (key) of each column is a variable length byte array with a max size of sizeof(int).

A document table can contain a maximum of 32767 distinctly named columns (sizeof(short)).

[DocumentTable specification](src/DocumentTable/README.md)  

## Usage
### CLI
Clone the source or [download the latest source as a zip file](https://github.com/kreeben/resin/archive/master.zip), build and run the CLI (rn.bat) with the following arguments:

	rn write --file source_json_filename --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take] [--gzip] [--lz]  
	rn query --dir store_directory -q query_statement [-p page_number] [-s page_size]  
	rn delete --ids comma_separated_list_of_ids --dir store_directory  
	rn merge --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take]  
	rn rewrite --file rdoc_filename --dir store_directory [--pk primary_key] [--skip num_of_items_to_skip] [--take num_to_take] [--gzip] [--lz]
  	rn export --source-file rdoc_filename --target-file json_filename
E.g.:

	rn write --file c:\temp\wikipedia.json --dir c:\resin\data\wikipedia --pk "id" --skip 0 --take 1000000
	rn query --dir c:\resin\data\wikipedia -q "label:the good the bad the ugly" -p 0 -s 10
	rn delete --ids "Q1476435" --dir c:\resin\data\wikipedia
	rn merge --dir c:\resin\data\wikipedia --pk "id" --skip 0 --take 1000000
	rn rewrite --file c:\temp\resin_data\636326999602241674.rdoc --dir c:\temp\resin_data\pg --pk "url"
	rn export --source-file c:\temp\resin_data\636326999602241674.rdoc --target-file c:\temp\636326999602241674.rdoc.json
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
	using (var writer = new UpsertTransaction(dir, new Analyzer(), Compression.NoCompression, primaryKey:"id", firstBatchDocuments))
	{
		long versionId = writer.Write();
	}
	
	// From stream
	using (var secondBatchDocuments = new JsonDocumentStream(fileName))
	using (var writer = new UpsertTransaction(dir, new Analyzer(), Compression.NoCompression, primaryKey:"id", secondBatchDocuments))
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

## Disk-based concurrent read/write
Resin is a library, not a service. It runs inside of your application's memory space. Because of that ResinDB has been optimized to be able to immediately respond to queries without having to first rebuild data structures in-memory. 

The default index type is a dense, bucket-less, doubly chained Unicode character trie. On disk it's represented as a bitmap.

ResinDB's read/write model allow for multi-threaded read and write access to the data files. Writing is append-only. Reading is snapshot-based.

## No schema
You may store documents with variable number columns ("fields"). 

If you have graph-like business entities you would like full queryability into, then flatten them out and use paths as field names. 

In queries you reference document fields by key (or path).

## Field-oriented indexing options
Resin creates and maintains an index per document field. 

You can opt out of indexing entirely. You can index verbatim (unanalyzed) data. You can choose to store data both is its original and its analyzed state, or you can choose to store either one of those.

Indexed fields (both analyzed and unanalyzed) can participate in queries. Primary keys or paths used as identifiers should not be analyzed but certanly indexed and if they're significant enough, also stored.

## Compression
Analyzed data is compressed in a corpus-wide trie.

Stored document fields can be compressed individually with either QuickLZ or GZip. For unstructured data this leaves a smaller footprint on disk and enables faster writes.

Compressing stored data affects querying very little. In some scenarios it also speeds up writing. 

## Full-text search
ResinDB's main index data structure is a disk-based doubly-linked character trie. Querying operations support includes term, fuzzy, prefix, phrase and range. 

## Word vector space model
Scores are calculated using a vector space tf-idf bag-of-words model.

## Mapping and reducing
Here's how the scoring mechanism works. User defines a set of documents by formulating a query composed of one or more term-based questions. A scoring function is run over the set. The result is a tree of scores, one branch per sub-query ("query clause"). The tree is flattened by applying boolean logic between the branches, paginated and finally a list of documents are fetched from the store.

E.g.:

__Question__: "What is a cat?"

__Parse into document__: [what, is, a, cat]

__Scan index__: what  
__Scan index__: is  
__Scan index__: a  
__Scan index__: cat  

__Found documents__: 

[(i), (have), a, cat],   
[what, (if), (i), (am), a, cat]  

__Normalize to fit into 4-dimensional space__:  
[what, is, a, cat]  
[null, null, a, cat],  
[what, null, a, cat]  

__Give each word a weight (tf-idf)__:  
[0.2, 0.1, 0.1, 3],  
[null, null, 0.1, 3],   
[0.2, null, 0.1, 3]   

Documents are mapped in vector space, sorted by their distance from the query, paginated and as a final step, fetched  from the file system. 

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

## Runtime environment
ResinDB is built for dotnet Core 1.1.
