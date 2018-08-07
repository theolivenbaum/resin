# SIR (is Resin)

16-bit wide vector-space model search engine with HTTP API and programmable read/write pipelines.

## What is this?

### Vector-space model

To provide full-text search words and phrases extracted from documents are mapped to a 65k dimensional vector-space that form clusters of syntactically similar "bag-of-chars". 

On disk and in-memory this model is represented as a binary tree ([VectorNode](src/Sir.Store/VectorNode.cs)).

Each node carries as their payload a list of document references.

This model works well with fuzzy/prefix/suffix queries and with phrases that appromixate each other.

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
