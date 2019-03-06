# Sir.Resin

This is a search engine and language model framework 
with an API that lets you train and project a language model onto a search tree 
that supports 64-bit wide vectors that in turn may represent words, phrases, documents, topics, topics of topics, or cluster IDs.
  
Built-in capabilities include interfacing with your language model through natural language queries 
or by using a structured, boolean query language that supports AND, OR, NOT and (nested (scope)). 

A number of models are included in the package.

## Bag-of-characters model

The first model is a graph of words embedded as bags-of-characters. 
This model creates clusters of documents that share similar words. 

Natural language queries are parsed into terms, then into bags-of-characters, 
then into an expression tree, each node representing a AND, OR or NOT set operation, 
then serialized and executed remotely. 

## Document model

The second model is a graph of documents embedded as bags-of-words. 
In this model documents gather around "topics". 

Natural language queries are parsed into clauses and each clause into a vector. 
A cluster of documents is located by reucing the clause vectors to a document 
by using vector addition/subtraction and then navigating the index graph by evaluating 
the cos angle between the query and the clusters.

## Topical model

The third model is a graph of documents represented as vectors or "bags-of-topics", 
in a space as wide as there are topics in the lexicon. 

Natural language queries are parsed into bags-of-characters, then into bags-of-words,
then mapped to a bags-of-topics space.

## Semantic model

The final model projects each previous to a single dimension creating a small, topical search space.

## Natural and scoped querying

Find documents with title "Rambo" or "First Blood" but only if the genre isn't "books", will be parsed into

	+(title:rambo title:first blood) -(genre:books)

## Create your own collection and then query it.

You may host one of these servers yourself, privately or publicly. Or you can use [this free search cloud](https://didyougogo.com).

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

### Human-friendly query GUI

	HTTPS GET didyougogo.com/?q=[phrase-or-term-query]&fields=title&skip=0&take=10&collection=[collection_name]

### Advanced query parser

	HTTPS GET didyougogo.com/queryparser/?q=[phrase-or-term-query]&qf=[scoped_query]&fields=title&skip=0&take=10&collection=[collection_name]

### HTTP reader/writer micro-service framework.
Create distributable readers and writers.  
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
