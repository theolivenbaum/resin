using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Tests
{
    public class Setup
    {
        private string _dir;

        public string Dir
        {
            get
            {
                if (_dir == null)
                {
                    _dir = @"c:\temp\resin_tests\" + Guid.NewGuid().ToString();
                    Directory.CreateDirectory(_dir);
                }
                return _dir;
            }
        }
    }
}