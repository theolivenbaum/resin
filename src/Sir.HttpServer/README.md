# Sir.HttpServer

This is a HTTP micro-service API. 
Plug in your readers and writers and design your read and write data pipelines. 
Host each plugin in isolation or all of them on the same machine.  

Allows you to  

- aggregate data from many sources
- dispatch data to many targets
- parse many formats
- understand many query languages
- ditribute your data while maintaining its queryability

## Programmability

Readers, writers and query parsers are mapped to HTTP media types.  

- Implement your own [https://github.com/kreeben/resin/blob/master/src/Sir/IWriter.cs](writer) to dispatch your data to many stores or to a particular store.
- Implement your own [https://github.com/kreeben/resin/blob/master/src/Sir/IReader.cs](reader) to aggregate data from many sources or read from a specific database.
- Add support for your favorite format by plugging in your AspnetCore MVC IInputFormatter/IOutputFormatter implementation.
