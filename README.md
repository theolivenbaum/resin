# Resin Search

## Demo

[A web search engine](https://didyougogo.com)

## What is this?

This is a distributable full-text search engine with HTTP API and programmable read/write pipelines, 
with support for virtually any type of document format as long as it can be carried over HTTP. 
Reading and writing JSON works out of the box. It's not hard to implement custom formats.

### Distributable micro-services

Presently, there are two services, both run on Kestrel. 
One handles the raw key/value payload and the index, the other stores postings (document references) 
and also performs set operations on its payload (i.e. AND, OR, NOT) before giving you a result. 
The former acts as a map/reduce orchestrator, the latter performs the calculations.
The services may be hosted together or in isolation.

### Read more

#### Create your own read and write plugins and jack them into Sir.HttpServer  
https://github.com/kreeben/resin/tree/master/src/Sir.HttpServer

#### A Int64/UInt64[] key/value store and map/reduce node  
https://github.com/kreeben/resin/tree/master/src/Sir.Postings

#### Embeddable document-based search engine and map/reduce orchestrator 
https://github.com/kreeben/resin/tree/master/src/Sir.Store

### Roadmap

Latest code is in `dev` branch.

- [x] v0.1a - 16-bit search engine
- [x] v0.2a - 32-bit search engine
- [x] v0.3a - search service
- [x] v0.4a - distributed search service
- [ ] v0.5b - join between collections
- [ ] v0.6b - semantic index
- [ ] v0.7b - "Web search for kids"
- [ ] v0.8 - voice-to-text
- [ ] v0.9 - text-to-voice
- [ ] v1.0 - image search
