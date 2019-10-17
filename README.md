# &#9084; Resin

Resin is a document database with a search index representation of a vector space. 
This space is built up from word embeddings generated from document fields of your choice 
and persisted on disk as bitmaps, scannable in a streaming fashion. 

If you have only embeddings, no documents, you might still find the APIs useful for when you
want to build and scan indices that represent them.

There is both an in-process and out-of-process (HTTP) API and there are two apps:

- _Sir.HttpServer_: HTTP search service
- _Sir.DbUtil_: index, train, validate and query via command-line

.Net Core 3 apps can embedd these:

- _Sir.KeyValue_: a key/value/document stream based database
- _Sir.VectorSpace_: hardware accellerated computations over and stream based storage of vectors and spaces
- _Sir.Search_: search engine

## Roadmap

- [x] v0.1a - bag-of-characters vector space language model
- [x] v0.2a - HTTP API
- [x] v0.3a - query language
- [ ] v0.4 - semantic language model
- [ ] v0.5 - image model
- [ ] v1.0 - voice model
- [ ] v2.0 - image-to-voice
- [ ] v2.1 - voice-to-text
- [ ] v2.2 - text-to-image
- [ ] v3.0 - AGI