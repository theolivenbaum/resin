# Sir.Store

Full-text search framework with a 16-bit vector space index. 

Uses a document storage model. 

Everything is auto-indexed.

A non-generic IDictionary serves as the model representation.

The store is able to unbox keys and values of the following types:

    /// <summary>
    /// Supported data types.
    /// </summary>
    public static class DataType
    {
        public static byte STRING = 1;
        public static byte BOOL = 2;
        public static byte CHAR = 3;
        public static byte FLOAT = 4;
        public static byte INT = 5;
        public static byte DOUBLE = 6;
        public static byte LONG = 7;
        public static byte DATETIME = 8;
    }

Keys and values that are not of those types will be ToStringed and seen upon as strings.

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

## Platform

.NET Core 2.0.