# Resin
##How to build your own full-text search in c# to replace Lucene.net

This piece is not about Lucene so much as it is a guide to follow if you want to build your own searcher-thingie in c#, or just something to get ideas from if you are into information retrieval. Yet to Google this article is very much about Lucene, so much so that querying it's enourmous index with the criteria "body:lucene" will render this document in it's results. One could argue that since Lucene is being brought up and very early too that this document certainly is about Lucene. Although I can buy into that notion I would still like to say that this will be mostly about how I built my own searcher-thingie.

##Why?

If you question me on the merits of building your own full-text search, there is already a well-known and very capable tool out there, Lucene.net, well, this whole article could fall apart or on it's head. But ok, let's see, there's this:

- You like Lucene and you're curious about the decisions behind the Lucene design and the reasons as to why it looks the way it does
- You wonder why it's so damn fast
- You wonder what parts of the Lucene.net design is there because of (java) legacy and if the design might be improved upon or simplified
- You sometimes wish that building and querying an index was surrounded by even less code, perhaps by leaning towards conventions you yourself have built up throughout the years of using Lucene
- You wonder what would happen if the .net community gathered around a .net project instead of a line-by-line java port, because soemtimes you want to understand why your search is behaving the way it does but you find the architecture behind Lucene to be extremely complex and you are scared to even look at the source code, not that complexity is neccessarily a java legacy. Some dotnetpeople also suffer from over engineering everything they touch. Did anyone notice what happened to Umbraco 5?
- The Lucene.net team has proven that a .net runtime hosted on a windows machine provides a wonderful environment for a creature such as a full-text search framework to live and enjoy itself but it makes you a little bit sad that they will always be a couple of years behind the core Lucene team
- You are just genuinely curious about the whole domain of information retrieval, perhaps because it is a small domain, relatively easy to grasp and at it's basic level the math is not frightening, and you see it as one of the tools taking us closer to IR's older cousin AI
- You want to pretend you are building something smart and AI-like and neural networks scare you worse than long, cold hotel corridors with kids riding their pedal cars up and down the carpets of an otherwise empty luxury estate

Lists with points are a boring read. Here's something to lighten up your mood.

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

Yeah, I know, it's a great framework and apparently a great protocol. I found it by googling "serialize binary c# fast".

###The citizens

For indexing we need something that can analyze text, an Analyzer. Also, something that can write index files and store documents, an IndexWriter, FieldFile and a DocumentFile. 

For querying we will need to be able to parse multi-criteria queries such as "title:Rambo title:Blood", in other words a QueryParser. The important questions for the parser to answer are what fields do we need to scan and what's the tokens that should match. Unlike Lucene, the convention I will be following is to interpret the space between two criterias such as the space character between "Rambo title:" in the query "title:Rambo title:Blood" meaning "AND" instead of "OR". In other words the query "title:Rambo title:Blood" will be parsed into "please find documents containing rambo AND blood in their title".

An IndexReader and a FieldReader will make it possible for a Scanner to get a list of document IDs containing the tokens at hand. A DocumentReader will assist in fetching the documents, in the state they were in at indexing time, from disk.

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

We use an analyzer to produce normalized tokens from text. The text "Hello world" could be normalized into new[]{"hello", "world"} if we lower-cased the text and used the separators ' ' and '!'. By tokenizing the text of a field we make the individual tokens queryable. Had we not, the query "title:Rambo" would produce zero documents (no movie in the whole world actually has the title "Rambo") but querying "title:Rambo title:Blood" would produce one hit. But only if you are scanning a database of Swedish movie titles because the original movie title was "First Blood". Swedish Media Institue (it's called something else, sorry, I forget) changed the title to the more declarative "Rambo: First Blood". This was perhaps to not confuse the Swedish audience as to which of the characters will say, at least once in the movie that "I didn't first blood, THEY drew first blood!", because that's Rambo's line, clarified right there in the title, for us lucky Swedes. I wonder, in the English language, does anything pertaining to nationality have to start with a captial letter?

Another thing we hope to achieve by analysing the text in the documents is to be able to normalize the words used when querying, so that matches are produced consistently. The analysis you want to do at indexing time is to acctually try to understand the contents of the document, that a "Tree" is the same thing as a "tree" and in the same ball park at least as "trees". What we are doing however is very rudimentary type of analysis. We are simply identifying the words but we could go further, investigate if any of those words are kind of the same, because although "trees" != "tree" their concepts intersect so much so that in the interest of full-text search they could be one and the same concept. Anyway, identifying and normalizing the words will be fine for now.
