# SIResin

16-bit wide vector-space model search engine with HTTP API and programmable read/write pipelines.

## Documentation

### Vector-space model

To provide full-text search across your documents words and phrases are mapped to a 65k dimensional vector-space that form clusters of syntactically similar "bag-of-chars". On disk and in-memory this model is represented as a binary tree ([VectorNode](src/Sir.Store/VectorNode.cs)).

### HTTP API

Send and recieve data in any format using any query language through pluggable read/write pipelines. [Read more](src/Sir.HttpServer/README.md).

## Platform

.NET Core 2.0.
