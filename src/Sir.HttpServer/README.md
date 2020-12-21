# Sir.HttpServer

Sir.HttpServer is a Kestrel application that serves a HTML search (and result) page and provides a HTTP JSON read/write API.

## HTTP API

### Write a document

HTTP POST `[host]/write/[collection]` (e.g. "http://localhost/write/mycollection")  
Content-Type: application/json  
`
[
	{
		"field1": "value1",
		"field2": "value2"
	}
]
`

### Query for documents

HTTP GET `[host]/query/?collection=mycollection&q=[my_query]&field=field1&field=field2`  
Accept: application/json  

## Web API

Search page designed for humans is here:  

HTTP GET `[host]/` (e.g. "http://localhost:54866/" if you're running Kestrel or "http://localhost:54865/" if you're running IISExpress)  
Accept: text/html