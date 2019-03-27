# Sir.Store

This is a embeddable document-based full-text search engine with a 64-bit vector space index 
in which words are encoded as non-continuous bags-of-characters. Other type of models are also supported as long as 
it fits inside of a 64-bit vector space.
This language model provide less accuracy than word2vec but also require far less computation, both when training and at querying time.

## Documents

Documents are separated into keys and values. 
String values are tokenized. 
String values that should not be analyzed (tokenized) should have a key that begins with "_" (underscore).
String values that should not be indexed should have a key that begins with "__" (double underscore).
Non-strings are ToString-ed into a single string token. This behavior might change in the future to support numeric ordering. 

## Tokens

Tokens are embedded into a 32-bit wide vectorspace that is represented as a binary search index. 
There is one index per analyzed key.