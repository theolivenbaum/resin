# &#9084; Resin

## Introduction

This project's roadmap matches the curriculum of [Guide To Vector Space Search and Computational Language Models](https://github.com/kreeben/vectorspacesearchguide).

Resin is search engine but also a toolbox for those who want a simple way to program, analyze and deploy a vector space model. 
 
Resin includes APIs for

- building a queryable space from embeddings
- writing/analyzing/tokenizing/vectorizing documents
- reading and writing in your favorite type of document format

Resin solves the problems of data queryability by providing an infrastructure for writing data, building spaces and representing them as graphs and by providing a query language that works over any model.

Resin also solves the issues that arise when you fancy querying over your data using natural language. You provide the model. Resin provides the queryability.

Resin is open-source and MIT-licensed. 

Are you interested in NLP or ML? Help is wanted. Code of conduct: always be cool.

## IModel

    public interface IModel<T>
    {
        AnalyzedData Tokenize(T data);
        Vector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream);
        Vector DeserializeVector(long vectorOffset, int componentCount, MemoryMappedViewAccessor vectorView);
        long SerializeVector(Vector vector, Stream vectorStream);
        (float identicalAngle, float foldAngle) Similarity();
        float CosAngle(Vector vec1, Vector vec2);
    }

By implementing this interface you provide the means for Resin to perform supervised training over your data, 
and then to query over it.

Tokenization involves creating embeddings from your data.

Serialization and deserialization procedures need to be provided by you but don't worry, boilderplate code can be found [here](https://github.com/kreeben/resin/blob/master/src/Sir.Store/Models/CbocModel.cs) and [here](https://github.com/kreeben/resin/blob/master/src/Sir.Store/Models/BocModel.cs).  

The identical angle determines how likely it is two nodes will be merged. The fold angle does not impact the formation of graphs but instead how they are balanced. Boilerplate code for the algorith for cos angle can also be found in the examples above.

Interface over HTTP with an IReader such as StoreReader, or provide your own IReader implementation.

Format your data any way you want. JSON support comes out-of-the-box. Implement other formats either by extending the appropriate Asp.Net model formatting facilities or by writing a custom format directly to the response stream by implementing an IReader.

### Example query

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

## Training

You may provide your own IWriter implementation or use the built-in StoreWriter to which you plug in your own IModel.

### Writing

Use any document format. JSON support comes out-of-the-box. Implement your custom format by extending the Asp.Net model format capabilities or by parsing the request stream yourself using a custom IWriter.

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
