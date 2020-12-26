# &#9084; Resin

[Introduction](https://github.com/kreeben/resin/blob/master/README.md)  
[User guide](https://github.com/kreeben/resin/blob/master/USER-GUIDE.md) 

## HTTP server Installation

### Download
 
Download a [pre-built package for Windows x64](https://github.com/kreeben/resin/releases/download/v0.4.0.5/sir.httpserver.win-x64.zip) or:

`git clone https://github.com/kreeben/resin.git`

### Install

Extract zip file or, if you used `git clone`, now run the following commands:  

`cd resin\src\sir.httpserver  

dotnet publish -p:PublishProfile=FolderProfile`

### Launch server

`cd resin\src\sir.httpserver\bin\release\net5.0\publish  

dotnet run`