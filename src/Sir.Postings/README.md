# Sir.Postings

## A micro service key/value store

This is a key/value store where the key is a Int64 and the payload is a set of Int64's, that you communicate with 
either through HTTP (e.g. if used as a read/write plugin for Sir.HttpServer) or embed directly into your application.

## A map/reduce node

After you have stored your data you may query it by posting boolean query expressions that reference the keys. 
You may execute queries composed of AND, OR and NOT terms. Intersect, union, except and concat set operations are performed on your data.
A window of that set, defined by skip and take query parameters, is returned to you.