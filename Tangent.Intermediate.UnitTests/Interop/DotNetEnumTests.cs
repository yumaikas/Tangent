﻿using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;

namespace Tangent.Intermediate.Interop.UnitTests
{
    internal enum TestEnum
    {
        a = 2,
        b = 3
    }

    internal enum ShortTestEnum : short
    {
        a = 2,
        b = 3
    }

    [TestClass]
    [ExcludeFromCodeCoverage]
    public class DotNetEnumTests
    {
        [TestMethod]
        public void EnumValueReturnsCorrectly()
        {
            var test = DotNetEnumType.For(typeof(TestEnum));
            // no boom? good.
        }

        [TestMethod]
        public void EnumShortValueReturnsCorrectly()
        {
            var test = DotNetEnumType.For(typeof(ShortTestEnum));
            // no boom? good.
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void EnumForClassExplodes()
        {
            var test = DotNetEnumType.For(typeof(string));
        }
    }
}
