using System;
using System.Collections.Generic;

namespace Sir.Search
{
    public class Document
    {
        public double Score { get; set; }
        public IList<Field> Fields { get; }

        public Document(IList<Field> fields)
        {
            Fields = fields;
        }

        public Field Get(string key)
        {
            foreach (var field in Fields)
            {
                if (field.Key == key)
                {
                    return field;
                }
            }

            throw new ArgumentException($"key {key} not found");
        }

        public bool TryGetValue(string key, out Field value)
        {
            foreach (var field in Fields)
            {
                if (field.Key == key)
                {
                    value = field;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}