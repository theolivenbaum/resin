# &#9084; Resin Search

Resin is a document database paired with a pluggable (extensible) 
search index that represents a vector space. 

Built from embeddings generated from document fields, spaces are
persisted on disk as bitmaps, scannable in a forward-only streaming fashion, 
an operation with a memory pressure that amounts to the size of a graph node, 
enabling the possibility to scan indices that are ~~large as hell~~ larger than memory. 

If you have only embeddings, no documents, you might still find some of the APIs useful for when you
want to build searchable spaces, e.g. Sir.VectorSpace.GraphBuilder and Sir.VectorSpace.PathFinder.

Spaces are configured by implementing Sir.IModel or Sir.IStringModel.

There is both an in-proc and out-of-process (HTTP) API.

## Reading, mapping, reducing and paging

__Write__ data flow: documents turn into vectors that turn into nodes in a graph that turn into a bitmap.

__Map__ data flow: query turns into a document that turn into a tree of vectors that is compared to the vectors of your space by performing a streaming binary search of index bitmap files.

__Reduce__ operation: each node in the query tree recieved a mapping to one or more posting lists ("document references") during the map step, now we materialize their postings lists then join them through intersection, union or deletion, while scoring them, and, finally, sort them by score and materialize the resulting document references as a list of scored and sorted documents, paged.

## Apps

- __Sir.HttpServer__: HTTP search service (read, write, query naturally or w/QL)
- __Sir.DbUtil__: write, validate and query via command-line

## Libs (.Net Core 3 apps can embedd and extend these)

- __Sir.KeyValue__: key/value/document System.IO.Stream-based database
- __Sir.VectorSpace__: hardware accellerated computations over and stream based storage of vectors and spaces
- __Sir.Search__: in-proc search engine (SessionFactory, WriteSession, ReadSession)

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
