# Sir.Store

This is a embeddable document-based full-text search engine with a 32-bit vector space index 
in which words are encoded as non-continuous bags-of-characters. 
This language model provide less accuracy than word2vec but also require far less computation, both when training and at query time.

## Documents

Documents are separated into keys and values. 
String values are tokenized but also stored in its original state. 
String values that should not be tokenized should have a key that begins with "_" (underscore).
Non-strings are ToString-ed into a single string token. 
This behavior might change in the future to support numeric ordering. 

## Tokens

Tokens are embedded into a 32-bit wide vectorspace that is represented as a binary search index. There is one index per key.