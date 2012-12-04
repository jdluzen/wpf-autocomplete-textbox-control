using System.Collections.Generic;

namespace DragonZ.Actb.Provider
{
    public interface IAutoCompleteDataProvider
    {
        IEnumerable<string> GetItems(string textPattern);
    }
}
