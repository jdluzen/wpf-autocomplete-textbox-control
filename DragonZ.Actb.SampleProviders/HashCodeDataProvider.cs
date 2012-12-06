using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DragonZ.Actb.Provider;

namespace DragonZ.Actb.SampleProviders
{
    public class HashCodeDataProvider : IAutoCompleteDataProvider
    {
        Dictionary<int, string> strings = new Dictionary<int, string>();

        public HashCodeDataProvider()
        {
            for (int i = 0; i < 1000000; i++)
            {
                string s = i.ToString();
                strings.Add(s.GetHashCode(), s);
            }
        }

        public string GetStringValue(object o)
        {
            return ((KeyValuePair<int, string>)o).Key + " is the key";
        }

        public IEnumerable<object> GetItems(string textPattern)
        {
            return strings.Where(pair => pair.Key.ToString().Contains(textPattern) || pair.Value.Contains(textPattern)).Cast<object>();
        }
    }
}
