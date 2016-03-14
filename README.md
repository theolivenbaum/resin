# Resin
It's a search framework you can reason about. It's simplistic but very capable. It is not built upon Lucene.

##How to build your own full-text search in c# to replace Lucene.net

This will not about Lucene so much as it is a guide to follow if you want to build your own search in c#, or just something to get ideas from if you are into information retrieval. Yet to Google this article is very much about Lucene, so much so that querying it's enourmous index with the term "body:lucene" will render this document in it's results. One could argue that since Lucene is being brought up and very early to that this document certainly is about Lucene. Although I can buy into that notion I would still like to say that this will be mostly about how I built my own searcher-thingie that can index 1M english wikipedia articles in aproximately 20 minutes and then respond to multi-criteria term-based queries towards that index in the tens of milliseconds.

##Why?

- You like Lucene and you're curious about the decisions behind the Lucene design and the reasons as to why it looks the way it does
- You wonder why it's so damn fast
- You wonder what parts of the Lucene.net design is there because of (java) legacy and if the design might be improved upon or simplified
- You sometimes wish that building and querying an index was surrounded by even less code, perhaps by leaning towards conventions you yourself have built up throughout the years of using Lucene
- You wonder what would happen if the .net community gathered around a .net project instead of a line-by-line java port, because soemtimes you'd like to understand why your search is acting the way it does but you find the architecture behind Lucene to be complex and you are scared to even look at the source code, not that complexity is neccessarily a java legacy. Some dotnetpeople also suffer from over engineering everything they touch. Did anyone notice what happened to Umbraco 5? (Too early?)
- The Lucene.net team has proven that a .net runtime hosted on a windows machine provides a wonderful environment for a creature such as a full-text search framework to live and enjoy itself but it makes you a little bit sad that they will always be a couple of years behind the core Lucene team
- You are just genuinely curious about the whole domain of information retrieval, perhaps because it is a small domain, relatively easy to grasp and at it's basic level the math is not frightening, and you see it as one of the tools taking us closer to IR's older cousin AI
- You want to pretend you are building something smart and AI-like and neural networks scare you worse than long, cold hotel corridors and kids riding their pedal cars up and down the carpets of an otherwise empty luxury estate

