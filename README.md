# &#9084; Resin

[![NuGet version (Newtonsoft.Json)](https://img.shields.io/nuget/v/Resin.Search.svg?style=flat-square)](https://www.nuget.org/packages/Resin.Search/) 

Overview | [How to install](https://github.com/kreeben/resin/blob/master/INSTALL.md) | [User guide](https://github.com/kreeben/resin/blob/master/USER-GUIDE.md) 

## HTTP search engine/embedded library
Launch a Resin HTTP server or use the Resin search library to search through any vector space. With hardware accelerated vector operations from 
[MathNet](https://github.com/mathnet/mathnet-numerics) Resin is especially well suited for problem spaces that can be defined as vector spaces.

Vector spaces are configured by implementing [IModel<T>](https://github.com/kreeben/resin/blob/master/src/Sir.VectorSpace/IModel.cs). 

## Document database
Resin stores data as document collections. It applies your prefered IModel<T> onto your data while you write and query it. 
The write pipeline produces a set of indices (graphs), one for each document field, that you may interact with by using the Resin web GUI, 
the Resin read/write JSON HTTP API, or programmatically.

## Vector-based indices
Resin indices are binary search trees and creates clusters of those vectors that are similar to each other, as you populate them with your data. 
Graph nodes are created in the [Tokenize](https://github.com/kreeben/resin/blob/master/src/Sir.VectorSpace/IModel.cs#L12) method of your model. 
When a node is added to the graph its cosine angle, i.e. its similarity to other nodes, determine its position (path) within the graph.

## Customizable vector spaces
Resin comes pre-loaded with two IModel vector space configurations: one for [text](https://github.com/kreeben/resin/blob/master/src/Sir.Search/Models/BagOfCharsModel.cs) 
and [another](https://github.com/kreeben/resin/blob/master/src/Sir.Search/Models/LinearClassifierImageModel.cs) for [MNIST](http://yann.lecun.com/exdb/mnist/) images. 
The text model has been tested by validating indices generated from [Wikipedia search engine dumps](https://dumps.wikimedia.org/other/cirrussearch/current/) as well as by parsing 
[Common Crawl](http://commoncrawl.org/) [WAT](https://commoncrawl.org/the-data/get-started/#WAT-Format), [WET](https://commoncrawl.org/the-data/get-started/#WET-Format) 
and [WARC](https://commoncrawl.org/the-data/get-started/#WARC-Format) files, to determine at which scale Resin may operate in and at what accuracy. 

The image model is included mostly as an example of how to implement your own prefered machine-learning algorithm for building custom-made search indices. 
The error rate of the image classifier is ~5%. 

## Performance
Currently, Wikipedia size data sets produce indices capable of sub-second phrase searching. 

## You may also  
- build, validate and optimize indices using the command-line tool [Sir.Cmd](https://github.com/kreeben/resin/blob/master/src/Sir.Cmd/README.md)
- read efficiently by specifying which fields to return in the JSON result
- implement messaging formats such as XML (or any other, really) if JSON is not suitable for your use case
- construct queries that join between fields and even between collections, that you may post as JSON to the read endpoint or create programatically.
- construct any type of indexing scheme that produces any type of embeddings with virtually any dimensionality using either sparse or dense vectors.

## Applications

### Executables

- __[Sir.HttpServer](https://github.com/kreeben/resin/blob/master/src/Sir.HttpServer/README.md)__: HTTP search service with HTML GUI and HTTP JSON API for reading and writing.  
- __[Sir.Cmd](https://github.com/kreeben/resin/blob/master/src/Sir.Cmd/README.md)__: Command line tool that executes commands that implement `Sir.ICommand`. Write, validate, optimize and more via command-line.

### Libraries

- __Sir.CommonCrawl__: Command for downloading and indexing Common Crawl WAT and WET files.  
- __Sir.Mnist__: Command for training and testing the accuracy of a index of MNIST images.  
- __Sir.Wikipedia__: Command for indexing Wikipedia.  
- __[Sir.Search](https://github.com/kreeben/resin/blob/master/src/Sir.Search/README.md)__: In-process search engine.  
- __Sir.Core__: Shared interfaces and types, such as `IModel`, `ICommand` and `IVector`.

## Roadmap

- [x] v0.1a - bag-of-characters vector space language model
- [x] v0.2a - HTTP API
- [x] v0.3a - query language
- [x] v0.4 - linear classifier image model
- [ ] v0.5 - semantic language model
- [ ] v1.0 - voice model
- [ ] v2.0 - image-to-voice
- [ ] v2.1 - voice-to-text
- [ ] v2.2 - text-to-image
- [ ] v2.3 - AI
