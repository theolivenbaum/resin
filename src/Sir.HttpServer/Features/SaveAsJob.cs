using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Sir.Documents;
using Sir.Search;

namespace Sir.HttpServer.Features
{
    public class SaveAsJob : BaseJob
    {
        private readonly SessionFactory _sessionFactory;
        private readonly QueryParser<string> _queryParser;
        private readonly ILogger _logger;
        private readonly ITextModel _model;
        private readonly HashSet<string> _indexFieldNames;
        private readonly string _target;
        private readonly int _skip;
        private readonly int _take;
        private readonly string[] _select;
        private readonly bool _truncate;

        public SaveAsJob(
            SessionFactory sessionFactory,
            QueryParser<string> queryParser,
            ITextModel model,
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
                IEnumerable<IDictionary<string, object>> documents;

                using (var readSession = _sessionFactory.CreateQuerySession(_model))
                {
                    documents = readSession.Search(query, _skip, _take).Documents;
                }

                //TODO: Remove this when cc_wat is rebuilt.
                var c = "cc_wat".ToHash();
                foreach (var d in documents)
                {
                    d.TryAdd(SystemFields.CollectionId, c);
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
                        documents.Select(dic =>
                            new Search.Document(
                                dic.Select(kvp => new Field(
                                    kvp.Key,
                                    kvp.Value,
                                    index: _indexFieldNames.Contains(kvp.Key),
                                    store: false)).ToList())),
                        _model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"error processing {this} {ex}");
            }
        }
    }
}