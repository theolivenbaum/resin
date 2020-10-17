# &#9084; Vector database with ML generated indices

Resin is a ML framework and search engine of vector spaces. You customize Resin to your needs by plugging in your own 
training algorithms into the write and indexing pipelines. 
The artefact of a indexing session is a traversable, scannable and deployable index that you may interact with through 
the Resin web GUI, its read/write `JSON HTTP API`, or programmatically.

Resin comes with two built-in models, one bag-of-words `IModel<string>` implementation for text 
as well as a IModel<IImage> implementation for [MNIST](http://yann.lecun.com/exdb/mnist/) images. You may provide your own models. 
You do so by implementing IModel<T>, whose principal function is to provide Resin with a way of converting `T` into `IVector`. 

To summarize, you can populate Resin with your data, query it and in other ways interact with it by using 
the built-in web search GUI, or you can:  
- create custom-made commands (`ICommand`) and execute them through the commandline tool DbUtil.exe  
- write data by HTTP POST-ing JSON formatted data to the built-in HTTP server write endpoints, and query by HTTP GET-ing  
- write IModel<T> implementations 
- programatically scan, traverse and manipulate your indices.

## Applications

### Executables

- __[Sir.HttpServer](https://github.com/kreeben/resin/blob/master/src/Sir.HttpServer/README.md)__: HTTP search service with HTML GUI and HTTP JSON API for reading and writing.  
- __[Sir.DbUtil](https://github.com/kreeben/resin/blob/master/src/Sir.DbUtil/README.md)__: Executes commands that implement `Sir.ICommand`. Write, validate, query and more via command-line.

### Libraries

- __Sir.CommonCrawl__: `ICommand` implementations for downloading and indexing Common Crawl WAT and WET files.  
- __Sir.Mnist__: `ICommand` implementations for training and testing the accuracy of a index of MNIST images.  
- __[Sir.Search](https://github.com/kreeben/resin/blob/master/src/Sir.Search/README.md)__: In-process search engine.  
- __Sir.Core__: Interfaces and types that need to be shared across libraries and apps, such as the `ICommand` and `IVector` interfaces.

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