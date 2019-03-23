# Resin

Resin is a combination of two things:

- A vector database where the key is a 64-bit vector (that may or may not translate into a string, it's up to you) 
and the payload is a list of Int64's.
- A remote execution node that stores lists of Int64 where the key is a Int64 and that also accepts query expressions 
that define set operations instead of keys.

On this architecture a language model framework has been built that carries a HTTP API that lets you: 

- read and write documents (in any format, JSON included out-of-the-box)
- create embeddings/language models from text
- query a model in dynamic (structured) or natural language
- get intents from utterances (i.e. map phrases to keys)
- build custom models in new vector spaces, based on previous models
- plug in your own reader/writer logic

Included out-of-the-box is a web GUI where you can

- query collections of documents with natural or structured queries
- create slices of existing collections

and a HTTP API that let you

- create new collections
- query naturally/structured over HTTP with content type negotiation 

The models included are:

## Bag-of-characters model

A graph of words embedded as bags-of-characters. 
This model creates clusters of documents that share similar words. 

Natural language queries are parsed into terms, then into bags-of-characters, 
then into an expression tree, each node representing a AND, OR or NOT set operation, 
then serialized and executed on a remote "postings server", producing a distinct set of document IDs 
that includes documents from as many clusters as there are (distinct) terms in the query.  

That set is sorted by score and a window defined by skip and take parameters are returned to the orchestrating server, who 
materializes the list of document IDs, i.e. reads and returns to the client 
(e.g. a browser or just about any other type of HTTP client) documents that have been formatted 
according to the HTTP clients "Accept" header.

## Document model (Not production-ready)

A graph of documents embedded as bags-of-words. 
In this model documents gather around syntax "topics". 

Natural language queries are parsed into clauses, each clause into a document vector. 
A cluster (of documents) is then located by reducing the clause vectors to a single document 
by using vector addition/subtraction and then navigating the index graph by evaluating 
the cos angle between the query and the clusters. Then end-result of the scan is a cluster ID 
that also corresponds to a postings list ID. If the topic is a big one, the result set will be large. 
If you've managed to pinpoint a shallow cluster your result set will be smaller.

## Natural and structured querying

To 

	find documents (with title) Rambo or First Blood but only if the genre isn't books
	
you can use natural language or structured:

	+(title:rambo title:first blood) -(genre:books)

## Create your own collection/endpoint

To create models from your data you may host one of these servers yourself, privately or publicly, 
or you can use a [free search cloud](https://didyougogo.com).

### POST a JSON document to the WRITE endpoint

	HTTPS POST didyougogo.com/io/[collection_name]
	Content-Type:application/json
	[
		{
			"field1":"value1"
		},
		{
			"field1":"value2"
		}
	]
###	Server should respond with a list of document IDs:

	[
		1,
		2
	]

### GET document by ID

	HTTPS GET didyougogo.com/io/[collection_name]?id=[document_id]
	Accept:application/json

### Query collection with natural language through the API

	HTTPS GET didyougogo.com/io/[collection_name]?q=[phrase-or-term]&fields=title&skip=0&take=10  
	Accept:application/json

### Query collection with query language through the API

	HTTPS GET didyougogo.com/io/[collection_name]?&qf=[scoped_query]&skip=0&take=10  
	Accept:application/json

### Query GUI

	HTTPS GET didyougogo.com/?q=[phrase-or-term-query]&fields=title&skip=0&take=10&collection=[collection_name]

### Advanced query parser

	HTTPS GET didyougogo.com/queryparser/?q=[phrase-or-term-query]&qf=[scoped_query]&fields=title&skip=0&take=10&collection=[collection_name]

### HTTP reader/writer micro-service framework.
Create distributable readers and writers. Splits a problem into two. 
https://github.com/kreeben/resin/tree/master/src/Sir.HttpServer

### A key/value writer and map/reduce node. 
Execute AND, OR and NOT set operations over local lists of Int64's (e.g. document references).  
https://github.com/kreeben/resin/tree/master/src/Sir.Postings

### Document writer and map/reduce orchestrator. 
On-disk database and in-memory index. Orchestrates remote set operations.   
https://github.com/kreeben/resin/tree/master/src/Sir.Store

### Roadmap

- [x] v0.1a - bag-of-characters term vector space language model
- [x] v0.2a - HTTP API comprised of distributable search microservices
- [x] v0.3a - boolean query language with support for AND ('+'), OR (' '), NOT ('-') and scope ('(', ')').
- [ ] v0.4a - bag-of-words document vector space language model
- [ ] v0.5b - semantic language model
- [ ] v0.6b - local join between collections
- [ ] v0.7b - distributed join between collections
- [ ] v0.8 - voice-to-text
- [ ] v0.9 - image search
- [ ] v1.0 - text-to-voice
- [ ] v2.0 - AI
