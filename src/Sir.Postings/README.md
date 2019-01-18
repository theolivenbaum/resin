# Sir.Postings

A key/value store where the key is a Int64 and the payload is a list of UInt64's.

After you have stored your payload, you can send queries to the service, boolean query expressions, that reference the keys.

This service will perform AND, OR and NOT arithmetics on your payload and return to you a window those UInt64 values that are the result of the query expression.
