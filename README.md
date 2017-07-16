# ResinDB

[![NuGet Version](https://img.shields.io/badge/nuget-v2.0.1-blue.svg)](https://www.nuget.org/packages/ResinDB)
[![Gitter](https://img.shields.io/gitter/room/nwjs/nw.js.svg)](https://gitter.im/ResinDB/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link)

ResinDB is a schemaless (document) database and search engine library with concurrent read/write and zero warm-up time that runs embedded in your app's memory space. You may utilize it's indexing and querying properties through an API, a query language and a command-line interface.  

## A unique feature set

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

- a disk-based replacement to your in-memory business entity cache
- a training data vector store
- a index of your data
- a component of a distributed database
- a framework for experimenting with scoring models 
- a big data analysis tool
- a search engine

## Realize, there is no schema (nor a whole lot of tables)

There exists databases that are document databases that have a schema and have support for SQL. Resin is not such a document database.

You know how you have to first create a schema (tables) before writing to a SQL database? With ResinDB you don't have to do that. You can just start writing immediately.

Imagine a document as a row in a database table. In a document database you may store rows of data in that table with variable number of columns. There is only one table.

In queries you reference document columns (fields) by key.

Do you need two tables? Then create two ResinDB databases. 

## Embedded, zero conf/warmup, with concurrent read/write

ResinDB is a library, not a service. Because of that ResinDB has been optimized to be able to immediately respond to queries without having to first rebuild data structures in-memory. 

ResinDB's read/write model allow for multi-threaded read and write access to the data files. Writing is append-only. Reading is snapshot-based.

## Query language and execution plan

ResinDB provides term-based lookups. A term references both a field (key) and a word (value):

	title:rambo
	
Issuing such a statement will yield all documents with the word `rambo` somwhere in the title, sorted by relevance.

A query string may contain groups of `key:value` pairs. Each such pair is a query term:

	title:rambo genre:action

Query terms may be concatenated by a `space` (meaning OR), a `+` sign (meaning AND) or a `-` sign (meaning NOT):

	title:first title:blood
	title:first+title:blood
	title:first-title:blood

Query terms may be grouped together by enclosing them in parenthesis:

	title:jesus+(genre:history genre:fiction)

### Fuzzy, prefix, range and phrase queries are re-written

A fuzzy query term is suffixed with a `~`:

	body:morpheous~

A prefix query term is suffixed with a `*`:

	body:morph*
	
A greater-than query term separates the key and the value with a `>`:

	created_date>2017-07-15

A less-than query term separates the key and the value with a `<`:

	created_date<2017-07-15

A range query:

	created_date<2017-07-15+created_date>2017-07-15

Same query re-ordered:

	created_date>2017-07-15+created_date<2017-07-15

This is a phrase query:
	
	title:"the good bad ugly and"

Resin re-writes it into:

	title:the title:good title:bad title:ugly title:and

A phrase can be fuzzy:

	title:"the good bad ugly and"~
	
Resin re-writes that into:

	title:the~ title:good~ title:bad~ title:ugly~ title:and~

When Resin is subjected to a fuzzy, prefix or range query it expands it to include all terms that exists in the corpus and that lives within the boundaries as specified by the prefix, fuzzy or range operators (`* ~ < >`).

E.g.

	title:bananna~

will be expanded into:

	title:banana title:bananas
	
...if those are the terms that exists in the corpus and are near enough to the original term.

You may follow the parsing of the query and its execution plan by switching to DEBUG logging (in log4net.config) and then issuing the query through the CLI.

### Query execution plan

Given a query string, a page number and a page size, the following constitutes the ResinDB read algorithm:

	//parse query string into a tree of query terms
	query_tree = parse(query_string)

	for each query_term in query_tree

		//scan index for matching terms (query term may be exact, fuzzy, prefix or range)
		terms = scan(query_term)

		for each term in terms	
		
			//get postings
			postings = read_postings(term)

			// calculate the relevance of each posting
			query_term.scores = score(postings)

	// apply boolean logic and summation between nodes to reduce tree into a list of scores
	scores = reduce(query_tree)

	// paginate
	paginated_scores = scores.skip(page_number*page_size).take(page_size)

	//fetch documents
	documents = read(paginated_scores)

## Dense Unicode trie

Resin's index is a dense, bucket-less, doubly chained Unicode character trie. On disk it's represented as a bitmap.

A dense (compact) trie is a trie where nodes void of data from any word have been left out. This is in contrast to a sparse (fully expanded) trie where each node has as many children as there are letters in the code page. Further more a word is marked by a flag directly on the node, instead of following the more common trie regime of marking the end of a word with a node carrying a null value.

The disk representation of a LcrsTrie is a LcrsNode. Nodes are stored in a node table.

## NodeTable compared to a SSTable

A sorted string table contains key/value pairs sorted by key.

A node table is a file with key/value pairs arranged in a tree. It's sorted by key. Keys are spread out onto many nodes in a trie-like fashion. The value connected to the key is an address to a list of postings. Each node is encoded with the weight of their sub tree such that skipping over sub trees is efficiently done by seeking a distance in the file equal to the weight of a sub tree root node * the size of a node (which is fixed in size).

From a sorted list of strings you can create a binary search tree. In a node table the nodes are already laid out as a tree in such a way that lexiographic depth-first search is a forward-only read. Breadth-first search could also be performed but would require lots of seeking back and forth. The effects had serializing been done depth-first is a layout that would allow for breadth-first search using forward-only read. 

## Data is stored in a DocumentTable

A document table is a table where  

- each row has a variable amount of named columns
- each column is a variable length byte array

[DocumentTable specification](src/DocumentTable/README.md) 

## Full-text search

ResinDB's main index data structure is a disk-based doubly-linked character trie. Querying operations support includes term, fuzzy, prefix, phrase and range. 

## Tf-idf weighted bag-of-words model

Scores are calculated using a vector space tf-idf bag-of-words model.

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

## Field-oriented indexing options

Resin creates and maintains an index per document field. 

You can opt out of indexing entirely. You can index verbatim (unanalyzed) data. You can choose to store data both is its original and its analyzed state, or you can choose to store either one of those.

Indexed fields (both analyzed and unanalyzed) can participate in queries. Primary keys or paths used as identifiers should not be analyzed but certanly indexed and if they're significant enough, also stored.

Indexed data is encoded as nodes in a corpus-wide trie.

## Compression

Stored document fields can be compressed individually with either QuickLZ or GZip. For unstructured data this leaves a smaller footprint on disk and enables faster writes.

Compressing documents affect querying performance very little. The reason for this is that no data needs to be read and deflated until scoring and pagination has been performed.

## Merge and truncate

Writing to a store uncontended yields a new index segment. Multiple simultaneous writes are allowed. When they happen the index forks into two or more branches and the document file fragments into two or more files. 

Querying is performed over multiple branches but takes a hit performance-wise when there are many.

A new segment is a minor performance hit.

Issuing multiple merge operations on a directory will lead to forks becoming merged (in order according to their wall-clock timestamp) and segments becoming truncated. Merge and truncate truncate operation wipe away unusable data and lead to increased querying performance and a smaller disk foot-print.

Merging two forks leads to a single multi-segmented index.

Issuing a merge operation on a single multi-segmented index results in a unisegmented index. If the merge operation was uncontended the store will now have a single branch/single segment index.

## Pluggable storage engine

Implement your own storage engine through the IDocumentStoreWriter, IDocumentStoreReadSessionFactory, IDocumentStoreReadSession and IDocumentStoreDeleteOperation interfaces.

## Flexible and extensible

Analyzers, tokenizers and scoring schemes are customizable.

Are you looking for something other than a document database or a search engine? Database builders or architects looking for Resin's indexing capabilities specifically and nothing but, can either 
- integrate as a store plug-in
- let Resin maintain a full-text index storing nothing but identifyers from your store (i.e. the master data is in your store and querying is done towards a Resin index)

## Runtime environment

ResinDB is built for dotnet Core 1.1.

## CLI

[Some test data](https://drive.google.com/file/d/0BzlUqjJAklk9bWZqbVZTRnlnblE/view?usp=sharing) (~20K novels from The Gutenberg Project, zipped, in JSON format, including a number of variations of The Bible).

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
