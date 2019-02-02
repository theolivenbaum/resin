# Resin Search

[A search engine](https://didyougogo.com)

## What is this?

A distributable full-text search engine with HTTP API. 
Programmable, pluggable reader/writers. 
Supports virtually any type of read/write document format as long as it can be carried over HTTP (JSON works out of the box).  
It's not very hard to implement custom formats.

### (Micro) services

Presently, there are two services, both run on Kestrel. 
One service services the raw key/value payload and the index, the other acts as sink for postings (document references) 
and can also perform set operations on its payload (i.e. AND, OR, NOT) before responding to read requests.  

In read-time, one service acts as a map/reduce orchestrator, the latter as reducer.  

These services may be hosted on one machine or in isolation.  

You can create custom reader and writer services, orchestrators and nodes, and plug them into Sir.HttpServer.

### Read more

#### HTTP API and host of reader/writer plugins.
https://github.com/kreeben/resin/tree/master/src/Sir.HttpServer

#### A Int64/UInt64[] key/value writer and queryable map/reduce node. 
https://github.com/kreeben/resin/tree/master/src/Sir.Postings

#### Document writer and queryable map/reduce orchestrator. 
Communicates over HTTP with one or more nodes.  
https://github.com/kreeben/resin/tree/master/src/Sir.Store

### Roadmap

- [x] v0.1a - bag-of-characters term vector space language model
- [x] v0.2a - HTTP API
- [x] v0.3a - distributable search microservices
- [ ] v0.4a - bag-of-words document vector space language model
- [ ] v0.5b - semantic language model
- [ ] v0.6b - local join between collections
- [ ] v0.7b - distributed join between collections
- [ ] v0.8 - voice-to-text
- [ ] v0.9 - image search
- [ ] v1.0 - text-to-voice
- [ ] v2.0 - AI