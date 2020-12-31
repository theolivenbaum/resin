# &#9084; Resin.Search

[Overview](https://github.com/kreeben/resin/blob/master/README.md) | [How to install](https://github.com/kreeben/resin/blob/master/INSTALL.md) | User guide 

## User guide

### How to use Sir.Cmd command-line tool
[Instructions for Sir.Cmd](https://github.com/kreeben/resin/blob/master/src/Sir.Cmd/README.md).

### How to use Sir.HttpServer
[Instructions for Sir.HttpServer](https://github.com/kreeben/resin/blob/master/src/Sir.HttpServer/README.md).

### How to index Wikipedia

#### 1. Download Cirrus search engine JSON backup file

[Download](https://dumps.wikimedia.org/other/cirrussearch/current/) a file that contains the word "content".
Wikipedia content is separaded by language into different files. Any will do.

Don't extract it. We'll be reading form the compressed file.

#### 2. Create a data directory on your local storage

E.g.  

´mkdir c:\temp\data\´

#### 3. Store Wikipedia data as Resin documents

To store the contents your newly downloaded Wikipedia data file as Resin documents, issue the following Sir.Cmd command:

`.\sir.bat storewikipedia --dataDirectory c:\temp\data --fileName d:\enwiki-20201026-cirrussearch-content.json.gz --collection wikipedia`

#### 4. Create indices

To create indices from the "text" and "title" fields of your Resin documents, that are segmented into pages of 100K documents,
a number that strikes a good enough balance between indexing and querying speed, issue the following command:  

`.\sir.bat optimize --dataDirectory c:\temp\data --collection wikipedia --skip 0 --take 10000000 --pageSize 100000 --reportFrequency 1000 --fields title,text`

Launch Sir.HttpServer and use a Postman-like client to query your Wikipedia collection, or use the web GUI, as described [here](https://github.com/kreeben/resin/blob/master/src/Sir.HttpServer/README.md).