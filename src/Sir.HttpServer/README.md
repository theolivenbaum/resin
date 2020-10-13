# Sir.HttpServer

Sir.HttpServer is a Kestrel application that both serves a HTML search (and result) page as well as providing a HTTP read/write API.

## HTTP API

### Write

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

### Read

HTTP GET `[host]/query/?collection=mycollection&q=[my_query]&field=field1&field=field2`  
Accept: application/json  

## Web API

Search page designed for humans is here:  

HTTP GET `[host]/` (e.g. "http://localhost/")  
Accept: text/html