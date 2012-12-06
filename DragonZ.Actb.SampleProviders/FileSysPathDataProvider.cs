using System;
using System.Collections.Generic;
using System.IO;
using DragonZ.Actb.Provider;

namespace DragonZ.Actb.SampleProviders
{
    public class FileSysPathDataProvider : IAutoCompleteDataProvider
    {
        public string GetStringValue(object o)
        {
            return (string)o;
        }

        public IEnumerable<object> GetItems(string textPattern)
        {
            if (textPattern.Length < 2 || textPattern[1] != ':')
            {
                return null;
            }
            var lastSlashPos = textPattern.LastIndexOf('\\');
            var baseFolder = textPattern;
            string partialMatch = null;
            if (lastSlashPos != -1)
            {
                baseFolder = textPattern.Substring(0, lastSlashPos);
                partialMatch = textPattern.Substring(lastSlashPos + 1);
            }
            try
            {
                return Directory.GetDirectories(baseFolder + '\\', partialMatch + "*");
            }
            catch
            {
                return null;
            }
        }
    }
}