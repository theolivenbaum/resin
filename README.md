# &#9084; Resin

Resin is a search library and service that can help you search through any vector space. It uses hardware accelerated vector operations from 
[MathNet](https://github.com/mathnet/mathnet-numerics) to build indices of your data that you may then scan with ease, progammatically or 
by using the built-in HTTP API. 

Resin stores data as document collections. It applies your prefered IModel<T> onto your data when writing and querying. 
The write pipeline produces a set of indices (graphs), one for each document field, that you may interact with by using the Resin web GUI, 
the Resin read/write JSON HTTP API, or programmatically.

Resin comes pre-loaded with two vector space configurations: one for [text](https://github.com/kreeben/resin/blob/master/src/Sir.Search/Models/BagOfCharsModel.cs) 
and [another](https://github.com/kreeben/resin/blob/master/src/Sir.Search/Models/LinearClassifierImageModel.cs) for [MNIST](http://yann.lecun.com/exdb/mnist/) images. 
The former has been tested by validating indices generated from Wikipedia search engine dumps as well as by parsing Common Crawl WAT, WET and WARC files, 
to determine at which scale Resin may operate in and at what accuracy. Currently, Wikipedia size data sets produce indices capable of sub-second phrase searching. 
The image model is included mostly as an example of how to implement your own prefered machine-learning algorithm for building custom-made search indices. 
The error rate of the image classifier is ~5%. 

You may plug in your own vector space configurations into Resin's read and write pipelines. You do so by implementing IModel<T>. 

You may also:  
- build, validate and optimize indices using the command-line tool [Sir.Cmd](https://github.com/kreeben/resin/blob/master/src/Sir.Cmd/README.md)
- read efficiently by specifying which fields to return in the JSON result
- implement messaging formats such as XML (or any other, really) if JSON is not suitable for your use case
- construct queries that join between fields and even between collections, that you may post to the write endpoint as JSON or create programatically.

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