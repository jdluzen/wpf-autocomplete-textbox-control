using System;
using System.Collections.Generic;
using DragonZ.Actb.Provider;

namespace DragonZ.Actb.SampleProviders
{
    public class SimpleStaticDataProvider : IAutoCompleteDataProvider
    {
        private IEnumerable<object> _source;

        public SimpleStaticDataProvider(IEnumerable<object> source)
        {
            _source = source;
        }

        public IEnumerable<object> GetItems(string textPattern)
        {
            foreach (string item in _source)
            {
                if (item.StartsWith(textPattern, StringComparison.OrdinalIgnoreCase))
                {
                    yield return item;
                }
            }
        }

        public string GetStringValue(object o)
        {
            return (string)o;
        }
    }
}