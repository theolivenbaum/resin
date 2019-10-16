# &#9084; Resin

Resin is a document database with a search index representation of a vector space. 
This space is comprised of word embeddings generated from document fields of your choice. 
If you have only embeddings and no documents you might still find the APIs useful for when you
want to build and scan indices that represent vector spaces.

There is both an in-process and out-of-process (HTTP) API and there are two apps:

- _Sir.HttpServer_: HTTP search service
- _Sir.DbUtil_: index, train, validate and query via command-line

.Net Core 3 apps can embedd these:

- _Sir.KeyValue_: a key/value/document stream based database
- _Sir.VectorSpace_: hardware accellerated computations over and stream based storage of vectors and matrices
- _Sir.Search_: embeddable search engine

## Roadmap

- [x] v0.1a - bag-of-characters term vector space language model
- [x] v0.2a - HTTP API
- [x] v0.3a - boolean query language with support for AND ('+'), OR (' '), NOT ('-') and scope ('(', ')').
- [ ] v0.4b - semantic language model
- [ ] v0.5 - TBD 
- [ ] v0.6 - TBD
- [ ] v0.7 - TBD
- [ ] v0.8 - TBD
- [ ] v0.9 - add support for voice
- [ ] v1.0 - add support for images
- [ ] v2.0 - implement text/image-model-to-voice
- [ ] v2.1 - implement text/voice-model-to-image
- [ ] v2.2 - implement image/voice-model-to-text
- [ ] v3.0 - AGI
