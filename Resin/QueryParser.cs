namespace Resin
{
    public class QueryParser
    {
        private readonly IAnalyzer _analyzer;

        public QueryParser(IAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public Query Parse(string query)
        {
            var interpreter = new QueryInterpreter(query, _analyzer);
            for (int i = 0; i < query.Length; i++)
            {
                interpreter.Step(i);
            }
            return interpreter.GetQuery();
        }
    }
}