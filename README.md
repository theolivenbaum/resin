# &#9084; Resin

Resin is a search engine of vector spaces that use hardware accelerated vector operations 
from [MathNet](https://github.com/mathnet/mathnet-numerics) when building and scanning indices.

Resin includes a web query GUI, a HTTP JSON API and an embeddable API for reading, writing and analyzing your data.

Resin comes pre-loaded with two vector space configurations (`models`), one for [text](https://github.com/kreeben/resin/blob/master/src/Sir.Search/Models/BagOfCharsModel.cs) 
and [another](https://github.com/kreeben/resin/blob/master/src/Sir.Search/Models/LinearClassifierImageModel.cs) for [MNIST](http://yann.lecun.com/exdb/mnist/) images. 
The latter is included mostly as an example of how to use machine-learning techniques to build custom-made search indices.

You may plug in your own models into Resin's read and write pipelines. You do so by implementing IModel<T>. Regardless of which model you use the write pipeline produces 
a traversable, scannable and deployable index that you may interact with through the Resin web GUI, its read/write `JSON HTTP API`, or programmatically.

You may also:  
- build, validate and optimize indices using the command-line tool [Sir.Cmd](https://github.com/kreeben/resin/blob/master/src/Sir.Cmd/README.md)
- write data by HTTP POST-ing JSON formatted data to the built-in HTTP server write endpoints
- read efficiently by specifying with fields to return in the JSON result  
- write IModel<T> implementations 
- programatically scan, traverse, perform calculations over and in other ways manipulate your indices.

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
- [ ] v3.0 - AI