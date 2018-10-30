using System;
using System.Collections.Generic;
using System.Text;

namespace Sir
{
    public interface IConfiguration
    {
        string Get(string key);
    }
}
