# Sir.HttpServer

This is a HTTP micro-service API. Plug in your readers and writers and design your read and write data pipelines. 
Host each plugin in isolation or all of them on the same machine.  

Allows you to  

- aggregate data from many sources
- dispatch data to many targets
- parse many formats
- understand many query languages

## Programmability

Readers, writers and query parsers are mapped to HTTP media types.  

- Implement your own writer to dispatch your data to many stores or to a particular store.
- Implement your own reader to aggregate data from many sources or read from a specific database.
- Add support for your favorite format by plugging in your AspnetCore MVC IInputFormatter/IOutputFormatter implementation.
- Add support for your favorite query language by plugging in your custom query parser.
