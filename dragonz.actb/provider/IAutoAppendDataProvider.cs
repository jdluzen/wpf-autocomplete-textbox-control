﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DragonZ.Actb.Provider
{
    public interface IAutoAppendDataProvider
    {
        string GetAppendText(string textPattern, string firstMatch);
    }
}
