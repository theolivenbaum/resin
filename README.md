# Sir.Resin

This is a trainable vector space model [search engine](https://didyougogo.com) with a simple boolean query language. 

## Bag-of-characters model

The first analysis pass yields a graph of words embedded as bags-of-characters.

## Vector space model

The second analysis pass yields a graph of documents embedded as continuous bags-of-words, or bags-of-bags-of-characters. This model creates clusters of similar documents. Let's call these clusters "topics".

## Semantic model

The third analysis pass yields a graph of topics embedded as vectors as wide as there are topics in the lexicon. They are bags-of-bags-of-bags-of-characters.

## Forth pass

The forth analysis pass will thus produce a bag-of-bag-of-bag-of-bag-of-characters model.

## The Forbidden Pass

Nobody shall pass here! But if you do, consider the circles in a kaleidoscope to be topics, 
each training pass a rotation of a cell. Every once in a while you'll get nonsense but most of the time there 
will be beautiful clear patterns.

## Scoped querying

To find all documents with title "Rambo" or "First Blood" but only if the genre isn't "books":

	+(title:rambo title:first blood) -(genre:books)

## Create your own collection and then query it.

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

	Server should respond with a list of document IDs:
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

## Read more

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
