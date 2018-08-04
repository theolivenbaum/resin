# Sir.Store

Full-text search framework with a 16-bit vector space index.

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

### .vec

	[char][int][char][int]...

## Platform

.NET Core 2.0.