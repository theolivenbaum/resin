# Sir.HttpServer

Sir.HttpServer is a Kestrel application that both serves a HTML search (and result) page and provides a HTTP read/write API.

## HTTP API

### Write

HTTP POST `[host]/io/[collection]` (e.g. "https://myapp.com/io/mycollection")

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

HTTP GET `[host]/io/[collection]?q=[my_query]&field=field1&field=field2`

Accept: application/json

## Web API

Search page designed for humans is here: `[host]/`