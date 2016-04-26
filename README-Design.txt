indices
----------------------------------------------------------------------------------------------
field									char trie
field.token								postings (docids/termfreq)
docid.field 							doc value

writing
----------------------------------------------------------------------------------------------
init writer:							git checkout -b datetime.now
										git rebase master

writer.write(doc):						find and rewrite or create new docid.field.doc file
										analyze
										call writer.write(docId, field, token, termfreq)

writer.write(
docid, field, token, termfreq): 		add token to trie
										find and rewrite field.token.postings

writer.remove(docid, field):			find and delete docid.field.doc
										find and update field.token.postings
										if field.token.postings is empty, remove token from trie

writer.dispose:							rewrite trie files
										git commit -m datetime.now
										git checkout master
										git rebase timestamp
										git branch -d timestamp

reading
----------------------------------------------------------------------------------------------
init searcher:							do nothing

searcher.search(query):					analyze query
										for each term (field+token) in query:
											find, read and cache one trie file 
											scan trie
											read field.token.postings from disk
											score
											join (and/or/not)
										read docid.field.doc files from disk and lazily return paged set of docs



