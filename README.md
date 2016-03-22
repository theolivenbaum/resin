# Resin
It's a full-text search framework you can reason about. It's simplistic and very capable. It is not built upon Lucene.

##A quick usage guide

####Here is a really interesting document

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

"[...]or all matter or all energy". Wow!

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
	var aliases = doc.Fields["aliases"].First();
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
		
		//This loads and caches the document
		var doc1 = result.Docs.First();
		
		// The following query requires 
		// - one hashtable lookup towards the field file index to find the field ID
		// - one hashtable lookup towards the token index to find the doc IDs
		// - for each doc ID: one hashtable lookup towards the doc cache
		var doc2 = searcher.Search("label:universe").Docs.First();
		
		// The following prefix query requires 
		// - one hashtable lookup towards the field file index to find the field ID
		// - one Trie scan to find matching tokens
		// - for each token: one hashtable lookup towards the token index to find the doc IDs
		// - append the results of the scan (as if the tokens are joined by "OR")
		// - for each doc ID: one hashtable lookup towards the doc cache
		var doc3 = searcher.Search("label:univ*").Docs.First();
	}	

####There is also a CLI

![alt text](https://github.com/kreeben/resin/blob/master/screenshot5.PNG "The Cli.")

More on the CLI [here&#8628;](#cli).
  
Use [freely](https://github.com/kreeben/resin/blob/master/LICENSE) and register [issues here](https://github.com/kreeben/resin/issues).

Contribute frequently. Go directly to an [introduction&#8628;](#citizens) of the parts that make up Resin.  
                  
##How to build your own full-text search in c#, yeah!

Here's some guidance I could have used when I started building search frameworks. Resin is the 6th iteration I've done. The codebase, it's pieces, get smaller and simpler each round. Use this to get some ideas from if you are into information retrieval. The nerd factor on that last sentence is completely off the charts, I'm well aware, thank you.

Even though this is about Resin to Google this article is very much about Lucene, so much so that querying it's enourmous index with the term "body:lucene" will render this document in it's results. One could argue that since Lucene is being brought up and very early to that this document certainly is about Lucene. I buy that but would still like to say, this will be mostly about how I built my own indexer-searcher thing that can _index 1M english wikipedia articles in approximately 3 minutes and then respond to multi-criteria term or prefix based queries towards that index in less than a tenth of a millisecond_. That's on my 3 year old i5 laptop. It's really thin. An orange Lenovo Yoga 2 Pro. Just sayin'.

##Why?

You mean why build a search framework? Well,

- You like Lucene and you're curious about the decisions behind the Lucene design and the reasons as to why it looks the way it does
- You wonder why it's so damn fast
- You wonder what parts of the Lucene.net design is there because of (java) legacy and if the design might be improved upon or simplified
- You sometimes wish that building and querying an index was surrounded by even less code, perhaps by leaning towards conventions you yourself have built up throughout the years of using Lucene
- You wonder what would happen if the .net community gathered around a .net project instead of a line-by-line java port, because sometimes you'd like to understand why your search is acting the way it does but you find the architecture behind Lucene to be complex and you are scared to even look at the source code, not that complexity is neccessarily a java legacy. Some dotnetpeople also suffer from over engineering everything they touch. A really good read on this topic is the Umbraco 5 codebase. It's very big and funny. *
- The Lucene.net team has proven that a .net runtime hosted on a windows machine provides a wonderful environment for a creature such as a full-text search framework to live and enjoy itself in but it makes you a little bit sad that they will always be a couple of years behind the core Lucene team
- You are just genuinely curious about the whole domain of information retrieval, perhaps because it is a small domain, relatively easy to grasp and at it's basic level the math is not frightening, and you see it as one of the tools taking us closer to IR's older cousin AI
- You want to pretend you are building something smart and AI-like and neural networks scare you worse than long, cold hotel corridors and kids riding their pedal cars up and down the carpets of an otherwise empty luxury estate
 
*) They were on to something though. They saw great gains in representing anything and everything as a node. Had they pulled it of, perhaps they could have by using less abstractions, Umbraco would have been easy to reason about and to refine even further.

Lists with points are a boring read. Here's something to lighten up your mood and then there's some code. At the end there will be a fully functional full-text search CLI. I will explain how to use that to create a Resin index from a Wikipedia dump and then how to query that index. There will be measurements on how Resin behaves in a fully cached state and in a partially cached state. 

Skip to [here&#8628;](#citizens) to make this an even shorter read. 

###The very short story of the small domain of full-text search

In this story there are documents with fields such as title and author and there are tokens, which is what you get when you chop up into pieces, or analyze, the values of those fields. There is also a task at hand, which is to be able to find any document by supplying any token it contains. If many documents contain that token then all of those documents shall be fetched and arranged in the order of relevance, the most relevant first. 

You find yourself in front of a huge stack of documents that reaches over you head, a couple of months worth of unopened mail, and from the documents in that pile you want to be able to produce a small, neat little dosier (as shallow as possible actually) of bills that absolutely, positively must be payed today. So you analyze all of the documents by splitting up the value of each field into tokens, making them lower-case, normalizing the text that you will later scan through, making that process easier which is good because you want the scanning to go fast later on. In an index you keep track of all of the tokens and their positions inside of the documents making it easy to find anything containing a certain token.

Later that day you start querying the index with tokens such as "pay" and "bill", one at a time. Invoices and letters from your friend Bill surface.

You realize that you need to be able to select documents based on more than one criteria so you introduce the concept of "AND" into your querying process which intersects the results of a multi-criteria query, giving you a small, neat little dosier of bills to pay. You then create and witness a new open-source project gaining immensly in popularity eventually leading to the point where you can acctually pay those bills. But by then, even though you used the same criteria the dosier became a little fatter and did not contain the same bills. Stuff had happened. The indexed had changed. Good thing you got payed. The end.

###Here's what you need

You need to be able to swiftly index documents without taking up too much memory or disk space. You need to be able to query that index for documents and get them back in exactly the same shape they were in before you started analyzing them. The process of querying must be fast. Not Lucene-fast, but fast. The time it takes to understand the query, perform the scan and then retrieve the documents from disk must be below a second, preferably tens of milliseconds (like Lucene) or at least around a couple of hundred milliseconds. You need to be able to update the index, add new documents and remove old ones. 

Even though you could be thinking about the values of fields as being objects, any Object, any IComparable even, that would actually make even more sense, to start with you will only solve the querying part, not the custom sorting of results that Lucene is capable of. Therefore you don't need your values to be of type IComparable, they can be strings.

<a name="citizens" id="citizens"></a>
###FIrst class citizens

To be able to call ourselves a full-text search framework we need something that can analyze text, an [Analyzer](https://github.com/kreeben/resin/blob/master/Resin/Analyzer.cs). Also, something that can write index files and store documents, an [IndexWriter](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs), [FieldFile](https://github.com/kreeben/resin/blob/master/Resin/FieldFile.cs) and a [DocumentFile](https://github.com/kreeben/resin/blob/master/Resin/DocumentFile.cs). 

We will need to be able to parse multi-criteria queries such as "title:Rambo +title:Blood", in other words a [QueryParser](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs). The important questions for the parser to answer are what fields do we need to scan and what are the tokens that should match. A space character between two query terms such as the space  between "Rambo title:" in the query `title:Rambo title:Blood` will be interpreted as `OR`, a plus sign as `AND`, a minus sign as `NOT`. In other words that query will be parsed into "please find documents that has both rambo AND blood in the title", or in a more machine-like language `scan the field named title for the tokens rambo and blood and return the intersection of their postings`.

An [IndexReader](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and a [FieldReader](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) will make it possible for a [Scanner](https://github.com/kreeben/resin/blob/master/Resin/Scanner.cs) to get a list of document IDs containing the tokens at hand. A DocumentReader will assist in fetching the documents, in the state they were in at indexing time, from disk.

##The Analyzer

	public class Analyzer
	{
		private readonly char[] _tokenSeparators;

		public Analyzer(char[] tokenSeparators = null)
		{
			_tokenSeparators = tokenSeparators ?? new[]
			{
				' ', '.', ',', ';', ':', '!', '"', '&', '?', '#', '*', '+', '|', '=', '-', '_', '@', '\'',
				'<', '>', '“', '”', '´', '`', '(', ')', '[', ']', '{', '}', '/', '\\',
				'\r', '\n', '\t'
			};
		}

		public string[] Analyze(string value)
		{
			return value.ToLowerInvariant().Split(_tokenSeparators, StringSplitOptions.RemoveEmptyEntries);
		}
	}
	
	[Test]
        public void Can_analyze()
        {
            var terms = new Analyzer().Analyze("Hello!World?");
            Assert.AreEqual(2, terms.Length);
            Assert.AreEqual("hello", terms[0]);
            Assert.AreEqual("world", terms[1]);
        }

An analyzer produces normalized tokens from text. `var text = "Hello World!"` may be normalized into `new[]{"hello", "world"}` if we lower-case it and split it up at characters ` ` and `!`. By tokenizing the text of a field we make the individual tokens insensitive to casing, queryable. Had we not only exact matches to the verbatim text can be made at runtime, if we want the querying to go fast. The query "title:Rambo" would produce zero documents (no movie in the whole world actually has the title "Rambo") but querying "title:Rambo\\: First Blood" would produce one hit. 

But only if you are scanning a database of Swedish movie titles because the original movie title was "First Blood". Swedish Media Institue (it's called something else, sorry, I forget) changed the title to the more declarative "Rambo: First Blood". This is probably what happened:

Guy: The americans have sent us new movie.

Boss: What's it called?

g: First blood!

b: First blood? What kind of silly name is that? Who's it about?

g: Well, there's this guy, Rambo, he...

b: Rambo! What great name! Put THAT in title. I'm feeling also this might be franchise.

Another thing we hope to achieve by analyzing text is to normalize between the words used when querying and the words in the documents so that matches can be produced consistently. 

I don't speak like that btw. They were definitely not swedish, maybe russian or ukranian. So go back to the voice you had originally in your head.

### Deep analysis
The analysis you want to do both at indexing and querying time is to acctually try to understand the contents of the text, that a "Tree" is the same thing as a "tree" and a component of "trees". What if you could also pick up on themes and subcontexts?

What we are doing however in Analyzer.cs is very rudimentary type of analysis. We are simply identifying the individual words. We could go further, investigate if any of those words are kind of the same, because although "trees" != "tree" their concepts intersect so much so that in the interest of full-text search they could and maybe should be one and the same concept. Anyway, identifying and normalizing the words will be fine for now.

##FieldFile
Tokens are stored in a field file. A field file is an index of all the tokens in a field. Tokens are stored together with postings. Postings are pointers to documents. Our postings contain the document ID and the positions the token takes within that document.

That means that if we know what field file to look in, we can find the answer to the query "title:rambo" by opening one field file, deserialize the contents of the file into this:

	// tokens/docids/positions
	IDictionary<string, IDictionary<int, IList<int>>> _tokens = DeserializeFieldFile(fileName);
	
	// ...and then we can find the document IDs. This operation does not take long.
	IDictionary<int, IList<int>> docPositions;
	if (!_tokens.TryGetValue(token, out docPositions))
	{
	    return null;
	}
	return docPositions;

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldFile.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/FieldFileTests.cs)

##DocumentFile

Documents are persisted on disk. How they look on disk is not very interesting. 

The in-memory equivalent of a document file is this:

	// docid/fields/values
	private readonly IDictionary<int, IDictionary<string, IList<string>>> _docs;

Here is a document on its own:

	// fields/values
	IDictionary<string, IList<string>> doc;

More than one document fit into a document file. A whole list of them would fit. Imagine how it looks in-memory. I mean I can only guess the shape but it looks to be covering a large area of your RAM. It's a huge tree of stuff. Almost as wierd-looking as the other huge tree of stuff, the token structure:

	// tokens/docids/positions
	IDictionary<string, IDictionary<int, IList<int>>> _tokens;

[Code](https://github.com/kreeben/resin/blob/master/Resin/DocumentFile.cs)

##IndexWriter

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

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs) and a [little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/IndexTests.cs)

## QueryParser
With our current parser we can interpret "title:Rambo", also `title:first title:blood`. The last query is what lucene decompiles this query into: `title:first blood`. We will try to mimic this later on but for now let's work with the decompiled format.

	var q = query.Split(' ').Select(t => t.Split(':'));

##FieldReader

You have already seen the in-memory representation of the field file:

	// tokens/docids/positions
	private readonly IDictionary<string, IDictionary<int, IList<int>>> _tokens;

A field reader can do this:

	var tokens = reader.GetAllTokens();
	var docPos = reader.GetDocPosition(string token);

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/FieldReaderTests.cs)

## Scanner

After a good parsing we get back a list of terms. A term is a field and a token, e.g. "title:rambo". 

All that we know, as a search framework, is called "a lexicon".

At the back of that lexicon is an index, the field file. A scanner scans the index and if it finds a match it returns all of the information we have about the connection between that token and the documents from where it originates, a list of postings: 

	public IList<int> GetDocIds(string field, string value)
    {
        int fieldId;
        if (_fieldIndex.TryGetValue(field, out fieldId))
        {
            var reader = GetReader(field);
            if (reader != null)
            {
                var positions = reader.GetDocPosition(value);
                if (positions != null)
                {
                    var ordered = positions.OrderByDescending(d => d.Value.Count).Select(d => d.Key).ToList();
                    return ordered;
                }
            }
        }
        return Enumerable.Empty<int>().ToList();
    }

Oh and there was also our ranking algorith did you spot it? Go back.

Here's the ranking:

	var ordered = positions.OrderByDescending(d => d.Value.Count).Select(d => d.Key).ToList();
	return ordered;

It is a scan to see if the token exists at all in a document. It doesn't care about how many times or where in the document although we did give it that information. This naive alorithm will soon be replaced to take into account the term and document frequency to give each hit a proper score.

[Code](https://github.com/kreeben/resin/blob/master/Resin/Scanner.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/ScannerTests.cs)

## IndexReader

The IndexReader needs a scanner. The results of a scan is a list of document ids. IndexReader resolves the document and returns that instead of the id.

	public IEnumerable<Document> GetDocuments(string field, string token)
	{
		var docs = _scanner.GetDocIds(field, token);
		foreach (var id in docs)
		{
			yield return GetDocFromDisk(id); // deserialization might take a while if it's a heavy document
		}
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/IndexTests.cs)

##Searcher

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

###Query language interpreter
AND, OR, NOT (+ -), prefix* and fuzzy~ [implemented here](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs).
TODO: nested clauses

###Prefix search
Implemented currently as a Trie scan [here](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs#L41).

Here's an example of a prefix search towards and index of 1M english wikipedia docs:
![alt text](https://github.com/kreeben/resin/blob/master/screenshot4.PNG "Trie's are fast")

More TODO:

###Fuzzy
The term-based search that is currently implemented is extremly fast because once you have deserialized the indexes the scan, the resolve of the document, they are all hash-table look-ups.

The problem of both prefix and fuzzy querying may be seen as a problem of finding out which tokens to look for. 

If you create an ngram-index from the lexicon and ngram the query token the same way and look up the terms for those grams, calculate the [Levenshtein distance](https://en.wikipedia.org/wiki/Levenshtein_distance), what is left are the term-based queries. Such a Lucene-inspired fuzzy query implementation would add a couple of steps to the querying pipeline and those steps would be all about finding out which terms to scan for.

###Ranking
If that goes well then what is left in our [MVP](https://en.wikipedia.org/wiki/Minimum_viable_product) is the ranking algorithm. That should be tons of fun. That's where the [Lucene core team](http://opensourceconnections.com/blog/2015/10/16/bm25-the-next-generation-of-lucene-relevation/) is at.

###Writing to an index in use
Lucene does it extremly well because of it's file format and because it's indices are easily mergable. It sees an update as "first create new index then merge with the current index then refresh the index reader".

<a name="point" id="point"></a>
###Multi-index searching
Lucene does it. It's pretty useful. It has a cool factor to it. Resin needs it.

##### Even though this is about Resin to Google this article is very much about Lucene, so much so that querying it's enourmous index with the term "body:lucene" will render this document in it's results.

If you read all the way to here you know a lot about Lucene. Not so much about John Rambo though. If Google hade seen the movie he would not have sent you here. The point being: even when doing the wrong thing or seing the problem at hand as in need of a less naive solution than you are willing to provide you can end up acheiving something close to the right thing, which is kind of what Google is doing.

Search is not rocket science. A good friend of mine used to say: search is just an algorithm. Another guy I used to work with used to say: remember, that code that you're looking at and that scare you because you don't understand it yet, it is just some code, written by some guy.

_-Two very wise men_

What comes next is not really rocket science either, it's something else. An AI has been built and it has the knowledge of the Google index plus all the extra data it's collected when you googled, gmailed, g-doced, youtubed or g++ed. Google Now knows where you are at, who you are there with, where you are going after that, and where you will sleep tonight (TM). It could play you like a game of Go if it wanted to.

Wouldn't it be better if we built a search service based on the knowledge _we_ choose and then, together, built an AI based on _that_, instead of what Google think it's okay to base an AI on? Today the g-AI is not very smart. Who here thinks it will get smarter by the day? 

Thus, the great importance of mergeable Resin indices. Because we could all

1. Finish up Resin (or a similar project, Lucene comes to mind)
2. Create a Resin indexing bot, promote that service and make that index not only searchable for everyone but
3. Make it and its usage data public, completely open in a format available to anyone
4. Inform that by using Chrome you are feeding the Google AI. By using Firefox you are feeding noone. We can even build a new browser. Browser wars are fun, especially to web devs.
5. Encourage research teams and others who use Resin to upload theis indices to the Resin server, because then they can query it distributively and have queries that span across theirs and other indices, including the Great Web Index and its Usage Data. Sharing of indices, the Facebook of Data, anyone can tap into its knowledge. The vastness of the data Facebook and Google have been collecting on us and on what we know would be nothing compared to what the Resin Index would contain if we deliberately made adding and sharing of knowledge an easy task. None of this seem so scale very well, does it?  
7. Watch how the media is now interested in the topic of privacy
8. Witness how Google's significance lowers.
9. Ensure the laws change to give us more privacy, because by now it is clear to everyone how much data Google have been collecting on you and have been using to drive and feed an AI with. Oh yeah and Bing also. Bad Bing!
10. Profit!

Don't think it can be done? Let there be another search engine war! All in good fun of course.

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

this 48

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

will 40

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

can 30

about 30

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

have 18

would 18

d 18

__little__ 18

__positions__ 18

__analyzer__ 18

__return__ 18

__very__ 16

how 16

your 16

__one__ 16

__like__ 16

than 16

also 16

__analyze__ 16

__able__ 16

__scan__ 16

__public__ 16
