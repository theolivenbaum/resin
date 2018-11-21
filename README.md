# Resin

## Demo

[A web search engine](https://didyougogo.com)

## What is this?

A full-text distributed search engine with HTTP API and programmable read/write pipelines, with support for any message format that can define its payload as a collection of documents, e.g. JSON, XML as well as custom formats.

Can you define your data entries as documents? Do you need to write data in any format you want and read it also in any format you want? Then this might be for you.

Do you need full-text search over your data? Both syntax and semantic? Do you also want to define your queries in any query language you want? Then a distributed search engine that doesn't crumble under its weight might be a good choice, one where you can implement any custom query language, so that all business analysts at the office can query for their favorite result.

### Vector-space model index

To provide full-text search words and phrases are extracted from documents and mapped to a 2 billion dimensional vector-space that form clusters of syntactically similar "bag-of-chars". In this language model, each character (glyph) is encoded as a 32-bit word and each word or phrase alike encoded as a 32-bit wide (but sparse) array. 

On disk this language model is represented as a bitmap, and in-memory as a binary tree ([VectorNode](src/Sir.Store/VectorNode.cs)).

Each node in the index tree carries as their payload a list of document references.

This model is a encodes less information than e.g. word2vec. 

Because of the smaller information payload, training the model, i.e. generating word encodings, takes less time compared to word2vec.

The model works excellent with approximate phrase search, which also happens to be one of the most common types of web search queries.

### Roadmap

Latest code is in `dev` branch.

- [x] v0.1a - 16-bit search engine
- [x] v0.2a - 32-bit search engine
- [x] v0.3a - search service
- [x] v0.4a - distributed search service
- [ ] v0.5b - "Hadoop for text"
- [ ] v0.6b - JITT semantic runtime engine (Just-In-Time Training)
- [ ] v0.7b - "Web search for kids"
- [ ] v0.8 - voice-to-text
- [ ] v0.9 - bot API
- [ ] v1.0 - image search