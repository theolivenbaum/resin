# Resin - Simple Information Retrieval

## Demo

[A web search engine](https://didyougogo.com)

## What is this?

This here is Resin a.k.a. Sir, a full-text search engine with HTTP API and programmable read/write pipelines, 
with support for virtually any type of messaging format as long as it can be carried over HTTP. 
Reading and writing JSON works out of the box. It's not hard to implement custom formats.

### Distributable micro-services

Presently, there are two services, both run on Kestrel. One handles the key/value payload and the index, the other serves postings (document references). 
They may be hosted together as one service or they can be distributed.

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