# StreamIndex<T>

A generic IO system for variable sized byte arrays.

## BlockWriter

Takes any class or struct, serializes it into a variable length byte array, writes it to a stream and returns the position in the stream of the first byte of the array as well as its size. 
  
Serialization is a abstract method for you to implement.

## BlockReader

Takes a list of offset/size tuples, reads a byte array from a stream and returns a list of structs or classes.
  
Deserialization is a abstract method for you to implement.