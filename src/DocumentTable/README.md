# DocumentTable

A document table is a table where  

- each row has a variable amount of named columns
- each column is a variable length byte array

A normal table, such as one from a RDBM system, does not allow storing of data in this fashion. A document table is a specialized table made for document database use cases.

On disk a document table can be represented as a header file and a table file where the header file is a column name index and where each row in the table file contains alternating keyID and value blocks, one pair for each of the row's columns. 

A value block is a byte array prepended with a size byte array. The max size of a value byte array is sizeof(int).

The name (key) of each column is a variable length byte array with a max size of sizeof(int).

A document table can contain a maximum of 32767 distinctly named columns (i.e. sizeof(short)) and a maximum of 2.147483647 x 10^9 rows (i.e. sizeof(int)).

### Example of a document table with three rows and two distinctivley unique keys:  

(Line breaks in file layouts are for clarity.)

#### Header file  
	key0 key1  
#### Table file
	keyId0 val_len value keyId1 val_len value  
	keyId1 val_len value  
	keyId0 val_len value keyId1 val_len value  

### Byte representation:  
#### Header file   
	int var_len_byte_arr int var_len_byte_arr  
#### Table file
	short int var_len_byte_arr short int var_len_byte_arr  
	short int var_len_byte_arr  
	short int var_len_byte_arr short int var_len_byte_arr  
  
To fetch a row from the table you need to know the starting byte position of the row as well as its size.

## BlockInfo

A block info is a tuple containing the starting byte position (long) of a block of data and its size (int). A block info is thus fixed in size. 

A block info file containing block info tuples that have been serialized into a bitmap, each block ordered by the index of the document table row it points to, can act as an index into the document table.

## Compression/encoding

You may choose to compress rows in the document table. Compression flags, i.e. data describing how to decode a stored value byte array into its original state, are stored in a batch info file.

Batches (of rows) have unique (incrementaly and uniformly) increasing version IDs (timestamps). 

Each row is compressed individually and each batch can be compressed (encoded) differently. 

### Example of a file containing batch compression information:

	start_row_index end_row_index compression_flag

### Byte representation:

	long long short

When using the same compression or encoding uniformly over all rows a batch info file is not needed.

## Versioning

Inserting a document with a primary key into a document table and then performing an update on that document will make it appear twice in the table file but with different row IDs. Those two occurrances also differ because they belong to different batches. 

Batches are timestamped. When reading from a document table only the last version, chronologically speaking, should be fetched.

## Implementations
### A document table row
[Document](https://github.com/kreeben/resin/blob/master/src/DocumentTable/Document.cs#L38)
### A writer that takes a document row and returns a [BlockInfo](https://github.com/kreeben/resin/blob/master/src/StreamIndex/BlockInfo.cs)
[DocumentWriter](https://github.com/kreeben/resin/blob/master/src/DocumentTable/DocumentWriter.cs)
### A reader that takes a list of BlockInfo's, sorts them by position, and returns deserialized document table rows.
[DocumentReader](https://github.com/kreeben/resin/blob/master/src/DocumentTable/DocumentReader.cs)
### A stream of document table rows (filters out obsolete data).
[DtblStream](https://github.com/kreeben/resin/blob/master/src/ResinCore/DtblStream.cs)

