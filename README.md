# &#9084; Resin Extensible Search Engine

Resin is a document database that's been paired with a extensible search index that represents a vector space. 

Built from embeddings that are extracted from document fields through tokenization during the write operation, spaces are
persisted on disk as bitmaps that are scannable in a streaming fashionvthat brings little pressure to memory, only what amounts to the size of a single graph node, which is usually very small, enabling the possibility to scan indices that are ~~really, really big~~ larger than memory. 

If you have only embeddings, no documents, you might still find some of the APIs useful for when you
want to build searchable spaces, e.g. Sir.VectorSpace.GraphBuilder and Sir.VectorSpace.PathFinder.

Spaces are configured by implementing Sir.IModel or Sir.IStringModel.

There is both an in-proc, NHibernate-like API in that there are sessions, a factory, and the notion of a unit of work, as well as JSON-friendly HTTP API.

## Write, map, materialize and page

__Write data flow__: documents that consists of keys and values, are persisted on disk and turned into vectors, through tokenization (IModel.Tokenize), each embedding placed in a graph (see "Balancing"), each node referencing one or more documents, that we turn into a bitmap that we persist on disk as a segment in a column index file.

__Map data flow__: a query, representing one or more terms, each term identifying both a column and a value, turns into a document that turns into a tree of vectors (through tokenization), each node representing a boolean set operation over your space (AND, OR, NOT), each compared to the vectors of your space by performing binary search over the nodes of your column bitmap files, so, luckily, not all vectors. Hopefully only, but this is not guaranteed to always be the case, to log(N) vectors, but sometimes more. How often and how many more depends to some degree on how you balanced your tree and to another, hopefully much smaller degree, and this goes for all probabilistic models, and we're probabilistic because two vectors that are not identical to another can be merged (see "Balancing"), on pure chance.

__Materialize operation__: each node in the query tree that recieved a mapping to one or more postings lists ("lists of document references") during the map step now materializes their postings and we can join them with those of their the parent, through intersection, union or deletion, and, once the tree's been materialized all the way down to the root, we have a list of references that we can sort by relevance so we can get on with what it is we really want, which is to materialize a list of scored and sorted documents that are __paged__.

## Balancing (algorithm)

Balancing the binary tree that represents your space is done by adjusting the merge factor ("IdenticalAngle") and the fold factor ("FoldAngle"). 

The location in the index, of each vector, is determined by calculating its angle to the node it most resembles. If the angle is greater than or equal to IdenticalAngle, the two nodes merge. If it is not identical a new node is added to the binary tree. If the angle is greater than FoldAngle, it is added as a left child to the node or, if that slot is taken, to the next left node that has a empty left slot, otherwise as a right child.

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
