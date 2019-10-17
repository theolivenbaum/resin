# &#9084; Resin Extensible Search Engine

Resin is a document database paired with a pluggable (extensible) 
search index that represents a vector space. 

Built from embeddings generated from document fields, spaces are
persisted on disk as bitmaps, scannable in a forward-only streaming fashion, 
an operation that brings pressure to memory that amounts to the size of a single graph node, 
which is usually very small, 
enabling the possibility to scan indices that are ~~large as hell~~ larger than memory. 

If you have only embeddings, no documents, you might still find some of the APIs useful for when you
want to build searchable spaces, e.g. Sir.VectorSpace.GraphBuilder and Sir.VectorSpace.PathFinder.

Spaces are configured by implementing Sir.IModel or Sir.IStringModel.

There is both an in-proc and out-of-process (HTTP) API.

## Writing, mapping, reducing and paging

__Write__ data flow: documents that consists of keys and values, are persisted as such, then turned into vectors through tokenization, each embedding placed as a node in a graph, each node referencing one or more documents, that turn into a bitmap that is persisted on disk as a segment in a column file.

__Map__ data flow: a query representing one or more terms, each term identifying both a column and a value, turns into a document that turns into a tree of vectors, each node representing a boolean set operation over your space, that is compared to the vectors in your index by performing a streaming binary search of your column bitmap files.

__Reduce__ operation: each node in the query tree that recieved a mapping to one or more posting lists ("lists of document references") during the map step now materializes their references and we can join them with those of their the parent, through intersection, union or deletion, and, once the tree's been materialized all the way down to the root, we have a list of references that we can sort by relevance so that we can do what we really came here to do, to materialize a list of scored and sorted documents that are paged.

## Balancing

Balancing the binary tree that represents your space is done by adjusting the merge factor ("IdenticalAngle") and the fold factor ("FoldAngle"). 

The location of each vector in the index is determined by calculating its angle to the node it most resembles. If the angle is greater than or equal to IdenticalAngle, the two nodes merge. If it is not identical an new node is added to the binary tree, as a left child if the angle is greater than FoldAngle, otherwise as a right child.

IdenticalAngle and FoldAngle are properties of IModel.

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