Lists with points are a boring read. Here's something to lighten up your mood and then there's some code. At the end there will be a fully functional full-text search lib. Skip to [here](#citizens) to make this an even shorter read. 

###The very short story of the small domain of full-text search

In this story there are documents with fields such as title and author and there are tokens, which is what you get when you chop up into pieces, or analyze, the values of those fields. There is also a task at hand, which is to be able to find any document by supplying any token it contains. If many documents contain that token then all of those documents shall be fetched and arranged in the order of relevance, the most relevant first. 

You find yourself in front of a huge stack of documents that reaches over you head, a couple of months worth of unopened mail, and from the documents in that pile you want to be able to produce a small, neat little dosier (as shallow as possible actually) of bills that absolutely, positively must be payed today. So you analyze all of the documents by splitting up the value of each field into tokens, making them lower-case, normalizing the text that you will later scan through, making this process easier which is good because you want the scanning to go fast later on. In an index you keep track of all of the tokens and their positions inside of the documents making it easy to find anything containing a certain token.

Later that day you start querying the index with tokens, one at a time, such as "pay" and "bill". Invoices and letters from your friend Bill surfaces.

You realize that you need to be able to select documents based on more than one criteria so you introduce the concept of "AND" into your querying process which intersects the results of a multi-criteria query, giving you a small, neat little dosier of bills to pay. You then create and witness a new open-source project gaining immensly in popularity eventually leading to the point where you can acctually pay those bills. But by then, even though you used the same criteria the dosier became a little fatter and did not contain the same bills. Stuff had happened. The indexed had changed. Good thing you got payed. The end.

###More requirements

We need to be able to swiftly index documents without taking up too much memory or disk space. We need to be able to query that index for documents and get them back in exactly the same shape they were in before we started analyzing them. The process of retrieving information, querying, must be fast. Not Lucene-fast, but fast. The time it takes to understand the query, perform the scan and then retrieve the documents from disk must be below a second, preferably tens of milliseconds (like Lucene) or at least around a couple of hundred milliseconds. We need to be able to update the index, add new documents and remove old ones. Even though we could be thinking about the values of fields as being objects, any Object, any IComparable even, that would actually make even more sense, to start with we will only solve the querying part, not the custom sorting of results that Lucene is capable of. Therefore we don't need our values to be of type IComparable, they can be strings.

###The heckler

"You said that the scanning process needs to be fast yet you make the tokens lower-case. Don't you know that comparing with ordinal invariant culture and in upper case is faster?"

No sir I did not know that. The reason is because even though I will never be looking directly into an index file, there would be no use, it's binary and I'm not that smart, I cannot stand knowing that those tokens lie there on my disk, screaming, like they're in pain or something.

"Oh, binary? Why not text, why not JSON?"

Because converting binary into the kind of object graph we need at querying time is faster.

"Well do you have specs for the binary file formats? Because if you're making up your own file format you got to have that. I mean, you just have to. Isn't that right? Kenny, isn't that right?"

All data structures are serialized using protobuf-net.

"Oh, that's pretty cewl."

Yeah, I know, it's a great framework and apparently a great protocol. I found it by googling "serialize binary c# fast". For Google's sake I find myself tokenizing my queries for him. I do the same when I text, email to my friends, again, to make life easier on Google, old chap.

<a name="citizens"></a>
###The citizens (all first class)

For indexing we need something that can analyze text, an [Analyzer](https://github.com/kreeben/resin/blob/master/Resin/Analyzer.cs). Also, something that can write index files and store documents, an [IndexWriter](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs), [FieldFile](https://github.com/kreeben/resin/blob/master/Resin/FieldFile.cs) and a [DocumentFile](https://github.com/kreeben/resin/blob/master/Resin/DocumentFile.cs). 

For querying we will need to be able to parse multi-criteria queries such as "title:Rambo title:Blood", in other words a [QueryParser](https://github.com/kreeben/resin/blob/master/Resin/QueryParser.cs). The important questions for the parser to answer are what fields do we need to scan and what's the tokens that should match. Unlike Lucene, the convention I will be following is to interpret the space between two criterias such as the space character between "Rambo title:" in the query "title:Rambo title:Blood" to mean "AND" instead of "OR". In other words the query "title:Rambo title:Blood" will be parsed into "please find documents containing rambo AND blood in their title", or in a more machine-like language "scan the field named title for the tokens rambo and blood and return the intersection of their postings".

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

We use an analyzer to produce normalized tokens from text. The text "Hello World!" may be normalized into new[]{"hello", "world"} if we lower-case the text and split it up at characters ' ' and '!'. By tokenizing the text of a field we make the individual tokens insensitive to casing, queryable. Had we not only exact matches to the verbatim text can be made at runtime, if we want the querying to go fast. The query "title:Rambo" would produce zero documents (no movie in the whole world actually has the title "Rambo") but querying "title:Rambo\\: First Blood" would produce one hit. But only if you are scanning a database of Swedish movie titles because the original movie title was "First Blood". Swedish Media Institue (it's called something else, sorry, I forget) changed the title to the more declarative "Rambo: First Blood". This was perhaps to not confuse the Swedish audience as to which of the characters in this movie will say, at least once in the movie that "I didn't first blood, THEY drew first blood!", because that's Rambo's line, clarified right there in the title, for us lucky Swedes.

Another thing we hope to achieve by analyzing text is to normalize between the words used when querying and the words in the documents so that matches can be produced consistently. 

The analysis you want to do both at indexing and querying time is to acctually try to understand the contents of the text, that a "Tree" is the same thing as a "tree" and a component of "trees". What we are doing however in Analyzer.cs is very rudimentary type of analysis. We are simply identifying the individual words. We could go further, investigate if any of those words are kind of the same, because although "trees" != "tree" their concepts intersect so much so that in the interest of full-text search they could and maybe should be one and the same concept. Anyway, identifying and normalizing the words will be fine for now.

##FieldFile
Tokens are stored in a field file. A field file is an index of all the tokens in a field. Tokens are stored together with postings. Postings are pointers to documents. Our postings contain the document ID and the positions the token takes within that document.

That means that if we know what field file to look in, we can find the answer to the query "title:rambo" by opening one field file, deserializing the contents of the file into this:

	// terms/docids/positions
	IDictionary<string, IDictionary<int, IList<int>>> _terms = DeserializeFieldFile(fileName);;
	
	// And then we can find the document IDs. This operation does not take long.
	IDictionary<int, IList<int>> docPositions;
	if (!_terms.TryGetValue(token, out docPositions))
	{
	    return null;
	}
	return docPositions;

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldFile.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/FieldFileTests.cs)

##DocumentFile

Documents should be persisted. Because if not, then what will return in response to a query? Lucene sometimes skips the part about fetching the fields of the documents in a search result because that's what you told it to do. Those queries execute very fast. But you should at least be returning documents where one of its fields have been deserialized, otherwise the resut of your full-text query is not very interesting. For now, in Resin, all fields are always returned.

I can't show you how the document file looks on disk, but the in-memory equivalent is this graph:

	// docid/fields/values
    private readonly IDictionary<int, IDictionary<string, IList<string>>> _docs;

That means more than one document fit into a document file. A whole list of them would fit. We should probably make the files relatively small in doc count so that the deserialization, which needs to be done at query time, is done swiftly.

[Code](https://github.com/kreeben/resin/blob/master/Resin/DocumentFile.cs)

##IndexWriter

Store the documents. But first analyze them and create field files that are queryable. There's not much to it:

	public void Write(Document doc)
	{
	    foreach (var field in doc.Fields)
	    {
	        foreach (var value in field.Value)
	        {
	            // persist the value of the field, as-is, by writing to a document file

	            var terms = _analyzer.Analyze(value);
	            foreach(var term in terms)
	            {
	            	// store the doc ID, token and its position in a field file
	            }
	        }
	    }
	}

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexWriter.cs) and a [little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/IndexTests.cs)

## QueryParser
With our current parser we can interpret "title:Rambo", also "title:first title:blood". The last query is what lucene decompiles this query into: "title:first blood". We will try to mimic this later on but for now let's work with the decompiled format. Btw, anyone may dig in and fix the parser.

	var q = query.Split(' ').Select(t => t.Split(':'));

##FieldReader

You have already seen the in-memory representation of the field file:

	// terms/docids/positions
    private readonly IDictionary<string, IDictionary<int, IList<int>>> _terms;

A field reader can do this:

	var terms = reader.GetAllTerms();
	var docPos = reader.GetDocPosition(string token);

[Code](https://github.com/kreeben/resin/blob/master/Resin/FieldReader.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/Tests/FieldReaderTests.cs)

## Scanner

After a good parsing we get back a list of terms. A term is a field and a value, e.g. "title:rambo". 

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

Here's the ranking mechanism:

	var ordered = positions.OrderByDescending(d => d.Value.Count).Select(d => d.Key).ToList();
	return ordered;

It orders the result based on how many times a token exists within the document. It doesn't care about where in the document although we gave it that information. Instead, for now, it cares only about how many times a token exists.

[Code](https://github.com/kreeben/resin/blob/master/Resin/Scanner.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/ScannerTests.cs)

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

[Code](https://github.com/kreeben/resin/blob/master/Resin/IndexReader.cs) and [a little bit of testing](https://github.com/kreeben/resin/blob/master/IndexTests.cs)

##Searcher

Finally, the searcher, a helper that takes an IndexReader and a QueryParser, accepting unparsed queries, lazily returning a list of documents:

	public IEnumerable<Document> Search(string query)
    {
        var terms = _parser.Parse(query).ToList();
        return _reader.GetDocuments(terms);
    }

[Code](https://github.com/kreeben/resin/blob/master/Resin/Searcher.cs) 

## Test spin

1. Download a Wikipedia JSON dump [here](https://dumps.wikimedia.org/wikidatawiki/entities/)
2. Use the [WikipediaJsonParser](https://github.com/kreeben/resin/blob/master/Resin.WikipediaJsonParser/Program.cs) to extract as many documents as you want. In a cmd window:

	cd path_to_resin_repo\Resin.WikipediaJsonParser\bin\debug
	rnw c:\downloads\wikipedia.json 0 1000000

This will generate a new file: wikipedia_resin.json. We skipped 0 documents and populated it with 1M.

3. Create an index. In a cmd window:
	
	cd path_to_resin_repo\Cli\bin\Debug
	rn write --file c:\downloads\wikipedia_resin.json --dir c:\temp\resin\wikipedia --skip 0 --take 1000000

4. After 20 minutes or so, do this:  

	rn query --dir c:\temp\resin\wikipedia -q "label:ringo"

![alt text](https://github.com/kreeben/resin/blob/master/screenshot.PNG "I have an SSD. The index was warmed up prior to the query.")

##Roadmap
It's around 800 locs, does term-based queries really fast and indexing within decent timeframes. In the next release there will be improvements to the query parsing. I don't see anything wrong with the Lucene query language. I will also try to achieve prefix based matching with the help of a [DAWG](https://en.wikipedia.org/wiki/Directed_acyclic_word_graph).
