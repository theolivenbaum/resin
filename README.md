# Resin
It's a full-text search framework you can reason about. It's simplistic and capable. It is not built upon Lucene.

##Quick usage guide

####Here's an interesting document

	{
	  "Fields": {
		"id": [
		  "Q1"
		],
		"label": [
		  "universe"
		],
		"description": [
		  "totality of planets, stars, galaxies, intergalactic space, or all matter or all energy"
		],
		"aliases": [
		  "cosmos The Universe existence space outerspace"
		]
	  },
	  "Id": 1
	}

####Here's a huge number of documents

	var docs = GetHugeNumberOfDocs();

####Add them to a Resin index

	var dir = @"c:\MyResinIndices\0";
	using (var writer = new IndexWriter(dir, new Analyzer()))
	{
		foreach (var doc in docs)
		{
			writer.Write(doc);
		}
	}

####Query that index (matching the whole term)

	using (var searcher = new Searcher(dir))
	{
		var result = searcher.Search("label:universe");
	}

####Prefix query

	var result = searcher.Search("label:univ*");

####And

	var result = searcher.Search("label:universe +aliases:cosmos");

####Or

	var result = searcher.Search("label:universe aliases:cosmos");

####Not

	var result = searcher.Search("label:universe -aliases:cosmos");

####Only a few at a time
	
	var page = 0;
	var size = 10;
	var result = searcher.Search("label:univ*", page, size);
	var totalNumberOfHits = result.Total;
	var docs = result.Docs.ToList(); // Will contain a maximum of 10 docs

####Only one

	var result = searcher.Search("id:Q1");
	var doc = result.Docs.First();

####All fields queryable, whole document returned

	var result = searcher.Search("id:Q1");
	var doc = result.Docs.First();
	var aliases = doc.Fields["aliases"].First(); // Each field contains a list of values
	// Print "cosmos The Universe existence space outerspace"
	Console.WriteLine(aliases);

####Analyze your index

	var field = "label";
	var scanner = new Scanner(dir);
	var tokens = scanner.GetAllTokens(field).OrderByDescending(t=>t.Count).ToList();
	File.WriteAllLines(Path.Combine(dir, "_" + field + ".txt"), tokens
		.Select(t=>string.Format(
			"{0} {1}", t.Token, t.Count)));

####Resin is fast because

	using (var searcher = new Searcher(dir))
	{
		// This loads and caches the token index for the "label" field
		var result = searcher.Search("label:universe");
		
		//This executes the query, loads and caches the documents (one in this case, the first)
		var doc1 = result.Docs.First();
		
		// The following query requires 
		// - one hashtable lookup towards the field file index to find the field ID
		// - one hashtable lookup towards the token index to find the doc IDs
		// - for each doc ID: one hashtable lookup towards the doc cache
		var docs = searcher.Search("label:universe").Docs.ToList();
		
		// The following prefix query requires 
		// - one hashtable lookup towards the field file index to find the field ID
		// - one [Trie](https://github.com/kreeben/resin/blob/master/Resin/Trie.cs) scan to find matching tokens
		// - for each token: one hashtable lookup towards the token index to find the doc IDs
		// - append the results of the scan (as if the tokens are joined by "OR")
		// - for each doc ID: one hashtable lookup towards the doc cache
		docs = searcher.Search("label:univ*").Docs.ToList();
	}	

####There is also a CLI

