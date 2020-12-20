using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Sir.Documents;
using Sir.Search;
using Sir.VectorSpace;

namespace Sir.HttpServer.Features
{
    public class SaveAsJob<T> : BaseJob
    {
        private readonly SessionFactory _sessionFactory;
        private readonly QueryParser<string> _queryParser;
        private readonly ILogger _logger;
        private readonly IModel<T> _model;
        private readonly HashSet<string> _indexFieldNames;
        private readonly string _target;
        private readonly int _skip;
        private readonly int _take;
        private readonly string[] _select;
        private readonly bool _truncate;

        public SaveAsJob(
            SessionFactory sessionFactory,
            QueryParser<string> queryParser,
            IModel<T> model,
            ILogger logger,
            string target,
            string[] collections,
            string[] fields,
            string[] select,
            string q, 
            bool and, 
            bool or,
            int skip,
            int take,
            bool truncate) 
            : base(collections, fields, q, and, or)
        {
            _indexFieldNames = new HashSet<string>(select);
            _sessionFactory = sessionFactory;
            _queryParser = queryParser;
            _logger = logger;
            _model = model;
            _target = target;
            _skip = skip;
            _take = take;
            _select = select;
            _truncate = truncate;
        }

        public override void Execute()
        {
            try
            {
                var query = _queryParser.Parse(
                    collections: Collections, 
                    q: Q, 
                    fields: Fields, 
                    select: _select,
                    and: And, 
                    or: Or);

                var targetCollectionId = _target.ToHash();
                IEnumerable<Document> documents;

                using (var readSession = _sessionFactory.CreateSearchSession(_model))
                {
                    documents = readSession.Search(query, _skip, _take).Documents;
                }

                if (_truncate)
                {
                    _sessionFactory.Truncate(targetCollectionId);
                }
                
                using (var documentWriter = new DocumentWriter(targetCollectionId, _sessionFactory))
                {
                    foreach (var field in _indexFieldNames)
                    {
                        documentWriter.EnsureKeyExists(field);
                    }
                }

                _sessionFactory.SaveAs(
                        targetCollectionId,
                        documents,
                        _model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"error processing {this} {ex}");
            }
        }
    }
}