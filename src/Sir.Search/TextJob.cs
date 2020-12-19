﻿using System.Collections.Generic;

namespace Sir.Search
{
    public class TextJob
    {
        public ITextModel Model { get; }
        public ulong CollectionId { get; private set; }
        public IEnumerable<Document> Documents { get; private set; }

        public TextJob(
            ulong collectionId, 
            IEnumerable<Document> documents, 
            ITextModel model)
        {
            Model = model;
            CollectionId = collectionId;
            Documents = documents;
        }
    }
}