# Resin

## Demo

[A web search engine](https://didyougogo.com)

## What is this?

A full-text search engine with HTTP API and programmable read/write pipelines.

### Vector-space model index

To provide full-text search words and phrases are extracted from documents and mapped to a 2 billion dimensional vector-space that form clusters of syntactically similar "bag-of-chars". In this language model, each character (glyph) is encoded as a 32-bit word (an int), and each word or phrase alike encoded as a 32-bit wide (but sparse) array. 

On disk this language model is represented as a bitmap, and in-memory as a binary tree ([VectorNode](src/Sir.Store/VectorNode.cs)).

Each node in the index tree carries as their payload a list of document references.

This model works excellent with fuzzy/prefix/suffix queries and especially well with phrases that appromixate each other.

### Fast reader, slow eater

Compared to other full-text search indexes, Resin is a very fast reader but a slow writer.

### API

#### API write

	IEnumerable<IDictionary> data = GetData();

	var sessionFactory = new SessionFactory(@"c:\mydir");

	using (var writer = new Writer(sessionFactory, new LatinTokenizer()))
	{
		writer.Write("mycollection", data);
	}

#### HTTP Write

To create a collection (table) and add three documents:

	HTTP POST http://localhost:54865/io/mycollection
	Content-Type: application/json

Body:

	[
		{
			"id":"0",
			"year":"1982",
			"title":"First Blood",
			"body":"Former Green Beret John Rambo is pursued into the mountains surrounding a small town by a tyrannical sheriff and his deputies, forcing him to survive using his combat skills."
		},
		{
			"id":"1",
			"year":"1985",
			"title":"Rambo: First Blood Part II",
			"body":"John Rambo is released from prison by the government for a top-secret covert mission to the last place on Earth he'd want to return - the jungles of Vietnam."
		},
		{
			"id":"2",
			"year":"2008",
			"title":"Rambo",
			"body":"In Thailand, John Rambo joins a group of mercenaries to venture into war-torn Burma, and rescue a group of Christian aid workers who were kidnapped by the ruthless local infantry unit."
		}
	]

Response:
	
	HTTP 201 Created
	Location: /io/mycollection

#### API Read/query

	var query = new BooleanKeyValueQueryParser().Parse("title:rambo\n+title:first blood", new LatinTokenizer());
	query.CollectionId = "mycollection".ToHash();
	var sessionFactory = new SessionFactory(@"c:\mydir");
	var reader = new Reader(sessionFactory);
	var documents = reader.Read(query);

#### HTTP Read/query

	HTTP PUT http://localhost:54865/io/mycollection
	Content-Type: text/plain

Body:

	title:rambo
	+title:first blood

Response:

	[
		{
			"id": "1",
			"year": "1985",
			"title": "Rambo: First Blood Part II",
			"body": "John Rambo is released from prison by the government for a top-secret covert mission to the last place on Earth he'd want to return - the jungles of Vietnam."
		}
	]

#### HTTP Update

	HTTP PUT http://localhost:54865/io/mycollection/1/?key=id

Body:

	[
		{
			"id": "1",
			"year": "1985",
			"title": "Rambo: First Blood Part II (second-best movie ever)",
			"body": "John Rambo is released from prison by the government for a top-secret covert mission to the last place on Earth he'd want to return - the jungles of Vietnam."
		}
	]

#### HTTP Delete

	HTTP DELETE http://localhost:54865/io/mycollection

Body:

	title:first blood

Response:

	HTTP 202 Marked for deletion

### Platform

.NET Core 2.0.
