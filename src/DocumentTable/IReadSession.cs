using System;
using System.Collections.Generic;
using System.IO;

namespace Resin.Documents
{
    public interface IReadSession : IDisposable
    {
        SegmentInfo Version { get; set; }
        Document ReadDocument(int documentId);
        DocHash ReadDocHash(int docId);
        Stream Stream { get; }
    }
}
