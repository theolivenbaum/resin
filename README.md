# &#9084; Resin Search

Resin is a document database paired with a pluggable (extensible) 
search index that represents a vector space. 

Built from embeddings generated from document fields of your choice, spaces are
persisted on disk as bitmaps, scannable in a forward-only streaming fashion, 
an operation with a memory pressure that amounts to the size of a graph node, 
enabling the possibility to scan indices that are larger than memory. 

If you have only embeddings, no documents, you might still find some of the APIs useful for when you
want to build searchable spaces, e.g. Sir.VectorSpace.GraphBuilder and Sir.VectorSpace.PathFinder.

Spaces are configured by implementing Sir.IModel or Sir.IStringModel.

There is both an in-proc and out-of-process (HTTP) API.

Here are the apps:

- _Sir.HttpServer_: HTTP search service
- _Sir.DbUtil_: index, train, validate and query via command-line

.Net Core 3 apps can embedd and extend these:

- _Sir.KeyValue_: key/value/document System.IO.Stream-based database
- _Sir.VectorSpace_: hardware accellerated computations over and stream based storage of vectors and spaces
- _Sir.Search_: in-proc search engine (SessionFactory, WriteSession, ReadSession)

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