![alt text](https://github.com/kreeben/resin/blob/master/screenshot5.PNG "The Cli.")

More on the CLI [here&#8628;](#cli).
  
Use [freely](https://github.com/kreeben/resin/blob/master/LICENSE) and register [issues here](https://github.com/kreeben/resin/issues).

Contribute frequently. Go directly to an [introduction&#8628;](#citizens) of the parts that make up Resin.  
                  
<a name="citizens" id="citizens"></a>
##First class citizens

To be able to call ourselves a full-text search framework we need something that can analyze text, an [Analyzer](https://github.com/kreeben/resin/blob/master/Resin/Analyzer.cs). Also, something that can write index files and store documents, an [IndexWriter](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs), [FieldWriter](https://github.com/kreeben/resin/blob/master/Resin/FieldWriter.cs) and a [DocumentWriter](https://github.com/kreeben/resin/blob/master/Resin/DocumentWriter.cs). 

We will need to be able to parse multi-criteria queries such as "title:Rambo +title:Blood", in other words a [QueryParser](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs). The important questions for the parser to answer are what fields do we need to scan and what are the tokens that should match. A space character between two query terms such as the space  between "Rambo title:" in the query `title:Rambo title:Blood` will be interpreted as `OR`, a plus sign as `AND`, a minus sign as `NOT`. In other words that query will be parsed into "please find documents that has both rambo AND blood in the title", or in a more machine-like language `scan the field named title for the tokens rambo and blood and return the intersection of their postings`.

An [IndexReader](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and a [FieldReader](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) will make it possible for a [Scanner](https://github.com/kreeben/resin/blob/master/Resin/Scanner.cs) to get a list of document IDs containing the tokens at hand. A DocumentReader will assist in fetching the documents, in the state they were in at indexing time, from disk.

####The Analyzer

	public IEnumerable<string> Analyze(string value)
	{
	    var token = new List<char>();
	    foreach (var c in value.ToLower(_culture))
	    {
	        if (IsSeparator(c))
	        {
	            if (token.Count > 0)
	            {
	                var tok = new string(token.ToArray());
	                if (!_stopwords.Contains(tok)) yield return tok;
	                token.Clear();
	            }
	        }
	        else
	        {
	            token.Add(c);
	        }
	    }
	    if (token.Count > 0)
	    {
	        var tok = new string(token.ToArray());
	        yield return tok;
	    }
	}
	
	private bool IsSeparator(char c)
	{
	    return
	        char.IsControl(c) ||
	        char.IsPunctuation(c) ||
	        char.IsSeparator(c) ||
	        char.IsWhiteSpace(c) ||
	        _tokenSeparators.Contains(c);
	}

An analyzer produces normalized tokens from text. `var text = "Hello World!"` may be normalized into `{"hello", "world"}` if we lower-case it and split it up at characters ` ` and `!`. By tokenizing the text of a field we make the individual tokens insensitive to casing, queryable. Had we not only exact matches to the verbatim text can be made at runtime, if we want the querying to go fast. The query "title:Rambo" would produce zero documents (no movie in the whole world actually has the title "Rambo") but querying "title:Rambo\\: First Blood" would produce one hit. 

But only if you are scanning a database of Swedish movie titles because the original movie title was "First Blood". Swedish Media Institue (it's called something else, sorry, I forget) changed the title to the more declarative "Rambo: First Blood". This is probably what happened:

Guy: Americans have sent us new movie.

Boss: What's it called?

g: First blood!

b: First blood? What kind of silly name is that? Who's it about?

g: Well, there's this guy, Rambo, he...

b: Rambo! What great name! Put THAT in title. I'm feeling also this might be franchise.

Another thing we hope to achieve by analyzing text is to normalize between the words used when querying and the words in the documents so that matches can be produced consistently. 

##### Deep analysis
The analysis you want to do both at indexing and querying time is to acctually try to understand the contents of the text, that a "Tree" is the same thing as a "tree" and a component of "trees". What if you could also pick up on themes and subcontexts?

What we are doing however in Analyzer.cs is very rudimentary type of analysis. We are simply identifying the individual words. We could go further, investigate if any of those words are kind of the same, because although "trees" != "tree" their concepts intersect so much so that in the interest of full-text search they could and maybe should be one and the same concept. Anyway, identifying and normalizing the words will be fine for now.

[Code](https://github.com/kreeben/resin/blob/master/Resin/Analyzer.cs) and [tests](https://github.com/kreeben/resin/blob/master/Tests/AnalyzerTests.cs)

####FieldWriter
Tokens are stored in a field file. A field file is an index of all the tokens in a field. Tokens are stored together with postings. Postings are pointers to documents. Our postings contain the document ID and how many times the token exists within that document, its document frequency.

That means that if we know what field file to look in, we can find the answer to the query "title:rambo" by opening the file, deserialize the contents into this:

	// tokens/docids/doc frequency
        private readonly IDictionary<string, IDictionary<int, int>> _tokens = DeserializeFieldFile(fileName);
	
	// ...and then we can find the document IDs. This operation does not take long.
	IDictionary<int, int> postings;
	if (!_tokens.TryGetValue(token, out postings))
	{
		return null;
	}
	return postings;

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldWriter.cs) and [tests](https://github.com/kreeben/resin/blob/master/Tests/FieldWriterTests.cs)

####DocumentWriter

Documents are persisted on disk. How they look on disk is not very interesting. 

The in-memory equivalent of a document file is this:

	// docid/fields/values
	private readonly IDictionary<int, IDictionary<string, IList<string>>> _docs;

Here is a document on its own:

	// fields/values
	IDictionary<string, IList<string>> doc;

More than one document fit into a document file. A whole list of them would fit. Imagine how it looks in-memory. I mean I can only guess the shape but it looks to be covering a large area of your RAM. It's a huge tree of stuff. 

[Code](https://github.com/kreeben/resin/blob/master/Resin/DocumentWriter.cs)

####IndexWriter

Store the documents. But also analyze them and create field files that are queryable. There's not much to it:

	public void Write(Document doc)
	{
	    foreach (var field in doc.Fields)
	    {
	        foreach (var value in field.Value)
	        {
	            // persist the value of the field, as-is, by writing to a document file

	            var tokens = _analyzer.Analyze(value);
	            foreach(var token in tokens)
	            {
	            	// store the doc ID, token and its position in a field file
	            }
	        }
	    }
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/IndexWriterTests.cs)

#### QueryParser
With our current parser we can interpret "title:Rambo", also `title:first title:blood`. The last query is what lucene decompiles this query into: `title:first blood`. We will try to mimic this later on but for now let's work with the decompiled format.

	var q = query.Split(' ').Select(t => t.Split(':'));

[Code](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/QueryParserTests.cs)

####FieldReader

You have already seen the in-memory representation of the field file:

	// tokens/docids/doc frequency
	private readonly IDictionary<string, IDictionary<int, int>> _tokens;

A field reader can do this:

	var tokens = reader.GetAllTokens();
	var docPos = reader.GetDocPosition(string token);

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/FieldReaderTests.cs)

#### Scanner

After a good parsing we get back a list of terms. A term is a field and a token, e.g. "title:rambo". 

All that we know, as a search framework, is called "a lexicon".

At the back of that lexicon is an index, the field file. A scanner scans the index and if it finds a match it returns the doc IDs and a score: 

	public IEnumerable<DocumentScore> GetDocIds(Term term)
	{
	    int fieldId;
	    if (_fieldIndex.TryGetValue(term.Field, out fieldId))
	    {
	        var reader = GetReader(term.Field);
	        if (reader != null)
	        {
	            if (term.Prefix)
	            {
	                term.Boost = 1;
	                return GetDocIdsByPrefix(term, reader);
	            }
	            term.Boost = 2;
	            return GetDocIdsExact(term, reader);
	        }
	    }
	    return Enumerable.Empty<DocumentScore>();
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/Scanner.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/ScannerTests.cs)

#### IndexReader

The IndexReader needs a scanner. The results of a scan is a list of document ids. IndexReader resolves the document and returns that instead of the id.

	public IEnumerable<Document> GetDocuments(string field, string token)
	{
		var docs = _scanner.GetDocIds(field, token);
		foreach (var id in docs)
		{
			yield return GetDocFromDisk(id); // deserialization might take a while if it's a heavy document
		}
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and  [tests](https://github.com/kreeben/resin/blob/master/Tests/IndexReaderTests.cs)

####Searcher

Finally, the searcher, a helper that takes an IndexReader and a QueryParser, accepting unparsed queries, lazily returning a list of documents:

	public IEnumerable<Document> Search(string query)
    {
        var terms = _parser.Parse(query).ToList();
        return _reader.GetDocuments(terms);
    }

[Code](https://github.com/kreeben/resin/blob/master/Resin/Searcher.cs) 

<a name="cli" id="cli"></a>
## Test spin

1. Download a Wikipedia JSON dump [here](https://dumps.wikimedia.org/wikidatawiki/entities/)
2. Use the [WikipediaJsonParser](https://github.com/kreeben/resin/blob/master/Resin.WikipediaJsonParser/Program.cs) to extract as many documents as you want. In a cmd window:

	cd path_to_resin_repo
	
	rnw c:\downloads\wikipedia.json 0 1000000

	This will generate a new file: wikipedia_resin.json. 0 documents was skipped and 1M taken from the wikipedia file.

3. Create an index:
	
	rn write --file c:\downloads\wikipedia_resin.json --dir c:\temp\resin\wikipedia --skip 0 --take 1000000

4. After 3 minutes or so, do this:  

	rn query --dir c:\temp\resin\wikipedia -q "label:ringo"

![alt text](https://github.com/kreeben/resin/blob/master/screenshot.PNG "I have an SSD. The index was warmed up prior to the query.")

A little less than a millisecond apparently. A couple of orders of magitude faster than Lucene. Here's what went down:

	var q = args[Array.IndexOf(args, "-q") + 1];
	var timer = new Stopwatch();
	using (var s = new Searcher(dir))
	{
	    for (int i = 0; i < 1; i++)
	    {
	        s.Search(q).Docs.ToList(); // this heats up the "label" field and pre-caches the documents
	    }
	    timer.Start();
	    var docs = s.Search(q).Docs.ToList(); // Fetch docs from cache
	    var elapsed = timer.Elapsed.TotalMilliseconds;
	    var position = 0;
	    foreach (var doc in docs)
	    {
	        Console.WriteLine(string.Join(", ", ++position, doc.Fields["id"][0], doc.Fields["label"][0]));
	    }
	    Console.WriteLine("{0} results in {1} ms", docs.Count, elapsed);
	}

Here is another test, this time the documents aren't pre-cached in the warmup:

	var q = args[Array.IndexOf(args, "-q") + 1];
	var timer = new Stopwatch();
	using (var s = new Searcher(dir))
	{
	    for (int i = 0; i < 1; i++)
	    {
	        s.Search(q); // warm up the "label" field
	    }
	    timer.Start();
	    var docs = s.Search(q).Docs.ToList(); // Fetch docs from disk
	    var elapsed = timer.Elapsed.TotalMilliseconds;
	    var position = 0;
	    foreach (var doc in docs)
	    {
	        Console.WriteLine(string.Join(", ", ++position, doc.Fields["id"][0], doc.Fields["label"][0]));
	    }
	    Console.WriteLine("{0} results in {1} ms", docs.Count, elapsed);
	}

![alt text](https://github.com/kreeben/resin/blob/master/screenshot3.PNG "Docs weren't in the cache.")

##Roadmap

####Query language interpreter
AND, OR, NOT (+ -), prefix* and fuzzy~ [implemented here](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs).
TODO: nested clauses

####Prefix search
Implemented currently as a Trie scan [here](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs#L41).

Here's an example of a prefix search towards and index of 1M english wikipedia docs:
![alt text](https://github.com/kreeben/resin/blob/master/screenshot4.PNG "Trie's are fast")

#####Other things to do:

####Fuzzy
The term-based search that is currently implemented is extremly fast because once you have deserialized the indexes the scan, the resolve of the document, they are all hash-table look-ups.

The problem of both prefix and fuzzy querying may be seen as a problem of finding out which tokens to look for. 

If you create an ngram-index from the lexicon and ngram the query token the same way and look up the terms for those grams, calculate the [Levenshtein distance](https://en.wikipedia.org/wiki/Levenshtein_distance), what is left are the term-based queries. Such a Lucene-inspired fuzzy query implementation would add a couple of steps to the querying pipeline and those steps would be all about finding out which terms to scan for.

####Ranking
If that goes well then what is left in our [MVP](https://en.wikipedia.org/wiki/Minimum_viable_product) is the ranking algorithm. That should be tons of fun. That's where the [Lucene core team](http://opensourceconnections.com/blog/2015/10/16/bm25-the-next-generation-of-lucene-relevation/) is at.

####Writing to an index in use
Lucene does it extremly well because of it's file format and because it's indices are easily mergable. It sees an update as "first create new index then merge with the current index then refresh the index reader".

####Scaling

Service-orient the different parts of Resin. Make scanning, scoring and resolving of documents each a service of its own. Increase caching abilities. Buy Gb network and use bare-metal.

####Extreme scaling

Shard the index and connect each subindex to each other in a network of nodes.

<a name="point" id="point"></a>
####Multi-index searching
Lucene does it. It's useful. Resin needs it.

## Final analysis

The top 100 tokens of this document, in the order of their frequency, making "the" the most important word. If you filter out some common words, stop words, you see more clearly the main concepts. 

the 346

a 210

to 162

of 156

and 152

you 130

that 120

__resin__ 110

in 110

it 102

is 92

s 80

be 70

var 56

i 54

__lucene__ 52

__documents__ 52

https 52 (markdown links)

__index__ 50

__tokens__ 50

__this__ 48

as 48

__field__ 48

com 48 (markdown links)

are 46

github 46 (markdown links)

kreeben 46 (markdown links)

blob 46 (markdown links)

master 46 (markdown links)

if 44

__document__ 44

cs 44

we 42

__will__ 40

__query__ 40

__title__ 40

at 38

__file__ 38

__text__ 36

__search__ 36

an 36

__string__ 34

__not__ 32

__up__ 32

__can__ 30

__about__ 30

for 30

__token__ 30

__querying__ 28

with 28

0 28

__int__ 28

__what__ 26

__because__ 26

__rambo__ 26

on 24

there 24

t 24

from 22

into 22

even 22

so 22

__wikipedia__ 22

then 22

by 22

__fields__ 22

__all__ 22

__value__ 22

__new__ 22

__blood__ 22

here 20

but 20

__fast__ 20

or 20

__code__ 20

they 20

__first__ 20

__need__ 20

__terms__ 20

__idictionary__ 20

__docs__ 20

__doc__ 20

__have__ 18

__would__ 18

d 18

__little__ 18

__positions__ 18

__analyzer__ 18

__return__ 18

__very__ 16

__how__ 16

__your__ 16

__one__ 16

__like__ 16

__than__ 16

__also__ 16

__analyze__ 16

__able__ 16

__scan__ 16

__public__ 16
