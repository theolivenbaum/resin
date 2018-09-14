# Sir.Store

Full-text search framework with 32-bit vector space indexing. 

Uses a document storage model. 

Everything is auto-indexed.

## File format

### _.kmap

	(key_id) key_hash

### _.val

	val val val... 

### _.vix

	(val_id) val_offset val_len val_type

### .key

	key key key... 

### .docs

	key_id val_id...

### .kix

	(key_id) key_offset key_len key_type

### .dix

	(doc_id) doc_offset doc_len

### .pos

	[doc_id doc_id next_page_offset] [doc_id       ]...

### .ix

	[node][node]...

See [VectorNode](src/Sir.Store/VectorNode.cs) for details.

### .vec

	[char][int][char][int]...
