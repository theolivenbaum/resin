# &#9084; Resin

## Introduction

Read [Guide To Vector Space Search and Computational Language Models](https://github.com/kreeben/vectorspacesearchguide).

Resin is a toolbox for those who want a simple way to program, analyze and deploy a vector space model. 
You may serve it documents (non-nested JSON is supported out-of-the-box) 
or use a lower-level API to serve sparse/dense vectors. 

Your data ends up being represented as nodes in a graph that can be traversed by comparing a query 
(which is also represented as a vector) to nodes from the graph and use their cosine angle as a guide as to 
what node to traverse to next and when to stop. 

Each node can reference external payloads. The built-in search engine that comes with Resin 
and that is powered by the same toolbox uses a node's external reference abilities to store postings 
(i.e. document references).

The highest level API in Resin is its HTTP read/write API that you can use as a document database and search engine. 
You may also run your own reader/writer using the same IReader/IWriter plugin system as the built-in 
search engine does.
 
Resin includes APIs for

- building queryable graphs from vectors that are (max) 64 bits wide
- visualizing graphs
- writing/analyzing/tokenizing/vectorizing documents
- querying document collections using natural language or with the built-in query language 
- adding support for your favorite type of document format
- serialization of documents/vectors/graphs

Resin is open-source and MIT-licensed. 

Are you interested in NLP or ML? Help is wanted. Code of conduct: always be cool.

## Reading

To find documents where title is  

	Rambo or First Blood but only if the genre isn't books
	
you may use natural language or structured:

	+(title:rambo title:first blood) -(genre:books)

Natural language queries are parsed into vectors and (AND, OR or NOT) set operations. 
The expression is executed on a remote server, producing a set of IDs of documents that came from as 
many clusters as there are (distinct) additative terms in the query.  

That set is sorted by score and a window defined by skip and take parameters is returned to the orchestrating server, 
who materializes the list of document IDs, i.e. reads and returns to the client a document stream formatted according 
to the HTTP client's "Accept" header.

## Writing

To create collections from your favorite data you may host one of these servers yourself, privately or publicly, 
or you can use a [free search cloud](https://didyougogo.com).

#### POST a JSON document to the WRITE endpoint

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

#### Query collection with natural language through the API

	HTTPS GET didyougogo.com/io/[collection_name]?q=[phrase-or-term]&fields=title&skip=0&take=10  
	Accept:application/json

#### Query collection with query language through the API

	HTTPS GET didyougogo.com/io/[collection_name]?&qf=[structured_query]&skip=0&take=10  
	Accept:application/json

#### Query GUI

	HTTPS GET didyougogo.com/?q=[phrase-or-term-query]&fields=title&skip=0&take=10&collection=[collection_name]

#### Slice collections using structured queries with the advanced query parser

	HTTPS GET didyougogo.com/queryparser/?q=[phrase-or-term-query]&qf=[structured_query]&fields=title&skip=0&take=10&collection=[collection_name]

#### Execute your write plugin

	HTTPS POST [hostname]/io/[collection_name]
	Content-Type:[IWriter.ContentType]
	Custom data payload

#### Execute your read plugin

	HTTPS GET [hostname]/io/[collection_name?[Custom query payload]]
	Content-Type:[IReader.ContentType]]

or when you have a larger query payload

	HTTPS PUT [hostname]/io/[collection_name]
	Content-Type:[IReader.ContentType]]
	Custom query payload

## Installing

Download a clone of this repository, launch the solution in Visual Studio to build. 
Embedd Sir.Core.dll and Sir.Store.plugin.dll to gain acess to the most common APIs.

You may also publish Resin as a web app.  
Create a IIS site that points to [path_of_repository]/src/publish. 
Resin is built on .Net Core so make sure the app pool type is "unmanaged".

Come back to this page and [register any bugs or issues you might find](https://github.com/kreeben/resin/issues).

## Deep-dive

#### HTTP reader/writer micro-service framework.
Plug in your custom read and write filters here.  
https://github.com/kreeben/resin/tree/master/src/Sir.HttpServer

#### A key/value writer and map/reduce node. 
Execute AND, OR and NOT set operations over local lists of Int64's.  
https://github.com/kreeben/resin/tree/master/src/Sir.Postings

#### Document writer and map/reduce orchestrator. 
Database and search index. Orchestrates remote set operations.  
https://github.com/kreeben/resin/tree/master/src/Sir.Store

## Roadmap

- [x] v0.1a - bag-of-characters term vector space language model
- [x] v0.2a - HTTP API comprised of distributable search microservices
- [x] v0.3a - boolean query language with support for AND ('+'), OR (' '), NOT ('-') and scope ('(', ')').
- [ ] v0.4b - improve WWW scale performance
- [ ] v0.5 - semantic language model
- [ ] v0.6 - local join between collections
- [ ] v0.7 - private online collections
- [ ] v0.8 - join (orchestrate) over private/public collections
- [ ] v0.9 - add support for voice
- [ ] v1.0 - add support for images
- [ ] v2.0 - implement text/image-model-to-voice
- [ ] v2.1 - implement text/voice-model-to-image
- [ ] v2.2 - implement image/voice-model-to-text
- [ ] v3.0 - AI