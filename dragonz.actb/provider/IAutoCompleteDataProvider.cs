using System;
using System.Collections.Generic;

namespace DragonZ.Actb.Provider
{
    public interface IAutoCompleteDataProvider
    {
        IEnumerable<object> GetItems(string textPattern);

        string GetStringValue(object o);
    }
}
